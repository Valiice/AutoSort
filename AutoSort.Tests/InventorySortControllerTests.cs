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
    public void Sorts_WhenOpened()
    {
        var ctrl = MakeController();
        ctrl.OnOpen(0);
        Assert.Single(_executor.Calls);
    }

    [Fact]
    public void DoesNotSortAgain_WithinCooldown()
    {
        _config.SortCooldownMs = 2000;
        var ctrl = MakeController();
        ctrl.OnOpen(1);    // first open — sorts
        ctrl.OnOpen(500);  // re-open — 500-1=499, which is <= 2000 — blocked
        Assert.Single(_executor.Calls);
    }

    [Fact]
    public void SortsAgain_AfterCooldownExpires()
    {
        _config.SortCooldownMs = 2000;
        var ctrl = MakeController();
        ctrl.OnOpen(1);     // first sort recorded at t=1
        ctrl.OnOpen(3000);  // 3000-1=2999, which is > 2000 — sorts again
        Assert.Equal(2, _executor.Calls.Count);
    }

    [Fact]
    public void DoesNotSort_WhenNotLoggedIn()
    {
        _state.IsLoggedIn = false;
        var ctrl = MakeController();
        ctrl.OnOpen(0);
        Assert.Empty(_executor.Calls);
    }

    [Fact]
    public void DoesNotSort_WhenDisabled()
    {
        _config.Enabled = false;
        var ctrl = MakeController();
        ctrl.OnOpen(0);
        Assert.Empty(_executor.Calls);
    }

    [Fact]
    public void DoesNotSort_DuringRetainerTransfer()
    {
        _state.VisibleAddons.Add("RetainerItemTransferProgress");
        var ctrl = MakeController();
        ctrl.OnOpen(0);
        Assert.Empty(_executor.Calls);
    }

    [Fact]
    public void SortsWithConfiguredCommands()
    {
        _config.SortCommands = new[] { "/foo", "/bar" };
        var ctrl = MakeController();
        ctrl.OnOpen(0);
        var executedCommands = Assert.Single(_executor.Calls);
        Assert.Equal(new[] { "/foo", "/bar" }, executedCommands);
    }

    [Fact]
    public void DoesNotSort_WhenCommandsEmpty()
    {
        _config.SortCommands = new string[0];
        var ctrl = MakeController();
        ctrl.OnOpen(0);
        Assert.Empty(_executor.Calls);
    }
}
