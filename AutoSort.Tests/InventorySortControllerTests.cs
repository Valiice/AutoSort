using AutoSort.Tests.Fakes;
using Xunit;

namespace AutoSort.Tests;

public class InventorySortControllerTests
{
    private readonly FakeGameState _state = new();
    private readonly FakeMacroExecutor _executor = new();
    private readonly FakeSortConfiguration _config = new();
    private readonly SynchronousScheduler _scheduler = new();

    private InventorySortController MakeController() =>
        new(_state, _executor, _scheduler, _config);

    [Fact]
    public void Sorts_WhenInventoryJustOpened()
    {
        var ctrl = MakeController();
        _state.InventoryOpen = false;
        ctrl.Tick(0);
        _state.InventoryOpen = true;
        ctrl.Tick(1);
        Assert.Single(_executor.Calls);
    }

    [Fact]
    public void DoesNotSort_WhenAlreadyOpen()
    {
        var ctrl = MakeController();
        _state.InventoryOpen = false;
        ctrl.Tick(0);
        _state.InventoryOpen = true;
        ctrl.Tick(1);  // transition — sorts once
        ctrl.Tick(2);  // still open — must not sort again
        Assert.Single(_executor.Calls);
    }

    [Fact]
    public void DoesNotSortAgain_WithinCooldown()
    {
        _config.SortCooldownMs = 2000;
        var ctrl = MakeController();
        _state.InventoryOpen = false; ctrl.Tick(0);
        _state.InventoryOpen = true;  ctrl.Tick(1);    // first open — sorts (1 - long.MinValue/2 >> 2000)
        _state.InventoryOpen = false; ctrl.Tick(2);
        _state.InventoryOpen = true;  ctrl.Tick(500);  // re-open — 500 - 1 = 499, not > 2000
        Assert.Single(_executor.Calls);
    }

    [Fact]
    public void SortsAgain_AfterCooldownExpires()
    {
        _config.SortCooldownMs = 2000;
        var ctrl = MakeController();
        _state.InventoryOpen = false; ctrl.Tick(0);
        _state.InventoryOpen = true;  ctrl.Tick(1);     // first sort at t=1
        _state.InventoryOpen = false; ctrl.Tick(2);
        _state.InventoryOpen = true;  ctrl.Tick(3000);  // 3000 - 1 = 2999 > 2000 — sorts again
        Assert.Equal(2, _executor.Calls.Count);
    }

    [Fact]
    public void DoesNotSort_WhenNotLoggedIn()
    {
        _state.IsLoggedIn = false;
        _state.InventoryOpen = false;
        var ctrl = MakeController();
        ctrl.Tick(0);
        _state.InventoryOpen = true;
        ctrl.Tick(1);
        Assert.Empty(_executor.Calls);
    }

    [Fact]
    public void DoesNotSort_WhenDisabled()
    {
        _config.Enabled = false;
        _state.InventoryOpen = false;
        var ctrl = MakeController();
        ctrl.Tick(0);
        _state.InventoryOpen = true;
        ctrl.Tick(1);
        Assert.Empty(_executor.Calls);
    }

    [Fact]
    public void DoesNotSort_DuringRetainerTransfer()
    {
        _state.InventoryOpen = false;
        var ctrl = MakeController();
        ctrl.Tick(0);
        _state.VisibleAddons.Add("RetainerItemTransferProgress");
        _state.InventoryOpen = true;
        ctrl.Tick(1);
        Assert.Empty(_executor.Calls);
    }
}
