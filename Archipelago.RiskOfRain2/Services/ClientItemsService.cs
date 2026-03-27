using Archipelago.RiskOfRain2.Network;
using Archipelago.RiskOfRain2.UI;
using KinematicCharacterController;
using RoR2;
using UnityEngine;

namespace Archipelago.RiskOfRain2.Services;

/// <summary>
/// Provides client-side management and event registration for item synchronization and related gameplay features in the
/// Archipelago integration.
/// </summary>
/// <remarks>This service is responsible for subscribing to and handling various game events related to item and
/// shrine synchronization, teleportation, and game mode transitions. It should be registered at the appropriate time in
/// the application's lifecycle to ensure correct event handling, and unregistered when no longer needed to prevent
/// unintended side effects. This class is not thread-safe.</remarks>
class ClientItemsService : IService
{
    public ClientItemsService()
    {
    }

    public void Register()
    {
        Log.LogDebug("Client Items Started");
        SyncLocationCheckProgress.OnLocationSynced += ArchipelagoCheckCountdownController.UpdateItemCountdown;
        SyncShrineCheckProgress.OnShrineSynced += ArchipelagoCheckCountdownController.UpdateShrineCountdown;
        ArchipelagoStartExplore.OnArchipelagoStartExplore += ArchipelagoStartExplore_OnArchipelagoStartExplore;
        ArchipelagoStartClassic.OnArchipelagoStartClassic += ArchipelagoStartClassic_OnArchipelagoStartClassic;
        ArchipelagoTeleportClient.OnArchipelagoTeleportClient += ArchipelagoTeleportClient_OnArchipelagoTeleportClient;
        Run.onRunDestroyGlobal += Run_onRunDestroyGlobal;
        ArchipelagoCheckCountdownController.AddObjective();
    }

    public void Unregister()
    {
        ArchipelagoTeleportClient.OnArchipelagoTeleportClient -= ArchipelagoTeleportClient_OnArchipelagoTeleportClient;
        SyncLocationCheckProgress.OnLocationSynced -= ArchipelagoCheckCountdownController.UpdateItemCountdown;
        SyncShrineCheckProgress.OnShrineSynced -= ArchipelagoCheckCountdownController.UpdateShrineCountdown;
        ArchipelagoStartExplore.OnArchipelagoStartExplore -= ArchipelagoStartExplore_OnArchipelagoStartExplore;
        ArchipelagoCheckCountdownController.RemoveObjective();
        Run.onRunDestroyGlobal -= Run_onRunDestroyGlobal;
    }

    private void ArchipelagoStartClassic_OnArchipelagoStartClassic()
    {
        Log.LogDebug("Client Classic Started");
        ArchipelagoCheckCountdownController.ShowShrineCountdown = false;
    }

    private void ArchipelagoStartExplore_OnArchipelagoStartExplore()
    {
        Log.LogDebug("Client Explore Started");
        ArchipelagoCheckCountdownController.ShowShrineCountdown = true;
    }

    private void ArchipelagoTeleportClient_OnArchipelagoTeleportClient()
    {
        TeleportLocalPlayersToRandomPosition();
    }

    /// <summary>
    /// Teleports all local players to a random valid position on the current stage
    /// by spawning an invisible barrel as a placement anchor.
    /// </summary>
    public static void TeleportLocalPlayersToRandomPosition()
    {
        foreach (NetworkUser local in NetworkUser.readOnlyLocalPlayersList)
        {
            if (local)
            {
                SpawnCard spawnCard = LegacyResourcesAPI.Load<SpawnCard>("SpawnCards/InteractableSpawnCard/iscBarrel1");

                Xoroshiro128Plus rng = new(RoR2Application.rng);
                if (DirectorCore.instance != null)
                {
                    var card = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, new DirectorPlacementRule
                    {
                        placementMode = DirectorPlacementRule.PlacementMode.Random
                    }, rng));
                    var position = card.transform.position;
                    Log.LogDebug($"teleport position {position + new Vector3(0, 10, 0)}");
                    var body = local.master.GetBody();
                    body.GetComponentInChildren<KinematicCharacterMotor>().SetPosition(position + new Vector3(0, 10, 0));
                    card.SetActive(false);
                }
            }
        }
    }

    private void Run_onRunDestroyGlobal(Run obj)
    {
        Unregister();
    }
}