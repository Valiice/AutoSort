# Event-Driven Multi-Inventory Sorting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Framework.Update polling with IAddonLifecycle events and add opt-in retainer inventory sorting.

**Architecture:** `InventorySortController.Tick()` is replaced by `OnOpen(long nowMs)`, removing the `_wasOpen` state machine since addon lifecycle events already fire exactly once per open. Two controller instances share the same class — main inventory (with cooldown) and retainer inventory (`SortCooldownMs = 0`). `Plugin.cs` subscribes to `IAddonLifecycle.PostSetup` events for five addon names across the two groups.

**Tech Stack:** C# 12, .NET 9, Dalamud.NET.Sdk 14.0.1, xUnit 2.9.3, ECommons (Svc, ECommonsMain, MacroManager, TickScheduler)

---

## File Map

| File | Change |
|---|---|
| `AutoSort.Tests/InventorySortControllerTests.cs` | Replace `Tick()` with `OnOpen()`; remove `DoesNotSort_WhenAlreadyOpen`; add `DoesNotSort_WhenCommandsEmpty` |
| `AutoSort/InventorySortController.cs` | `OnOpen(long)` replaces `Tick(long)`; remove `_wasOpen`; add `Any()` guard |
| `AutoSort/IGameState.cs` | Remove `IsInventoryOpen()` |
| `AutoSort.Tests/Fakes/FakeGameState.cs` | Remove `InventoryOpen` property and `IsInventoryOpen()` |
| `AutoSort/GameState.cs` | Remove `IsInventoryOpen()` and `IsAddonReady()` helper |
| `AutoSort/Configuration.cs` | Add `RetainerSortEnabled` (bool) and `RetainerSortCommands` (List\<string\>) |
| `AutoSort/Plugin.cs` | IAddonLifecycle subscriptions; two controllers; nested `RetainerSortConfig` adapter |

---

## Task 1: Migrate tests and controller from Tick() to OnOpen()

**Files:**
- Modify: `AutoSort.Tests/InventorySortControllerTests.cs`
- Modify: `AutoSort/InventorySortController.cs`

Writing failing tests first: the new tests call `ctrl.OnOpen()` which doesn't exist yet. Compilation failure is the RED state.

- [ ] **Step 1: Rewrite the test file**

Replace the entire contents of `AutoSort.Tests/InventorySortControllerTests.cs`:

```csharp
using AutoSort.Tests.Fakes;
using Xunit;

namespace AutoSort.Tests;

public class InventorySortControllerTests
{
    private readonly FakeGameState _state = new();
    private readonly FakeMacroExecutor _executor = new();
    private readonly FakeSortConfiguration _config = new();
    private readonly SynchronousScheduler _scheduler = new();

    private InventorySortController MakeController() =>
        new(_state, _executor, _scheduler, _config);

    [Fact]
    public void Sorts_WhenOpened()
    {
        var ctrl = MakeController();
        ctrl.OnOpen(0);
        Assert.Single(_executor.Calls);
    }

    [Fact]
    public void DoesNotSortAgain_WithinCooldown()
    {
        _config.SortCooldownMs = 2000;
        var ctrl = MakeController();
        ctrl.OnOpen(1);    // first open — sorts
        ctrl.OnOpen(500);  // re-open — 500-1=499, which is <= 2000 — blocked
        Assert.Single(_executor.Calls);
    }

    [Fact]
    public void SortsAgain_AfterCooldownExpires()
    {
        _config.SortCooldownMs = 2000;
        var ctrl = MakeController();
        ctrl.OnOpen(1);     // first sort recorded at t=1
        ctrl.OnOpen(3000);  // 3000-1=2999, which is > 2000 — sorts again
        Assert.Equal(2, _executor.Calls.Count);
    }

    [Fact]
    public void DoesNotSort_WhenNotLoggedIn()
    {
        _state.IsLoggedIn = false;
        var ctrl = MakeController();
        ctrl.OnOpen(0);
        Assert.Empty(_executor.Calls);
    }

    [Fact]
    public void DoesNotSort_WhenDisabled()
    {
        _config.Enabled = false;
        var ctrl = MakeController();
        ctrl.OnOpen(0);
        Assert.Empty(_executor.Calls);
    }

    [Fact]
    public void DoesNotSort_DuringRetainerTransfer()
    {
        _state.VisibleAddons.Add("RetainerItemTransferProgress");
        var ctrl = MakeController();
        ctrl.OnOpen(0);
        Assert.Empty(_executor.Calls);
    }

    [Fact]
    public void SortsWithConfiguredCommands()
    {
        _config.SortCommands = new[] { "/foo", "/bar" };
        var ctrl = MakeController();
        ctrl.OnOpen(0);
        var executedCommands = Assert.Single(_executor.Calls);
        Assert.Equal(new[] { "/foo", "/bar" }, executedCommands);
    }
}
```

- [ ] **Step 2: Run tests — verify compile error (RED)**

```bash
dotnet test AutoSort.Tests/AutoSort.Tests.csproj
```

Expected: Build error — `'InventorySortController' does not contain a definition for 'OnOpen'`

- [ ] **Step 3: Update InventorySortController.cs**

Replace the entire file:

```csharp
namespace AutoSort;

public sealed class InventorySortController
{
    private readonly IGameState _gameState;
    private readonly IMacroExecutor _macroExecutor;
    private readonly IActionScheduler _scheduler;
    private readonly ISortConfiguration _config;

    private long _lastSortTime = long.MinValue / 2;

    public InventorySortController(
        IGameState gameState,
        IMacroExecutor macroExecutor,
        IActionScheduler scheduler,
        ISortConfiguration config)
    {
        _gameState = gameState;
        _macroExecutor = macroExecutor;
        _scheduler = scheduler;
        _config = config;
    }

    public void OnOpen(long nowMs)
    {
        if (!_config.Enabled) return;
        if (!_gameState.IsLoggedIn) return;
        if (_gameState.IsAddonVisible("RetainerItemTransferProgress")) return;
        if (nowMs - _lastSortTime <= _config.SortCooldownMs) return;

        _lastSortTime = nowMs;
        _scheduler.Schedule(() => _macroExecutor.Execute(_config.SortCommands), _config.ExecutionDelayMs);
    }
}
```

**Cooldown logic note:** The guard is `<= SortCooldownMs` (not `>`). With the default `SortCooldownMs = 0`, `nowMs - _lastSortTime <= 0` is false for any positive elapsed time, so the sort fires. With `SortCooldownMs = 2000`, any call within 2000ms of the last sort is blocked.

- [ ] **Step 4: Run tests — verify all pass (GREEN)**

```bash
dotnet test AutoSort.Tests/AutoSort.Tests.csproj
```

Expected: 7 tests pass, 0 failures. (No `DoesNotSort_WhenAlreadyOpen` — that concept no longer exists.)

- [ ] **Step 5: Commit**

```bash
git add AutoSort.Tests/InventorySortControllerTests.cs AutoSort/InventorySortController.cs
git commit -m "Replace Tick() with OnOpen(), remove _wasOpen state machine"
```

---

## Task 2: Add empty-commands guard (TDD)

**Files:**
- Modify: `AutoSort.Tests/InventorySortControllerTests.cs`
- Modify: `AutoSort/InventorySortController.cs`

Empty `SortCommands` is the retainer default state. Without a guard, `Schedule` would be called with an empty enumerable.

- [ ] **Step 1: Add the failing test**

Append to the test class in `AutoSort.Tests/InventorySortControllerTests.cs` (inside the class, before the closing `}`):

```csharp
    [Fact]
    public void DoesNotSort_WhenCommandsEmpty()
    {
        _config.SortCommands = new string[0];
        var ctrl = MakeController();
        ctrl.OnOpen(0);
        Assert.Empty(_executor.Calls);
    }
```

- [ ] **Step 2: Run test — verify it fails (RED)**

```bash
dotnet test AutoSort.Tests/AutoSort.Tests.csproj --filter "FullyQualifiedName~DoesNotSort_WhenCommandsEmpty"
```

Expected: FAIL — `Assert.Empty() Failure: Collection [[ ]]` (scheduler was called with empty list)

- [ ] **Step 3: Add the guard to OnOpen**

In `AutoSort/InventorySortController.cs`, add `using System.Linq;` at the top, then add the empty-commands guard as the second check in `OnOpen`:

```csharp
using System.Linq;

namespace AutoSort;

public sealed class InventorySortController
{
    private readonly IGameState _gameState;
    private readonly IMacroExecutor _macroExecutor;
    private readonly IActionScheduler _scheduler;
    private readonly ISortConfiguration _config;

    private long _lastSortTime = long.MinValue / 2;

    public InventorySortController(
        IGameState gameState,
        IMacroExecutor macroExecutor,
        IActionScheduler scheduler,
        ISortConfiguration config)
    {
        _gameState = gameState;
        _macroExecutor = macroExecutor;
        _scheduler = scheduler;
        _config = config;
    }

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
}
```

- [ ] **Step 4: Run all tests — verify all pass (GREEN)**

```bash
dotnet test AutoSort.Tests/AutoSort.Tests.csproj
```

Expected: 8 tests pass, 0 failures.

- [ ] **Step 5: Commit**

```bash
git add AutoSort/InventorySortController.cs AutoSort.Tests/InventorySortControllerTests.cs
git commit -m "Guard against empty sort commands"
```

---

## Task 3: Remove IsInventoryOpen() from IGameState, FakeGameState, and GameState

**Files:**
- Modify: `AutoSort/IGameState.cs`
- Modify: `AutoSort.Tests/Fakes/FakeGameState.cs`
- Modify: `AutoSort/GameState.cs`

No test changes needed — no test currently calls `IsInventoryOpen()`. Running the test suite after each file change confirms nothing breaks.

- [ ] **Step 1: Update IGameState.cs**

Replace the entire file:

```csharp
namespace AutoSort;

public interface IGameState
{
    bool IsLoggedIn { get; }
    bool IsAddonVisible(string name);
}
```

- [ ] **Step 2: Update FakeGameState.cs**

Replace the entire file:

```csharp
using System.Collections.Generic;

namespace AutoSort.Tests.Fakes;

public class FakeGameState : IGameState
{
    public bool IsLoggedIn { get; set; } = true;
    public HashSet<string> VisibleAddons { get; } = new();

    public bool IsAddonVisible(string name) => VisibleAddons.Contains(name);
}
```

- [ ] **Step 3: Update GameState.cs**

Replace the entire file (`IsInventoryOpen()` and its `IsAddonReady` helper are both removed):

```csharp
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
```

- [ ] **Step 4: Run all tests — verify all pass**

```bash
dotnet test AutoSort.Tests/AutoSort.Tests.csproj
```

Expected: 8 tests pass, 0 failures.

- [ ] **Step 5: Commit**

```bash
git add AutoSort/IGameState.cs AutoSort.Tests/Fakes/FakeGameState.cs AutoSort/GameState.cs
git commit -m "Remove IsInventoryOpen() — replaced by IAddonLifecycle events"
```

---

## Task 4: Add retainer fields to Configuration

**Files:**
- Modify: `AutoSort/Configuration.cs`

No behaviour change for existing users — both fields default to off/empty.

- [ ] **Step 1: Add retainer fields to Configuration.cs**

The current file ends with `IReadOnlyList<string> ISortConfiguration.SortCommands => SortCommands;` and `Save(...)`. Add two new fields after `SortCommands`:

```csharp
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

    public bool RetainerSortEnabled { get; set; } = false;
    public List<string> RetainerSortCommands { get; set; } = new();

    IReadOnlyList<string> ISortConfiguration.SortCommands => SortCommands;

    public void Save(IDalamudPluginInterface pi) => pi.SavePluginConfig(this);
}
```

- [ ] **Step 2: Build to verify it compiles**

```bash
dotnet build AutoSort/AutoSort.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AutoSort/Configuration.cs
git commit -m "Add RetainerSortEnabled and RetainerSortCommands to Configuration"
```

---

## Task 5: Refactor Plugin.cs — IAddonLifecycle, two controllers, RetainerSortConfig

**Files:**
- Modify: `AutoSort/Plugin.cs`

This task replaces `Framework.Update` with `IAddonLifecycle` event subscriptions and wires up the second (retainer) controller. The nested `RetainerSortConfig` class adapts `Configuration`'s retainer fields into `ISortConfiguration`.

- [ ] **Step 1: Replace Plugin.cs**

Replace the entire file:

```csharp
using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;

namespace AutoSort;

public sealed class Plugin : IDalamudPlugin
{
    private readonly InventorySortController _mainController;
    private readonly InventorySortController _retainerController;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var gameState = new GameState();
        _mainController = new InventorySortController(
            gameState, new MacroExecutor(), new TickSchedulerAdapter(), config);
        _retainerController = new InventorySortController(
            gameState, new MacroExecutor(), new TickSchedulerAdapter(), new RetainerSortConfig(config));

        Svc.AddonLifecycle.RegisterListener(
            AddonEvent.PostSetup,
            ["Inventory", "InventoryLarge", "InventoryExpansion"],
            OnMainInventoryOpen);
        Svc.AddonLifecycle.RegisterListener(
            AddonEvent.PostSetup,
            ["InventoryRetainer", "InventoryRetainerLarge"],
            OnRetainerInventoryOpen);
    }

    private void OnMainInventoryOpen(AddonEvent type, AddonArgs args) =>
        _mainController.OnOpen(DateTimeOffset.Now.ToUnixTimeMilliseconds());

    private void OnRetainerInventoryOpen(AddonEvent type, AddonArgs args) =>
        _retainerController.OnOpen(DateTimeOffset.Now.ToUnixTimeMilliseconds());

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(OnMainInventoryOpen, OnRetainerInventoryOpen);
        ECommonsMain.Dispose();
    }

    private sealed class RetainerSortConfig : ISortConfiguration
    {
        private readonly Configuration _cfg;
        public RetainerSortConfig(Configuration cfg) => _cfg = cfg;
        public bool Enabled => _cfg.RetainerSortEnabled;
        public int SortCooldownMs => 0;
        public int ExecutionDelayMs => _cfg.ExecutionDelayMs;
        public IReadOnlyList<string> SortCommands => _cfg.RetainerSortCommands;
    }
}
```

**Key notes:**
- `AddonEvent.PostSetup` fires once when an addon finishes its setup (becomes visible/ready) — this is the open event.
- `UnregisterListener(OnMainInventoryOpen, OnRetainerInventoryOpen)` removes both handlers in one call (params overload).
- `RetainerSortConfig.SortCooldownMs` is hardcoded to `0` — retainer sorts every open with no cooldown.
- Both controllers share the same `GameState` instance (read-only, safe to share).
- Each controller gets its own `MacroExecutor` and `TickSchedulerAdapter` instance (stateless, fine to duplicate).

- [ ] **Step 2: Build the full solution**

```bash
dotnet build AutoSort.sln
```

Expected: Build succeeded, 0 errors. If `Dalamud.Game.Addon.Lifecycle.AddonArgTypes` does not exist in this SDK version, change to `using Dalamud.Game.Addon.Lifecycle;` only (AddonArgs may be in the same namespace).

- [ ] **Step 3: Run tests — verify all still pass**

```bash
dotnet test AutoSort.Tests/AutoSort.Tests.csproj
```

Expected: 8 tests pass, 0 failures.

- [ ] **Step 4: Commit**

```bash
git add AutoSort/Plugin.cs
git commit -m "Switch to IAddonLifecycle events, add retainer inventory sorting"
```
