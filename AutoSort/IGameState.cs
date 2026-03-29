namespace AutoSort;

public interface IGameState
{
    bool IsLoggedIn { get; }
    bool IsInventoryOpen();
    bool IsAddonVisible(string name);
}
