using System.Collections.Generic;

namespace AutoSort.Tests.Fakes;

public class FakeGameState : IGameState
{
    public bool IsLoggedIn { get; set; } = true;
    public bool InventoryOpen { get; set; }
    public HashSet<string> VisibleAddons { get; } = new();

    public bool IsInventoryOpen() => InventoryOpen;
    public bool IsAddonVisible(string name) => VisibleAddons.Contains(name);
}
