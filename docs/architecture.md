# Architecture

This is a mono-repo containing two components that together enable Risk of Rain 2 to participate in [Archipelago](https://archipelago.gg/) multiworld randomizer games.

## Repository Layout

```
ror2_archipelago_enhanced/
├── Archipelago.RiskOfRain2/          # C# BepInEx mod (game client)
│   ├── Archipelago.RiskOfRain2.csproj
│   ├── Archipelago/                  # Core partial classes
│   │   ├── ArchipelagoPlugin.cs          # BepInEx entry point
│   │   ├── ArchipelagoClient.cs          # AP session state & fields
│   │   ├── ArchipelagoClient.Connection.cs    # Connect, slot data, reconnection
│   │   ├── ArchipelagoClient.RunLifecycle.cs  # SetupRun, CleanupRun, victory, hooks
│   │   ├── ArchipelagoItemLogicController.cs       # Item pickup tracking & checks
│   │   ├── ArchipelagoItemLogicController.ItemGrant.cs  # Item granting & traps
│   │   └── ArchipelagoItemLogicController.Queues.cs     # Item queue processing
│   ├── Console/
│   │   └── ArchipelagoConsoleCommand.cs   # In-game console commands
│   ├── Extensions/                   # Utility extension methods
│   │   ├── IEnumerableExtensions.cs  # PickRandom helper
│   │   └── LocationExtensions.cs     # Scene ID <-> name mappings
│   ├── Interfaces/
│   │   └── IService.cs               # Register/Unregister interface
│   ├── Services/
│   │   ├── ClientItemsService.cs     # Client-side item bar (non-host)
│   │   ├── DeathLinkManager.cs       # Cross-game death synchronization
│   │   ├── LocationCheckService.cs   # Explore mode location detection
│   │   ├── LocationInformationTemplate.cs  # Per-environment check counts
│   │   ├── ShrineChanceService.cs    # Shrine reward modification
│   │   ├── StageBlockerService.cs    # Stage unlock gating
│   │   ├── ItemPoolService.cs        # Item pool limiting & expansion
│   │   └── SeerPortalService.cs      # Seer portal spawning
│   ├── Network/                      # R2API multiplayer messages (13 types)
│   │   ├── ArchipelagoStartMessage.cs
│   │   ├── ArchipelagoEndMessage.cs
│   │   ├── SyncLocationCheckProgress.cs
│   │   └── ... (10 more message types)
│   ├── Utilities/
│   │   ├── EnvironmentIds.cs         # AP environment ID constants
│   │   └── Log.cs                    # Centralized logging
│   ├── UI/
│   │   ├── ArchipelagoConnectButtonController.cs  # Lobby connect panel
│   │   ├── ArchipelagoLocationsInEnvironmentController.cs
│   │   ├── ArchipelagoScoreboardController.cs     # 3-page scoreboard (checks, environments, pool)
│   │   ├── ArchipelagoCheckCountdownController.cs # Per-check countdown HUD
│   │   ├── ArchipelagoTotalChecksObjectiveController.cs
│   │   └── AssetBundleHelper.cs      # Asset bundle loading
│   └── connectbundle                 # Unity AssetBundle (UI prefabs)
├── worlds/ror2/                      # Python AP world (server-side generation)
│   ├── __init__.py                   # RiskOfRainWorld class
│   ├── items.py                      # Item definitions & pool weights
│   ├── locations.py                  # Location ID generation
│   ├── options.py                    # Player YAML configuration
│   ├── regions.py                    # Region graph (Classic & Explore)
│   ├── rules.py                      # Access/logic rules
│   ├── ror2environments.py           # Environment data + DLC tables
│   ├── docs/                         # Docs served on AP website
│   └── test/                         # Python unit tests
├── ror2.apworld                      # Packaged Python world (ZIP)
├── Archipelago.RiskOfRain2.sln       # VS solution
├── manifest.json                     # Thunderstore package metadata
├── nuget.config                      # NuGet feed configuration
└── ror2-archipelago.code-workspace   # VS Code workspace
```

## Key Classes

### ArchipelagoPlugin (`Archipelago/ArchipelagoPlugin.cs`)

BepInEx entry point. GUID: `com.Ijwu.Archipelago`. Responsibilities:

- Initializes `ArchipelagoClient`, config entries, and lobby UI
- Registers all 13 R2API network message types
- Routes events between UI, console commands, and `ArchipelagoClient`
- Manages reconnection coroutine on disconnect
- Handles client-only concerns (non-host players)

### ArchipelagoClient (`Archipelago/ArchipelagoClient*.cs`)

Core session and connection manager, split into partial classes. Owns the `ArchipelagoSession` object.

| File | Responsibility |
|------|---------------|
| `ArchipelagoClient.cs` | Fields, properties, session-level state |
| `ArchipelagoClient.Connection.cs` | `Connect()`, slot data parsing, reconnection, message handling |
| `ArchipelagoClient.RunLifecycle.cs` | `SetupRun()`, `CleanupRun()`, `HookGame()`/`UnhookGame()`, victory detection |

See [connection-lifecycle.md](connection-lifecycle.md) for details.

### ArchipelagoItemLogicController (`Archipelago/ArchipelagoItemLogicController*.cs`)

Item pickup tracking and AP check generation, split into partial classes.

| File | Responsibility |
|------|---------------|
| `ArchipelagoItemLogicController.cs` | Core logic, pickup counting, check sending, `Precollect()` |
| `ArchipelagoItemLogicController.ItemGrant.cs` | Item granting, traps, pickup notifications |
| `ArchipelagoItemLogicController.Queues.cs` | Item queue processing on Unity main thread |

### Services (implement `IService`)

All services implement the `IService` interface:

```csharp
interface IService {
    void Register();    // Subscribe to game events
    void Unregister();  // Unsubscribe from game events
}
```

| Service | Mode | Purpose |
|---------|------|---------|
| `DeathLinkManager` | Both | Sends/receives cross-game death events |
| `LocationCheckService` | Explore | Detects per-stage location checks (chests, shrines, etc.) |
| `StageBlockerService` | Explore | Blocks stages until unlock items received |
| `ShrineChanceService` | Explore | Modifies shrine spawn rules |
| `ItemPoolService` | Both | Restricts item drops to an expandable pool per tier |
| `ClientItemsService` | Both | Non-host client progress bar management |
| `SeerPortalService` | Explore | Spawns seer portals showing unlocked destinations |

### Network Messages

13 R2API network message types handle multiplayer synchronization. See [multiplayer.md](multiplayer.md).

## Dependency Stack

```
Risk of Rain 2 (Unity 2021.3.33)
  └── BepInEx 5.x (mod loader)
       └── R2API 5.x (RoR2 modding API)
            └── This mod (Archipelago.RiskOfRain2)
                 ├── Archipelago.MultiClient.Net 6.6.1 (AP client library)
                 ├── MMHOOK.RoR2 (IL hooks for runtime patching)
                 └── Newtonsoft.Json 13.0.3
```

## Two-Component Architecture

The C# mod (game client) and Python world (server-side) communicate through:

1. **Slot Data** — Python world sends configuration to C# client on login (see [ap-world.md](ap-world.md))
2. **Location Checks** — C# client reports completed checks to AP server
3. **Item Grants** — AP server sends items to C# client for spawning
4. **Status Updates** — C# client reports victory/completion to AP server
