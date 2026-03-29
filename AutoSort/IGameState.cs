namespace AutoSort;

public interface IGameState
{
    bool IsLoggedIn { get; }
    bool IsAddonVisible(string name);
}
