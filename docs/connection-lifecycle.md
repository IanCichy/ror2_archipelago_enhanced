# Connection Lifecycle

The connection lifecycle uses a two-tier state model where the AP session persists across game runs, while per-run state is created and destroyed each time the player starts or ends a run.

## State Tiers

### Session-Level State (Persists Across Runs)

Managed by `ArchipelagoClient`. Created once on `Connect()`, destroyed on `TeardownSession()`:

- `ArchipelagoSession` — WebSocket connection to AP server
- Credentials — `lastServerUrl`, `lastSlotName`, `lastPassword`
- Slot data cache — `cachedGoalIsExplore`, `cachedDeathLinkEnabled`, `cachedItemPickupStep`, `cachedShrineUseStep`, `cachedItemPoolLimiting`, `cachedSlotData`
- `DeathLinkManager` + `DeathLinkService`
- Victory conditions — `acceptableEndings[]`, `acceptableLosses[]`
- Session event subscriptions — `OnMessageReceived`, `SocketClosed`, `ErrorReceived`
- `lastReceivedItemindex` — Prevents duplicate item delivery across reconnects

### Run-Level State (Per Game Run)

Created by `SetupRun()`, destroyed by `CleanupRun()`:

- `ArchipelagoItemLogicController` (ItemLogic) — Pickup counting and check sending
- `StageBlockerService` (Explore mode) — Stage access gating
- `LocationCheckService` (Explore mode) — Per-stage location tracking
- `ShrineChanceService` (Explore mode) — Shrine modification
- `ItemPoolService` (when pool limiting enabled) — Per-tier drop filtering
- `itemCheckBar` / `shrineCheckBar` — Progress bar UI
- Game hooks — Chat, run destroy, game over, item drops

## Connection Flow

```
User clicks "Connect" in lobby (or uses console command)
         │
         ▼
  ArchipelagoClient.Connect(url, slotName, password)
         │
         ├── Already connected? ──Yes──▶ CleanupRun() → SetupRun() → done
         │                               (reuse existing session)
         │
         ▼ No
  TeardownSession()  (clean up any stale session)
         │
         ▼
  ArchipelagoSessionFactory.CreateSession(url)
         │
         ▼
  session.TryConnectAndLogin("Risk of Rain 2", slot, AllItems)
         │
         ├── Failed? ──▶ Log errors, session = null, return
         │
         ▼ Success
  Parse & cache slot data:
    - finalStageDeath, itemPickupStep, shrineUseStep
    - deathLink, goal (classic/explore)
    - victory condition, progressive stages, seer portals
         │
         ▼
  Create DeathLinkService + DeathLinkManager
  Subscribe session-level events
  Initialize stage unlocks
         │
         ▼
  SetupRun()  (first run)
```

## SetupRun() Details

Called on first connect and when reusing an existing session for a new run:

1. Creates `ArchipelagoItemLogicController` with current session
2. **Explore mode**: Creates `StageBlockerService`, `LocationCheckService`, `ShrineChanceService`, two progress bars
3. **Classic mode**: Creates single progress bar, subscribes to `SyncLocationCheckProgress`
4. Sets `ItemPickupStep` on bars from cached slot data
5. Restores cached ItemLogic state if `hasCachedRunState` is true (run 2+)
6. Subscribes to `OnItemDropProcessed` event
7. Hooks DeathLink if enabled
8. Calls `HookGame()` — subscribes to chat, run destroy, game over, item drops, etc.
9. Broadcasts `ArchipelagoStartMessage` + mode-specific start message to clients
10. Calls `ItemLogic.Precollect()` — processes pre-collected environment items

## CleanupRun() Details

Called when a run ends (player dies, exits to menu) or on disconnect:

1. **Re-entrance guard**: Returns immediately if `ItemLogic == null` (prevents double-dispose from `Run_onRunDestroyGlobal` and `Session_SocketClosed` racing)
2. Calls `UnhookGame()` — unsubscribes all per-run game hooks and handler hooks
3. Caches ItemLogic state for potential session reuse:
   - `cachedItemLogicPickupStep`, `cachedItemLogicTotalChecks`
   - `cachedItemLogicCurrentChecks`, `cachedItemLogicPickedUpItemCount`
4. Disposes ItemLogic, progress bars
5. Nullifies per-run service references (`StageBlockerService`, `LocationCheckService`, `ShrineChanceService`, `ItemPoolService`)

## TeardownSession()

Destroys session-level state. Only called on intentional disconnect or unrecoverable error:

1. Unsubscribes session events (`OnMessageReceived`, `SocketClosed`, `ErrorReceived`)
2. Optionally disconnects socket (if `disconnect: true`)
3. Nullifies session, DeathLinkManager, DeathLinkService
4. Does NOT clear `hasCachedRunState` — preserves cached run state for reconnection

## Reconnection

When the socket disconnects unexpectedly:

```
Socket error/close detected
         │
         ▼
  reconnecting = true
  CleanupRun()    ◄── caches ItemLogic state
  TeardownSession()
  Fire OnClientDisconnect event
         │
         ▼
  ArchipelagoPlugin starts AttemptReconnection() coroutine
         │
         ▼
  Loop (max 5 attempts, 3 second delay each):
    Connect() on background thread (ManualResetEventSlim for signaling)
      │
      ├── reconnecting=true prevents clearing lastReceivedItemindex
      │   and hasCachedRunState, so:
      │   - Items won't be re-delivered
      │   - Run progress is restored from cache
      │
      ├── Connected? ──▶ Restore LocationCheckService state, break
      │
      └── Failed? ──▶ Next attempt
         │
         ▼
  All attempts failed → Dispose() (full cleanup)
```

## Intentional Disconnect

User-initiated via console command `archipelago_disconnect`:

```
Disconnect()
  ├── ChangeButtonWhenDisconnected()
  ├── Dispose()
  │     ├── CleanupRun()
  │     └── TeardownSession(disconnect: true)
  ├── Send ArchipelagoEndMessage to clients
  └── Fire OnClientDisconnect("Disconnected.")
```

## Full Lifecycle Diagram

```
┌─────────────────────────────────────────────────┐
│                  SESSION LEVEL                   │
│  (AP connection, slot data, DeathLink, creds)    │
│                                                  │
│  Connect() ─────────────────── TeardownSession() │
│      │                              ▲            │
│      │    ┌──────────────────┐      │            │
│      │    │    RUN LEVEL     │      │            │
│      │    │  (ItemLogic,     │      │            │
│      │    │   handlers, UI,  │      │            │
│      │    │   game hooks)    │      │            │
│      │    │                  │      │            │
│      ├───▶│  SetupRun()      │      │            │
│      │    │      │           │      │            │
│      │    │      ▼           │      │            │
│      │    │  [gameplay]      │      │            │
│      │    │      │           │      │            │
│      │    │      ▼           │      │            │
│      │    │  CleanupRun()  ──┼──────┘            │
│      │    │  (caches state)  │  (on error only)  │
│      │    └──────────────────┘                   │
│      │           │                               │
│      │           ▼                               │
│      └────── SetupRun()  ◄── session reuse       │
│              (restores cache)                    │
└─────────────────────────────────────────────────┘
```

## Key Design Decisions

- **Session survives runs**: Players don't need to re-enter credentials or wait for a new WebSocket handshake between runs
- **State caching**: ItemLogic progress (pickup count, check count) is cached before disposal and restored on the next run
- **Re-entrance guard**: `CleanupRun()` checks `ItemLogic == null` to prevent double-dispose when both `Run_onRunDestroyGlobal` and `Session_SocketClosed` fire simultaneously
- **Reconnect preserves progress**: The `reconnecting` flag prevents `Connect()` from resetting `lastReceivedItemindex` and `hasCachedRunState`
