---
name: validate-features
description: Build, package, and validate the Archipelago RoR2 world by generating a multiworld and inspecting the spoiler log. Use this skill after making changes to the Python AP world files or C# client to verify everything generates correctly. Trigger on requests like "validate", "test generation", "check the spoiler", "rebuild and test", or "/validate-features".
---

# ValidateFeatures

Rebuild the ror2 apworld, generate a multiworld, and parse the spoiler log to confirm features are present.

## Paths

| What | Path |
|------|------|
| Python world source | `c:/Users/IanCi/Repos/Archipelago.RiskOfRain2/worlds/ror2/` |
| C# client source | `c:/Users/IanCi/Repos/Archipelago.RiskOfRain2/Archipelago.RiskOfRain2/` |
| Archipelago install | `G:/Archipelago/` |
| YAML config | `G:/Archipelago/Players/ror2.yaml` |
| Apworld target | `G:/Archipelago/lib/worlds/ror2.apworld` |
| Generator exe | `G:/Archipelago/ArchipelagoGenerate.exe` |
| Output dir | `G:/Archipelago/output/` |
| r2modman profiles | `$APPDATA/r2modmanPlus-local/RiskOfRain2/profiles/` |
| Compiled DLL | `Archipelago.RiskOfRain2/bin/Debug/netstandard2.1/Archipelago.RiskOfRain2.dll` |

## Workflow

### Step 1: Build C# client (if C# files changed)

```bash
cd "c:/Users/IanCi/Repos/Archipelago.RiskOfRain2/Archipelago.RiskOfRain2" && dotnet build
```

Copy DLL to r2modman profiles (Archip, Testing, Default):

```bash
SRC="c:/Users/IanCi/Repos/Archipelago.RiskOfRain2/Archipelago.RiskOfRain2/bin/Debug/netstandard2.1/Archipelago.RiskOfRain2.dll"
for profile in Archip Testing Default; do
    cp "$SRC" "$APPDATA/r2modmanPlus-local/RiskOfRain2/profiles/$profile/BepInEx/plugins/Sneaki-Archipelago/Archipelago.RiskOfRain2.dll"
done
```

### Step 2: Package apworld

Create a zip of all `.py` files, docs, and `archipelago.json` from the source world directory:

```python
import zipfile, os, json

source_dir = 'c:/Users/IanCi/Repos/Archipelago.RiskOfRain2/worlds/ror2'
output_path = 'c:/Users/IanCi/Repos/Archipelago.RiskOfRain2/ror2.apworld'

with zipfile.ZipFile(output_path, 'w', zipfile.ZIP_DEFLATED) as zf:
    for f in os.listdir(source_dir):
        if f.endswith('.py'):
            zf.write(os.path.join(source_dir, f), f'ror2/{f}')
    for f in os.listdir(os.path.join(source_dir, 'test')):
        if f.endswith('.py'):
            zf.write(os.path.join(source_dir, 'test', f), f'ror2/test/{f}')
    for f in os.listdir(os.path.join(source_dir, 'docs')):
        zf.write(os.path.join(source_dir, 'docs', f), f'ror2/docs/{f}')
    meta = {"compatible_version": 7, "version": 7, "game": "Risk of Rain 2",
            "minimum_ap_version": "0.6.6", "maximum_ap_version": "0.6.6"}
    zf.writestr('ror2/archipelago.json', json.dumps(meta))
```

Then install it:

```bash
cp "c:/Users/IanCi/Repos/Archipelago.RiskOfRain2/ror2.apworld" "G:/Archipelago/lib/worlds/ror2.apworld"
```

### Step 3: Generate multiworld

```bash
cd "G:/Archipelago" && ./ArchipelagoGenerate.exe --player_files_path "G:/Archipelago/Players" 2>&1
```

Check for errors in the output. A successful run ends with `Done. Enjoy.` and creates a zip in `G:/Archipelago/output/`.

### Step 4: Parse spoiler log

Extract the spoiler `.txt` from the latest output zip and validate:

```python
import zipfile, glob, os, sys
sys.stdout.reconfigure(encoding='utf-8')

zips = sorted(glob.glob('G:/Archipelago/output/AP_*.zip'), key=os.path.getmtime)
latest = zips[-1]

with zipfile.ZipFile(latest) as z:
    spoiler_name = [n for n in z.namelist() if n.endswith('.txt')][0]
    content = z.read(spoiler_name).decode('utf-8-sig')
```

#### What to check in the spoiler

- **Settings block** (top of file): Confirm options match the YAML (DLC toggles, game mode, victory condition, feature toggles like loot pool limiting / skill randomization / survivor locking)
- **Starting Items**: Check granted environments and Dio's
- **Locations section**: Each environment name appears as `<Environment>: Chest N:`, `<Environment>: Shrine N:`, etc.
- **Items granted**: Look for new item types (e.g., `Item Pool Expansion`, `Skill Unlock`, `Survivor Unlock`, `Progressive Stage`)
- **Playthrough**: Verify the victory is reachable

#### DLC environment names to search for

**SOTV**: Siphoned Forest, Aphelian Sanctuary, Sulfur Pools, Void Locus, The Planetarium
**SOTS**: Shattered Abodes, Disturbed Impact, Viscous Falls, Reformed Altar, Treeborn Colony, Golden Dieback, Helminth Hatchery, Prime Meridian
**AC**: Pretender's Precipice, Iron Alluvium, Iron Auroras, Repurposed Crater, Conduit Canyon, Solutional Haunt, Neural Sanctum

### Step 5: Report results

Summarize what was validated:
- Build status (errors/warnings)
- Generation success
- Total locations/items
- Which features are present in the spoiler
- Any missing environments or unexpected issues

## Troubleshooting

- **UnicodeEncodeError**: Use `sys.stdout.reconfigure(encoding='utf-8')` and decode with `utf-8-sig`
- **Generation fails with import error**: A Python syntax error in the world files. Check the traceback for the offending file/line.
- **Missing environments**: Verify DLC toggles are enabled in `G:/Archipelago/Players/ror2.yaml`
- **apworld not loading**: The installed Archipelago uses Python 3.13 (bundled). Include `.py` source files in the apworld, not `.pyc` compiled with a different Python version.
