# Archipelago.RiskOfRain2 | ![Discord Shield](https://discordapp.com/api/guilds/731205301247803413/widget.png?style=shield)

This mod adds support to Risk of Rain 2 for playing as an Archipelago client. For more information on Archipelago head over to https://archipelago.gg or join our Discord.

Should be multiplayer compatible. Be sure to scale up your YAML settings if you play in multiplayer. All players require the mod in multiplayer.

## Features

### Game Modes

**Classic Mode**: Every item pickup fills a progress bar which gives location checks. Configure the total number of locations (40-250).

**Explore Mode**: Each environment has location checks (chests, shrines, scavengers, radio scanners, newt altars). Environments are locked as items — you need to receive them from the multiworld to access them. Stage progression can be gated by Stage items (progressive or individual).

### Victory Conditions

| Victory | Description | DLC Required |
|---------|-------------|-------------|
| Any | Any ending counts as a win | None |
| Mithrix | Defeat Mithrix in Commencement | None |
| Limbo | Defeat the Scavenger in A Moment, Whole | None |
| Voidling | Defeat the Voidling in The Planetarium | SOTV (DLC1) |
| False Son | Defeat the False Son in Prime Meridian | SOTS (DLC2) |
| Solus Wing | Defeat the Solus Wing in Solutional Haunt | AC (DLC3) |

**Final Stage Death**: Optionally, dying on a final stage can count as a win.

### DLC Support

All three Risk of Rain 2 DLC expansions are supported:

| DLC | Environments Added | Victory Condition |
|-----|-------------------|-------------------|
| **Survivors of the Void** (DLC1) | Siphoned Forest, Aphelian Sanctuary, Sulfur Pools, Void Locus, The Planetarium | Voidling |
| **Seekers of the Storm** (DLC2) | Shattered Abodes, Disturbed Impact, Viscous Falls, Reformed Altar, Treeborn Colony, Golden Dieback, Helminth Hatchery, Prime Meridian | False Son |
| **Alloyed Collective** (DLC3) | Pretender's Precipice, Iron Alluvium, Iron Auroras, Repurposed Crater, Conduit Canyon, Solutional Haunt, Neural Sanctum | Solus Wing |

Enable each DLC in your YAML with `dlc_sotv`, `dlc_sots`, and `dlc_ac`. Only enable DLCs that the host has installed.

### Loot Pool Limiting

When enabled, the in-game item drop pool starts with a small number of items per tier (configurable). As you receive **Item Pool Expansion** items from the multiworld, more items become available in each tier's drop pool.

| Option | Default | Description |
|--------|---------|-------------|
| `loot_pool_limiting` | off | Enable/disable the feature |
| `starting_white_items` | 3 | White items available at start |
| `starting_green_items` | 2 | Green items available at start |
| `starting_red_items` | 1 | Red items available at start |
| `starting_boss_items` | 1 | Boss items available at start |
| `starting_lunar_items` | 1 | Lunar items available at start |
| `starting_equipment` | 1 | Equipment available at start |
| `pool_expansions_per_tier` | 5 | Expansion items per tier in the AP pool |

### Skill Randomization

When enabled, all survivor skills from every character are injected into every survivor's loadout picker. You can choose any skill for any slot through the normal character select loadout UI.

- **Primary** slot is always unlocked
- **Secondary**, **Utility**, and **Special** slots start locked
- Receive **Skill Unlock** items from the multiworld to unlock slots (3 total: secondary -> utility -> special)
- Each player picks their own skills independently — no seeded randomization

### Survivor Locking

When enabled, only 1 survivor is available at the start. Receive **Survivor Unlock** items from the multiworld to unlock additional survivors. The starting survivor and unlock order are determined by a shared seed so all players in co-op get the same roster.

| Option | Default | Description |
|--------|---------|-------------|
| `survivor_locking` | off | Enable/disable |
| `total_survivor_unlocks` | 5 | Number of Survivor Unlock items in the pool (1-14) |

## Gameplay

The Risk of Rain 2 players send checks by causing items to spawn in-game. This includes opening chests, defeating bosses, using scrappers and 3D printers, opening lunar pods, and accessing terminals.
An item check is only sent out after a certain number of items are picked up. This count is configurable in the player's YAML.

### Achieving Victory or Defeat

Achieving victory depends on your YAML's `victory` setting. By default ("any"), beating any boss or obliterating counts as a win.

Due to the nature of roguelike games, you can possibly die and lose your place completely. This is mitigated partly by the free grants of `Dio's Best Friend`
but it is still possible to lose. If you do lose, you can reconnect to the Archipelago server and start a new run. The server will send you the items you have
earned thus far, giving you a small boost to the start of your run.

## YAML Settings

A complete YAML template is provided in [`ror2.yaml`](./ror2.yaml). Copy it to your Archipelago `Players/` folder and customize it.

### Quick Start Example

```yaml
description: MyRoR2Game
name: Player1

game: Risk of Rain 2
requires:
  version: 0.5.0

Risk of Rain 2:
  goal: 1                    # explore mode
  victory: 0                 # any victory
  dlc_sotv: 1                # enable SOTV
  dlc_sots: 1                # enable SOTS
  dlc_ac: 1                  # enable AC
  chests_per_stage: 10
  shrines_per_stage: 5
  scanner_per_stage: 1
  altars_per_stage: 1
  require_stages: 1
  progressive_stages: 1
  total_revivals: 4
  start_with_revive: 1
  item_pickup_step: 1
  enable_lunar: 1
  item_pool_presets: 0
  item_weights: 0
  loot_pool_limiting: 1      # enable loot pool limiting
  skill_randomization: 1     # enable skill picker
  survivor_locking: 1        # enable survivor locking
  total_survivor_unlocks: 5
  death_link: 0
```

## Installation

### Server Side (Python AP World)

The RoR2 world files live in the Archipelago repository at `worlds/ror2/`. If you're running a local Archipelago server, ensure these files are up to date with the new DLC and feature additions.

### Client Side (RoR2 Mod)

1. Install [r2modman](https://thunderstore.io/package/ebkr/r2modman/)
2. Install the Archipelago mod and its dependencies via r2modman
3. Replace the `Archipelago.RiskOfRain2.dll` in your r2modman profile's `BepInEx/plugins/` folder with the one built from this repository:
   ```
   bin/Debug/netstandard2.1/Archipelago.RiskOfRain2.dll
   ```
4. Launch the game via r2modman ("Start modded")

### Building from Source

```bash
cd Archipelago.RiskOfRain2
dotnet build
```

The output DLL will be at `Archipelago.RiskOfRain2/bin/Debug/netstandard2.1/Archipelago.RiskOfRain2.dll`.

## Connecting to an Archipelago Server

There will be a menu button on the right side of the screen. Click it in order to bring up the in lobby mod config. From here you can expand the Archipelago sections and fill in the relevant info.

Keep password blank if there is no password on the server.

Simply check `Enable Archipelago?` and when you start the run it will automatically connect and print a message stating successful connection in your in-game chat.

### In-Game Commands

These commands are to be used in-game by using ``Ctrl + Alt + ` `` and then typing the following:
- `archipelago_connect <url> <port> <slot> [password]` — Connect to AP server
- `archipelago_disconnect` — Disconnect from AP
- `archipelago_deathlink true/false` — Toggle deathlink
- `archipelago_final_stage_death true/false` — Toggle final stage death

Explore Mode only:
- `archipelago_show_unlocked_stages` — Show which stages have been received
- `archipelago_highlight_satellite true/false` — Highlight the satellite for visibility

## Changelog

**2.0.0 (Development)**
* Full DLC2 (Seekers of the Storm) and DLC3 (Alloyed Collective) environment support
* New victory conditions: False Son (DLC2) and Solus Wing (DLC3)
* Loot Pool Limiting: Start with a restricted item drop pool that expands via AP items
* Skill Randomization: Cross-survivor skill picker UI with AP-gated slot unlocks
* Survivor Locking: Start with 1 survivor, unlock more via AP items
* 6 new multiplayer sync messages for co-op feature parity
* DLC3 ending detection via runtime name lookup (no GameLibs dependency)

**1.1.3**
* Fixed connection issues.
* Update client protocol version.
    * Now only works on Archipelago server version 0.3.4 or higher.

**1.1.2**
* SOTV Ending now counts as an acceptable ending.
* Added YAML toggle for 'Death on the final stage counts as a win'.

**1.1.1**
* Update plugin version so it appears properly in the logs.

**1.1.0**
* Update to support Survivors of the Void DLC and updated R2API.
* Fix Archipelago PrintJSON packets.

**1.0.2**
* Update supported Archipelago version to function on current AP source.

**1.0.1**
* Fix chat box getting stuck on enabled sometimes.
* Stop lunar coins, elite drops, artifacts, and artifact keys from counting towards location checks.
* Names not appearing in multiplayer fixed.
* Fix lunar equipment grants not previously working.

**1.0 (First Stable Release)**
* Release of all changes from 0.1.5 up to 0.1.7.

## Known Issues

* Splitscreen support is unlikely at the moment.
* If you start a new run but join an existing AP session, you will get spammed with notifications for all your pickups.
* DLC3 ending uses `cachedName` string matching since GameLibs NuGet doesn't include `DLC3Content` yet. This will work at runtime but may need updating when GameLibs catches up.
* Skill injection happens at `Initialize()` time — if catalog loading order changes in a future RoR2 update, the skill collection may need to be deferred to a later hook.
