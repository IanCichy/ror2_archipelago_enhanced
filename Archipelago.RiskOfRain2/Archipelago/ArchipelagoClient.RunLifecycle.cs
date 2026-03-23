using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.RiskOfRain2.Console;
using Archipelago.RiskOfRain2.Network;
using Archipelago.RiskOfRain2.Services;
using Archipelago.RiskOfRain2.UI;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;
using RoR2.UI;
using System;
using System.Linq;
using UnityEngine;

namespace Archipelago.RiskOfRain2;

/// <summary>
/// Per-run lifecycle: setup, cleanup, hooks, victory, and game-over UI.
/// </summary>
public partial class ArchipelagoClient
{
    /// <summary>
    /// Creates per-run state: ItemLogic, handlers, UI bars, game hooks.
    /// Called on first connect and on session reuse for subsequent runs.
    /// </summary>
    public void SetupRun()
    {
        isEndingAcceptable = false;

        ItemLogic = new ArchipelagoItemLogicController(session);

        // Initialize countdown objective for check progress
        ArchipelagoCheckCountdownController.ItemStep = (int)cachedItemPickupStep;
        ArchipelagoCheckCountdownController.ItemsPickedUp = 0;
        ArchipelagoCheckCountdownController.ShowItemCountdown = true;

        if (cachedGoalIsExplore)
        {
            Log.LogDebug("Setting up explore mode for run");
            StageBlockerService = new StageBlockerService();
            ItemLogic.StageBlockerService = StageBlockerService;
            StageBlockerService.BlockAll();

            // Backwards compatibility: if the AP world doesn't define stage items,
            // unlock all stages immediately. Otherwise lock them for item-gated unlock.
            if (cachedLegacyStagesUnlocked)
            {
                StageBlockerService.StageUnlocks["Stage 1"] = true;
                StageBlockerService.StageUnlocks["Stage 2"] = true;
                StageBlockerService.StageUnlocks["Stage 3"] = true;
                StageBlockerService.StageUnlocks["Stage 4"] = true;
            }
            else if (!isInGame)
            {
                StageBlockerService.StageUnlocks["Stage 1"] = false;
                StageBlockerService.StageUnlocks["Stage 2"] = false;
                StageBlockerService.StageUnlocks["Stage 3"] = false;
                StageBlockerService.StageUnlocks["Stage 4"] = false;
            }

            LocationCheckService = new LocationCheckService(session, LocationCheckService.BuildTemplateFromSlotData(cachedSlotData));
            ShrineChanceService = new ShrineChanceService();

            ArchipelagoCheckCountdownController.ShrineStep = (int)cachedShrineUseStep;
            ArchipelagoCheckCountdownController.ShrinesUsed = 0;
            ArchipelagoCheckCountdownController.ShowShrineCountdown = true;

            LocationCheckService.ItemPickupStep = cachedItemPickupStep;
            LocationCheckService.ShrineUseStep = cachedShrineUseStep;
        }
        else
        {
            Log.LogDebug("Setting up classic mode for run");
            ArchipelagoLocationsInEnvironmentController.RemoveObjective();
            ArchipelagoCheckCountdownController.ShowShrineCountdown = false;
            new AllChecksCompleteInStage().Send(NetworkDestination.Clients);
        }

        ArchipelagoCheckCountdownController.AddObjective();

        // Item pool limiting
        if (cachedItemPoolLimiting)
        {
            ItemPoolService = new ItemPoolService();
            ItemPoolService.Initialize(cachedSlotData);
            ItemLogic.ItemPoolService = ItemPoolService;
        }

        // Initialize ItemLogic location tracking from session state.
        // On first connect, Session_PacketReceived won't fire because ItemLogic
        // is created after TryConnectAndLogin. On session reuse, restore cached state.
        if (hasCachedRunState)
        {
            ItemLogic.ItemPickupStep = cachedItemLogicPickupStep;
            ItemLogic.TotalChecks = cachedItemLogicTotalChecks;
            ItemLogic.CurrentChecks = cachedItemLogicCurrentChecks;
            ItemLogic.PickedUpItemCount = cachedItemLogicPickedUpItemCount;
        }
        else
        {
            ItemLogic.InitializeFromConnectionState(!cachedGoalIsExplore, (int)cachedItemPickupStep);
        }

        ItemLogic.OnItemDropProcessed += ItemLogicHandler_ItemDropProcessed;
        if (cachedDeathLinkEnabled)
        {
            DeathLinkManager?.Register();
        }
        HookGame();

        // These messages are idempotent — safe to re-send on session reuse
        new ArchipelagoStartMessage().Send(NetworkDestination.Clients);
        if (!cachedGoalIsExplore)
        {
            new ArchipelagoStartClassic().Send(NetworkDestination.Clients);
        }
        else
        {
            new ArchipelagoStartExplore().Send(NetworkDestination.Clients);
        }

        // Enqueue all received items for this run. Handles both first connect
        // (items missed during login) and session reuse (re-granting items
        // for the new run since the player's inventory is empty).
        ItemLogic.ProcessAllReceivedItems();
        ItemLogic.Precollect();
    }

    /// <summary>
    /// Tears down per-run state. The AP session stays alive for reuse.
    /// </summary>
    public void CleanupRun()
    {
        // Re-entrance guard: Run_onRunDestroyGlobal and Session_SocketClosed can
        // both call CleanupRun() near-simultaneously. Prevent double-dispose.
        if (ItemLogic == null) return;

        UnhookGame();

        if (ItemLogic != null)
        {
            // Cache state before disposing for session reuse
            hasCachedRunState = true;
            cachedItemLogicPickupStep = ItemLogic.ItemPickupStep;
            cachedItemLogicTotalChecks = ItemLogic.TotalChecks;
            cachedItemLogicCurrentChecks = ItemLogic.CurrentChecks;
            cachedItemLogicPickedUpItemCount = ItemLogic.PickedUpItemCount;

            ItemLogic.OnItemDropProcessed -= ItemLogicHandler_ItemDropProcessed;
            ItemLogic.Dispose();
            ItemLogic = null;
        }

        ArchipelagoCheckCountdownController.RemoveObjective();

        // In the case the player joins a lobby that uses different settings, the previous objects may still exist and may be called again when hooks are started.
        // To prevent this, the old objects will be thrown away when cleaning up.
        StageBlockerService = null;
        LocationCheckService = null;
        ShrineChanceService = null;
        ItemPoolService = null;
        bossDefeatedOnVictoryStage = false;
    }

    private void HookGame()
    {
        On.RoR2.UI.ChatBox.SubmitChat += ChatBox_SubmitChat;
        RoR2.Run.onRunDestroyGlobal += Run_onRunDestroyGlobal;
        On.RoR2.Run.BeginGameOver += Run_BeginGameOver;
        ArchipelagoChatMessage.OnChatReceivedFromClient += ArchipelagoChatMessage_OnChatReceivedFromClient;
        ReleasePanel = AssetBundleHelper.LoadPrefab("ReleasePrompt");
        CollectPanel = AssetBundleHelper.LoadPrefab("CollectPrompt");
        On.RoR2.UI.GameEndReportPanelController.Awake += GameEndReportPanelController_Awake;
        OnReleaseClick += WillRelease;
        OnCollectClick += WillCollect;
        On.RoR2.SceneObjectToggleGroup.Awake += SceneObjectToggleGroup_Awake;
        ArchipelagoScoreboardController.Register();

        StageBlockerService?.Register();
        LocationCheckService?.Register();
        ShrineChanceService?.Register();
        ItemPoolService?.Register();
        ArchipelagoConsoleCommand.OnArchipelagoDeathLinkCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoDeathLinkCommandCalled;
        ArchipelagoConsoleCommand.OnArchipelagoFinalStageDeathCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoFinalStageDeathCommandCalled;
        On.RoR2.PortalDialerController.PortalDialerPreDialState.OnEnter += PortalDialerPreDialState_OnEnter;
        On.RoR2.BossGroup.OnDefeatedServer += BossGroup_OnDefeatedServer;
        RoR2.Stage.onStageStartGlobal += Stage_onStageStartGlobal_VictoryCheck;
    }

    private void UnhookGame()
    {
        On.RoR2.UI.ChatBox.SubmitChat -= ChatBox_SubmitChat;
        RoR2.Run.onRunDestroyGlobal -= Run_onRunDestroyGlobal;
        On.RoR2.Run.BeginGameOver -= Run_BeginGameOver;
        ArchipelagoChatMessage.OnChatReceivedFromClient -= ArchipelagoChatMessage_OnChatReceivedFromClient;
        On.RoR2.UI.GameEndReportPanelController.Awake -= GameEndReportPanelController_Awake;
        OnReleaseClick -= WillRelease;
        OnCollectClick -= WillCollect;
        On.RoR2.SceneObjectToggleGroup.Awake -= SceneObjectToggleGroup_Awake;
        ArchipelagoScoreboardController.Unregister();

        DeathLinkManager?.Unregister();
        StageBlockerService?.Unregister();
        LocationCheckService?.Unregister();
        ShrineChanceService?.Unregister();
        ItemPoolService?.Unregister();
        ArchipelagoConsoleCommand.OnArchipelagoDeathLinkCommandCalled -= ArchipelagoConsoleCommand_OnArchipelagoDeathLinkCommandCalled;
        ArchipelagoConsoleCommand.OnArchipelagoFinalStageDeathCommandCalled -= ArchipelagoConsoleCommand_OnArchipelagoFinalStageDeathCommandCalled;
        On.RoR2.PortalDialerController.PortalDialerPreDialState.OnEnter -= PortalDialerPreDialState_OnEnter;
        On.RoR2.BossGroup.OnDefeatedServer -= BossGroup_OnDefeatedServer;
        RoR2.Stage.onStageStartGlobal -= Stage_onStageStartGlobal_VictoryCheck;
    }

    private void PortalDialerPreDialState_OnEnter(On.RoR2.PortalDialerController.PortalDialerPreDialState.orig_OnEnter orig, PortalDialerController.PortalDialerPreDialState self)
    {
        ChatMessage.Send($"<style=cIsUtility>[AP]</style> Victory condition is <style=cIsUtility>{ArchipelagoClient.victoryCondition}</style>.");
        orig(self);
    }

    private void ItemLogicHandler_ItemDropProcessed(int pickedUpCount)
    {
        int step = ItemLogic.ItemPickupStep;
        int current = pickedUpCount % step;
        ArchipelagoCheckCountdownController.UpdateItemCountdown(current, step);
        new SyncLocationCheckProgress(current, step).Send(NetworkDestination.Clients);
    }

    private void ChatBox_SubmitChat(On.RoR2.UI.ChatBox.orig_SubmitChat orig, ChatBox self)
    {
        var text = self.inputField.text;
        if (IsConnected && !string.IsNullOrEmpty(text))
        {
            var sayPacket = new SayPacket();
            sayPacket.Text = text;
            session.Socket.SendPacketAsync(sayPacket);

            self.inputField.text = string.Empty;
            orig(self);
        }
        else
        {
            orig(self);
        }
    }

    private void ArchipelagoChatMessage_OnChatReceivedFromClient(string message)
    {
        if (IsConnected && !string.IsNullOrEmpty(message))
        {
            var sayPacket = new SayPacket();
            sayPacket.Text = message;
            session.Socket.SendPacketAsync(sayPacket);
        }
    }

    private void ArchipelagoConsoleCommand_OnArchipelagoDeathLinkCommandCalled(bool link)
    {
        if (link)
        {
            DeathLinkManager?.Register();
            deathLinkService.EnableDeathLink();
        }
        else
        {
            DeathLinkManager?.Unregister();
            deathLinkService.DisableDeathLink();
        }
    }

    private void ArchipelagoConsoleCommand_OnArchipelagoFinalStageDeathCommandCalled(bool finalstage)
    {
        finalStageDeath = finalstage;
    }

    private void SceneObjectToggleGroup_Awake(On.RoR2.SceneObjectToggleGroup.orig_Awake orig, SceneObjectToggleGroup self)
    {
        Log.LogDebug($"Scene group length {self.toggleGroups.Length}");
        if (self.toggleGroups != null)
        {
            for (var i = 0; i < self.toggleGroups.Length; i++)
            {
                if (self.toggleGroups[i].objects != null && self.toggleGroups[i].objects[0] != null)
                {
                    if (self.toggleGroups[i].objects[0].name == "NewtStatue" || self.toggleGroups[i].objects[0].name == "NewtStatue (1)")
                    {
                        Log.LogDebug($"Scene Object Toggle Group min:{self.toggleGroups[i].minEnabled} max:{self.toggleGroups[i].maxEnabled}");
                        Log.LogDebug("Changing newt alters min and max values");
                        self.toggleGroups[i].minEnabled = 1;
                        self.toggleGroups[i].maxEnabled = 2;
                        Log.LogDebug($"Scene Object Toggle Group  min:{self.toggleGroups[i].minEnabled} max:{self.toggleGroups[i].maxEnabled}");
                        break;
                    }
                }
            }
        }
        orig(self);
    }

    private void Run_BeginGameOver(On.RoR2.Run.orig_BeginGameOver orig, Run self, GameEndingDef gameEndingDef)
    {
        // If ending is acceptable, finish the archipelago run.
        if (IsEndingAcceptable(gameEndingDef))
        {
            isEndingAcceptable = true;
            SendVictory();
        }
        orig(self, gameEndingDef);
    }

    private void SendVictory()
    {
        var packet = new StatusUpdatePacket();
        packet.Status = ArchipelagoClientState.ClientGoal;
        session.Socket.SendPacketAsync(packet);

        // Mark all checks complete in the UI so the scoreboard/objective panel reflects victory
        ArchipelagoTotalChecksObjectiveController.CurrentChecks = ArchipelagoTotalChecksObjectiveController.TotalChecks;
        new SyncTotalCheckProgress(
            ArchipelagoTotalChecksObjectiveController.CurrentChecks,
            ArchipelagoTotalChecksObjectiveController.TotalChecks
        ).Send(NetworkDestination.Clients);

        new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
    }

    private bool IsEndingAcceptable(GameEndingDef gameEndingDef)
    {
        Log.LogDebug($"ending stage is {Stage.instance.sceneDef.cachedName}");
        return acceptableEndings.Contains(gameEndingDef) ||
            (finalStageDeath && gameEndingDef == RoR2Content.GameEndings.StandardLoss && acceptableLosses.Contains(Stage.instance.sceneDef.cachedName)) ||
            (finalStageDeath && gameEndingDef == RoR2Content.GameEndings.ObliterationEnding && acceptableLosses.Contains(Stage.instance.sceneDef.cachedName));
    }

    // Boss-kill victory detection for encounters that don't trigger a standard GameEndingDef.
    // Step 1: Flag when boss group is defeated on a victory-eligible stage.
    private void BossGroup_OnDefeatedServer(On.RoR2.BossGroup.orig_OnDefeatedServer orig, BossGroup self)
    {
        orig(self);
        if (Stage.instance == null) return;
        var sceneName = Stage.instance.sceneDef.cachedName;
        if (IsVictoryStageForBossKill(sceneName))
        {
            bossDefeatedOnVictoryStage = true;
            Log.LogDebug($"Boss defeated on victory stage: {sceneName}");
            ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cIsHealing>Boss defeated! Complete the stage to claim victory.</style>");
        }
    }

    // Step 2: When the next stage loads, check if we left a victory stage after a boss kill.
    private void Stage_onStageStartGlobal_VictoryCheck(Stage stage)
    {
        if (!bossDefeatedOnVictoryStage || isEndingAcceptable) return;
        if (session == null || !session.Socket.Connected) return;
        bossDefeatedOnVictoryStage = false;

        Log.LogInfo($"Victory achieved via boss kill on victory stage (now on {stage.sceneDef.cachedName}).");
        isEndingAcceptable = true;
        SendVictory();
    }

    private void SetAnyVictoryCondition()
    {
        victoryCondition = VictoryAny;
        acceptableEndings = new[] {
            RoR2Content.GameEndings.MainEnding,
            RoR2Content.GameEndings.LimboEnding,
            DLC1Content.GameEndings.VoidEnding,
            DLC2Content.GameEndings.RebirthEndingDef,
            // Solus Heart has no GameEndingDef — handled via boss-kill hook
        };
        acceptableLosses = new[] {
            "moon",
            "moon2",
            "voidraid",
            "mysteryspace",
            "limbo",
            "meridian",
            "solusweb",
        };
    }

    private bool IsVictoryStageForBossKill(string sceneName)
    {
        // False Son on Prime Meridian
        if ((victoryCondition == VictoryRebirth || victoryCondition == VictoryAny) && sceneName == "meridian")
            return true;
        // Solus Heart on Neural Sanctum
        if ((victoryCondition == VictorySolusHeart || victoryCondition == VictoryAny) && sceneName == "solusweb")
            return true;
        return false;
    }

    // Session-level: automatically set up a new run when the session persists across runs.
    private void Run_onRunStartGlobal(Run obj)
    {
        if (IsConnected && ItemLogic == null)
        {
            Log.LogDebug("Session alive, auto-setting up new run.");
            SetupRun();
        }
        else if (!IsConnected && !string.IsNullOrEmpty(lastServerUrl))
        {
            // Socket disconnected during menu transition — reconnect using cached credentials
            Log.LogDebug("Session lost during menu transition — auto-reconnecting.");
            try
            {
                Connect(lastServerUrl, lastSlotName, lastPassword);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Auto-reconnect on run start failed: {ex.Message}");
            }
        }

        // Run.Start() picks the starting stage BEFORE onRunStartGlobal fires,
        // so our CanPickStage hook wasn't in place. Re-validate the picked stage.
        if (StageBlockerService != null && obj.nextStageScene != null)
        {
            if (StageBlockerService.CheckBlocked(obj.nextStageScene.cachedName))
            {
                Log.LogWarning($"Starting stage {obj.nextStageScene.cachedName} is blocked — re-picking.");
                obj.PickNextStageSceneFromCurrentSceneDestinations();
            }
        }
    }

    // When exiting to menu/game this will run — only cleans up the run, session stays alive
    private void Run_onRunDestroyGlobal(Run obj)
    {
        isInGame = false;
        CleanupRun();
    }

    private void GameEndReportPanelController_Awake(On.RoR2.UI.GameEndReportPanelController.orig_Awake orig, GameEndReportPanelController self)
    {
        if (isEndingAcceptable && ReleasePromptPanel == null)
        {
            GameObject menuOutline;
            if (genericMenuButton != null)
            {
                menuOutline = genericMenuButton.transform.Find("HoverOutline").gameObject;
            }
            else
            {
                menuOutline = null;
            }

            var releasePermission = Convert.ToString(session.RoomState.ReleasePermissions);
            var collectPermission = Convert.ToString(session.RoomState.CollectPermissions);
            bool canRelease = (releasePermission == "Goal" || releasePermission == "Enabled");
            bool canCollect = (collectPermission == "Goal" || collectPermission == "Enabled");
            Log.LogDebug($"can release {releasePermission} can collect {collectPermission}");
            Log.LogDebug($"release? {canRelease} collect? {canCollect}");
            var gameEndReportPanel = self.transform.Find("SafeArea (JUICED)/BodyArea");
            if (canRelease)
            {
                var rp = GameObject.Instantiate(ReleasePanel);
                rp.transform.SetParent(gameEndReportPanel.transform, false);
                rp.transform.localPosition = new Vector3(0, 0, 0);
                rp.transform.localScale = Vector3.one;
                var release = self.transform.Find("SafeArea (JUICED)/BodyArea/ReleasePrompt(Clone)/Panel/Release/").gameObject;
                release.AddComponent<HGButton>();
                var releaseCancel = self.transform.Find("SafeArea (JUICED)/BodyArea/ReleasePrompt(Clone)/Panel/Cancel/").gameObject;
                releaseCancel.AddComponent<HGButton>();
                release.GetComponent<HGButton>().onClick.AddListener(() => { OnReleaseClick(true); });
                releaseCancel.GetComponent<HGButton>().onClick.AddListener(() => { OnReleaseClick(false); });
                ReleasePromptPanel = self.transform.Find("SafeArea (JUICED)/BodyArea/ReleasePrompt(Clone)").gameObject;
            }
            if (canCollect)
            {
                var cp = GameObject.Instantiate(CollectPanel);
                cp.transform.SetParent(gameEndReportPanel.transform, false);
                cp.transform.localPosition = new Vector3(0, 0, 0);
                cp.transform.localScale = Vector3.one;
                var collect = self.transform.Find("SafeArea (JUICED)/BodyArea/CollectPrompt(Clone)/Panel/Collect/").gameObject;
                collect.AddComponent<HGButton>();
                var collectCancel = self.transform.Find("SafeArea (JUICED)/BodyArea/CollectPrompt(Clone)/Panel/Cancel/").gameObject;
                collectCancel.AddComponent<HGButton>();
                collect.GetComponent<HGButton>().onClick.AddListener(() => { OnCollectClick(true); });
                collectCancel.GetComponent<HGButton>().onClick.AddListener(() => { OnCollectClick(false); });
                CollectPromptPanel = self.transform.Find("SafeArea (JUICED)/BodyArea/CollectPrompt(Clone)").gameObject;
                CollectPromptPanel.SetActive(false);
            }
            if (canCollect && !canRelease)
            {
                CollectPromptPanel.SetActive(true);
            }
        }
        orig(self);
    }

    private void WillRelease(bool prompt)
    {
        var sayPacket = new SayPacket();
        if (prompt && isEndingAcceptable)
        {
            Log.LogDebug($"Releasing the rest of the items {isEndingAcceptable}");
            sayPacket.Text = "!release";
            session.Socket.SendPacketAsync(sayPacket);
        }
        ReleasePromptPanel.SetActive(false);
        if (CollectPromptPanel != null)
        {
            CollectPromptPanel.SetActive(true);
        }
    }

    private void WillCollect(bool prompt)
    {
        var sayPacket = new SayPacket();
        if (prompt && isEndingAcceptable)
        {
            Log.LogDebug($"Collect the rest of the items {isEndingAcceptable}");
            sayPacket.Text = "!collect";
            session.Socket.SendPacketAsync(sayPacket);
        }
        CollectPromptPanel?.SetActive(false);
    }
}
