using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Logging;

namespace AutoSort;

public sealed class Plugin : IDalamudPlugin
{
    private readonly GameState _gameState;
    private readonly InventorySortController _mainController;
    private readonly InventorySortController _retainerController;
    private bool _wasMainOpen;
    private bool _wasRetainerOpen;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _gameState = new GameState();
        _mainController = new InventorySortController(
            _gameState, new MacroExecutor(), new TickSchedulerAdapter(), config);
        _retainerController = new InventorySortController(
            _gameState, new MacroExecutor(), new TickSchedulerAdapter(), new RetainerSortConfig(config));

        Svc.Framework.Update += OnUpdate;
    }

    // DEBUG: candidate retainer addon names to probe
    private static readonly string[] RetainerAddonCandidates =
    {
        "InventoryRetainer", "InventoryRetainerLarge",
        "RetainerGrid", "RetainerGrid0", "RetainerGrid1",
        "RetainerList", "RetainerInventory", "RetainerSellList",
        "RetainerSell", "RetainerTaskAsk", "RetainerTaskResult",
    };
    private bool _retainerProbed;

    private void OnUpdate(object _)
    {
        var nowMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        var mainOpen = _gameState.IsAddonVisible("Inventory")
                    || _gameState.IsAddonVisible("InventoryLarge")
                    || _gameState.IsAddonVisible("InventoryExpansion");
        if (mainOpen && !_wasMainOpen)
        {
            PluginLog.Information("[AutoSort] Main inventory opened");
            _mainController.OnOpen(nowMs);
        }
        _wasMainOpen = mainOpen;

        // DEBUG: log which retainer addons are visible (once per retainer visit)
        if (_gameState.IsAddonVisible("RetainerList") && !_retainerProbed)
        {
            _retainerProbed = true;
            foreach (var name in RetainerAddonCandidates)
            {
                if (_gameState.IsAddonVisible(name))
                    PluginLog.Information($"[AutoSort] DEBUG retainer addon visible: {name}");
            }
        }
        if (!_gameState.IsAddonVisible("RetainerList"))
            _retainerProbed = false;

        var retainerOpen = _gameState.IsAddonVisible("InventoryRetainer")
                        || _gameState.IsAddonVisible("InventoryRetainerLarge");
        if (retainerOpen && !_wasRetainerOpen)
        {
            PluginLog.Information("[AutoSort] Retainer inventory opened");
            _retainerController.OnOpen(nowMs);
        }
        _wasRetainerOpen = retainerOpen;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        ECommonsMain.Dispose();
    }

    private sealed class RetainerSortConfig : ISortConfiguration
    {
        private readonly Configuration _cfg;
        public RetainerSortConfig(Configuration cfg) => _cfg = cfg;
        public bool Enabled => _cfg.RetainerSortEnabled;
        public int SortCooldownMs => 0;
        public int ExecutionDelayMs => _cfg.ExecutionDelayMs;
        public IReadOnlyList<string> SortCommands => _cfg.RetainerSortCommands;
    }
}
