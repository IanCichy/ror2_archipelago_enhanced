# AP World (Python)

The `worlds/ror2/` directory contains the server-side Archipelago world definition. This is what the Archipelago server uses to generate randomized games. It is packaged as `ror2.apworld` (a ZIP file) for distribution.

## File Structure

| File | Purpose |
|------|---------|
| `__init__.py` | `RiskOfRainWorld` class — main world definition |
| `items.py` | Item table, filler table, pool weight presets, tier total constants |
| `locations.py` | Location ID generation and mapping |
| `options.py` | `ROR2Options` dataclass — all player YAML settings |
| `regions.py` | Region graph builders for Classic and Explore modes |
| `rules.py` | Access rules (e.g., "Stage 2 requires Stage 2 item") |
| `ror2environments.py` | Environment data tables per DLC |
| `docs/setup_en.md` | Setup guide (served on AP website) |
| `docs/en_Risk of Rain 2.md` | Game description for AP website |
| `test/` | Unit tests (Classic mode, various victory goals) |

## World Class: `RiskOfRainWorld`

```python
class RiskOfRainWorld(World):
    game = "Risk of Rain 2"
    options_dataclass = ROR2Options
    required_client_version = (0, 5, 0)
```

### Key Methods

- **`generate_early()`** — Calculates total revivals, validates DLC-dependent victory conditions (falls back to "any" if DLC not enabled)
- **`create_regions()`** — Delegates to `create_classic_regions()` or `create_explore_regions()` based on goal option
- **`create_items()`** — Builds the item pool: Dio's, Beads, Radar Scanner, environment unlocks, stage items, pool expansion items (when pool limiting enabled), weighted junk fill
- **`create_junk_pool()`** — Generates weighted filler items from presets or manual weights
- **`fill_slot_data()`** — Returns configuration dict sent to the C# client on login
- **`create_events()`** — Classic: milestone events every 25 locations. Explore: Stage 5 access event + Victory event.

## Region Graphs

### Classic Mode
Single region "Petrichor V" containing all `ItemPickup1` through `ItemPickup{N}` locations.

### Explore Mode
```
Menu
  └── Petrichor V (hub)
       ├── OrderedStage_1 (requires: Stage 1 unlock or start)
       │    ├── Titanic Plains
       │    ├── Distant Roost
       │    ├── Siphoned Forest (SOTV)
       │    └── ... (DLC environments)
       ├── OrderedStage_2 (requires: Stage 2 item)
       ├── OrderedStage_3 (requires: Stage 3 item)
       ├── OrderedStage_4 (requires: Stage 4 item)
       ├── OrderedStage_5 (requires: Stage 5 event)
       └── Victory
```

Each environment region contains locations like `{EnvName}: Chest {N}`, `{EnvName}: Shrine {N}`, etc.

## Slot Data Contract

`fill_slot_data()` returns a dictionary that the C# client parses in `ArchipelagoClient.Connect()`:

```python
{
    "goal": 0|1,                    # 0=classic, 1=explore
    "victory": 0-5,                 # 0=any, 1=mithrix, 2=voidling, 3=limbo, 4=false_son, 5=solus_wing
    "itemPickupStep": int,          # Pickups before check (0-5)
    "shrineUseStep": int,           # Shrines before check (0-3)
    "totalLocations": int,          # Classic: total location count
    "chestsPerStage": int,          # Explore: chests per environment
    "shrinesPerStage": int,
    "scavengersPerStage": int,
    "scannerPerStage": int,
    "altarsPerStage": int,
    "totalRevivals": int,           # Percentage of Dio's in pool
    "startWithRevive": bool,
    "finalStageDeath": bool,
    "deathLink": bool,
    "requireStages": bool,
    "progressiveStages": bool,
    "dlcSotv": bool,
    "dlcSots": bool,
    "dlcAc": bool,
    "itemPoolLimiting": bool,
    "startingWhitePool": int,       # Pool limiting: starting items per tier
    "startingGreenPool": int,
    "startingRedPool": int,
    "startingBossPool": int,
    "startingLunarPool": int,
    "startingVoidPool": int,
    "startingEquipmentPool": int,
    "itemsPerWhiteExpansion": int,  # Pool limiting: items added per expansion
    "itemsPerGreenExpansion": int,
    "itemsPerRedExpansion": int,
    "itemsPerBossExpansion": int,
    "itemsPerLunarExpansion": int,
    "itemsPerVoidExpansion": int,
    "itemsPerEquipmentExpansion": int,
    "seed": str,                    # 16-digit random seed
    "offset": int                   # Item ID offset
}

## Item ID Ranges

Defined in `items.py` with offsets:

| Range | Category | Examples |
|-------|----------|---------|
| `37001–37014` | Upgrades & Special | Common Item, Uncommon Item, Equipment, Dio's, Beads, Radar Scanner |
| `37101–37107` | Pool Expansion | White/Green/Red/Boss/Lunar/Void/Equipment Pool Expansion |
| `37301–37303` | Filler | Money, Lunar Coin, 1000 Exp |
| `37401–37404` | Traps | Mountain, Time Warp, Combat, Teleport |
| `37501–37505` | Stages | Stage 1–4, Progressive Stage |
| `37700–37999` | Environments | Per-environment unlock items |

## Options Reference

All options are defined in `options.py` as classes inheriting from `Choice`, `Range`, `Toggle`, or `DefaultOnToggle`. Grouped in the YAML editor:

**Explore Mode Options:** `ChestsPerEnvironment`, `ShrinesPerEnvironment`, `ScavengersPerEnvironment`, `ScannersPerEnvironment`, `AltarsPerEnvironment`, `RequireStages`, `ProgressiveStages`

**Item Pool Limiting:** `ItemPoolLimiting`, `StartingWhitePool`, `StartingGreenPool`, `StartingRedPool`, `StartingBossPool`, `StartingLunarPool`, `StartingVoidPool`, `StartingEquipmentPool`, `ItemsPerWhiteExpansion`, `ItemsPerGreenExpansion`, `ItemsPerRedExpansion`, `ItemsPerBossExpansion`, `ItemsPerLunarExpansion`, `ItemsPerVoidExpansion`, `ItemsPerEquipmentExpansion`

**Classic Mode Options:** `TotalLocations`

**Core Options:** `Goal`, `Victory`, `TotalRevivals`, `StartWithRevive`, `FinalStageDeath`, `ItemPickupStep`, `ShrineUseStep`, `DeathLink`

**DLC:** `DLC_SOTV`, `DLC_SOTS`, `DLC_AC`

**Item Weights:** `ItemWeights` (preset selector), `ItemPoolPresetToggle`, plus 18 individual weight sliders

## Environment Tables

`ror2environments.py` defines environment data per DLC:

- `environment_vanilla_table` — Base game environments
- `environment_sotv_table` — Survivors of the Void environments
- `environment_sots_table` — Seekers of the Storm environments
- `environment_ac_table` — Alloyed Collective environments

Each DLC also has an `*_orderedstages_table` mapping ordered stage tiers (1–5) to available environments.

## Running Tests

```bash
cd worlds/ror2
python -m pytest test/
```

Tests cover: Classic mode generation, various victory goal configurations, item pool limiting (with/without lunar/void).

## Packaging as .apworld

The `ror2.apworld` file is a ZIP archive of the `worlds/ror2/` directory. To rebuild:

```bash
cd worlds
zip -r ../ror2.apworld ror2/
```

The `.apworld` is committed to the repo as a pre-built artifact. It must be placed in the Archipelago server's `worlds/` or `custom_worlds/` directory for the server to recognize Risk of Rain 2 as a supported game.
