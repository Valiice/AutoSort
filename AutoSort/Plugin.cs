using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;

namespace AutoSort;

public sealed class Plugin : IDalamudPlugin
{
    private readonly InventorySortController _mainController;
    private readonly InventorySortController _retainerController;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var gameState = new GameState();
        _mainController = new InventorySortController(
            gameState, new MacroExecutor(), new TickSchedulerAdapter(), config);
        _retainerController = new InventorySortController(
            gameState, new MacroExecutor(), new TickSchedulerAdapter(), new RetainerSortConfig(config));

        Svc.AddonLifecycle.RegisterListener(
            AddonEvent.PostSetup,
            ["Inventory", "InventoryLarge", "InventoryExpansion"],
            OnMainInventoryOpen);
        Svc.AddonLifecycle.RegisterListener(
            AddonEvent.PostSetup,
            ["InventoryRetainer", "InventoryRetainerLarge"],
            OnRetainerInventoryOpen);
    }

    private void OnMainInventoryOpen(AddonEvent type, AddonArgs args) =>
        _mainController.OnOpen(DateTimeOffset.Now.ToUnixTimeMilliseconds());

    private void OnRetainerInventoryOpen(AddonEvent type, AddonArgs args) =>
        _retainerController.OnOpen(DateTimeOffset.Now.ToUnixTimeMilliseconds());

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(OnMainInventoryOpen, OnRetainerInventoryOpen);
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
