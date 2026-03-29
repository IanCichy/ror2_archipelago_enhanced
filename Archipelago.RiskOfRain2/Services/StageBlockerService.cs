using Archipelago.RiskOfRain2.Console;
using Archipelago.RiskOfRain2.Extensions;
using EntityStates;
using R2API.Utils;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Services;

/// <summary>
/// Provides services for managing stage and environment progression, blocking, and unlocking within a run. Controls
/// which stages and environments are accessible based on progression rules and external conditions.
/// </summary>
/// <remarks>StageBlockerService is responsible for enforcing environment locks and progression logic, such as
/// blocking access to certain stages until specific conditions are met. It tracks which environments are available,
/// unlocked, or completed, and integrates with various game systems to prevent or allow access to environments as
/// appropriate. This service is typically used in scenarios where stage progression must be controlled dynamically,
/// such as in randomizer or challenge modes. Thread safety is not guaranteed; use from the main game thread.</remarks>
class StageBlockerService : IService
{

    public LocationExtensions locationsNames = new LocationExtensions();
    public int MostRecentStageGroup = 0;

    // Stage Progression system
    public static Dictionary<string, bool> StageUnlocks = new()
    {
        { "Stage 1", false },
        { "Stage 2", false },
        { "Stage 3", false },
        { "Stage 4", false },
    };

    public static int AmountOfStages = 0;

    // Static tracking for the scoreboard panel
    public static HashSet<string> AllSessionEnvironments { get; } = new HashSet<string>();
    public static HashSet<string> UnlockedEnvironments { get; } = new HashSet<string>();
    public static HashSet<string> CompletedEnvironments { get; } = new HashSet<string>();
    // Stage group mapping: scene name → AP stage group (0-4).
    // AP Stage 1 = game ordered stage 2 (first advancement after starting stages).
    // Group 0 = starting stages (no stage key required).
    public static readonly Dictionary<string, int> StageLookup = new()
    {
        // Starting stages (group 0)
        { "blackbeach", 0 }, { "blackbeach2", 0 }, { "golemplains", 0 }, { "golemplains2", 0 },
        { "lakes", 0 }, { "snowyforest", 0 },
        { "village", 0 }, { "villagenight", 0 }, { "lakesnight", 0 },
        // Vanilla + SOTV
        { "ancientloft", 1 },
        { "foggyswamp", 1 },
        { "goolake", 1 },
        { "frozenwall", 2 },
        { "sulfurpools", 2 },
        { "wispgraveyard", 2 },
        { "dampcavesimple", 3 },
        { "rootjungle", 3 },
        { "shipgraveyard", 3 },
        { "skymeadow", 4 },
        // SOTS
        { "lemuriantemple", 1 },
        { "habitat", 2 },
        { "habitatfall", 2 },
        { "helminthroost", 4 },
        { "meridian", 3 },
        // AC (per wiki: nest=Stage2, iron=Stage3, crater/canyon=Stage4, haunt=Stage5)
        { "nest", 1 },               // Pretender's Precipice = game Stage 2 = AP Stage 1
        { "ironalluvium", 2 },        // Iron Alluvium = game Stage 3 = AP Stage 2
        { "ironalluvium2", 2 },       // Iron Auroras = game Stage 3 = AP Stage 2
        { "repurposedcrater", 3 },    // Repurposed Crater = game Stage 4 = AP Stage 3
        { "conduitcanyon", 3 },       // Conduit Canyon = game Stage 4 = AP Stage 3
        { "solutionalhaunt", 4 },     // Solutional Haunt = game Stage 5 = AP Stage 4
    };

    // End Stage Progression system


    // A list of stages that should be blocked because they are locked by archipelago
    // uses scene names: https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Developer-Reference/Scene-Names/
    List<int> blockedStages;
    List<int> unblockedStages;
    HashSet<string> blockedStringStages;
    HashSet<string> unblockedStringStages;
    List<SceneDef> availableStages;
    private bool manuallyPickingStage = false; // used to keep track of when the call to PickNextStageScene is from the StageBlocker
    private bool voidPortalSpawned = false; // used for the deep void portal in Void Locus.
    private SceneDef prevOrderedStage = null; // used to keep track of what the scene was before the next scene is selected
    public static bool ProgressiveStages = false;
    public static bool ShowSeerPortals = false;
    public static string RevertToBeginningMessage = "";

    private SeerPortalService seerPortal;

    public StageBlockerService()
    {
        Log.LogDebug($"StageBlocker handler constructor.");
        blockedStages = new List<int>();
        unblockedStages = new List<int>();
        blockedStringStages = new HashSet<string>();
        unblockedStringStages = new HashSet<string>();
        availableStages = new List<SceneDef>();
        AmountOfStages = 0;
        AllSessionEnvironments.Clear();
        UnlockedEnvironments.Clear();
        CompletedEnvironments.Clear();

        // blocking stages should be down by the owner of this object
    }

    public void Register()
    {
        On.RoR2.SceneDirector.PlaceTeleporter += SceneDirector_PlaceTeleporter;
        On.RoR2.TeleporterInteraction.AttemptToSpawnAllEligiblePortals += TeleporterInteraction_AttemptToSpawnAllEligiblePortals1;
        On.RoR2.SeerStationController.SetTargetScene += SeerStationController_SetTargetScene;
        On.EntityStates.Interactables.MSObelisk.ReadyToEndGame.OnEnter += ReadyToEndGame_OnEnter;
        On.EntityStates.Interactables.MSObelisk.TransitionToNextStage.FixedUpdate += TransitionToNextStage_FixedUpdate;
        On.RoR2.PortalDialerController.PerformActionServer += PortalDialerController_PerformActionServer;
        On.RoR2.FrogController.Pet += FrogController_Pet;
        On.RoR2.Interactor.PerformInteraction += Interactor_PerformInteraction;
        On.RoR2.SceneExitController.Begin += SceneExitController_Begin;
        On.EntityStates.LunarTeleporter.Active.OnEnter += Active_OnEnter;
        On.RoR2.Run.CanPickStage += Run_CanPickStage;
        On.RoR2.Run.PickNextStageScene += Run_PickNextStageScene;
        On.RoR2.UI.ChatBox.OnEnable += ChatBox_OnEnable;
        On.RoR2.VoidStageMissionController.FixedUpdate += VoidStageMissionController_FixedUpdate;
        On.RoR2.VoidStageMissionController.OnDisable += VoidStageMissionController_OnDisable;
        ArchipelagoConsoleCommand.OnArchipelagoShowUnlockedStagesCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoShowUnlockedStagesCommandCalled;
        On.RoR2.SceneDef.AddDestinationsToWeightedSelection += SceneDef_AddDestinationsToWeightedSelection;
        On.RoR2.PortalSpawner.Start += PortalSpawner_Start;
    }

    public void Unregister()
    {
        On.RoR2.SceneDirector.PlaceTeleporter -= SceneDirector_PlaceTeleporter;
        On.RoR2.TeleporterInteraction.AttemptToSpawnAllEligiblePortals -= TeleporterInteraction_AttemptToSpawnAllEligiblePortals1;
        On.RoR2.SeerStationController.SetTargetScene -= SeerStationController_SetTargetScene;
        On.EntityStates.Interactables.MSObelisk.ReadyToEndGame.OnEnter -= ReadyToEndGame_OnEnter;
        On.EntityStates.Interactables.MSObelisk.TransitionToNextStage.FixedUpdate -= TransitionToNextStage_FixedUpdate;
        On.RoR2.PortalDialerController.PerformActionServer -= PortalDialerController_PerformActionServer;
        On.RoR2.FrogController.Pet -= FrogController_Pet;
        On.RoR2.Interactor.PerformInteraction -= Interactor_PerformInteraction;
        On.RoR2.SceneExitController.Begin -= SceneExitController_Begin;
        On.EntityStates.LunarTeleporter.Active.OnEnter -= Active_OnEnter;
        On.RoR2.Run.CanPickStage -= Run_CanPickStage;
        On.RoR2.Run.PickNextStageScene -= Run_PickNextStageScene;
        On.RoR2.UI.ChatBox.OnEnable -= ChatBox_OnEnable;
        On.RoR2.VoidStageMissionController.FixedUpdate -= VoidStageMissionController_FixedUpdate;
        On.RoR2.VoidStageMissionController.OnDisable -= VoidStageMissionController_OnDisable;
        On.RoR2.SceneDef.AddDestinationsToWeightedSelection -= SceneDef_AddDestinationsToWeightedSelection;
        On.RoR2.PortalSpawner.Start -= PortalSpawner_Start;

        // Reset values to prevent issues when restarting a run
        blockedStages = null;
        unblockedStages = null;
        blockedStringStages = null;
        unblockedStringStages = null;
        seerPortal = null;
        availableStages = null;
        AllSessionEnvironments.Clear();
        UnlockedEnvironments.Clear();
        CompletedEnvironments.Clear();
        MostRecentStageGroup = 0;
    }

    private void SceneDef_AddDestinationsToWeightedSelection(On.RoR2.SceneDef.orig_AddDestinationsToWeightedSelection orig, SceneDef self, WeightedSelection<SceneDef> dest, Func<SceneDef, bool> canAdd)
    {
        // This forces it to use the normal destination group instead of switching to the looped group after the first loop. (the looped ones are in this group for some reason).
        // This is probably really unstable with updates to the game but I don't see any other way to do this currently.
        if (self.destinationsGroup)
        {
            self.destinationsGroup.AddToWeightedSelection(dest, canAdd);
        }
        else
        {
            orig(self, dest, canAdd);
        }
    }

    private void ChatBox_OnEnable(On.RoR2.UI.ChatBox.orig_OnEnable orig, RoR2.UI.ChatBox self)
    {
        orig(self);
        if (RevertToBeginningMessage != "")
        {
            ChatMessage.SendColored(RevertToBeginningMessage, Color.red);
            RevertToBeginningMessage = "";
        }
    }

    public void BlockAll()
    {
        foreach (SceneDef scenedef in SceneCatalog.allSceneDefs)
        {
            Log.LogDebug($"scene index {SceneCatalog.FindSceneIndex(scenedef.cachedName)} scene name {scenedef.cachedName}");
            Log.LogDebug($"blocked by loop? {scenedef.isLockedBeforeLooping}");
            scenedef.isLockedBeforeLooping = false; // this is only used for the bazaar to block them before the first loop which we dont want

            if (scenedef.sceneType == SceneType.Stage || scenedef.sceneType == SceneType.Intermission)
            {
                SceneIndex index = SceneCatalog.FindSceneIndex(scenedef.cachedName);
                if (index == SceneIndex.Invalid) return;

                Block(scenedef.cachedName);

            }
        }

        // scenes from https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Developer-Reference/Scene-Names/
    }

    public void UnBlockAll()
    {
        blockedStringStages.Clear();
    }

    /// <summary>
    /// Marks all environments belonging to the given stage tier as unlocked
    /// in <see cref="UnlockedEnvironments"/> so the scoreboard reflects them.
    /// </summary>
    public void UnlockEnvironmentsForStage(int stageTier)
    {
        foreach (var entry in StageLookup)
        {
            if (entry.Value == stageTier && AllSessionEnvironments.Contains(entry.Key))
            {
                UnlockedEnvironments.Add(entry.Key);
                if (blockedStringStages.Remove(entry.Key))
                {
                    unblockedStringStages.Add(entry.Key);
                    Log.LogDebug($"Stage tier unlock: unblocked {entry.Key} (tier {stageTier})");
                }
            }
        }
    }

    /// <summary>
    /// Marks all environments up to the given progressive stage count as unlocked.
    /// </summary>
    public void UnlockEnvironmentsForProgressiveStages(int amount)
    {
        for (int tier = 1; tier <= amount; tier++)
        {
            UnlockEnvironmentsForStage(tier);
        }
    }

    /**
     * Blocks a given environment.
     * Returns true if the stage was blocked by this call.
     */
    public bool Block(string stageName)
    {
        if (blockedStringStages.Contains(stageName))
        {
            Log.LogDebug($"Environment already blocked: index {stageName}.");
            return false;
        }
        Log.LogDebug($"Blocking environment: index {stageName}.");
        blockedStringStages.Add(stageName);
        AllSessionEnvironments.Add(stageName);
        return true;
    }

    /**
     * Unblocks a given environment.
     * Returns true if the stage was unblocked by this call.
     */
    public bool UnBlock(int index)
    {
        string stageName = LocationExtensions.InternalSceneName[index];
        Log.LogDebug($"UnBlocking environment: index {stageName}.");
        unblockedStringStages.Add(stageName);
        UnlockedEnvironments.Add(stageName);
        return blockedStringStages.Remove(stageName);
    }

    /**
     * Returns true if a stage is blocked.
     */
    public bool CheckBlocked(string stageName)
    {
        if (Run.instance.nextStageScene != null && StageLookup.ContainsKey(stageName))
        {
            int tier = StageLookup[stageName];
            // Tier 0 = starting stages — never block by tier, only by individual environment unlock
            if (tier > 0)
            {
                string stageKey = $"Stage {tier}";
                if (!ProgressiveStages && StageUnlocks.ContainsKey(stageKey) && !StageUnlocks[stageKey])
                {
                    return true;
                }
                else if (ProgressiveStages && tier > AmountOfStages)
                {
                    return true;
                }
            }
        }
        return blockedStringStages.Contains(stageName);
    }
    private void ArchipelagoConsoleCommand_OnArchipelagoShowUnlockedStagesCommandCalled()
    {
        foreach (var scene in unblockedStringStages)
        {
            if (LocationExtensions.InternalSceneName.ContainsValue(scene))
            {
                ChatMessage.Send($"{scene}");
            }
        }
    }

    /**
     * Unalign the teleporter when Commencement is not unlocked.
     */
    private void Active_OnEnter(On.EntityStates.LunarTeleporter.Active.orig_OnEnter orig, EntityStates.LunarTeleporter.Active self)
    {
        if (CheckBlocked("moon2"))
        {
            ChatMessage.SendColored("Just not feeling it right now.", new Color(0x5d, 0xd5, 0xe2));
            self.outer.SetNextState(new EntityStates.LunarTeleporter.ActiveToIdle());
            return;
        }
        orig(self);
    }

    /**
     * Force the SceneExitController to rereoll the scene before moving to the next scene.
     * This is to help prevent going into the same environment on the next stage.
     */

    private void SceneExitController_Begin(On.RoR2.SceneExitController.orig_Begin orig, SceneExitController self)
    {
        // Suppose the player(s) enters a scene where they do not have a valid destination currently.
        // They would be guaranteed to be stuck in that level on the next stage.
        // By forcefully repicking the next scene, the player(s) can go to a scene that was unblocked while in the current scene.
        if (self.isColossusPortal)
        {
            bool runNextStage = true;
            int stageOrder = SceneCatalog.mostRecentSceneDef.stageOrder;

            Log.LogDebug($"SceneExitController_SetState checking for blocked stages. Current stage order {stageOrder}, mostRecent..{MostRecentStageGroup}.");
            if (stageOrder > 5) stageOrder = MostRecentStageGroup; // if the stage order is greater than 5, use the current scene's stage order instead

            switch (stageOrder)
            {
                case 1:
                    runNextStage = CheckBlocked("lemuriantemple");
                    break;
                case 2:
                    // with habitatfall being a stage you usually cant get to without an initial loop we need to add special handling for it
                    runNextStage = CheckBlocked("habitat") && CheckBlocked("habitatfall");
                    WeightedSelection<SceneDef> tier2Selection = new WeightedSelection<SceneDef>();
                    if (!CheckBlocked("habitat")) tier2Selection.AddChoice(SceneCatalog.FindSceneDef("habitat"), 10);
                    if (!CheckBlocked("habitatfall")) tier2Selection.AddChoice(SceneCatalog.FindSceneDef("habitatfall"), 10);
                    // This will prevent what loop you are on to decided what stage you go to.
                    if (!runNextStage)
                    {
                        self.isAlternatePath = false;
                    }
                    Run.instance.PickNextStageScene(tier2Selection);
                    self.tier3AlternateDestinationScene = Run.instance.nextStageScene;
                    self.destinationScene = Run.instance.nextStageScene;
                    break;
                case 3:
                case 4:
                case 5:
                    runNextStage = CheckBlocked("meridian");
                    break;
            }


            self.useRunNextStageScene = runNextStage;
        }

        if (self.useRunNextStageScene)
        {
            manuallyPickingStage = true;
            Run.instance.PickNextStageSceneFromCurrentSceneDestinations();
            Log.LogDebug("SceneExitController_SetState forcefully reroll next stagescene");
            manuallyPickingStage = false;
        }

        // Catch-all: if a portal (e.g. AC encrypted portal / Sentry Key beacon)
        // sets a fixed destinationScene that is blocked, redirect to a normal stage.
        if (!self.useRunNextStageScene && self.destinationScene != null && CheckBlocked(self.destinationScene.cachedName))
        {
            Log.LogWarning($"Portal destination {self.destinationScene.cachedName} is blocked — redirecting to normal stage.");
            ChatMessage.SendColored($"The path to {self.destinationScene.cachedName} is sealed. Redirecting...", Color.yellow);
            self.destinationScene = null;
            self.useRunNextStageScene = true;
            manuallyPickingStage = true;
            Run.instance.PickNextStageSceneFromCurrentSceneDestinations();
            manuallyPickingStage = false;
        }

        MostRecentStageGroup = SceneCatalog.mostRecentSceneDef.stageOrder;
        orig(self);
    }

    /**
     * Block interaction with the Void Fields portal if the environment is not unlocked.
     */
    private void Interactor_PerformInteraction(On.RoR2.Interactor.orig_PerformInteraction orig, Interactor self, GameObject interactableObject)
    {
        // I settled on hooking this method because I tried all other alternatives I could think of first.
        // I attempted using all of the following with little or no success:
        // - PortalSpawner_AttemptSpawnPortalServer: failed to block voidstage from spawning on teleporter
        // - PortalSpawner_Start: failed to block voidstage portal from spawning on teleporter
        // - GenericInteraction_RoR2_IInteractable_GetInteractability: broke all interactables
        // - GenericInteraction_RoR2_IInteractable_OnInteractionBegin: didn't seem to be called when using void portals

        // Blocking the use of void portals here is preferred over SceneExitController_SetState.
        // This is because it's more user friendly to let the user know they cannot travel to the void
        //  rather than redirect them to the next stage without warning.

        if (NetworkServer.active && interactableObject)
        {
            // TODO how much does this affect performance?
            foreach (IInteractable comp in interactableObject.GetComponents<IInteractable>())
            {
                GenericInteraction gi = comp as GenericInteraction;
                if (gi)
                {
                    switch (gi.contextToken)
                    {
                        case "PORTAL_ARENA_CONTEXT":
                            if (CheckBlocked("arena"))
                            {
                                ChatMessage.SendColored("The void rejects you.", new Color(0x88, 0x02, 0xd6));
                                gi.SetInteractabilityConditionsNotMet();
                            }
                            else gi.SetInteractabilityAvailable();
                            break;
                        case "PORTAL_VOID_CONTEXT":
                            if (CheckBlocked("voidstage"))
                            {
                                ChatMessage.SendColored("The void rejects you.", new Color(0x88, 0x02, 0xd6));
                                gi.SetInteractabilityConditionsNotMet();
                            }
                            else gi.SetInteractabilityAvailable();
                            break;
                        case "PORTAL_GOLDSHORES_CONTEXT":
                            if (CheckBlocked("goldshores"))
                            {
                                // prevents goldshores from being used from the halcyon shrine if not unlocked
                                ChatMessage.SendColored("The gold portal was missing the key to enter but stayed to taunt you.", Color.yellow);
                                gi.SetInteractabilityConditionsNotMet();
                            }
                            else gi.SetInteractabilityAvailable();
                            break;
                            // not blocking voidraid:
                            // NOTE: Planetarium has two entrances, one in Void Locus and one in Commencement
                            // Since this currently seems like an edge case where the player would truely decide to do both
                            //  if the player gets the Planetarium portal from Void Locus, they can travel there.
                            // Only the glass frog interaction in Commencement will be blocked.
                            // This also prevents the player from becoming stuck.

                            // Arguably the other portals could be handled here as well,
                            // however it seems more user friendly to just not spawn the portal at all rather
                            // than spawn the portal and make it unable to be interacted with.
                    }
                }
            }
        }
        orig(self, interactableObject);
    }

    /**
     * Block players from petting the frog and refund them if the Planetarium is not unlocked.
     */
    private void FrogController_Pet(On.RoR2.FrogController.orig_Pet orig, FrogController self, Interactor interactor)
    {
        // We block usage of the frog out of quality of life.
        // It would feel unfail to use 10 coins just to not spawn a portal or spawn a portal the user cannot use.
        // By adding coins back to the users inventory, it shows that the transaction cannot go through.
        // Adding a message also makes this even more clear.

        if (CheckBlocked("voidraid"))
        {
            Log.LogDebug("Blocking petting the frog for planetarium.");
            // Only host can refund the coin and having the host send the message prevents duplicate messages.
            if (NetworkServer.active)
            {
                Log.LogDebug("blocking planetarium as host.");
                // refund the lunar coin if the player who payed the coin is this client's player
                //interactor.GetComponent<NetworkUser>().AwardLunarCoins(1); // (only the server actually executes the contents of this method) // TODO give coin only to one person
                foreach (NetworkUser local in NetworkUser.readOnlyLocalPlayersList)
                {
                    Log.LogDebug("Refunding coins...");
                    local.AwardLunarCoins(1);
                    // TODO This does in fact give more coins back in multiplayer since every player would get a coin.
                    // I don't have a solution for this right now : ^)
                }

                ChatMessage.SendColored("The frog does not want to be pet.", Color.white);
            }
            return;
        }
        orig(self, interactor);
    }

    /**
     * Prevent the dialer from changing states if the Bulwark's Ambry is not unlocked.
     */
    private bool PortalDialerController_PerformActionServer(On.RoR2.PortalDialerController.orig_PerformActionServer orig, PortalDialerController self, byte[] sequence)
    {
        Log.LogDebug("PortalDialerController_PerformActionServer called.");
        if (CheckBlocked("artifactworld"))
        {
            // give a message so the user is aware the portal dialer interaction is blocked
            ChatMessage.SendColored($"The code will never work without Hidden Realm: Bulwark's Ambry.", Color.white);
            return false;
        }
        return orig(self, sequence);
    }
    /**
     * Block going to A Monument, Whole if the environment is not unlocked.
     */
    private void TransitionToNextStage_FixedUpdate(On.EntityStates.Interactables.MSObelisk.TransitionToNextStage.orig_FixedUpdate orig, EntityStates.Interactables.MSObelisk.TransitionToNextStage self)
    {
        // If the player decides to commit to Obliterating,
        //  they transition state should simply end the game normally
        //  (since the player should not be allowed into limbo).
        if (CheckBlocked("limbo"))
        {
            // run normal obliterate ending
            Run.instance.BeginGameOver(RoR2Content.GameEndings.ObliterationEnding);
            self.outer.SetNextState(new Idle());
        }
        orig(self);
    }

    /**
     * Give a warning before attempting to Obliterate while A Monument, Whole is still blocked.
     */
    private void ReadyToEndGame_OnEnter(On.EntityStates.Interactables.MSObelisk.ReadyToEndGame.orig_OnEnter orig, EntityStates.Interactables.MSObelisk.ReadyToEndGame self)
    {
        // Giving this warning is important for fairness.
        // This is because if the player decides to still Obliterate,
        //  we are just going to forcefully end the run.

        // Check if this is the server running this OnEnter, since mutliplayer clients could run this.
        // This is used to prevent duplicate messages being sent in multiplayer.
        if (NetworkServer.active && CheckBlocked("limbo"))
        {
            for (int i = 0; i < CharacterMaster.readOnlyInstancesList.Count; i++)
            {
                if (CharacterMaster.readOnlyInstancesList[i].inventory.GetItemCountEffective(RoR2Content.Items.LunarTrinket) > 0)
                {
                    ChatMessage.SendColored("Despite having Beads, you are not yet ready...", new Color(0x5d, 0xd5, 0xe2));
                    break;
                }
            }
        }
        orig(self);
    }

    /**
     * Block shop interation with Bazaar Seers for environments that are blocked.
     */
    private void SeerStationController_SetTargetScene(On.RoR2.SeerStationController.orig_SetTargetScene orig, SeerStationController self, SceneDef sceneDef)
    {
        // For the seers, we will not change their behavior for how they pick environments.
        // This behaviour could be changed but would require changing logic in the middle of SetUpSeerStations() which would take IL Hooks.
        // This has the consequence that seers can pick environments that are blocked.
        // In that case, we can just block the seer be able to be interacted with.
        // We also should hide the destination of the Seer since the it will not be reenabled when the player obtains the environment.

        string sceneName = sceneDef.cachedName;
        if (CheckBlocked(sceneName))
        {
            self.GetComponent<PurchaseInteraction>().SetAvailable(false);
            Log.LogDebug($"Bazaar Seer attempted to pick scene {sceneName}; blocked.");
            return;
        }
        else
        {
            Log.LogDebug($"Bazaar Seer picked scene {sceneName}");
        }
        orig(self, sceneDef);
    }

    private void SceneDirector_PlaceTeleporter(On.RoR2.SceneDirector.orig_PlaceTeleporter orig, SceneDirector self)
    {
        orig(self);
        try
        {
            seerPortal = null;
            seerPortal = new SeerPortalService();
            seerPortal.Initialize();
        }
        catch (Exception ex)
        {
            Log.LogError($"SeerPortal initialization failed: {ex}");
        }
    }

    /**
     * Block portals for blocked environments that would be spawned by the finishing teleporter event.
     */
    private void TeleporterInteraction_AttemptToSpawnAllEligiblePortals1(On.RoR2.TeleporterInteraction.orig_AttemptToSpawnAllEligiblePortals orig, TeleporterInteraction self)
    {
        // If the player unlocks the environments while they have orbs, they can still recieved the portals.
        // But as soon as the teleporter finishes, we will not give them the portals.
        // There could be a more friendly alternative but this should be fine.

        // the portals spawned by the teleporter event are for:
        // Hidden Realm: Bazaar Between Time
        // Hidden Realm: Gilded Coast
        // Hidden Realm: A Moment, Fractured
        GetAvailableStages();
        if (CheckBlocked("bazaar"))
        {
            if (self.shouldAttemptToSpawnShopPortal)
            {
                Log.LogDebug("Blue / bazaar portal blocked.");
                ChatMessage.Send("The blue portal was too shy to come out!");
            }
            self.shouldAttemptToSpawnShopPortal = false;
        }
        if (CheckBlocked("goldshores"))
        {
            if (self.shouldAttemptToSpawnGoldshoresPortal)
            {
                Log.LogDebug("Gold / goldshores portal blocked.");
                ChatMessage.Send("The gold portal was missing the key to enter and disappeared!");
            }
            self.shouldAttemptToSpawnGoldshoresPortal = false;
        }
        if (CheckBlocked("mysteryspace"))
        {
            if (self.shouldAttemptToSpawnMSPortal)
            {
                Log.LogDebug("Celestial / mysteryspace portal blocked.");
                ChatMessage.Send("The celestial portal decided you aren't ready!");
            }
            self.shouldAttemptToSpawnMSPortal = false;
        }
        orig(self);
    }

    private void PortalSpawner_Start(On.RoR2.PortalSpawner.orig_Start orig, PortalSpawner self)
    {

        if (self.bannedEventFlag == "FalseSonBossComplete")
        {
            self.bannedEventFlag = ""; // this prevents the colossus portal from being blocked after false son has been defeated
        }
        orig(self);
    }

    /**
     * Forcefully fail to the CanPickStage check for stages that are blocked.
     */
    private bool Run_CanPickStage(On.RoR2.Run.orig_CanPickStage orig, Run self, SceneDef scenedef)
    {
        Log.LogDebug($"Checking CanPickStage for {scenedef.nameToken}...");
        string stageName = scenedef.cachedName;
        if (CheckBlocked(stageName))
        {
            // if the stage is blocked, it cannot be picked
            Log.LogDebug("blocking.");
            return false;
        }
        availableStages.Add(scenedef);
        Log.LogDebug("passing through.");

        return orig(self, scenedef);
    }

    // Stages that should never appear as seer portal destinations
    private static readonly HashSet<string> PortalExcludedStages = new()
    {
        "moon2", "voidstage", "voidraid", "meridian",
        "solutionalhaunt", "solusweb", "arena", "bazaar",
        "goldshores", "artifactworld", "limbo", "mysteryspace",
    };

    public void GetAvailableStages()
    {
        availableStages.Clear();
        manuallyPickingStage = true;
        Run.instance.PickNextStageSceneFromCurrentSceneDestinations();
        manuallyPickingStage = false;

        // Augment with tier-skip destinations: if the game's destination list
        // is missing unblocked environments in the target tier, add them.
        // This handles cases where the game's scene-to-scene routing doesn't
        // include all environments our mod has unblocked (e.g. DLC stages,
        // or tier-skipping past a locked tier).
        if (availableStages.Count > 0 && unblockedStringStages != null)
        {
            // Determine the target tier from what the game picked
            int targetTier = -1;
            foreach (var stage in availableStages)
            {
                if (StageLookup.TryGetValue(stage.cachedName, out int t) && t > 0)
                {
                    targetTier = t;
                    break;
                }
            }

            // Add any unblocked environments in the same target tier that the game missed
            if (targetTier > 0)
            {
                var existingNames = new HashSet<string>();
                foreach (var s in availableStages) existingNames.Add(s.cachedName);

                foreach (string unblocked in unblockedStringStages)
                {
                    if (existingNames.Contains(unblocked)) continue;
                    if (PortalExcludedStages.Contains(unblocked)) continue;
                    if (!StageLookup.TryGetValue(unblocked, out int tier)) continue;
                    if (tier != targetTier) continue;

                    SceneDef sd = SceneCatalog.FindSceneDef(unblocked);
                    if (sd != null)
                    {
                        availableStages.Add(sd);
                        Log.LogDebug($"Seer augment: added {unblocked} (tier {tier})");
                    }
                }
            }
        }
        else if (availableStages.Count == 0 && unblockedStringStages != null)
        {
            // No normal destinations — find the next reachable tier and show those
            int currentTier = SceneCatalog.mostRecentSceneDef != null
                ? (StageLookup.TryGetValue(SceneCatalog.mostRecentSceneDef.cachedName, out int ct) ? ct : -1)
                : -1;

            for (int tier = currentTier + 1; tier <= 4; tier++)
            {
                foreach (string unblocked in unblockedStringStages)
                {
                    if (PortalExcludedStages.Contains(unblocked)) continue;
                    if (!StageLookup.TryGetValue(unblocked, out int t) || t != tier) continue;

                    SceneDef sd = SceneCatalog.FindSceneDef(unblocked);
                    if (sd != null)
                    {
                        availableStages.Add(sd);
                        Log.LogDebug($"Seer tier-skip: added {unblocked} (tier {tier})");
                    }
                }
                if (availableStages.Count > 0) break; // stop at first tier with destinations
            }
        }

        if (availableStages.Count > 0 && ShowSeerPortals && seerPortal != null)
        {
            seerPortal.CreatePortal(availableStages);
        }
    }

    private void Run_PickNextStageScene(On.RoR2.Run.orig_PickNextStageScene orig, Run self, WeightedSelection<SceneDef> choices)
    {
        // When the does not have a valid next environment, we will move them to an environment within the same orderedstage.
        // When this happens, we will consider the player as "lost".
        // If the player doesn't have a next environment when lost, the player will be moved back to orderedstage 1.
        // The reason for this is if the player is playing with explore mode, the player's next environment could be in a different already unlocked environment.
        // Thus if the next unlock is somewhere, it would be nice to the the player get to that somewhere without restarting the run.

        bool hasHabitat = false;
        bool hasHabitatFall = false;

        // Since hatitatfall is a stage you usually cant get to without an initial loop we need to add special handling for it
        choices.choices.ForEachTry(choice =>
        {
            if (choice.value.cachedName == "habitat") hasHabitat = true;
            if (choice.value.cachedName == "habitatfall") hasHabitatFall = true;
        });

        if (hasHabitat || hasHabitatFall)
        {
            // We need a new sceneGroup here because startingSceneGroup has all the first stages in it and we only want to roll the two alternate stages at this level.
            SceneCollection habitatSceneGroup = new SceneCollection();
            SceneCollection originalStartingSceneGroup = self.startingSceneGroup;
            self.startingSceneGroup = habitatSceneGroup;
            self.startingSceneGroup.AddToWeightedSelection(choices, self.CanPickStage);
            orig(self, choices);
            self.startingSceneGroup = originalStartingSceneGroup;
            return;
        }


        // 46 = Void Locus and if you are on that stage and you dont have The Planetarium the player will be moved back to orderedstage 1.
        if (SceneCatalog.mostRecentSceneDef.cachedName == "voidstage" && CheckBlocked("voidraid"))
        {
            Log.LogDebug("loaded Void Locus without The Planetarium");
            SceneCatalog.mostRecentSceneDef.stageOrder = 1;
            Log.LogDebug("Switching to stage 1");
            self.startingSceneGroup.AddToWeightedSelection(choices, self.CanPickStage);

        }

        // there are 2 conditions when we should mess with this call:
        // - the call to PickNextStageScene should have originated from stage blocker
        //      (since it gets called at the beginning of the scene by the game, and at the end by the stage blocker)
        // - this should do nothing special unless the current scene happens to be an ordered stage
        if (manuallyPickingStage && SceneCatalog.mostRecentSceneDef && 1 <= SceneCatalog.mostRecentSceneDef.stageOrder && 5 >= SceneCatalog.mostRecentSceneDef.stageOrder)
        {
            //string nextStage = $"Stage {self.nextStageScene.stageOrder - 1}";
            //Log.LogDebug($"Stage {self.nextStageScene.stageOrder} == {StageUnlocks[nextStage]}");
            // populate choices (in some manner) when there are no choices
            if (0 == choices.Count)
            {
                string reason = "";
                Log.LogDebug("no choices for next scene; setting up alternate choices");

                if (prevOrderedStage) Log.LogDebug($"prev scene {prevOrderedStage.sceneDefIndex} in stage {prevOrderedStage.stageOrder}");
                else Log.LogDebug("no prev scene");
                Log.LogDebug($"Most recent scene stage order Stage {SceneCatalog.mostRecentSceneDef.stageOrder}");
                if (!StageUnlocks[$"Stage {SceneCatalog.mostRecentSceneDef.stageOrder}"] && !ProgressiveStages)
                {
                    reason = $"you need Stage {SceneCatalog.mostRecentSceneDef.stageOrder}";
                }
                else if (SceneCatalog.mostRecentSceneDef.stageOrder > AmountOfStages && ProgressiveStages)
                {
                    reason = $"you need {SceneCatalog.mostRecentSceneDef.stageOrder} Progressive Stages";
                }
                else
                {
                    List<string> stagesNeeded = new List<string>();
                    reason = $"you are missing ";
                    foreach (KeyValuePair<string, int> entry in StageLookup)
                    {

                        if (entry.Value == SceneCatalog.mostRecentSceneDef.stageOrder)
                        {
                            stagesNeeded.Add(entry.Key);
                        }
                    }
                    if (stagesNeeded != null && stagesNeeded.Count > 0)
                    {
                        for (var i = 0; i < stagesNeeded.Count; i++)
                        {
                            if (i < stagesNeeded.Count - 1 || stagesNeeded.Count == 1)
                            {
                                reason += $"{LocationExtensions.GetLocationName(stagesNeeded[i])}, ";
                            }
                            else
                            {
                                reason += $"or {LocationExtensions.GetLocationName(stagesNeeded[i])}";
                            }
                        }
                    }

                }
                // Non-progressive: try to skip forward to the next unlocked tier
                if (!ProgressiveStages)
                {
                    int currentTier = SceneCatalog.mostRecentSceneDef.stageOrder;
                    for (int tier = currentTier + 1; tier <= 5; tier++)
                    {
                        string stageKey = $"Stage {tier}";
                        if (StageUnlocks.ContainsKey(stageKey) && StageUnlocks[stageKey])
                        {
                            foreach (var entry in StageLookup)
                            {
                                if (entry.Value == tier && !CheckBlocked(entry.Key))
                                {
                                    SceneDef sd = SceneCatalog.FindSceneDef(entry.Key);
                                    if (sd != null)
                                    {
                                        choices.AddChoice(sd, 1f);
                                        Log.LogDebug($"Tier-skip: added {entry.Key} (tier {tier})");
                                    }
                                }
                            }
                            if (choices.Count > 0)
                            {
                                RevertToBeginningMessage = $"Skipping to Stage {tier} environments!";
                                break;
                            }
                        }
                    }
                }

                // If tier-skipping didn't find anything (or progressive mode), fall back to stage 1
                if (choices.Count == 0)
                {
                    RevertToBeginningMessage = $"Unable to advance to the next set of stages because {reason}!";

                    Log.LogDebug("adding choices for stage 1");
                    self.startingSceneGroup.AddToWeightedSelection(choices, self.CanPickStage);
                }
            }
            else Log.LogDebug("there are choices for the next scene; skipping tampering said choices");

            prevOrderedStage = SceneCatalog.mostRecentSceneDef;
        }

        // If choices is still empty after the startingSceneGroup fallback, it means
        // the only unblocked environments aren't in the default startingSceneGroup
        // (e.g., AC's "nest" which the game doesn't include in startingSceneGroup).
        // Manually add any unblocked scene as a candidate — any stage is better than crashing.
        if (choices.Count == 0)
        {
            Log.LogDebug("startingSceneGroup fallback produced no choices; adding any unblocked scene");
            foreach (string unblocked in unblockedStringStages)
            {
                SceneDef sd = SceneCatalog.FindSceneDef(unblocked);
                if (sd != null && sd.sceneType == SceneType.Stage)
                {
                    choices.AddChoice(sd, 1f);
                    Log.LogDebug($"Added unblocked scene as fallback: {unblocked} (stageOrder={sd.stageOrder})");
                }
            }
        }

        Log.LogDebug($"PickNextStageScene: final choices.Count={choices.Count}, manuallyPicking={manuallyPickingStage}");
        orig(self, choices);
        if (self.nextStageScene != null)
            Log.LogDebug($"next scene {self.nextStageScene.cachedName} in stage {self.nextStageScene.stageOrder}");
        else
            Log.LogWarning("PickNextStageScene failed to select a stage — nextStageScene is null!");
    }

    // Checks to see when the Deep Portal spawns and to see if you have The Planetarium to proceed.
    private void VoidStageMissionController_FixedUpdate(On.RoR2.VoidStageMissionController.orig_FixedUpdate orig, VoidStageMissionController self)
    {
        orig(self);
        if (!CheckBlocked("voidraid"))
        {
            return;
        }
        if (self.numBatteriesActivated >= self.numBatteriesSpawned && self.numBatteriesSpawned > 0 && !voidPortalSpawned)
        {
            Log.LogDebug("Portal Activated");
            voidPortalSpawned = true;
            var deepPortal = GameObject.Find("DeepVoidPortal(Clone)");
            deepPortal.GetComponent<SceneExitController>().useRunNextStageScene = true;
        }
    }
    // Needed to reset voidPortalSpawned to false for the next time the user is on Void Locus.
    private void VoidStageMissionController_OnDisable(On.RoR2.VoidStageMissionController.orig_OnDisable orig, VoidStageMissionController self)
    {
        orig(self);
        voidPortalSpawned = false;
    }
}