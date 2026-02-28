using Archipelago.RiskOfRain2.Handlers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Archipelago.RiskOfRain2.Lookup
{
    public class LocationNames
    {
        public static readonly Dictionary<int, string> locationsNames = new()
        {
            { 3, "Aphelian Sanctuary" },
            { 7, "Distant Roost" },
            { 8, "Distant Roost (2)" },
            { 28, "Verdant Falls"},
            { 10, "Abyssal Depths" },
            { 12, "Wetland Aspect" },
            { 13, "Rallypoint Delta" },
            { 15, "Titanic Plains" },
            { 16, "Titanic Plains (2)" },
            { 17, "Abandoned Aqueduct" },
            { 35, "Sundered Grove" },
            { 37, "Siren's Call" },
            { 38, "Sky Meadow" },
            { 39, "Siphoned Forest" },
            { 41, "Sulfur Pools" },
            { 47, "Scorched Acres" },
            { 32, "Commencement" },
            { 4, "Void Fields" },
            { 46, "Void Locus" },
            { 45, "The Planetarium" },
            { 5, "Hidden Realm: Bulwark's Ambry"},
            { 6, "Hidden Realm: Bazaar Between Time"},
            { 14, "Hidden Realm: Gilded Coast" },
            { 27, "Hidden Realm: A Moment, Whole"},
            { 33, "Hidden Realm: A Moment, Fractured" },
            { 48, "Shattered Abodes" },
            { 49, "Disturbed Impact" },
            { 50, "Viscous Falls" },
            { 51, "Reformed Altar" },
            { 52, "Treeborn Colony" },
            { 53, "Golden Dieback" },
            { 54, "Helminth Hatchery" },
            { 55, "Prime Meridian" },
            // Alloyed Collective
            { 56, "Pretender's Precipice" },
            { 57, "Iron Alluvium" },
            { 58, "Iron Auroras" },
            { 59, "Repurposed Crater" },
            { 60, "Conduit Canyon" },
            { 61, "Solutional Haunt" },
            { 62, "Neural Sanctum" }
        };

        public static readonly Dictionary<int, string> cachedLocationsNames = new()
        {
            { 3, "ancientloft" },
            { 4, "arena" },
            { 5, "artifactworld" },
            { 6, "bazaar" },
            { 7, "blackbeach" },
            { 8, "blackbeach2" },
            { 10, "dampcavesimple" },
            { 12, "foggyswamp" },
            { 13, "frozenwall" },
            { 14, "goldshores" },
            { 15, "golemplains" },
            { 16, "golemplains2" },
            { 17, "goolake" },
            { 27, "limbo" },
            { 28, "lakes"},
            { 32, "moon2" },
            { 33, "mysteryspace" },
            { 35, "rootjungle" },
            { 37, "shipgraveyard" },
            { 38, "skymeadow" },
            { 39, "snowyforest" },
            { 41, "sulfurpools" },
            { 45, "voidraid" },
            { 46, "voidstage" },
            { 47, "wispgraveyard" },
            { 48, "village" },
            { 49, "villagenight" },
            { 50, "lakesnight" },
            { 51, "lemuriantemple" },
            { 52, "habitat" },
            { 53, "habitatfall" },
            { 54, "helminthroost" },
            { 55, "meridian" },
            // Alloyed Collective
            { 56, "nest" },
            { 57, "ironalluvium" },
            { 58, "ironalluvium2" },
            { 59, "repurposedcrater" },
            { 60, "conduitcanyon" },
            { 61, "solutionalhaunt" },
            { 62, "solusweb" },
        };

        public string GetLocationName(string cachedName)
        {
            int sceneIndex = GetSceneIndex(cachedName);
            if (locationsNames.TryGetValue(sceneIndex, out string locationName))
            {
                return locationName;
            }
            return "";
        }

        public string GetLocationNameByIndex(int index)
        {
            if (locationsNames.TryGetValue(index, out string locationName))
            {
                return locationName;
            }
            return "";
        }
        public string GetCachedLocationNameByIndex(int index)
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

        public bool CachedLocationNamesContains(string cachedName)
        {
            return cachedLocationsNames.ContainsValue(cachedName);
        }

        public int GetSceneIndex(string cachedName)
        {
            foreach (var scene in cachedLocationsNames)
            {
                if (scene.Value == cachedName)
                {
                    return scene.Key;
                }
            }
            return 0;
        }

    }
}
