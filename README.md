# Risk of Rain 2 - Archipelago Enhanced

A [Risk of Rain 2](https://store.steampowered.com/app/632360/Risk_of_Rain_2/) mod for [Archipelago](https://archipelago.gg) multiworld randomizer. Connect your RoR2 runs to an Archipelago session alongside players in other games.

Originally forked from [kindasneaki/Archipelago.RiskOfRain2](https://github.com/kindasneaki/Archipelago.RiskOfRain2).

## Features

### DLC Support
- **Survivors of the Void (SOTV)** - Void items, Void Locus, The Planetarium
- **Seekers of the Storm (SOTS)** - Shattered Abodes, Reformed Altar, Treeborn Colony, Helminth Hatchery, Prime Meridian, and more
- **Alloyed Collective (AC)** - Pretender's Precipice, Iron Alluvium, Repurposed Crater, Conduit Canyon, Solutional Haunt, Neural Sanctum

Each DLC can be individually toggled in your YAML settings.

### Victory Conditions
| Victory | Requirement | DLC |
|---------|------------|-----|
| Mithrix | Defeat Mithrix on Commencement | Base |
| Voidling | Defeat Voidling in The Planetarium | SOTV |
| Limbo | Defeat Twisted Scavenger in A Moment, Whole | Base |
| False Son | Defeat False Son on Prime Meridian | SOTS |
| Solus Heart | Defeat Solus Heart in Neural Sanctum | AC |
| Any | Any of the above | - |

**Final Stage Death**: Optionally, dying on a victory stage counts as a win.

### Game Modes

**Explore Mode** - Locations are tied to specific environments. Open chests, beat shrines, find scanners, and discover newt altars to send checks. Environments are items in the pool - you must unlock them to visit new stages. The teleporter favors stages with remaining checks.

**Classic Mode** - Item pickups fill a counter that sends location checks. Simpler, no stage gating.

### In-Game UI
- **Scoreboard (Tab)** - 3-page overlay showing session info, environment unlock status (red/yellow/green), stage keys, hidden realms, and special stages
- **Objectives Panel** - Per-stage check breakdown (chests, shrines, scanners, newt altars) with color-coded counts
- **Check Countdown** - "Next item check in X pickup(s)" tracker
- **Connect Panel** - In-lobby AP connection with auto-minimize on connect
- **Styled Chat** - AP messages with RoR2 rich text formatting

### Explore Mode Check Rules
- **Chests**: Opening chests, lunar pods, void cradles. Sacrifice artifact drops count as chests
- **Shrines**: Blood, Chance (on reward), Combat, Order, Mountain, Woods (last 2 uses), Halcyon/Colossus
- **Scanners**: Radio scanners (guaranteed spawn per stage)
- **Newt Altars**: Finding altars sends a check (refunds lunar coin, no blue portal until checks depleted)
- **Scavengers**: Opening scavenger bags

### Multiplayer
Fully multiplayer compatible. All players need the mod installed. Scale up your YAML settings for more players. Session state syncs across all connected clients.

## Setup

### Requirements
- Risk of Rain 2
- [r2modman](https://thunderstore.io/package/ebkr/r2modman/) or manual BepInEx setup
- [Archipelago](https://archipelago.gg) (v0.6.4+)

### Installation
1. Install the mod via r2modman or manually place the DLL in `BepInEx/plugins/`
2. Place the `.apworld` file in your Archipelago `lib/worlds/` folder

### Connecting
1. Generate a seed with your YAML on the Archipelago server
2. Launch RoR2 with the mod enabled
3. In the character select lobby, fill in your server URL, port, slot name, and password
4. Click **Connect To AP**
5. Click **Ready** to start the run

## YAML Settings

Create a YAML at [Archipelago RoR2 Settings](https://archipelago.gg/games/Risk%20of%20Rain%202/player-settings) or manually:

| Setting | Values | Description |
|---------|--------|-------------|
| `goal` | 0 (Classic), 1 (Explore) | Game mode |
| `victory` | 0-5 | Victory condition (0=any, 1=Mithrix, 2=Voidling, 3=Limbo, 4=False Son, 5=Solus Heart) |
| `total_locations` | 40-250 | Classic mode total checks |
| `chests_per_stage` | 2-20 | Explore mode chests per environment |
| `shrines_per_stage` | 2-20 | Explore mode shrines per environment |
| `item_pickup_step` | 0-5 | Items picked up before a check is sent |
| `shrine_use_step` | 0-3 | Shrines used before a check is sent |
| `require_stages` | true/false | Stage key items gate progression |
| `progressive_stages` | true/false | Use progressive stage items instead |
| `death_link` | true/false | Enable DeathLink |
| `dlc_sotv` | true/false | Enable SOTV content |
| `dlc_sots` | true/false | Enable SOTS content |
| `dlc_ac` | true/false | Enable AC content |

Item weight presets: Default, Uncommon, Legendary, Chaos, No Scraps, Even, Scraps Only, Lunartic, Void

## Console Commands

Open the console with `Ctrl+Alt+~`.

| Command | Description |
|---------|-------------|
| `archipelago_connect <url> <port> <slot> [pw]` | Connect to AP server |
| `archipelago_disconnect` | Disconnect |
| `archipelago_reconnect` | Attempt reconnection |
| `archipelago_deathlink <true/false>` | Toggle DeathLink |
| `archipelago_final_stage_death <true/false>` | Toggle final stage death |
| `archipelago_show_unlocked_stages` | Show unlocked stages |

## Known Issues

- Splitscreen is untested and likely unsupported
- 1 filler item may fail to place during seed generation (harmless)

## Project Structure

```
Archipelago.RiskOfRain2/    # C# BepInEx mod (client-side)
worlds/ror2/                # Python Archipelago world (server-side)
docs/sprints/               # Sprint planning docs
```

## Credits

- Original mod by [Ijwu](https://github.com/Ijwu) and [kindasneaki](https://github.com/kindasneaki)
- Enhanced by [IanCichy](https://github.com/IanCichy)
- Built for [Archipelago](https://archipelago.gg)
