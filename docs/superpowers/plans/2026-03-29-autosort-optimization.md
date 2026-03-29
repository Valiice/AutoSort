# AutoSort Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor `Plugin.cs` into focused, testable units; add an xUnit test project with full coverage of the sort state machine; expose configuration for cooldown, delay, and sort commands.

**Architecture:** Extract four interfaces (`IGameState`, `IMacroExecutor`, `IActionScheduler`, `ISortConfiguration`) so `InventorySortController` depends on no Dalamud types. Real implementations (`GameState`, `MacroExecutor`, `TickSchedulerAdapter`) hold all game API calls. `Plugin.cs` becomes a thin bootstrap that wires dependencies and forwards `Framework.Update` to `controller.Tick(nowMs)`.

**Tech Stack:** C# / .NET 10, Dalamud.NET.Sdk 14.0.1, ECommons (submodule), xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.12.0

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Create | `AutoSort/IGameState.cs` | Interface: login state + addon visibility |
| Create | `AutoSort/IMacroExecutor.cs` | Interface: execute sort commands |
| Create | `AutoSort/IActionScheduler.cs` | Interface: schedule a delayed action |
| Create | `AutoSort/ISortConfiguration.cs` | Interface: enabled, cooldown, commands |
| Create | `AutoSort/Configuration.cs` | IPluginConfiguration + ISortConfiguration with defaults |
| Create | `AutoSort/InventorySortController.cs` | State machine: `Tick(long nowMs)` |
| Create | `AutoSort/GameState.cs` | Real IGameState (unsafe addon checks) |
| Create | `AutoSort/MacroExecutor.cs` | Real IMacroExecutor (wraps MacroManager) |
| Create | `AutoSort/TickSchedulerAdapter.cs` | Real IActionScheduler (wraps TickScheduler) |
| Modify | `AutoSort/Plugin.cs` | Thin bootstrap only |
| Create | `AutoSort.Tests/AutoSort.Tests.csproj` | xUnit test project |
| Modify | `AutoSort.sln` | Add test project |
| Create | `AutoSort.Tests/Fakes/FakeGameState.cs` | Controllable IGameState stub |
| Create | `AutoSort.Tests/Fakes/FakeMacroExecutor.cs` | Records Execute calls |
| Create | `AutoSort.Tests/Fakes/FakeSortConfiguration.cs` | Mutable config for tests |
| Create | `AutoSort.Tests/Fakes/SynchronousScheduler.cs` | Runs action immediately |
| Create | `AutoSort.Tests/InventorySortControllerTests.cs` | All controller tests |

---

## Task 1: Extract Interfaces, Configuration, and Stub Controller

**Files:**
- Create: `AutoSort/IGameState.cs`
- Create: `AutoSort/IMacroExecutor.cs`
- Create: `AutoSort/IActionScheduler.cs`
- Create: `AutoSort/ISortConfiguration.cs`
- Create: `AutoSort/Configuration.cs`
- Create: `AutoSort/InventorySortController.cs`

- [ ] **Step 1: Create `AutoSort/IGameState.cs`**

```csharp
namespace AutoSort;

public interface IGameState
{
    bool IsLoggedIn { get; }
    bool IsInventoryOpen();
    bool IsAddonVisible(string name);
}
```

- [ ] **Step 2: Create `AutoSort/IMacroExecutor.cs`**

```csharp
using System.Collections.Generic;

namespace AutoSort;

public interface IMacroExecutor
{
    void Execute(IEnumerable<string> commands);
}
```

- [ ] **Step 3: Create `AutoSort/IActionScheduler.cs`**

Note: ECommons defines its own `IScheduler` in `ECommons.Schedulers` — this interface is named `IActionScheduler` to avoid the collision.

```csharp
using System;

namespace AutoSort;

public interface IActionScheduler
{
    void Schedule(Action action, int delayMs);
}
```

- [ ] **Step 4: Create `AutoSort/ISortConfiguration.cs`**

```csharp
using System.Collections.Generic;

namespace AutoSort;

public interface ISortConfiguration
{
    bool Enabled { get; }
    int SortCooldownMs { get; }
    int ExecutionDelayMs { get; }
    IReadOnlyList<string> SortCommands { get; }
}
```

- [ ] **Step 5: Create `AutoSort/Configuration.cs`**

```csharp
using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace AutoSort;

[Serializable]
public class Configuration : IPluginConfiguration, ISortConfiguration
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

    IReadOnlyList<string> ISortConfiguration.SortCommands => SortCommands;

    public void Save(IDalamudPluginInterface pi) => pi.SavePluginConfig(this);
}
```

- [ ] **Step 6: Create `AutoSort/InventorySortController.cs` (stub — empty Tick)**

```csharp
namespace AutoSort;

public class InventorySortController
{
    private readonly IGameState _gameState;
    private readonly IMacroExecutor _macroExecutor;
    private readonly IActionScheduler _scheduler;
    private readonly ISortConfiguration _config;

    private bool _wasOpen;
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

    public void Tick(long nowMs) { }
}
```

`_lastSortTime` is initialized to `long.MinValue / 2` (not `0`) so the very first inventory open always fires — regardless of `SortCooldownMs`. The value is far enough from `long.MaxValue` that `nowMs - _lastSortTime` cannot overflow.

- [ ] **Step 7: Verify the plugin project compiles**

Run:
```
dotnet build AutoSort/AutoSort.csproj -c Debug
```

Expected: build succeeds. Fix any compilation errors before continuing.

- [ ] **Step 8: Commit**

```
git add AutoSort/IGameState.cs AutoSort/IMacroExecutor.cs AutoSort/IActionScheduler.cs AutoSort/ISortConfiguration.cs AutoSort/Configuration.cs AutoSort/InventorySortController.cs
git commit -m "Add interfaces, Configuration, and stub InventorySortController"
```

---

## Task 2: Create Test Project and Fakes

**Files:**
- Create: `AutoSort.Tests/AutoSort.Tests.csproj`
- Modify: `AutoSort.sln`
- Create: `AutoSort.Tests/Fakes/FakeGameState.cs`
- Create: `AutoSort.Tests/Fakes/FakeMacroExecutor.cs`
- Create: `AutoSort.Tests/Fakes/FakeSortConfiguration.cs`
- Create: `AutoSort.Tests/Fakes/SynchronousScheduler.cs`

- [ ] **Step 1: Create `AutoSort.Tests/AutoSort.Tests.csproj`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Dalamud.NET.Sdk/14.0.1">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\AutoSort\AutoSort.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add test project to solution**

Run:
```
dotnet sln AutoSort.sln add AutoSort.Tests/AutoSort.Tests.csproj
```

Expected: `Project "AutoSort.Tests/AutoSort.Tests.csproj" added to the solution.`

- [ ] **Step 3: Create `AutoSort.Tests/Fakes/FakeGameState.cs`**

```csharp
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
```

- [ ] **Step 4: Create `AutoSort.Tests/Fakes/FakeMacroExecutor.cs`**

```csharp
using System.Collections.Generic;

namespace AutoSort.Tests.Fakes;

public class FakeMacroExecutor : IMacroExecutor
{
    public List<IEnumerable<string>> Calls { get; } = new();
    public void Execute(IEnumerable<string> commands) => Calls.Add(commands);
}
```

- [ ] **Step 5: Create `AutoSort.Tests/Fakes/FakeSortConfiguration.cs`**

`SortCooldownMs` defaults to `0` so tests that don't care about cooldown can use simple timestamps like `Tick(0)` / `Tick(1)` without needing to account for a non-zero cooldown window.

```csharp
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
```

- [ ] **Step 6: Create `AutoSort.Tests/Fakes/SynchronousScheduler.cs`**

Calls the action immediately and ignores `delayMs` so tests can assert on executor calls synchronously.

```csharp
using System;

namespace AutoSort.Tests.Fakes;

public class SynchronousScheduler : IActionScheduler
{
    public void Schedule(Action action, int delayMs) => action();
}
```

- [ ] **Step 7: Build and run tests (0 tests expected)**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug
```

Expected output contains: `No test is available` or `Total tests: 0`. Fix any build errors before continuing.

- [ ] **Step 8: Commit**

```
git add AutoSort.Tests/ AutoSort.sln
git commit -m "Add xUnit test project and fakes"
```

---

## Task 3: TDD — Core Sort Behavior

**Files:**
- Create: `AutoSort.Tests/InventorySortControllerTests.cs`
- Modify: `AutoSort/InventorySortController.cs`

### Cycle 1: Sorts_WhenInventoryJustOpened

- [ ] **Step 1: Create test file with first failing test**

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
    public void Sorts_WhenInventoryJustOpened()
    {
        var ctrl = MakeController();
        _state.InventoryOpen = false;
        ctrl.Tick(0);
        _state.InventoryOpen = true;
        ctrl.Tick(1);
        Assert.Single(_executor.Calls);
    }
}
```

- [ ] **Step 2: Run test — verify it fails**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug --filter "Sorts_WhenInventoryJustOpened"
```

Expected: FAIL — `_executor.Calls` is empty because `Tick` is a no-op.

- [ ] **Step 3: Implement transition detection in `InventorySortController.Tick`**

Replace the empty `Tick` body:

```csharp
public void Tick(long nowMs)
{
    var isOpen = _gameState.IsInventoryOpen();

    if (isOpen && !_wasOpen && nowMs - _lastSortTime > _config.SortCooldownMs)
    {
        _lastSortTime = nowMs;
        _scheduler.Schedule(() => _macroExecutor.Execute(_config.SortCommands), _config.ExecutionDelayMs);
    }

    _wasOpen = isOpen;
}
```

- [ ] **Step 4: Run test — verify it passes**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug --filter "Sorts_WhenInventoryJustOpened"
```

Expected: PASS.

### Cycle 2: DoesNotSort_WhenAlreadyOpen

- [ ] **Step 5: Add second test to the test class**

```csharp
[Fact]
public void DoesNotSort_WhenAlreadyOpen()
{
    var ctrl = MakeController();
    _state.InventoryOpen = false;
    ctrl.Tick(0);
    _state.InventoryOpen = true;
    ctrl.Tick(1);  // transition — sorts once
    ctrl.Tick(2);  // still open — must not sort again
    Assert.Single(_executor.Calls);
}
```

- [ ] **Step 6: Run test — verify it passes (no code change needed)**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug --filter "DoesNotSort_WhenAlreadyOpen"
```

Expected: PASS — `_wasOpen` is set to `true` after the first open tick, so the condition `!_wasOpen` is false on subsequent ticks.

- [ ] **Step 7: Commit**

```
git add AutoSort.Tests/InventorySortControllerTests.cs AutoSort/InventorySortController.cs
git commit -m "TDD: core sort transition behavior"
```

---

## Task 4: TDD — Cooldown

**Files:**
- Modify: `AutoSort.Tests/InventorySortControllerTests.cs`

`SortCooldownMs` is set to `2000` explicitly in these tests. `_lastSortTime` starts at `long.MinValue / 2` so the initial sort always fires regardless of `nowMs`.

### Cycle 1: DoesNotSortAgain_WithinCooldown

- [ ] **Step 1: Add failing test**

```csharp
[Fact]
public void DoesNotSortAgain_WithinCooldown()
{
    _config.SortCooldownMs = 2000;
    var ctrl = MakeController();
    _state.InventoryOpen = false; ctrl.Tick(0);
    _state.InventoryOpen = true;  ctrl.Tick(1);    // first open — sorts (1 - long.MinValue/2 >> 2000)
    _state.InventoryOpen = false; ctrl.Tick(2);
    _state.InventoryOpen = true;  ctrl.Tick(500);  // re-open — 500 - 1 = 499, not > 2000
    Assert.Single(_executor.Calls);
}
```

- [ ] **Step 2: Run test — verify it passes (no code change needed)**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug --filter "DoesNotSortAgain_WithinCooldown"
```

Expected: PASS — the cooldown check `nowMs - _lastSortTime > SortCooldownMs` is already in `Tick`.

### Cycle 2: SortsAgain_AfterCooldownExpires

- [ ] **Step 3: Add test**

```csharp
[Fact]
public void SortsAgain_AfterCooldownExpires()
{
    _config.SortCooldownMs = 2000;
    var ctrl = MakeController();
    _state.InventoryOpen = false; ctrl.Tick(0);
    _state.InventoryOpen = true;  ctrl.Tick(1);     // first sort at t=1
    _state.InventoryOpen = false; ctrl.Tick(2);
    _state.InventoryOpen = true;  ctrl.Tick(3000);  // 3000 - 1 = 2999 > 2000 — sorts again
    Assert.Equal(2, _executor.Calls.Count);
}
```

- [ ] **Step 4: Run both cooldown tests**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug --filter "Cooldown"
```

Expected: both PASS.

- [ ] **Step 5: Commit**

```
git add AutoSort.Tests/InventorySortControllerTests.cs
git commit -m "TDD: cooldown behavior"
```

---

## Task 5: TDD — Guard Conditions

**Files:**
- Modify: `AutoSort.Tests/InventorySortControllerTests.cs`
- Modify: `AutoSort/InventorySortController.cs`

### Cycle 1: DoesNotSort_WhenNotLoggedIn

- [ ] **Step 1: Add failing test**

```csharp
[Fact]
public void DoesNotSort_WhenNotLoggedIn()
{
    _state.IsLoggedIn = false;
    _state.InventoryOpen = false;
    var ctrl = MakeController();
    ctrl.Tick(0);
    _state.InventoryOpen = true;
    ctrl.Tick(1);
    Assert.Empty(_executor.Calls);
}
```

- [ ] **Step 2: Run — verify it fails**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug --filter "DoesNotSort_WhenNotLoggedIn"
```

Expected: FAIL — `Tick` ignores `IsLoggedIn`.

- [ ] **Step 3: Add login guard to `Tick`**

```csharp
public void Tick(long nowMs)
{
    if (!_gameState.IsLoggedIn) return;

    var isOpen = _gameState.IsInventoryOpen();

    if (isOpen && !_wasOpen && nowMs - _lastSortTime > _config.SortCooldownMs)
    {
        _lastSortTime = nowMs;
        _scheduler.Schedule(() => _macroExecutor.Execute(_config.SortCommands), _config.ExecutionDelayMs);
    }

    _wasOpen = isOpen;
}
```

- [ ] **Step 4: Run — verify it passes**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug --filter "DoesNotSort_WhenNotLoggedIn"
```

Expected: PASS.

### Cycle 2: DoesNotSort_WhenDisabled

- [ ] **Step 5: Add failing test**

```csharp
[Fact]
public void DoesNotSort_WhenDisabled()
{
    _config.Enabled = false;
    _state.InventoryOpen = false;
    var ctrl = MakeController();
    ctrl.Tick(0);
    _state.InventoryOpen = true;
    ctrl.Tick(1);
    Assert.Empty(_executor.Calls);
}
```

- [ ] **Step 6: Run — verify it fails**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug --filter "DoesNotSort_WhenDisabled"
```

Expected: FAIL — `Tick` ignores `Enabled`.

- [ ] **Step 7: Add enabled guard to `Tick`**

```csharp
public void Tick(long nowMs)
{
    if (!_config.Enabled) return;
    if (!_gameState.IsLoggedIn) return;

    var isOpen = _gameState.IsInventoryOpen();

    if (isOpen && !_wasOpen && nowMs - _lastSortTime > _config.SortCooldownMs)
    {
        _lastSortTime = nowMs;
        _scheduler.Schedule(() => _macroExecutor.Execute(_config.SortCommands), _config.ExecutionDelayMs);
    }

    _wasOpen = isOpen;
}
```

- [ ] **Step 8: Run — verify it passes**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug --filter "DoesNotSort_WhenDisabled"
```

Expected: PASS.

### Cycle 3: DoesNotSort_DuringRetainerTransfer

- [ ] **Step 9: Add failing test**

```csharp
[Fact]
public void DoesNotSort_DuringRetainerTransfer()
{
    _state.InventoryOpen = false;
    var ctrl = MakeController();
    ctrl.Tick(0);
    _state.VisibleAddons.Add("RetainerItemTransferProgress");
    _state.InventoryOpen = true;
    ctrl.Tick(1);
    Assert.Empty(_executor.Calls);
}
```

- [ ] **Step 10: Run — verify it fails**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug --filter "DoesNotSort_DuringRetainerTransfer"
```

Expected: FAIL — `Tick` ignores `RetainerItemTransferProgress`.

- [ ] **Step 11: Add retainer guard to `Tick` — final full implementation**

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

- [ ] **Step 12: Run all tests — verify all pass**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug
```

Expected: all 6 tests PASS.

- [ ] **Step 13: Commit**

```
git add AutoSort.Tests/InventorySortControllerTests.cs AutoSort/InventorySortController.cs
git commit -m "TDD: guard conditions (login, enabled, retainer transfer)"
```

---

## Task 6: TDD — Configured Commands

**Files:**
- Modify: `AutoSort.Tests/InventorySortControllerTests.cs`

- [ ] **Step 1: Add test**

```csharp
[Fact]
public void SortsWithConfiguredCommands()
{
    _config.SortCommands = new[] { "/foo", "/bar" };
    var ctrl = MakeController();
    _state.InventoryOpen = false;
    ctrl.Tick(0);
    _state.InventoryOpen = true;
    ctrl.Tick(1);
    var executedCommands = Assert.Single(_executor.Calls);
    Assert.Equal(new[] { "/foo", "/bar" }, executedCommands);
}
```

- [ ] **Step 2: Run — verify it passes (no code change needed)**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug --filter "SortsWithConfiguredCommands"
```

Expected: PASS — `Tick` already passes `_config.SortCommands` to the executor.

- [ ] **Step 3: Run full suite**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug
```

Expected: all 7 tests PASS.

- [ ] **Step 4: Commit**

```
git add AutoSort.Tests/InventorySortControllerTests.cs
git commit -m "TDD: configured sort commands"
```

---

## Task 7: Add Real Implementations

**Files:**
- Create: `AutoSort/GameState.cs`
- Create: `AutoSort/MacroExecutor.cs`
- Create: `AutoSort/TickSchedulerAdapter.cs`

- [ ] **Step 1: Create `AutoSort/GameState.cs`**

Moves the unsafe addon logic out of `Plugin.cs`.

```csharp
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSort;

public sealed unsafe class GameState : IGameState
{
    public bool IsLoggedIn => Svc.ClientState.IsLoggedIn;

    public bool IsInventoryOpen() =>
        IsAddonReady("Inventory") ||
        IsAddonReady("InventoryLarge") ||
        IsAddonReady("InventoryExpansion");

    public bool IsAddonVisible(string name)
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>(name, out var addon))
            return addon->IsVisible;
        return false;
    }

    private static bool IsAddonReady(string name)
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>(name, out var addon))
            return GenericHelpers.IsAddonReady(addon);
        return false;
    }
}
```

- [ ] **Step 2: Create `AutoSort/MacroExecutor.cs`**

```csharp
using System;
using System.Collections.Generic;
using ECommons.Automation;
using ECommons.Logging;

namespace AutoSort;

public class MacroExecutor : IMacroExecutor
{
    public void Execute(IEnumerable<string> commands)
    {
        try
        {
            MacroManager.Execute(commands);
        }
        catch (Exception e)
        {
            PluginLog.Error($"AutoSort: Macro execution failed: {e.Message}");
        }
    }
}
```

- [ ] **Step 3: Create `AutoSort/TickSchedulerAdapter.cs`**

`TickScheduler` self-disposes after firing — never wrap it in `using`.

```csharp
using System;
using ECommons.Schedulers;

namespace AutoSort;

public class TickSchedulerAdapter : IActionScheduler
{
    public void Schedule(Action action, int delayMs) =>
        new TickScheduler(action, delayMs);
}
```

- [ ] **Step 4: Build plugin project**

Run:
```
dotnet build AutoSort/AutoSort.csproj -c Debug
```

Expected: build succeeds. Fix any compilation errors before continuing.

- [ ] **Step 5: Commit**

```
git add AutoSort/GameState.cs AutoSort/MacroExecutor.cs AutoSort/TickSchedulerAdapter.cs
git commit -m "Add real implementations: GameState, MacroExecutor, TickSchedulerAdapter"
```

---

## Task 8: Refactor Plugin.cs

**Files:**
- Modify: `AutoSort/Plugin.cs`

- [ ] **Step 1: Replace `Plugin.cs` with the thin bootstrap**

```csharp
using System;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;

namespace AutoSort;

public sealed class Plugin : IDalamudPlugin
{
    private readonly InventorySortController _controller;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _controller = new InventorySortController(
            new GameState(),
            new MacroExecutor(),
            new TickSchedulerAdapter(),
            config);
        Svc.Framework.Update += OnUpdate;
    }

    private void OnUpdate(object _) =>
        _controller.Tick(DateTimeOffset.Now.ToUnixTimeMilliseconds());

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        ECommonsMain.Dispose();
    }
}
```

- [ ] **Step 2: Build the full plugin**

Run:
```
dotnet build AutoSort/AutoSort.csproj -c Debug
```

Expected: build succeeds with no errors or warnings about missing types.

- [ ] **Step 3: Run full test suite one final time**

Run:
```
dotnet test AutoSort.Tests/AutoSort.Tests.csproj -c Debug
```

Expected: all 7 tests PASS.

- [ ] **Step 4: Commit**

```
git add AutoSort/Plugin.cs
git commit -m "Refactor Plugin.cs to thin bootstrap"
```
