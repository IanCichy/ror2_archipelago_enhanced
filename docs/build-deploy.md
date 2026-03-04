# Build & Deploy

## Prerequisites

- .NET SDK (targeting `netstandard2.1`)
- NuGet package sources configured (see `nuget.config`):
  - BepInEx: `https://nuget.bepinex.dev/v3/index.json`
  - NuGet: `https://api.nuget.org/v3/index.json`
- r2modman (optional, for local testing deployment)

## Building the Mod

From the repository root:

```bash
# Debug build
dotnet build Archipelago.RiskOfRain2.sln

# Release build
dotnet build Archipelago.RiskOfRain2.sln -c Release
```

Or from the project directory:

```bash
cd Archipelago.RiskOfRain2
dotnet build -c Release
```

## What the Build Produces

The `.csproj` has a post-build target (`CopyMod`) that automatically:

1. **Stages files** to `Archipelago.RiskOfRain2/bin/Build/`:
   - `Archipelago.RiskOfRain2.dll` (compiled mod)
   - `Archipelago.RiskOfRain2.pdb` (debug symbols)
   - `Archipelago.RiskOfRain2.deps.json`
   - `Archipelago.MultiClient.Net.dll` (AP networking library)
   - `Newtonsoft.Json.dll`
   - `README.md`, `CHANGELOG.md`, `manifest.json`, `icon.png`
   - `connectbundle` (Unity AssetBundle with UI prefabs)

2. **Creates Thunderstore ZIP** at:
   ```
   Archipelago.RiskOfRain2/bin/zip/Sneaki-Archipelago-{version}.zip
   ```

3. **Deploys to local r2modman profile** at:
   ```
   %USERPROFILE%/AppData/Roaming/r2modmanPlus-local/RiskOfRain2/profiles/Testing/BepInEx/plugins/Sneaki-Archipelago/
   ```

## Dependencies (NuGet)

| Package | Version | Purpose |
|---------|---------|---------|
| `BepInEx.Core` | 5.* | Mod loader framework |
| `BepInEx.Analyzers` | 1.0.* | Build-time analyzers |
| `Archipelago.MultiClient.Net` | 6.6.1 | AP client networking |
| `Newtonsoft.Json` | 13.0.3 | JSON serialization |
| `UnityEngine.Modules` | 2021.3.33 | Unity engine references |
| `RiskOfRain2.GameLibs` | 1.4.1-r.0 | RoR2 game libraries |
| `MMHOOK.RoR2` | 2025.12.9 | IL hooks for runtime patching |
| `RoR2BepInExPack` | 1.40.0 | BepInEx + RoR2 dependencies |
| `R2API` | 5.0.5 | RoR2 modding API |

## Thunderstore Dependencies

From `manifest.json`:
- `bbepis-BepInExPack-5.4.2101`
- `RiskofThunder-HookGenPatcher-1.2.3`
- `tristanmcpherson-R2API-4.4.1`

## Version Management

Version is set in `Archipelago.RiskOfRain2/Archipelago.RiskOfRain2.csproj`:

```xml
<VersionPrefix>1.5.3</VersionPrefix>
```

Also mirrored in:
- `ArchipelagoPlugin.cs`: `public const string PluginVersion = "1.5.3";`
- `manifest.json`: `"version_number": "1.5.3"`

When bumping the version, update all three locations.

## Deploying to Thunderstore

1. Build in Release mode
2. The ZIP at `bin/zip/Sneaki-Archipelago-{version}.zip` is ready for upload
3. Upload to [Thunderstore](https://thunderstore.io/) under the Risk of Rain 2 community

## Packaging the AP World

The `ror2.apworld` file is a ZIP of `worlds/ror2/`:

```bash
cd worlds
zip -r ../ror2.apworld ror2/
```

Place the `.apworld` in the Archipelago server's `worlds/` or `custom_worlds/` directory.

## Local Testing Setup

1. Install [r2modman](https://thunderstore.io/package/ebkr/r2modman/) or r2modman Plus
2. Create a profile named "Testing"
3. Install required dependencies (BepInExPack, HookGenPatcher, R2API) in the profile
4. Build the mod — post-build automatically deploys to the Testing profile
5. Launch RoR2 through r2modman

## Project Structure Reference

```
Archipelago.RiskOfRain2/
├── Archipelago.RiskOfRain2.csproj   # Build config + post-build packaging
├── bin/
│   ├── Build/                       # Staging directory (auto-populated)
│   ├── Debug/netstandard2.1/        # Debug output
│   ├── Release/netstandard2.1/      # Release output
│   └── zip/                         # Thunderstore ZIPs
├── obj/                             # Build intermediates
└── connectbundle                    # Pre-built Unity AssetBundle
```
