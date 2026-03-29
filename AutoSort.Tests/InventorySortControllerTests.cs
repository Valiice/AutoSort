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
}
