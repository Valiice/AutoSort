# Event-Driven Multi-Inventory Sorting Design

**Date:** 2026-03-29
**Status:** Approved

## Goal

Replace `Framework.Update` polling with `IAddonLifecycle` event-driven detection, and add retainer inventory sorting support.

## Architecture

`InventorySortController.Tick()` is replaced by `OnOpen(long nowMs)`. The `_wasOpen` field is removed — `IAddonLifecycle` events fire exactly once on the closed→open transition, making it redundant. Two controller instances are wired in `Plugin.cs`: one for the three main inventory addons (with cooldown), one for the two retainer inventory addons (cooldown = 0, sorts every open). `Framework.Update` is no longer subscribed to.

```
AutoSort/
  Plugin.cs                     ← subscribe to IAddonLifecycle; two controllers; no Framework.Update
  InventorySortController.cs    ← OnOpen(long) replaces Tick(long); _wasOpen removed
  IGameState.cs                 ← remove IsInventoryOpen(); keep IsLoggedIn + IsAddonVisible
  GameState.cs                  ← remove IsInventoryOpen() implementation
  Configuration.cs              ← add RetainerSortEnabled (bool), RetainerSortCommands (List<string>)
  IMacroExecutor.cs             ← unchanged
  IActionScheduler.cs           ← unchanged
  ISortConfiguration.cs         ← unchanged
  MacroExecutor.cs              ← unchanged
  TickSchedulerAdapter.cs       ← unchanged

AutoSort.Tests/
  Fakes/
    FakeGameState.cs            ← remove InventoryOpen property
  InventorySortControllerTests.cs ← replace Tick() calls with OnOpen(); update test list
```

## Interfaces

### IGameState (modified)

```csharp
public interface IGameState
{
    bool IsLoggedIn { get; }
    bool IsAddonVisible(string name);
}
```

`IsInventoryOpen()` removed. Detection is now event-driven, not polled.

### ISortConfiguration, IMacroExecutor, IActionScheduler

Unchanged.

## InventorySortController

`_wasOpen` removed. `OnOpen(long nowMs)` is the sole entry point:

```csharp
public void OnOpen(long nowMs)
{
    if (!_config.Enabled) return;
    if (!_config.SortCommands.Any()) return;
    if (!_gameState.IsLoggedIn) return;
    if (_gameState.IsAddonVisible("RetainerItemTransferProgress")) return;
    if (nowMs - _lastSortTime <= _config.SortCooldownMs) return;

    _lastSortTime = nowMs;
    _scheduler.Schedule(() => _macroExecutor.Execute(_config.SortCommands), _config.ExecutionDelayMs);
}
```

The cooldown guard (`<= SortCooldownMs`) replaces the old `> SortCooldownMs` sense. With `SortCooldownMs = 0`, the guard is always false (any positive elapsed time passes), so retainer sorts every open.

## Plugin.cs

Subscribes to `IAddonLifecycle` events for five addons across two groups:

```csharp
Svc.AddonLifecycle.RegisterListener(
    AddonEvent.PostSetup,
    ["Inventory", "InventoryLarge", "InventoryExpansion"],
    OnMainInventoryOpen);

Svc.AddonLifecycle.RegisterListener(
    AddonEvent.PostSetup,
    ["InventoryRetainer", "InventoryRetainerLarge"],
    OnRetainerInventoryOpen);

private void OnMainInventoryOpen(AddonEvent type, AddonArgs args) =>
    _mainController.OnOpen(DateTimeOffset.Now.ToUnixTimeMilliseconds());

private void OnRetainerInventoryOpen(AddonEvent type, AddonArgs args) =>
    _retainerController.OnOpen(DateTimeOffset.Now.ToUnixTimeMilliseconds());
```

`Dispose()` calls `Svc.AddonLifecycle.UnregisterListener` for both groups.

A private nested class adapts `Configuration`'s retainer fields into `ISortConfiguration` for the retainer controller:

```csharp
private sealed class RetainerSortConfig : ISortConfiguration
{
    private readonly Configuration _cfg;
    public RetainerSortConfig(Configuration cfg) => _cfg = cfg;
    public bool Enabled => _cfg.RetainerSortEnabled;
    public int SortCooldownMs => 0;
    public int ExecutionDelayMs => _cfg.ExecutionDelayMs;
    public IReadOnlyList<string> SortCommands => _cfg.RetainerSortCommands;
}
```

## Configuration

Two new fields added to `Configuration`:

| Field | Type | Default |
|---|---|---|
| `RetainerSortEnabled` | `bool` | `false` |
| `RetainerSortCommands` | `List<string>` | `new()` (empty) |

`RetainerSortEnabled` defaults to `false` so retainer sorting is opt-in. Existing behaviour is unchanged for users who have not configured it.

`RetainerSortCommands` defaults empty — until the user populates it, `DoesNotSort_WhenCommandsEmpty` covers this: an empty command list means the scheduler is never called.

## Test Project

### FakeGameState (modified)

Remove `InventoryOpen` property and the `IsInventoryOpen()` method.

### InventorySortControllerTests (updated)

All `Tick()` calls replaced with `OnOpen()`. Tests that relied on `_wasOpen` state (sustained-open) are removed. One new test covers empty commands.

| Test | What it covers |
|---|---|
| `Sorts_WhenOpened` | OnOpen triggers sort |
| `DoesNotSort_WhenNotLoggedIn` | Login guard |
| `DoesNotSort_WhenDisabled` | Enabled flag |
| `DoesNotSortAgain_WithinCooldown` | Cooldown blocks repeat call |
| `SortsAgain_AfterCooldownExpires` | Cooldown expires correctly |
| `DoesNotSort_DuringRetainerTransfer` | RetainerItemTransferProgress guard |
| `SortsWithConfiguredCommands` | Executor receives commands from config |
| `DoesNotSort_WhenCommandsEmpty` | Empty command list = no sort (retainer default) |

`FakeSortConfiguration` gains no new fields — retainer behaviour is covered by setting `SortCooldownMs = 0` (already the default) and empty `SortCommands`.

## Real Implementations

- `GameState` — remove `IsInventoryOpen()` and its `IsAddonReady` helper
- `Plugin.cs` — unsubscribe from `Framework.Update`; subscribe/unsubscribe `IAddonLifecycle` listeners

## Out of Scope

- Config UI for retainer commands
- Per-retainer sort command customisation
- Armory board / chocobo saddlebag sorting
