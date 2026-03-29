using System.Collections.Generic;

namespace AutoSort.Tests.Fakes;

public class FakeSortConfiguration : ISortConfiguration
{
    public bool Enabled { get; set; } = true;
    public int SortCooldownMs { get; set; } = 0;
    public int ExecutionDelayMs { get; set; } = 0;
    public IReadOnlyList<string> SortCommands { get; set; } =
        new[] { "/itemsort execute inventory" };
}
