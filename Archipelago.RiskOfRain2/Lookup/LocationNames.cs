using Archipelago.RiskOfRain2.Handlers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Archipelago.RiskOfRain2.Lookup
{
    public class LocationNames
    {
        // Keys are Python/AP IDs from worlds/ror2/ror2environments.py.
        // For vanilla and SOTV stages, Python IDs happen to match C# SceneCatalog indices.
        // For SOTS and AC stages, they differ (Python uses sequential IDs 48+).
        // These Python IDs are used for AP item/location ID computation, so they MUST match the Python world.

        public static readonly Dictionary<int, string> locationsNames = new()
        {
            // Vanilla
            { 7, "Distant Roost" },
            { 8, "Distant Roost (2)" },
            { 15, "Titanic Plains" },
            { 16, "Titanic Plains (2)" },
            { 28, "Verdant Falls" },
            { 17, "Abandoned Aqueduct" },
            { 12, "Wetland Aspect" },
            { 13, "Rallypoint Delta" },
            { 47, "Scorched Acres" },
            { 10, "Abyssal Depths" },
            { 37, "Siren's Call" },
            { 35, "Sundered Grove" },
            { 38, "Sky Meadow" },
            // Vanilla special/hidden
            { 4, "Void Fields" },
            { 32, "Commencement" },
            { 5, "Hidden Realm: Bulwark's Ambry" },
            { 6, "Hidden Realm: Bazaar Between Time" },
            { 14, "Hidden Realm: Gilded Coast" },
            { 27, "Hidden Realm: A Moment, Whole" },
            { 33, "Hidden Realm: A Moment, Fractured" },
            // SOTV
            { 39, "Siphoned Forest" },
            { 3, "Aphelian Sanctuary" },
            { 41, "Sulfur Pools" },
            { 46, "Void Locus" },
            { 45, "The Planetarium" },
            // SOTS (Python IDs 48-55, NOT C# SceneCatalog indices)
            { 48, "Shattered Abodes" },
            { 49, "Disturbed Impact" },
            { 50, "Viscous Falls" },
            { 51, "Reformed Altar" },
            { 52, "Treeborn Colony" },
            { 53, "Golden Dieback" },
            { 54, "Helminth Hatchery" },
            { 55, "Prime Meridian" },
            // AC (Python IDs 56-62)
            { 56, "Pretender's Precipice" },
            { 57, "Iron Alluvium" },
            { 58, "Iron Auroras" },
            { 59, "Repurposed Crater" },
            { 60, "Conduit Canyon" },
            { 61, "Solutional Haunt" },
            { 62, "Neural Sanctum" },
        };

        public static readonly Dictionary<int, string> cachedLocationsNames = new()
        {
            // Vanilla
            { 7, "blackbeach" },
            { 8, "blackbeach2" },
            { 15, "golemplains" },
            { 16, "golemplains2" },
            { 28, "lakes" },
            { 17, "goolake" },
            { 12, "foggyswamp" },
            { 13, "frozenwall" },
            { 47, "wispgraveyard" },
            { 10, "dampcavesimple" },
            { 37, "shipgraveyard" },
            { 35, "rootjungle" },
            { 38, "skymeadow" },
            // Vanilla special/hidden
            { 4, "arena" },
            { 32, "moon2" },
            { 5, "artifactworld" },
            { 6, "bazaar" },
            { 14, "goldshores" },
            { 27, "limbo" },
            { 33, "mysteryspace" },
            // SOTV
            { 39, "snowyforest" },
            { 3, "ancientloft" },
            { 41, "sulfurpools" },
            { 46, "voidstage" },
            { 45, "voidraid" },
            // SOTS (Python IDs 48-55, NOT C# SceneCatalog indices)
            { 48, "village" },
            { 49, "villagenight" },
            { 50, "lakesnight" },
            { 51, "lemuriantemple" },
            { 52, "habitat" },
            { 53, "habitatfall" },
            { 54, "helminthroost" },
            { 55, "meridian" },
            // AC (Python IDs 56-62)
            { 56, "nest" },
            { 57, "ironalluvium" },
            { 58, "ironalluvium2" },
            { 59, "repurposedcrater" },
            { 60, "conduitcanyon" },
            { 61, "solutionalhaunt" },
            { 62, "solusweb" },
        };

        // Reverse lookup: scene cached name → Python/AP ID (built once, O(1) lookups)
        private static readonly Dictionary<string, int> cachedNameToIndex;

        static LocationNames()
        {
            cachedNameToIndex = new Dictionary<string, int>(cachedLocationsNames.Count);
            foreach (var kvp in cachedLocationsNames)
            {
                cachedNameToIndex[kvp.Value] = kvp.Key;
            }
        }

        public static string GetLocationName(string cachedName)
        {
            int sceneIndex = GetSceneIndex(cachedName);
            if (locationsNames.TryGetValue(sceneIndex, out string locationName))
            {
                return locationName;
            }
            return "";
        }

        public static string GetLocationNameByIndex(int index)
        {
            if (locationsNames.TryGetValue(index, out string locationName))
            {
                return locationName;
            }
            return "";
        }

        public static string GetCachedLocationNameByIndex(int index)
        {
            if (cachedLocationsNames.TryGetValue(index, out string cachedName))
            {
                return cachedName;
            }
            return "";
        }

        public bool LocationNamesContains(string sceneName)
        {
            return locationsNames.ContainsValue(sceneName);
        }

        public static bool CachedLocationNamesContains(string cachedName)
        {
            return cachedNameToIndex.ContainsKey(cachedName);
        }

        public static int GetSceneIndex(string cachedName)
        {
            return cachedNameToIndex.TryGetValue(cachedName, out int index) ? index : 0;
        }

    }
}
