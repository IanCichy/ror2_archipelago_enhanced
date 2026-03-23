namespace Archipelago.RiskOfRain2;

/// <summary>
/// Shared Python/AP environment IDs from worlds/ror2/ror2environments.py.
/// For vanilla and SOTV, these match C# SceneCatalog indices.
/// For SOTS and AC, Python uses sequential IDs (48+) that differ from SceneCatalog.
/// These IDs are used for AP item/location ID computation and must match the Python world.
/// </summary>
internal static class EnvironmentIds
{
    // Vanilla
    public const int Blackbeach = 7;        // Distant Roost
    public const int Blackbeach2 = 8;       // Distant Roost (2)
    public const int Golemplains = 15;      // Titanic Plains
    public const int Golemplains2 = 16;     // Titanic Plains (2)
    public const int Lakes = 28;            // Verdant Falls
    public const int Goolake = 17;          // Abandoned Aqueduct
    public const int Foggyswamp = 12;       // Wetland Aspect
    public const int Frozenwall = 13;       // Rallypoint Delta
    public const int Wispgraveyard = 47;    // Scorched Acres
    public const int Dampcavesimple = 10;   // Abyssal Depths
    public const int Shipgraveyard = 37;    // Siren's Call
    public const int Rootjungle = 35;       // Sundered Grove
    public const int Skymeadow = 38;        // Sky Meadow

    // SOTV
    public const int Snowyforest = 39;      // Siphoned Forest
    public const int Ancientloft = 3;       // Aphelian Sanctuary
    public const int Sulfurpools = 41;      // Sulfur Pools

    // SOTS (Python IDs, NOT C# SceneCatalog indices)
    public const int Village = 48;          // Shattered Abodes (C# scene index: 54)
    public const int Villagenight = 49;     // Disturbed Impact (C# scene index: 55)
    public const int Lakesnight = 50;       // Viscous Falls (C# scene index: 34)
    public const int Lemuriantemple = 51;   // Reformed Altar (C# scene index: 36)
    public const int Habitat = 52;          // Treeborn Colony (C# scene index: 21)
    public const int Habitatfall = 53;      // Golden Dieback (C# scene index: 22)
    public const int Helminthroost = 54;    // Helminth Hatchery (C# scene index: 23)
    public const int Meridian = 55;         // Prime Meridian (C# scene index: 40)

    // AC (Python IDs)
    public const int Nest = 56;             // Pretender's Precipice
    public const int Ironalluvium = 57;     // Iron Alluvium
    public const int Ironalluvium2 = 58;    // Iron Auroras
    public const int Repurposedcrater = 59; // Repurposed Crater
    public const int Conduitcanyon = 60;    // Conduit Canyon
    public const int Solutionalhaunt = 61;  // Solutional Haunt
    public const int Solusweb = 62;         // Neural Sanctum

    // Hidden realms / special
    public const int Arena = 4;             // Void Fields
    public const int Artifactworld = 5;     // Hidden Realm: Bulwark's Ambry
    public const int Bazaar = 6;            // Hidden Realm: Bazaar Between Time
    public const int Goldshores = 14;       // Hidden Realm: Gilded Coast
    public const int Limbo = 27;            // Hidden Realm: A Moment, Whole
    public const int Moon2 = 32;            // Commencement
    public const int Mysteryspace = 33;     // Hidden Realm: A Moment, Fractured
    public const int Voidraid = 45;         // The Planetarium
    public const int Voidstage = 46;        // Void Locus
}
