using System;
using System.Collections.Generic;
using System.Threading;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.RiskOfRain2.Console;
using Archipelago.RiskOfRain2.Services;
using Archipelago.RiskOfRain2.Net;
using Archipelago.RiskOfRain2.UI;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Archipelago.RiskOfRain2;

/// <summary>
/// Connection, slot data parsing, reconnection, and session-level event handlers.
/// </summary>
public partial class ArchipelagoClient
{
    public void Connect(string url, string slotName, string password = null)
    {
        // Cache credentials for reconnection
        lastServerUrl = url;
        lastSlotName = slotName;
        lastPassword = password;

        // Session reuse: if already connected, just set up a new run
        if (IsConnected)
        {
            ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cIsHealing>Reusing existing Archipelago session.</style>");
            CleanupRun();
            SetupRun();
            return;
        }

        // Stale session: clean up before creating a new one
        TeardownSession();

        ChatMessage.Send($"<style=cIsUtility>[AP]</style> Attempting to connect to Archipelago at {url}.");

        try
        {
            session = ArchipelagoSessionFactory.CreateSession(url);
        }
        catch (Exception e)
        {
            OnClientDisconnect?.Invoke(e.Message);
            return;
        }

        // On fresh connect (not reconnecting), reset item index and cached run state
        // so we don't replay already-received items or restore stale progress.
        // During reconnection, preserve both so AttemptReconnection can restore
        // the player's progress after the session is re-established.
        if (!reconnecting)
        {
            lastReceivedItemindex = 0;
            hasCachedRunState = false;
        }

        var result = session.TryConnectAndLogin("Risk of Rain 2", slotName, ItemsHandlingFlags.AllItems, new Version(0, 6, 4), password: password);

        if (!result.Successful)
        {
            LoginFailure failureResult = (LoginFailure)result;
            foreach (var err in failureResult.Errors)
            {
                ChatMessage.Send($"<style=cIsUtility>[AP]</style> <style=cDeath>{err}</style>");
                Log.LogError(err);
            }
            session = null;
            return;
        }

        LoginSuccessful successResult = (LoginSuccessful)result;
        ArchipelagoConnectButtonController.ChangeButtonWhenConnected();
        ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cIsHealing>Connected!</style>");

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
        DeathLinkManager = new DeathLinkManager(deathLinkService);
        cachedDeathLinkEnabled = false;
        if (successResult.SlotData.TryGetValue("deathLink", out var enabledeathlink))
        {
            cachedDeathLinkEnabled = Convert.ToBoolean(enabledeathlink);
            if (cachedDeathLinkEnabled)
            {
                deathLinkService.EnableDeathLink(); // deathlink should just be enabled, the DeathLinkService assumes it is already enabled
            }
        }

        // Item pool limiting
        cachedItemPoolLimiting = false;
        if (successResult.SlotData.TryGetValue("itemPoolLimiting", out var itemPoolLimitingObj))
        {
            cachedItemPoolLimiting = Convert.ToBoolean(itemPoolLimitingObj);
        }
        Log.LogDebug($"itemPoolLimiting: {cachedItemPoolLimiting}");

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
                    victoryCondition = "Mithrix";
                    break;
                // Voidling
                case "2":
                    acceptableEndings = new[] { DLC1Content.GameEndings.VoidEnding };
                    acceptableLosses = new[] { "voidraid" };
                    victoryCondition = "Voidling";
                    break;
                // Limbo
                case "3":
                    acceptableEndings = new[] { RoR2Content.GameEndings.LimboEnding };
                    acceptableLosses = new[] { "mysteryspace", "limbo" };
                    victoryCondition = "Limbo";
                    break;
                // False Son (Rebirth)
                case "4":
                    acceptableEndings = new[] { DLC2Content.GameEndings.RebirthEndingDef };
                    acceptableLosses = new[] { "meridian" };
                    victoryCondition = "Rebirth";
                    break;
                // Solus Heart — defeat Solus Heart in Neural Sanctum (scene: solusweb)
                // Path: Solutional Haunt (Solus Wing) → Computational Exchange → Neural Sanctum (Solus Heart)
                // No standard GameEndingDef — detected via boss-kill + scene-transition hook.
                case "5":
                    acceptableEndings = new GameEndingDef[] { };
                    acceptableLosses = new[] { "solusweb" };
                    victoryCondition = "Solus Heart";
                    break;
                default:
                    SetAnyVictoryCondition();
                    break;
            }
        }
        else
        {
            SetAnyVictoryCondition();
        }

        // Progressive stages and seer portals (session-level, static fields)
        if (successResult.SlotData.TryGetValue("progressiveStages", out var progressive))
        {
            StageBlockerService.ProgressiveStages = Convert.ToBoolean(progressive);
        }
        if (successResult.SlotData.TryGetValue("showSeerPortals", out var showSeerPortals))
        {
            StageBlockerService.ShowSeerPortals = Convert.ToBoolean(showSeerPortals);
        }

        ConnectedPlayerName = session.Players.GetPlayerName(session.ConnectionInfo.Slot);
        genericMenuButton = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/UI/GenericMenuButton.prefab").WaitForCompletion();

        // Subscribe session-level events
        session.MessageLog.OnMessageReceived += Session_OnMessageReceived;
        session.Socket.SocketClosed += Session_SocketClosed;
        session.Socket.ErrorReceived += Socket_ErrorReceived;
        ArchipelagoConsoleCommand.OnArchipelagoReconnectCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoReconnectCommandCalled;
        Run.onRunStartGlobal += Run_onRunStartGlobal;

        // Stage unlock initialization (one-time, session-level)
        // Needed for backwards compatability
        // Note: StageBlockerService is created in SetupRun, so we cache the
        // legacy flag here and apply it after SetupRun initializes the service.
        cachedLegacyStagesUnlocked = session.Items.GetItemName(37501) == null;

        // Set up the first run
        SetupRun();
    }

    /// <summary>
    /// Unsubscribes session-level events and nulls the session.
    /// Optionally disconnects the socket if still connected.
    /// </summary>
    private void TeardownSession(bool disconnect = false)
    {
        if (session == null) return;

        session.MessageLog.OnMessageReceived -= Session_OnMessageReceived;
        session.Socket.SocketClosed -= Session_SocketClosed;
        session.Socket.ErrorReceived -= Socket_ErrorReceived;
        ArchipelagoConsoleCommand.OnArchipelagoReconnectCommandCalled -= ArchipelagoConsoleCommand_OnArchipelagoReconnectCommandCalled;
        Run.onRunStartGlobal -= Run_onRunStartGlobal;

        if (disconnect && session.Socket.Connected)
        {
            session.Socket.DisconnectAsync();
        }

        session = null;
        DeathLinkManager = null;
        deathLinkService = null;
        // NOTE: hasCachedRunState is intentionally NOT cleared here.
        // On dirty disconnect → reconnect, CleanupRun caches state before
        // TeardownSession runs. Clearing here would lose that cached state.
        // It is cleared on fresh connect (when !reconnecting) in Connect().
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

    private void Socket_ErrorReceived(Exception e, string message)
    {
        Log.LogDebug($"Error received: {e}, message: {message}");
        reconnecting = true;
        Session_SocketClosed(message);
    }

    private void Session_SocketClosed(string reason)
    {
        CleanupRun();
        TeardownSession();

        new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
        OnClientDisconnect?.Invoke(reason);
    }

    public IEnumerator<WaitForSeconds> AttemptReconnection()
    {
        Log.LogDebug("Attempting to reconnect!");
        if (!isInGame)
        {
            ArchipelagoConnectButtonController.ChangeButtonWhenDisconnected();
        }

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            ChatMessage.Send($"<style=cIsUtility>[AP]</style> Reconnection attempt #{attempt}");
            yield return new WaitForSeconds(3f);

            // Run Connect on a background thread so the UI doesn't freeze
            // during the TCP handshake / TryConnectAndLogin call.
            var connectSignal = new System.Threading.ManualResetEventSlim(false);
            new Thread(() =>
            {
                try
                {
                    Connect(lastServerUrl, lastSlotName, lastPassword);
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Reconnection attempt {attempt} failed: {ex.Message}");
                }
                connectSignal.Set();
            }).Start();

            // Yield until the background thread finishes
            while (!connectSignal.IsSet)
                yield return new WaitForSeconds(0.25f);

            if (IsConnected)
            {
                ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cIsHealing>Reconnected to Archipelago.</style>");
                // Guard with Run.instance: if the run ended while we were
                // disconnected, isInGame may be stale (true) but the run is gone.
                if (LocationCheckService != null && isInGame && Run.instance != null)
                {
                    LocationCheckService.CatchUpSceneLocations(LocationCheckService.CurrentSceneDef.cachedName);
                    LocationCheckService.LoadItemPickupHooks();
                }
                reconnecting = false;
                yield break;
            }
        }

        ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cDeath>Failed to reconnect after 5 attempts.</style>");
        Dispose();
        reconnecting = false;
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

    private void ArchipelagoConsoleCommand_OnArchipelagoReconnectCommandCalled()
    {
        reconnecting = true;
        Dispose();
        new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
        OnClientDisconnect?.Invoke("Manual reconnect requested.");
    }
}
