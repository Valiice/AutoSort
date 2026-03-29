using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Logging;
using ECommons.Schedulers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSort;

public sealed unsafe class Plugin : IDalamudPlugin
{
    private bool _wasOpen = false;
    private long _lastSortTime = 0;

    private const int SortCooldownMs = 2000;
    private const int ExecutionDelayMs = 50;

    private readonly List<string> _sortCommands = new()
    {
        "/itemsort condition inventory stack des",
        "/itemsort condition inventory id asc",
        "/itemsort condition inventory ilv des",
        "/itemsort condition inventory category asc",
        "/itemsort execute inventory"
    };

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        Svc.Framework.Update += OnUpdate;
    }

    private void OnUpdate(object framework)
    {
        if (!Svc.ClientState.IsLoggedIn) return;

        if (IsAddonVisible("RetainerItemTransferProgress")) return;

        var isOpen = IsInventoryOpen();

        if (isOpen && !_wasOpen)
        {
            var currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            if (currentTime - _lastSortTime > SortCooldownMs)
            {
                _lastSortTime = currentTime;
                PluginLog.Debug("AutoSort: Inventory ready. Executing optimized macro...");

                new TickScheduler(ExecuteSortMacro, ExecutionDelayMs);
            }
        }

        _wasOpen = isOpen;
    }

    private static bool IsInventoryOpen()
    {
        return IsAddonReady("Inventory") ||
               IsAddonReady("InventoryLarge") ||
               IsAddonReady("InventoryExpansion");
    }

    private static bool IsAddonReady(string name)
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>(name, out var addon))
        {
            return GenericHelpers.IsAddonReady(addon);
        }
        return false;
    }

    private bool IsAddonVisible(string name)
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>(name, out var addon))
        {
            return addon->IsVisible;
        }
        return false;
    }

    private void ExecuteSortMacro()
    {
        try
        {
            MacroManager.Execute(_sortCommands);
        }
        catch (Exception e)
        {
            PluginLog.Error($"AutoSort: Macro execution failed: {e.Message}");
        }
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        ECommonsMain.Dispose();
    }
}
