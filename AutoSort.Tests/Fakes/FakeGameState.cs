using System.Collections.Generic;

namespace AutoSort.Tests.Fakes;

public class FakeGameState : IGameState
{
    public bool IsLoggedIn { get; set; } = true;
    public HashSet<string> VisibleAddons { get; } = new();

    public bool IsAddonVisible(string name) => VisibleAddons.Contains(name);
}
