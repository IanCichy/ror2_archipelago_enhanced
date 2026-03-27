using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archipelago.RiskOfRain2.Services;

/// <summary>
/// Restricts which drone types can spawn as broken interactables.
/// AP items unlock additional drone types over the course of a session.
/// </summary>
public class DronePoolService : IService
{
    // Mapping from AP item ID to RoR2 spawn card name(s).
    // Base game drone names are verified; DLC drone names marked with TODO need runtime verification.
    private static readonly Dictionary<long, DroneInfo> DroneRegistry = new()
    {
        { 37201, new DroneInfo("Gunner Drone",       "iscBrokenDrone1") },
        { 37202, new DroneInfo("Healing Drone",      "iscBrokenDrone2") },
        { 37203, new DroneInfo("Gunner Turret",      "iscBrokenTurret1") },
        { 37204, new DroneInfo("Missile Drone",      "iscBrokenMissileDrone") },
        { 37205, new DroneInfo("Emergency Drone",    "iscBrokenEmergencyDrone") },
        { 37206, new DroneInfo("Equipment Drone",    "iscBrokenEquipmentDrone") },
        { 37207, new DroneInfo("Incinerator Drone",  "iscBrokenFlameDrone") },
        { 37208, new DroneInfo("TC-280 Prototype",   "iscBrokenMegaDrone") },
        // DLC drones — spawn card names need runtime verification (use ap_dump_drones command)
        { 37209, new DroneInfo("Cleanup Drone",      "iscBrokenCleanupDrone") },       // TODO verify
        { 37210, new DroneInfo("Barrier Drone",      "iscBrokenBarrierDrone") },       // TODO verify
        { 37211, new DroneInfo("Jailer Drone",       "iscBrokenJailerDrone") },        // TODO verify
        { 37212, new DroneInfo("Bombardment Drone",  "iscBrokenBombardmentDrone") },   // TODO verify
        { 37213, new DroneInfo("Freeze Drone",       "iscBrokenFreezeDrone") },        // TODO verify
        { 37214, new DroneInfo("Transport Drone",    "iscBrokenTransportDrone") },     // TODO verify
        { 37215, new DroneInfo("Junk Drone",         "iscBrokenJunkDrone") },          // TODO verify
    };

    private readonly HashSet<string> allowedDroneCards = new();

    public static DronePoolService Instance { get; private set; }
    public static event Action OnDronePoolChanged;

    public void Initialize(Dictionary<string, object> slotData)
    {
        Instance = this;
        // Healing + Gunner are always allowed from the start
        allowedDroneCards.Add(DroneRegistry[37201].SpawnCardName); // Gunner
        allowedDroneCards.Add(DroneRegistry[37202].SpawnCardName); // Healing
    }

    /// <summary>
    /// Unlocks a drone type by AP item ID. Returns the friendly name for chat display, or null if unknown.
    /// </summary>
    public string UnlockDrone(long itemId)
    {
        if (!DroneRegistry.TryGetValue(itemId, out var info)) return null;

        bool isNew = allowedDroneCards.Add(info.SpawnCardName);
        if (isNew)
        {
            OnDronePoolChanged?.Invoke();
            Log.LogDebug($"Drone unlocked: {info.FriendlyName} ({info.SpawnCardName})");
        }
        return info.FriendlyName;
    }

    public bool IsDroneAllowed(string spawnCardName) => allowedDroneCards.Contains(spawnCardName);

    public int UnlockedCount => allowedDroneCards.Count;

    public void Register()
    {
        SceneDirector.onGenerateInteractableCardSelection += FilterDrones;
    }

    public void Unregister()
    {
        SceneDirector.onGenerateInteractableCardSelection -= FilterDrones;
        Instance = null;
    }

    private void FilterDrones(SceneDirector director, DirectorCardCategorySelection selection)
    {
        for (int catIdx = 0; catIdx < selection.categories.Length; catIdx++)
        {
            if (selection.categories[catIdx].name != "Drones") continue;

            var original = selection.categories[catIdx].cards;
            var filtered = original
                .Where(c => allowedDroneCards.Contains(c.spawnCard.name))
                .ToArray();

            if (filtered.Length != original.Length)
            {
                Log.LogDebug($"DronePoolService: filtered {original.Length} → {filtered.Length} drone cards");
                selection.categories[catIdx].cards = filtered;
            }
        }
    }

    /// <summary>
    /// Debug helper: logs all drone spawn cards found in the current stage's interactable selection.
    /// Call via console command to verify spawn card names at runtime.
    /// </summary>
    public static void DumpDroneCards(SceneDirector director, DirectorCardCategorySelection selection)
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

    private class DroneInfo
    {
        public string FriendlyName { get; }
        public string SpawnCardName { get; }
        public DroneInfo(string friendlyName, string spawnCardName)
        {
            FriendlyName = friendlyName;
            SpawnCardName = spawnCardName;
        }
    }
}
