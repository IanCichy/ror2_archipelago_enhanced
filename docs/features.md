# Features

## Game Modes

### Classic Mode

Every N item pickups sends one location check to the Archipelago server.

- **Item Pickup Step**: Configurable (YAML `item_pickup_step`). The value N means every (N+1)th pickup triggers a check. Default: 1 (every 2nd pickup).
- **Total Locations**: Configurable 40–250. Determines how many AP locations exist for this player.
- Item pickups that count: chests, bosses, scrappers, 3D printers, lunar pods, terminals, multishops, Artifact of Sacrifice drops.
- All stages are accessible — no stage gating.
- Single progress bar shows pickup progress toward next check.

### Explore Mode

Each environment has a fixed number of location checks (chests, shrines, scavengers, scanners, newt altars). Environments must be unlocked via AP items.

- **Per-environment locations** (all configurable):
  - Chests: 2–20 (default 10)
  - Shrines: 2–20 (default 5)
  - Scavengers: 0–1 (default 0)
  - Radio Scanners: 0–1 (default 1)
  - Newt Altars: 0–2 (default 1)
- **Shrine Use Step**: Configurable. Like Item Pickup Step but for shrines.
- **Stage progression**: Environments are grouped into ordered stages (1–5). Access to Stage N+1 requires receiving a Stage item from AP.
  - `Require Stages`: Toggle whether Stage items gate progression (default on)
  - `Progressive Stages`: Use "Progressive Stage" items instead of specific "Stage 2", "Stage 3", etc. (default on)
- Two progress bars: item checks and shrine checks.
- Seer Portals can spawn to show which stages are unlocked.

## Victory Conditions

| Victory | Scene | DLC | Description |
|---------|-------|-----|-------------|
| Any | Various | None | Any recognized victory counts |
| Mithrix | `moon` / `moon2` | Base | Defeat Mithrix in Commencement |
| Voidling | `voidraid` | SOTV | Defeat the Voidling in The Planetarium |
| Limbo | `mysteryspace` / `limbo` | Base | Defeat the Scavenger in A Moment, Whole |
| Rebirth | `meridian` | SOTS | Defeat the False Son in Prime Meridian |
| Solus Wing | — | AC | Defeat the Solus Wing in Neural Sanctum (Rebirth ending) |

**Final Stage Death**: Optional toggle. When enabled, dying on the final stage associated with your victory condition counts as a win. Also counts obliteration for Limbo.

## Item Categories

### Upgrades (received from AP)
| Item | Rarity | Effect |
|------|--------|--------|
| Common Item | White | Random common item |
| Uncommon Item | Green | Random uncommon item |
| Legendary Item | Red | Random legendary item |
| Boss Item | Yellow | Random boss item |
| Equipment | Orange | Random equipment |
| Lunar Item | Blue | Random lunar item |
| Void Item | Purple | Random void item (SOTV) |
| Item Scrap (White/Green/Red/Yellow) | Various | Scrap for printers |

### Special Items
| Item | Effect |
|------|--------|
| Dio's Best Friend | Extra life (revive) |
| Beads of Fealty | Enables Limbo ending |
| Radar Scanner | Highlights nearby interactables |

### Filler
| Item | Effect |
|------|--------|
| Money | Grants in-game gold |
| Lunar Coin | Grants lunar coins |
| 1000 Exp | Grants experience |

### Traps
| Item | Effect |
|------|--------|
| Mountain Trap | Adds mountain shrine stack (harder teleporter) |
| Time Warp Trap | Advances the difficulty timer |
| Combat Trap | Spawns enemies |
| Teleport Trap | Teleports the player randomly |

### Environment Unlocks (Explore mode)
36+ environment items, one per stage. Receiving an environment item unlocks that stage for play. Examples: Titanic Plains, Abandoned Aqueduct, Rallypoint Delta, Sky Meadow, etc.

### Stage Progression (Explore mode)
- Stage 1, Stage 2, Stage 3, Stage 4 (or Progressive Stage x4)

## Item Weight Presets

Control the distribution of items in the AP pool:

| Preset | Description |
|--------|-------------|
| Default | Balanced distribution |
| New | Adjusted test weights |
| Uncommon | Heavy uncommon items |
| Legendary | Heavy legendary items |
| Chaos | Random weights (capped for rare items) |
| No Scraps | Removes all scrap items |
| Even | Equal weights for all items |
| Scraps Only | Only scrap items |
| Lunartic | All lunar items |
| Void | All void items |

Alternatively, set `item_pool_presets: false` and configure individual item weights manually in YAML.

## DLC Support

| DLC | Code | Adds |
|-----|------|------|
| Survivors of the Void (SOTV) | `dlc_sotv` | Void items, Voidling victory, SOTV environments |
| Seekers of the Storm (SOTS) | `dlc_sots` | Rebirth/False Son victory, SOTS environments, Colossus portals |
| Alloyed Collective (AC) | `dlc_ac` | Solus Wing victory, AC environments |

DLC toggles affect: environment availability, victory condition validation, void item pool.

## DeathLink

When enabled, dying in RoR2 sends a death event to all connected Archipelago games, and vice versa.

- 10-second cooldown prevents cyclic deaths
- Can be toggled mid-run via console: `archipelago_deathlink true/false`
- Configured per-player in YAML

## Console Commands

All commands require server/host privileges (`SenderMustBeServer`).

| Command | Syntax | Description |
|---------|--------|-------------|
| `archipelago_connect` | `<url> <port> <slot> [password]` | Connect to AP server |
| `archipelago_disconnect` | — | Disconnect from AP |
| `archipelago_reconnect` | — | Force reconnection attempt |
| `archipelago_deathlink` | `<true/false>` | Toggle DeathLink |
| `archipelago_final_stage_death` | `<true/false>` | Toggle final stage death as win |
| `archipelago_show_unlocked_stages` | — | Show current stage unlock progress |
| `archipelago_highlight_satellite` | `<true/false>` | Toggle radar satellite highlight |

## BepInEx Configuration

Stored in BepInEx config file, editable from lobby UI:

| Setting | Default | Description |
|---------|---------|-------------|
| `satellite` | `true` | Highlight radar satellites |
| `slotName` | `""` | Archipelago slot name |
| `serverName` | `archipelago.gg` | Server URL |
| `port` | `38281` | Server port |
| `password` | `""` | Server password |
