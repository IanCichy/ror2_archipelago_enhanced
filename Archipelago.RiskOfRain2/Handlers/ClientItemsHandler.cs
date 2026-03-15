using Archipelago.RiskOfRain2.Net;
using Archipelago.RiskOfRain2.UI;
using KinematicCharacterController;
using RoR2;
using UnityEngine;

namespace Archipelago.RiskOfRain2.Handlers
{
    class ClientItemsHandler : IHandler
    {
        public ClientItemsHandler()
        {
        }

        public void Hook()
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

        public void UnHook()
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
            foreach (NetworkUser local in NetworkUser.readOnlyLocalPlayersList)
            {
                if (local)
                {
                    SpawnCard spawnCard = ScriptableObject.CreateInstance<SpawnCard>();
                    spawnCard = LegacyResourcesAPI.Load<SpawnCard>("SpawnCards/InteractableSpawnCard/iscBarrel1");

                    Xoroshiro128Plus xoroshiro128PlusRadioScanner = new Xoroshiro128Plus(RoR2Application.rng);
                    if (DirectorCore.instance != null)
                    {
                        var card = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, new DirectorPlacementRule
                        {
                            placementMode = DirectorPlacementRule.PlacementMode.Random
                        }, xoroshiro128PlusRadioScanner));
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
            UnHook();
        }
    }
}
