using System;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;

namespace AutoSort;

public sealed class Plugin : IDalamudPlugin
{
    private readonly InventorySortController _controller;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _controller = new InventorySortController(
            new GameState(),
            new MacroExecutor(),
            new TickSchedulerAdapter(),
            config);
        Svc.Framework.Update += OnUpdate;
    }

    private void OnUpdate(object _) =>
        _controller.OnOpen(DateTimeOffset.Now.ToUnixTimeMilliseconds());

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        ECommonsMain.Dispose();
    }
}
