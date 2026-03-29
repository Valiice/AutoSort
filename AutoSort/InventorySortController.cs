namespace AutoSort;

public class InventorySortController
{
    private readonly IGameState _gameState;
    private readonly IMacroExecutor _macroExecutor;
    private readonly IActionScheduler _scheduler;
    private readonly ISortConfiguration _config;

    private bool _wasOpen;
    private long _lastSortTime = long.MinValue / 2;

    public InventorySortController(
        IGameState gameState,
        IMacroExecutor macroExecutor,
        IActionScheduler scheduler,
        ISortConfiguration config)
    {
        _gameState = gameState;
        _macroExecutor = macroExecutor;
        _scheduler = scheduler;
        _config = config;
    }

    public void Tick(long nowMs) { }
}
