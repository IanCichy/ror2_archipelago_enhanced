# Multiplayer Sync System

The mod uses R2API's networking layer to synchronize Archipelago state between the host and all connected clients. All players in a multiplayer lobby must have the mod installed.

## Architecture

- **Host** owns the `ArchipelagoClient` and communicates directly with the AP server
- **Clients** receive state updates via R2API network messages
- Messages use `NetworkDestination.Clients` (host → all clients) or `NetworkDestination.Server` (client → host)

## Network Message Types

All 12 message types are registered in `ArchipelagoPlugin.Awake()`:

| Message | Direction | Purpose |
|---------|-----------|---------|
| `ArchipelagoStartMessage` | Host → Clients | Signals AP session started, clients create `ClientItemsService` |
| `ArchipelagoStartClassic` | Host → Clients | Initialize Classic mode UI/objectives |
| `ArchipelagoStartExplore` | Host → Clients | Initialize Explore mode UI/objectives |
| `ArchipelagoEndMessage` | Host → Clients | Session ended, clients clean up UI/handlers |
| `SyncLocationCheckProgress` | Host → Clients | Item pickup count sync (Classic mode bar updates) |
| `SyncShrineCheckProgress` | Host → Clients | Shrine progress sync (Explore mode) |
| `SyncTotalCheckProgress` | Host → Clients | Overall check progress |
| `SyncCurrentEnvironmentCheckProgress` | Host → Clients | Current stage location progress |
| `AllChecksComplete` | Host → Clients | All AP locations done |
| `AllChecksCompleteInStage` | Host → Clients | All locations in current stage done |
| `NextStageObjectives` | Host → Clients | Stage progression hints |
| `ArchipelagoChatMessage` | Client → Host | Chat forwarding (client sends chat to host for AP relay) |
| `ArchipelagoTeleportClient` | Host → Clients | Teleport players (exploration mode) |

## Host-Only Operations

Only the host (or single-player) can:

- Connect to the AP server
- See and interact with the lobby connect panel
- Send location checks and receive items
- Process game logic (item drops, victory conditions)

Guard pattern used throughout:
```csharp
if (NetworkServer.active && RoR2Application.isInMultiPlayer || isSinglePlayer)
{
    // Host-only logic
}
```

## Client Behavior

Non-host clients:

1. Receive `ArchipelagoStartMessage` → create `ClientItemsService` which manages their local progress bar
2. Receive sync messages → update local UI (progress bars, objectives)
3. Submit chat → forward to host via `ArchipelagoChatMessage`, host relays to AP server
4. Receive `ArchipelagoEndMessage` → clean up local UI

`ClientItemsService` implements `IService` and manages the client-side item check progress bar. It hooks `SyncLocationCheckProgress.OnLocationSynced` to update the bar when the host reports progress.

## Chat Flow

```
Client types chat message
         │
         ▼
ArchipelagoPlugin.ChatBox_SubmitChat (client side)
  - Creates ArchipelagoChatMessage
  - Sends to NetworkDestination.Server
         │
         ▼
Host receives ArchipelagoChatMessage
  - ArchipelagoChatMessage_OnChatReceivedFromClient fires
  - Host sends SayPacket to AP server
         │
         ▼
AP server broadcasts to other players
         │
         ▼
Host receives via Session_OnMessageReceived
  - Parses colored message parts
  - Displays via ChatMessage.Send()
  (Visible to all local players via RoR2's built-in chat)
```

## DeathLink in Multiplayer

- DeathLink events kill all players in the lobby
- When any player dies, the host detects it via `CharacterMaster.OnBodyDeath` hook
- Host sends the deathlink event to AP
- Incoming deathlinks kill all `PlayerCharacterMasterController.instances`
- 10-second cooldown prevents cyclic death chains
- Player name for deathlink uses `playerCharacterMasterController.GetDisplayName()` or falls back to AP slot name

## State Sync Flow

```
Host completes a location check
         │
         ├── SyncLocationCheckProgress → Clients update item bar
         ├── SyncTotalCheckProgress → Clients update total progress
         └── (if Explore mode)
              ├── SyncCurrentEnvironmentCheckProgress → Clients update stage progress
              └── AllChecksCompleteInStage → Clients see stage complete
```
