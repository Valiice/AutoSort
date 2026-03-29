# AutoSort Optimization Design

**Date:** 2026-03-29
**Status:** Approved

## Goal

Refactor the single `Plugin.cs` into focused, testable units. Add an xUnit test project covering the sort state machine. Expose configuration so cooldown, delay, and sort commands can be changed without recompiling.

## Architecture

`Plugin.cs` today does five things at once: init, framework polling, addon checks, state machine, and macro execution. The new structure separates each responsibility:

```
AutoSort/
  Plugin.cs                   ← thin bootstrap: init, wire, dispose
  InventorySortController.cs  ← state machine (all sort logic)
  IGameState.cs               ← interface: login + addon visibility
  IMacroExecutor.cs           ← interface: fire sort commands
  IScheduler.cs               ← interface: delayed action scheduling
  ISortConfiguration.cs       ← interface: enabled, cooldown, commands
  GameState.cs                ← real IGameState (unsafe game calls)
  MacroExecutor.cs            ← real IMacroExecutor
  TickSchedulerAdapter.cs     ← real IScheduler (wraps TickScheduler)
  Configuration.cs            ← IPluginConfiguration + ISortConfiguration

AutoSort.Tests/
  AutoSort.Tests.csproj       ← xUnit, references AutoSort project
  Fakes/
    FakeGameState.cs
    FakeMacroExecutor.cs
    FakeSortConfiguration.cs
    SynchronousScheduler.cs
  InventorySortControllerTests.cs
```

## Interfaces

No Dalamud types cross the interface boundary — `InventorySortController` depends only on these:

```csharp
public interface IGameState
{
    bool IsLoggedIn { get; }
    bool IsInventoryOpen();
    bool IsAddonVisible(string name);
}

public interface IMacroExecutor
{
    void Execute(IEnumerable<string> commands);
}

public interface IScheduler
{
    void Schedule(Action action, int delayMs);
}

public interface ISortConfiguration
{
    bool Enabled { get; }
    int SortCooldownMs { get; }
    int ExecutionDelayMs { get; }
    IReadOnlyList<string> SortCommands { get; }
}
```

## InventorySortController

Owns `_wasOpen` and `_lastSortTime`. `Tick(long nowMs)` is the sole entry point — taking time as a parameter makes it deterministic in tests.

```csharp
public void Tick(long nowMs)
{
    if (!_config.Enabled) return;
    if (!_gameState.IsLoggedIn) return;
    if (_gameState.IsAddonVisible("RetainerItemTransferProgress")) return;

    var isOpen = _gameState.IsInventoryOpen();

    if (isOpen && !_wasOpen && nowMs - _lastSortTime > _config.SortCooldownMs)
    {
        _lastSortTime = nowMs;
        _scheduler.Schedule(() => _macroExecutor.Execute(_config.SortCommands), _config.ExecutionDelayMs);
    }

    _wasOpen = isOpen;
}
```

`Plugin.OnUpdate` becomes a one-liner:

```csharp
private void OnUpdate(object _) =>
    _controller.Tick(DateTimeOffset.Now.ToUnixTimeMilliseconds());
```

## Configuration

`Configuration` implements both `IPluginConfiguration` and `ISortConfiguration`. Defaults match the current hardcoded values — no behaviour change out of the box.

| Field | Default |
|---|---|
| `Enabled` | `true` |
| `SortCooldownMs` | `2000` |
| `ExecutionDelayMs` | `50` |
| `SortCommands` | The existing five `/itemsort` commands |

`SortCommands` is `List<string>` on `Configuration` (serializable, mutable) but exposed as `IReadOnlyList<string>` via the interface.

## Test Project

### Fakes

- `FakeGameState` — property bags: `IsLoggedIn`, `InventoryOpen`, `HashSet<string> VisibleAddons`
- `FakeMacroExecutor` — records calls: `List<IEnumerable<string>> Calls`
- `FakeSortConfiguration` — mutable properties with same defaults as `Configuration`
- `SynchronousScheduler` — calls `action()` immediately, ignores `delayMs`

### Test Cases (InventorySortControllerTests)

| Test | What it covers |
|---|---|
| `DoesNotSort_WhenNotLoggedIn` | Early exit on login check |
| `DoesNotSort_WhenDisabled` | Enabled flag respected |
| `Sorts_WhenInventoryJustOpened` | Closed→open transition triggers sort |
| `DoesNotSort_WhenAlreadyOpen` | No re-sort on sustained open |
| `DoesNotSortAgain_WithinCooldown` | Cooldown window blocks repeat sort |
| `SortsAgain_AfterCooldownExpires` | Sort fires again after cooldown |
| `DoesNotSort_DuringRetainerTransfer` | RetainerItemTransferProgress blocks sort |
| `SortsWithConfiguredCommands` | Executor receives commands from config |

## Real Implementations

- `GameState` — moves the existing `IsAddonReady` / `IsAddonVisible` unsafe logic out of `Plugin.cs`
- `MacroExecutor` — wraps `MacroManager.Execute`, logs error on exception
- `TickSchedulerAdapter` — `new TickScheduler(action, delayMs)`; never wrapped in `using` (self-disposes after firing)

## Future: Option B (IAddonLifecycle)

The `IGameState` interface is the natural seam for swapping polling for event-driven detection. A future `AddonLifecycleGameState` could subscribe to `IAddonLifecycle.RegisterPostSetup` for the three inventory addons and set an internal flag that `IsInventoryOpen()` reads. `InventorySortController` and all tests remain unchanged.

## Out of Scope

- Config UI window
- Per-inventory-type sort command customisation
- Retainer inventory sorting
