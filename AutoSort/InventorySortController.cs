namespace AutoSort;

public sealed class InventorySortController
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

    public void Tick(long nowMs)
    {
        var isOpen = _gameState.IsInventoryOpen();

        if (isOpen && !_wasOpen && nowMs - _lastSortTime > _config.SortCooldownMs)
        {
            _lastSortTime = nowMs;
            _scheduler.Schedule(() => _macroExecutor.Execute(_config.SortCommands), _config.ExecutionDelayMs);
        }

        _wasOpen = isOpen;
    }
}
