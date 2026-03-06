using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.RiskOfRain2.Console;
using Archipelago.RiskOfRain2.Handlers;
using Archipelago.RiskOfRain2.Net;
using Archipelago.RiskOfRain2.UI;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace Archipelago.RiskOfRain2
{
    public class ArchipelagoClient : IDisposable
    {
        public delegate void ClientDisconnected(string reason);
        public event ClientDisconnected OnClientDisconnect;

        public string LastServerUrl { get; set; }
        public string LastSlotName { get; set; }
        public string LastPassword { get; set; }
        public bool IsConnected => session != null && session.Socket.Connected;

        internal DeathLinkHandler DeathLink { get; private set; }
        internal StageBlockerHandler StageBlocker { get; private set; }
        internal LocationHandler LocationHandler { get; private set; }
        internal ShrineChanceHandler ShrineChance { get; private set; }

        public ArchipelagoItemLogicController ItemLogic;
        public ArchipelagoLocationCheckProgressBarUI ItemCheckBar;
        public ArchipelagoLocationCheckProgressBarUI ShrineCheckBar;

        private ArchipelagoSession session;
        private DeathLinkService deathLinkService;
        private bool finalStageDeath = false;
        private bool isEndingAcceptable = false;
        public GameObject ReleasePanel;
        public GameObject CollectPanel;
        public GameObject ReleasePromptPanel;
        public GameObject CollectPromptPanel;
        public delegate void ReleaseClick(bool prompt);
        public static ReleaseClick OnReleaseClick;
        public delegate void CollectClick(bool prompt);
        public static CollectClick OnCollectClick;

        public bool Reconnecting { get; set; } = false;
        public static int LastReceivedItemIndex { get; set; } = 0;
        public static bool IsInGame { get; set; } = false;
        public static string ConnectedPlayerName;
        public static string VictoryCondition;
        // Acceptable ending types
        private GameEndingDef[] acceptableEndings;
        // Acceptable stages to die on
        private string[] acceptableLosses;

        // Cached slot data for session reuse across runs
        private bool cachedGoalIsExplore;
        private bool cachedDeathLinkEnabled;
        private uint cachedItemPickupStep = 3;
        private uint cachedShrineUseStep = 3;
        private Dictionary<string, object> cachedSlotData;

        // Pending location checks that failed to send during disconnect.
        // Held at the client level so they survive LocationHandler recreation.
        private readonly List<long> cachedPendingChecks = new List<long>();

        // Cached ItemLogic state for restoring across runs
        private bool hasCachedRunState;
        private int cachedItemLogicPickupStep;
        private int cachedItemLogicTotalChecks;
        private int cachedItemLogicCurrentChecks;
        private int cachedItemLogicPickedUpItemCount;

        public ArchipelagoClient()
        {
            ArchipelagoConsoleCommand.OnArchipelagoReconnectCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoReconnectCommandCalled;
        }

        public void Connect(string url, string slotName, string password = null)
        {
            // Cache credentials for reconnection
            LastServerUrl = url;
            LastSlotName = slotName;
            LastPassword = password;

            // Session reuse: if already connected, just set up a new run
            if (IsConnected)
            {
                ChatMessage.SendColored("Reusing existing Archipelago session.", Color.green);
                CleanupRun();
                SetupRun();
                return;
            }

            // Stale session: clean up before creating a new one
            TeardownSession();

            ChatMessage.SendColored($"Attempting to connect to Archipelago at {url}.", Color.green);

            try
            {
                session = ArchipelagoSessionFactory.CreateSession(url);
            }
            catch (Exception e)
            {
                OnClientDisconnect?.Invoke(e.Message);
                return;
            }

            // On fresh connect (not Reconnecting), reset item index and cached run state
            // so we don't replay already-received items or restore stale progress.
            // During reconnection, preserve both so AttemptReconnection can restore
            // the player's progress after the session is re-established.
            if (!Reconnecting)
            {
                LastReceivedItemIndex = 0;
                hasCachedRunState = false;
            }

            var result = session.TryConnectAndLogin("Risk of Rain 2", slotName, ItemsHandlingFlags.AllItems, new Version(0, 6, 4), password: password);

            if (!result.Successful)
            {
                LoginFailure failureResult = (LoginFailure)result;
                foreach (var err in failureResult.Errors)
                {
                    ChatMessage.SendColored(err, Color.red);
                    Log.LogError(err);
                }
                session = null;
                return;
            }

            LoginSuccessful successResult = (LoginSuccessful)result;
            ArchipelagoConnectButtonController.ChangeButtonWhenConnected();
            ChatMessage.SendColored("Connected!", Color.green);

            // Parse and cache slot data (session-level, survives across runs)
            if (successResult.SlotData.TryGetValue("finalStageDeath", out var stageDeathObject))
            {
                finalStageDeath = Convert.ToBoolean(stageDeathObject);
            }
            // to keep this setting working in previous versions of AP
            // TODO remove at ap version 3.9
            else if (successResult.SlotData.TryGetValue("FinalStageDeath", out var oldStageDeathObject))
            {
                finalStageDeath = Convert.ToBoolean(oldStageDeathObject);
            }
            Log.LogDebug($"finalStageDeath {finalStageDeath} ");

            cachedItemPickupStep = 3;
            cachedShrineUseStep = 3;
            if (successResult.SlotData.TryGetValue("itemPickupStep", out var oitemPickupStep))
            {
                cachedItemPickupStep = Convert.ToUInt32(oitemPickupStep);
                Log.LogDebug($"itemPickupStep from slot data: {cachedItemPickupStep}");
                cachedItemPickupStep++; // Add 1 because the user's YAML will contain a value equal to "number of pickups before sent location"
            }

            if (successResult.SlotData.TryGetValue("shrineUseStep", out var oshrineUseStep))
            {
                cachedShrineUseStep = Convert.ToUInt32(oshrineUseStep);
                Log.LogDebug($"shrineUseStep from slot data: {cachedShrineUseStep}");
                cachedShrineUseStep++; // Add 1 because the user's YAML will contain a value equal to "number of pickups before sent location"
            }

            // DeathLink (session-level)
            deathLinkService = DeathLinkProvider.CreateDeathLinkService(session);
            Log.LogDebug("Starting DeathLink service");
            DeathLink = new DeathLinkHandler(deathLinkService);
            cachedDeathLinkEnabled = false;
            if (successResult.SlotData.TryGetValue("deathLink", out var enabledeathlink))
            {
                cachedDeathLinkEnabled = Convert.ToBoolean(enabledeathlink);
                if (cachedDeathLinkEnabled)
                {
                    deathLinkService.EnableDeathLink(); // deathlink should just be enabled, the DeathLinkHandler assumes it is already enabled
                }
            }

            // Cache goal mode and slot data for run reuse
            cachedGoalIsExplore = false;
            if (successResult.SlotData.TryGetValue("goal", out var classicmode))
            {
                cachedGoalIsExplore = Convert.ToBoolean(classicmode);
            }
            cachedSlotData = new Dictionary<string, object>(successResult.SlotData);

            // Victory conditions (session-level)
            if (successResult.SlotData.TryGetValue("victory", out var victory))
            {
                switch (victory.ToString())
                {
                    // Mithrix
                    case "1":
                        acceptableEndings = new[] { RoR2Content.GameEndings.MainEnding };
                        acceptableLosses = new[] { "moon", "moon2" };
                        VictoryCondition = "Mithrix";
                        break;
                    // Voidling
                    case "2":
                        acceptableEndings = new[] { DLC1Content.GameEndings.VoidEnding };
                        acceptableLosses = new[] { "voidraid" };
                        VictoryCondition = "Voidling";
                        break;
                    // Limbo
                    case "3":
                        acceptableEndings = new[] { RoR2Content.GameEndings.LimboEnding };
                        acceptableLosses = new[] { "mysteryspace", "limbo" };
                        VictoryCondition = "Limbo";
                        break;
                    case "4":
                        acceptableEndings = new[] { DLC2Content.GameEndings.RebirthEndingDef };
                        VictoryCondition = "Rebirth";
                        break;
                    default:
                        VictoryCondition = "any";
                        acceptableEndings = new[] {
                            RoR2Content.GameEndings.MainEnding,
                            RoR2Content.GameEndings.LimboEnding,
                            DLC1Content.GameEndings.VoidEnding,
                            DLC2Content.GameEndings.RebirthEndingDef
                        };
                        acceptableLosses = new[] {
                            "moon",
                            "moon2",
                            "voidraid",
                            "mysteryspace",
                            "limbo",
                            "meridian"
                        };
                        break;
                }
            }
            else
            {
                VictoryCondition = "any";
                acceptableEndings = new[] {
                    RoR2Content.GameEndings.MainEnding,
                    RoR2Content.GameEndings.LimboEnding,
                    DLC1Content.GameEndings.VoidEnding,
                    DLC2Content.GameEndings.RebirthEndingDef
                };
                acceptableLosses = new[] {
                    "moon",
                    "moon2",
                    "voidraid",
                    "mysteryspace",
                    "limbo",
                    "meridian"
                };
            }

            // Progressive stages and seer portals (session-level, static fields)
            if (successResult.SlotData.TryGetValue("progressiveStages", out var progressive))
            {
                StageBlockerHandler.ProgressiveStages = Convert.ToBoolean(progressive);
            }
            
            if (successResult.SlotData.TryGetValue("showSeerPortals", out var showSeerPortals))
            {
                StageBlockerHandler.ShowSeerPortals = Convert.ToBoolean(showSeerPortals);
            }

            ConnectedPlayerName = session.Players.GetPlayerName(session.ConnectionInfo.Slot);
            // Subscribe session-level events
            SubscribeSessionEvents();

            // Stage unlock initialization (one-time, session-level)
            // Needed for backwards compatability
            if (session.Items.GetItemName(37501) == null)
            {
                StageBlockerHandler.StageUnlocks["Stage 1"] = true;
                StageBlockerHandler.StageUnlocks["Stage 2"] = true;
                StageBlockerHandler.StageUnlocks["Stage 3"] = true;
                StageBlockerHandler.StageUnlocks["Stage 4"] = true;
            }
            else if (!IsInGame)
            {
                StageBlockerHandler.StageUnlocks["Stage 1"] = false;
                StageBlockerHandler.StageUnlocks["Stage 2"] = false;
                StageBlockerHandler.StageUnlocks["Stage 3"] = false;
                StageBlockerHandler.StageUnlocks["Stage 4"] = false;
            }

            // Set up the first run
            SetupRun();
        }

        /// <summary>
        /// Creates per-run state: ItemLogic, handlers, UI bars, game hooks.
        /// Called on first connect and on session reuse for subsequent runs.
        /// </summary>
        public void SetupRun(bool midRunReconnect = false)
        {
            isEndingAcceptable = false;

            ItemLogic = new ArchipelagoItemLogicController(session);
            ItemCheckBar = null;
            ShrineCheckBar = null;

            if (cachedGoalIsExplore)
            {
                Log.LogDebug("Setting up explore mode for run");
                StageBlocker = new StageBlockerHandler();
                ItemLogic.StageBlocker = StageBlocker;
                StageBlocker.BlockAll();
                LocationHandler = new LocationHandler(session, LocationHandler.BuildTemplateFromSlotData(cachedSlotData));
                // Restore any pending checks from a prior handler (saved in CleanupRun)
                if (cachedPendingChecks.Count > 0)
                {
                    LocationHandler.AddPendingChecks(cachedPendingChecks);
                    cachedPendingChecks.Clear();
                }
                ShrineChance = new ShrineChanceHandler();

                ItemCheckBar = new ArchipelagoLocationCheckProgressBarUI(new Vector2(-40, 0), Vector2.zero, "Item Check Progress:");
                ShrineCheckBar = new ArchipelagoLocationCheckProgressBarUI(new Vector2(0, 170), new Vector2(50, -50), "Shrine Check Progress:");

                ShrineCheckBar.ItemPickupStep = (int)cachedShrineUseStep;

                LocationHandler.ItemBar = ItemCheckBar;
                LocationHandler.ShrineBar = ShrineCheckBar;
                LocationHandler.ItemPickupStep = cachedItemPickupStep;
                LocationHandler.ShrineUseStep = cachedShrineUseStep;
            }
            else
            {
                Log.LogDebug("Setting up classic mode for run");
                ArchipelagoLocationsInEnvironmentController.RemoveObjective();
                new AllChecksCompleteInStage().Send(NetworkDestination.Clients);
            }

            // Make the bar if it has not been created because classic mode or the slot data was missing
            if (null == ItemCheckBar)
            {
                Log.LogDebug("Setting up bar for classic");
                ItemCheckBar = new ArchipelagoLocationCheckProgressBarUI(Vector2.zero, Vector2.zero);
                SyncLocationCheckProgress.OnLocationSynced += ItemCheckBar.UpdateCheckProgress; // the item bar updates from the netcode in classic mode
            }

            ItemCheckBar.ItemPickupStep = (int)cachedItemPickupStep;

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
                DeathLink?.Hook();
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

            // Enqueue received items. On mid-run reconnect, only process items
            // received since the last known index to avoid duplicating items
            // the player already has. On fresh connect/new run, process all.
            if (midRunReconnect)
            {
                ItemLogic.ProcessItemsSinceIndex(LastReceivedItemIndex);
            }
            else
            {
                ItemLogic.ProcessAllReceivedItems();
            }
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

            if (ItemCheckBar != null)
            {
                SyncLocationCheckProgress.OnLocationSynced -= ItemCheckBar.UpdateCheckProgress;
                ItemCheckBar.Dispose();
                ItemCheckBar = null;
            }

            if (ShrineCheckBar != null)
            {
                ShrineCheckBar.Dispose();
                ShrineCheckBar = null;
            }

            // Save any pending checks before destroying the handler
            if (LocationHandler != null)
            {
                cachedPendingChecks.AddRange(LocationHandler.GetPendingChecks());
            }

            // In the case the player joins a lobby that uses different settings, the previous objects may still exist and may be called again when hooks are started.
            // To prevent this, the old objects will be thrown away when cleaning up.
            StageBlocker = null;
            LocationHandler = null;
            ShrineChance = null;
        }

        /// <summary>
        /// Subscribes session-level events on the current session.
        /// </summary>
        private void SubscribeSessionEvents()
        {
            session.MessageLog.OnMessageReceived += Session_OnMessageReceived;
            session.Socket.SocketClosed += Session_SocketClosed;
            session.Socket.ErrorReceived += Socket_ErrorReceived;
            Run.onRunStartGlobal += Run_onRunStartGlobal;
        }

        /// <summary>
        /// Unsubscribes session-level events from the current session.
        /// </summary>
        private void UnsubscribeSessionEvents()
        {
            if (session == null) return;
            session.MessageLog.OnMessageReceived -= Session_OnMessageReceived;
            session.Socket.SocketClosed -= Session_SocketClosed;
            session.Socket.ErrorReceived -= Socket_ErrorReceived;
            Run.onRunStartGlobal -= Run_onRunStartGlobal;
        }

        /// <summary>
        /// Unsubscribes session-level events and nulls the session.
        /// Optionally disconnects the socket if still connected.
        /// </summary>
        private void TeardownSession(bool disconnect = false)
        {
            if (session == null) return;

            UnsubscribeSessionEvents();

            if (disconnect && session.Socket.Connected)
            {
                session.Socket.DisconnectAsync();
            }

            session = null;
            DeathLink = null;
            deathLinkService = null;
            // NOTE: hasCachedRunState is intentionally NOT cleared here.
            // On dirty disconnect → reconnect, CleanupRun caches state before
            // TeardownSession runs. Clearing here would lose that cached state.
            // It is cleared on fresh connect (when !Reconnecting) in Connect().
        }

        /// <summary>
        /// Full teardown: per-run state + session-level state.
        /// Only called on intentional disconnect or unrecoverable error.
        /// </summary>
        public void Dispose()
        {
            CleanupRun();
            TeardownSession(disconnect: true);
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

            StageBlocker?.Hook();
            LocationHandler?.Hook();
            ShrineChance?.Hook();
            ArchipelagoConsoleCommand.OnArchipelagoDeathLinkCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoDeathLinkCommandCalled;
            ArchipelagoConsoleCommand.OnArchipelagoFinalStageDeathCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoFinalStageDeathCommandCalled;
            On.RoR2.PortalDialerController.PortalDialerPreDialState.OnEnter += PortalDialerPreDialState_OnEnter;
        }

        private void PortalDialerPreDialState_OnEnter(On.RoR2.PortalDialerController.PortalDialerPreDialState.orig_OnEnter orig, PortalDialerController.PortalDialerPreDialState self)
        {
            ChatMessage.SendColored($"Victory condition is {ArchipelagoClient.VictoryCondition}.", Color.magenta);
            orig(self);
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

            DeathLink?.UnHook();
            StageBlocker?.UnHook();
            LocationHandler?.UnHook();
            ShrineChance?.UnHook();
            ArchipelagoConsoleCommand.OnArchipelagoDeathLinkCommandCalled -= ArchipelagoConsoleCommand_OnArchipelagoDeathLinkCommandCalled;
            ArchipelagoConsoleCommand.OnArchipelagoFinalStageDeathCommandCalled -= ArchipelagoConsoleCommand_OnArchipelagoFinalStageDeathCommandCalled;
            On.RoR2.PortalDialerController.PortalDialerPreDialState.OnEnter -= PortalDialerPreDialState_OnEnter;
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

        private void ArchipelagoConsoleCommand_OnArchipelagoDeathLinkCommandCalled(bool link)
        {
            if (link)
            {
                DeathLink?.Hook();
                deathLinkService.EnableDeathLink();
            }
            else
            {
                DeathLink?.UnHook();
                deathLinkService.DisableDeathLink();
            }
        }

        private void ArchipelagoConsoleCommand_OnArchipelagoFinalStageDeathCommandCalled(bool finalstage)
        {
            finalStageDeath = finalstage;
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

        private void ArchipelagoConsoleCommand_OnArchipelagoReconnectCommandCalled()
        {
            Reconnecting = true;
            Dispose();
            new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
            OnClientDisconnect?.Invoke("Manual reconnect requested.");
        }

        private void ItemLogicHandler_ItemDropProcessed(int pickedUpCount)
        {
            if (ItemCheckBar != null)
            {
                ItemCheckBar.CurrentItemCount = pickedUpCount;
                if ((ItemCheckBar.CurrentItemCount % ItemLogic.ItemPickupStep) == 0)
                {
                    ItemCheckBar.CurrentItemCount = 0;
                }
                else
                {
                    ItemCheckBar.CurrentItemCount = ItemCheckBar.CurrentItemCount % ItemLogic.ItemPickupStep;
                }
            }
            new SyncLocationCheckProgress(ItemCheckBar.CurrentItemCount, ItemCheckBar.ItemPickupStep).Send(NetworkDestination.Clients);
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

        private void Socket_ErrorReceived(Exception e, string message)
        {
            Log.LogDebug($"Error received: {e}, message: {message}");
            Reconnecting = true;
            Session_SocketClosed(message);
        }

        private void Session_SocketClosed(string reason)
        {
            CleanupRun();
            TeardownSession();

            new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
            OnClientDisconnect?.Invoke(reason);
        }

        public System.Collections.IEnumerator AttemptReconnection()
        {
            Log.LogDebug("Attempting to reconnect!");
            if (!IsInGame)
            {
                ArchipelagoConnectButtonController.ChangeButtonWhenDisconnected();
            }

            int attempt = 0;
            float delay = 5f;
            const float maxDelay = 30f;
            while (true)
            {
                attempt++;
                ChatMessage.Send($"Reconnection attempt #{attempt} (next retry in {delay}s)");
                yield return new WaitForSeconds(delay);
                delay = Mathf.Min(delay + 5f, maxDelay);

                // Only run the blocking network I/O on a background thread.
                // Connect() touches Unity APIs and shared state, so we can't
                // call it wholesale off the main thread.
                var url = LastServerUrl;
                var slot = LastSlotName;
                var pass = LastPassword;
                ArchipelagoSession bgSession = null;
                LoginResult bgResult = null;
                Exception bgError = null;

                var connectTask = Task.Run(() =>
                {
                    try
                    {
                        bgSession = ArchipelagoSessionFactory.CreateSession(url);
                        bgResult = bgSession.TryConnectAndLogin(
                            "Risk of Rain 2", slot, ItemsHandlingFlags.AllItems,
                            new Version(0, 6, 4), password: pass);
                    }
                    catch (Exception ex)
                    {
                        bgError = ex;
                    }
                });

                // Poll until the background task completes (0.5s for responsiveness)
                float elapsed = 0f;
                const float timeout = 30f;
                while (!connectTask.IsCompleted && elapsed < timeout)
                {
                    yield return new WaitForSeconds(0.5f);
                    elapsed += 0.5f;
                }

                // Handle timeout — abandon this attempt
                if (!connectTask.IsCompleted)
                {
                    Log.LogWarning($"Reconnection attempt {attempt} timed out after {timeout}s");
                    continue;
                }

                if (bgError != null)
                {
                    Log.LogWarning($"Reconnection attempt {attempt} failed: {bgError.Message}");
                    continue;
                }

                if (bgResult == null || !bgResult.Successful)
                {
                    if (bgResult is LoginFailure failure)
                    {
                        foreach (var err in failure.Errors)
                            Log.LogWarning($"Reconnection attempt {attempt}: {err}");
                    }
                    continue;
                }

                // Network succeeded — set up on the main thread.
                // Don't call Connect() (which would call SetupRun with ProcessAllReceivedItems
                // and duplicate every item the player already has). Instead, subscribe events
                // on the new session and call SetupRun with midRunReconnect=true.
                session = bgSession;
                try
                {
                    SubscribeSessionEvents();
                    bool isMidRun = IsInGame && Run.instance != null;
                    SetupRun(midRunReconnect: isMidRun);
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Reconnection attempt {attempt} setup failed: {ex.Message}");
                    continue;
                }

                if (IsConnected)
                {
                    ChatMessage.SendColored("Reconnected to Archipelago.", Color.green);
                    if (LocationHandler != null && IsInGame && Run.instance != null)
                    {
                        LocationHandler.FlushPendingChecks();
                        LocationHandler.CatchUpSceneLocations(LocationHandler.CurrentSceneDef.cachedName);
                        LocationHandler.LoadItemPickupHooks();
                    }
                    Reconnecting = false;
                    yield break;
                }
            }

        }

        private void Session_OnMessageReceived(LogMessage message)
        {
            Thread thread = new Thread(() => Session_OnMessageReceived_Thread(message));
            thread.Start();
            Thread.Sleep(20);
        }

        private void Session_OnMessageReceived_Thread(LogMessage message)
        {
            string text = "";
            foreach (var part in message.Parts)
            {
                var hex = part.Color.R.ToString("X2") + part.Color.G.ToString("X2") + part.Color.B.ToString("X2");
                text += $"<color=#{hex}>" + part + "</color>";
            }

            ChatMessage.Send(text);
        }

        private void Run_BeginGameOver(On.RoR2.Run.orig_BeginGameOver orig, Run self, GameEndingDef gameEndingDef)
        {
            // If ending is acceptable, finish the archipelago run.
            if (IsEndingAcceptable(gameEndingDef))
            {
                isEndingAcceptable = true;

                var packet = new StatusUpdatePacket();
                packet.Status = ArchipelagoClientState.ClientGoal;
                session.Socket.SendPacketAsync(packet);

                new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
            }
            orig(self, gameEndingDef);
        }

        private bool IsEndingAcceptable(GameEndingDef gameEndingDef)
        {
            Log.LogDebug($"ending stage is {Stage.instance.sceneDef.cachedName}");
            return acceptableEndings.Contains(gameEndingDef) ||
                (finalStageDeath && gameEndingDef == RoR2Content.GameEndings.StandardLoss) && (acceptableLosses.Contains(Stage.instance.sceneDef.cachedName)) ||
                (finalStageDeath && gameEndingDef == RoR2Content.GameEndings.ObliterationEnding) && (acceptableLosses.Contains(Stage.instance.sceneDef.cachedName));
        }

        // Session-level: automatically set up a new run when the session persists across runs.
        // After a run ends, CleanupRun() tears down per-run state but the AP session stays
        // alive. When the player starts a new run, this fires and re-creates per-run state.
        private void Run_onRunStartGlobal(Run obj)
        {
            if (IsConnected && ItemLogic == null)
            {
                Log.LogDebug("Session alive, auto-setting up new run.");
                SetupRun();
            }
        }

        // When exiting to menu/game this will run — only cleans up the run, session stays alive
        private void Run_onRunDestroyGlobal(Run obj)
        {
            IsInGame = false;
            CleanupRun();
        }

        /// <summary>
        /// Intentional disconnect initiated by the user (e.g. console command).
        /// Performs synchronous full cleanup so callers don't need to wait for
        /// the async SocketClosed callback.
        /// </summary>
        public void Disconnect()
        {
            if (session == null) return;
            ArchipelagoConnectButtonController.ChangeButtonWhenDisconnected();
            Dispose();
            new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
            OnClientDisconnect?.Invoke("Disconnected.");
        }

        private void GameEndReportPanelController_Awake(On.RoR2.UI.GameEndReportPanelController.orig_Awake orig, GameEndReportPanelController self)
        {
            if (isEndingAcceptable && ReleasePromptPanel == null)
            {
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
}
