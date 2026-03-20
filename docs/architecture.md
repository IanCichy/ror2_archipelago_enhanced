# Architecture

This is a mono-repo containing two components that together enable Risk of Rain 2 to participate in [Archipelago](https://archipelago.gg/) multiworld randomizer games.

## Repository Layout

```
ror2_archipelago_enhanced/
├── Archipelago.RiskOfRain2/          # C# BepInEx mod (game client)
│   ├── Archipelago.RiskOfRain2.csproj
│   ├── ArchipelagoPlugin.cs          # BepInEx entry point
│   ├── ArchipelagoClient.cs          # AP session & connection lifecycle
│   ├── ArchipelagoItemLogicController.cs  # Item pickup tracking & checks
│   ├── Console/
│   │   └── ArchipelagoConsoleCommand.cs   # In-game console commands
│   ├── Extensions/                   # Utility extension methods
│   │   ├── IEnumerableExtensions.cs  # PickRandom helper
│   │   └── LocationExtensions.cs     # Scene ID <-> name mappings
│   ├── Services/
│   │   ├── IService.cs               # Register/Unregister interface
│   │   ├── ClientItemsService.cs     # Client-side item bar (non-host)
│   │   ├── DeathLinkManager.cs       # Cross-game death synchronization
│   │   ├── LocationCheckService.cs   # Explore mode location detection
│   │   ├── ShrineChanceService.cs    # Shrine reward modification
│   │   ├── StageBlockerService.cs    # Stage unlock gating
│   │   ├── ItemPoolService.cs        # Item pool limiting & expansion
│   │   └── SeerPortal.cs             # Seer portal spawning
│   ├── Net/                          # R2API multiplayer messages (13 types)
│   │   ├── ArchipelagoStartMessage.cs
│   │   ├── ArchipelagoEndMessage.cs
│   │   ├── SyncLocationCheckProgress.cs
│   │   └── ... (10 more message types)
│   ├── UI/
│   │   ├── ArchipelagoConnectButtonController.cs  # Lobby connect panel
│   │   ├── ArchipelagoLocationCheckProgressBarUI.cs  # Progress bars
│   │   ├── ArchipelagoLocationsInEnvironmentController.cs
│   │   ├── ArchipelagoScoreboardController.cs     # 3-page scoreboard (checks, environments, pool)
│   │   ├── ArchipelagoCheckCountdownController.cs # Per-check countdown HUD
│   │   └── ArchipelagoTotalChecksObjectiveController.cs
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

### ArchipelagoPlugin (`ArchipelagoPlugin.cs`)

BepInEx entry point. GUID: `com.Ijwu.Archipelago`. Responsibilities:

- Initializes `ArchipelagoClient`, config entries, and lobby UI
- Registers all 12 R2API network message types
- Routes events between UI, console commands, and `ArchipelagoClient`
- Manages reconnection coroutine on disconnect
- Handles client-only concerns (non-host players)

### ArchipelagoClient (`ArchipelagoClient.cs`)

Core session and connection manager. Owns the `ArchipelagoSession` object. Responsibilities:

- Connection lifecycle: `Connect()` → `SetupRun()` → `CleanupRun()` → `TeardownSession()`
- Slot data parsing and caching (survives across runs)
- Victory condition evaluation
- Game hook management (`HookGame()` / `UnhookGame()`)
- Chat relay to/from AP server
- Release/Collect UI at game end screen

See [connection-lifecycle.md](connection-lifecycle.md) for details.

### ArchipelagoItemLogicController (`ArchipelagoItemLogicController.cs`)

Item pickup tracking and AP check generation. Responsibilities:

- Intercepts item drops via `On.RoR2.PickupDropletController.CreatePickupDroplet`
- Counts pickups and sends location checks at configured intervals
- Receives items from AP and spawns them in-game
- Manages item queue (processes on Unity main thread)
- Handles environment/stage unlock items via `Precollect()`
- Tracks `CurrentChecks`, `TotalChecks`, `PickedUpItemCount`, `ItemPickupStep`

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

### Net Messages

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
