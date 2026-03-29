using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace AutoSort;

[Serializable]
public sealed class Configuration : IPluginConfiguration, ISortConfiguration
{
    public int Version { get; set; } = 0;

    public bool Enabled { get; set; } = true;
    public int SortCooldownMs { get; set; } = 2000;
    public int ExecutionDelayMs { get; set; } = 50;

    public List<string> SortCommands { get; set; } = new()
    {
        "/itemsort condition inventory stack des",
        "/itemsort condition inventory id asc",
        "/itemsort condition inventory ilv des",
        "/itemsort condition inventory category asc",
        "/itemsort execute inventory"
    };

    IReadOnlyList<string> ISortConfiguration.SortCommands => SortCommands;

    public void Save(IDalamudPluginInterface pi) => pi.SavePluginConfig(this);
}
