using System.Linq;

namespace AutoSort;

public sealed class InventorySortController
{
    private readonly IGameState _gameState;
    private readonly IMacroExecutor _macroExecutor;
    private readonly IActionScheduler _scheduler;
    private readonly ISortConfiguration _config;

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

    public void OnOpen(long nowMs)
    {
        if (!_config.Enabled) return;
        if (!_config.SortCommands.Any()) return;
        if (!_gameState.IsLoggedIn) return;
        if (_gameState.IsAddonVisible("RetainerItemTransferProgress")) return;
        if (nowMs - _lastSortTime <= _config.SortCooldownMs) return;

        _lastSortTime = nowMs;
        _scheduler.Schedule(() => _macroExecutor.Execute(_config.SortCommands), _config.ExecutionDelayMs);
    }
}
