using System.Collections.Generic;

namespace AutoSort;

public interface ISortConfiguration
{
    bool Enabled { get; }
    int SortCooldownMs { get; }
    int ExecutionDelayMs { get; }
    IReadOnlyList<string> SortCommands { get; }
}
