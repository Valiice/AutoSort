using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSort;

public sealed unsafe class GameState : IGameState
{
    public bool IsLoggedIn => Svc.ClientState.IsLoggedIn;

    public bool IsAddonVisible(string name)
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>(name, out var addon))
            return addon->IsVisible;
        return false;
    }
}
