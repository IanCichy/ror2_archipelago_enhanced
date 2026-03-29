using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Services;

/// <summary>
/// Manages crafting station availability (scrappers, printers, cauldrons, cleansing pools, recyclers).
/// In soft mode: vanilla spawns plus guaranteed stations near teleporter as unlocked.
/// In hard mode: stations don't spawn until unlocked via AP items.
/// </summary>
public class CraftingStationService : IService
{
    // Crafting station unlock order (matches Progressive Crafting item count)
    private static readonly string[] StationNames =
    {
        "Scrapper",
        "3D Printer",
        "Cauldron",
        "Cleansing Pool",
        "Recycler",
    };

    // Spawn card names for filtering in hard mode (verify at runtime)
    // Each station type may have multiple spawn card variants
    private static readonly Dictionary<int, string[]> StationSpawnCards = new()
    {
        { 0, new[] { "iscScrapper" } },                                          // Scrapper
        { 1, new[] { "iscDuplicator", "iscDuplicatorLarge", "iscDuplicatorWild" } },  // 3D Printers (white, green, red)
        { 2, new[] { "iscLunarCauldron, White", "iscLunarCauldron, Green" } },    // Cauldrons — TODO verify names
        { 3, new[] { "iscShrineCleanse", "iscShrineCleanseSandy", "iscShrineCleanseSnowy" } },  // Cleansing Pools
        { 4, new[] { "iscShrineRestack", "iscShrineRestackSandy", "iscShrineRestackSnowy" } },  // Recycler (Shrine of Order)
    };

    // Spawn cards for guaranteed spawns near teleporter
    private static readonly string[] GuaranteedSpawnCards =
    {
        "iscScrapper",
        "iscDuplicator",
        "iscLunarCauldron, White",   // TODO verify
        "iscShrineCleanse",
        "iscShrineRestack",
    };

    private int unlockedCount = 0;
    private int mode = 0; // 0=off, 1=soft, 2=hard
    private HashSet<string> allowedSpawnCards = new();

    public static CraftingStationService Instance { get; private set; }

    public int UnlockedCount => unlockedCount;
    public int Mode => mode;

    public void Initialize(Dictionary<string, object> slotData)
    {
        Instance = this;
        mode = 0;
        if (slotData.TryGetValue("craftingStationMode", out var modeObj))
        {
            mode = Convert.ToInt32(modeObj);
        }
        unlockedCount = 0;
        allowedSpawnCards.Clear();
        Log.LogDebug($"CraftingStationService initialized: mode={mode}");
    }

    /// <summary>
    /// Called when a Progressive Crafting item is received. Returns the station name for chat.
    /// </summary>
    public string UnlockNext()
    {
        if (unlockedCount >= StationNames.Length) return null;

        string name = StationNames[unlockedCount];

        // Add all spawn card variants for this station to the allowed set
        if (StationSpawnCards.TryGetValue(unlockedCount, out var cards))
        {
            foreach (var card in cards)
            {
                allowedSpawnCards.Add(card);
            }
        }

        unlockedCount++;
        Log.LogDebug($"Crafting station unlocked: {name} (total: {unlockedCount}/{StationNames.Length})");
        return name;
    }

    public string GetStationName(int index) => index < StationNames.Length ? StationNames[index] : "Unknown";
    public bool IsUnlocked(int index) => index < unlockedCount;

    public void Register()
    {
        if (mode == 2) // Hard mode: filter crafting stations from natural spawns
        {
            SceneDirector.onGenerateInteractableCardSelection += FilterCraftingStations;
        }
        // Both soft and hard: spawn guaranteed stations near teleporter
        if (mode > 0)
        {
            On.RoR2.TeleporterInteraction.ChargingState.OnEnter += TeleporterCharging_SpawnStations;
        }
    }

    public void Unregister()
    {
        SceneDirector.onGenerateInteractableCardSelection -= FilterCraftingStations;
        On.RoR2.TeleporterInteraction.ChargingState.OnEnter -= TeleporterCharging_SpawnStations;
        Instance = null;
    }

    /// <summary>
    /// Hard mode: remove crafting station spawn cards from the interactable pool.
    /// Only allows stations that have been unlocked.
    /// </summary>
    private void FilterCraftingStations(SceneDirector director, DirectorCardCategorySelection selection)
    {
        // Build set of all crafting spawn card names that should be blocked
        var blocked = new HashSet<string>();
        foreach (var kvp in StationSpawnCards)
        {
            foreach (var card in kvp.Value)
            {
                if (!allowedSpawnCards.Contains(card))
                {
                    blocked.Add(card);
                }
            }
        }

        if (blocked.Count == 0) return;

        for (int catIdx = 0; catIdx < selection.categories.Length; catIdx++)
        {
            var original = selection.categories[catIdx].cards;
            var filtered = original
                .Where(c => !blocked.Contains(c.spawnCard.name))
                .ToArray();

            if (filtered.Length != original.Length)
            {
                Log.LogDebug($"CraftingStationService: filtered {original.Length - filtered.Length} crafting cards from category '{selection.categories[catIdx].name}'");
                selection.categories[catIdx].cards = filtered;
            }
        }
    }

    /// <summary>
    /// Spawn guaranteed crafting stations near the teleporter when it starts charging.
    /// Only spawns stations that have been unlocked.
    /// </summary>
    private void TeleporterCharging_SpawnStations(
        On.RoR2.TeleporterInteraction.ChargingState.orig_OnEnter orig,
        RoR2.TeleporterInteraction.ChargingState self)
    {
        orig(self);

        if (!NetworkServer.active) return;
        if (unlockedCount == 0) return;

        var teleporter = TeleporterInteraction.instance;
        if (teleporter == null) return;

        Vector3 teleporterPos = teleporter.transform.position;

        for (int i = 0; i < unlockedCount && i < GuaranteedSpawnCards.Length; i++)
        {
            try
            {
                SpawnStationNearPosition(GuaranteedSpawnCards[i], teleporterPos, i, unlockedCount);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to spawn guaranteed crafting station '{GuaranteedSpawnCards[i]}': {ex.Message}");
            }
        }
    }

    private static void SpawnStationNearPosition(string spawnCardName, Vector3 center, int index, int total)
    {
        // Arrange stations in a circle around the teleporter
        float radius = 15f;
        float angle = (2f * Mathf.PI * index) / Mathf.Max(total, 1);
        Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        Vector3 targetPos = center + offset;

        var spawnCard = Addressables.LoadAssetAsync<InteractableSpawnCard>(spawnCardName).WaitForCompletion();
        if (spawnCard == null)
        {
            // Try Resources.Load as fallback
            spawnCard = Resources.Load<InteractableSpawnCard>($"SpawnCards/InteractableSpawnCard/{spawnCardName}");
        }
        if (spawnCard == null)
        {
            Log.LogWarning($"Could not load spawn card: {spawnCardName}");
            return;
        }

        var spawnRequest = new DirectorSpawnRequest(spawnCard, new DirectorPlacementRule
        {
            placementMode = DirectorPlacementRule.PlacementMode.NearestNode,
            position = targetPos,
        }, RoR2Application.rng);

        var result = DirectorCore.instance.TrySpawnObject(spawnRequest);
        if (result != null)
        {
            Log.LogDebug($"Spawned guaranteed crafting station: {spawnCardName} at {result.transform.position}");
        }
        else
        {
            Log.LogWarning($"DirectorCore failed to spawn: {spawnCardName}");
        }
    }

    /// <summary>
    /// Debug: dump all interactable spawn cards to log for verifying names.
    /// </summary>
    public static void DumpInteractableCards(SceneDirector director, DirectorCardCategorySelection selection)
    {
        for (int catIdx = 0; catIdx < selection.categories.Length; catIdx++)
        {
            var category = selection.categories[catIdx];
            Log.LogInfo($"Category: {category.name}");
            foreach (var card in category.cards)
            {
                Log.LogInfo($"  Card: {card.spawnCard.name}  Cost: {card.spawnCard.directorCreditCost}");
            }
        }
    }
}
