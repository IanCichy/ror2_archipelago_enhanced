using R2API;
using R2API.Utils;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Console
{
    /// <summary>
    /// Developer-only debug console commands for testing.
    /// To remove: delete this file and rebuild.
    /// </summary>
    public static class DebugCommands
    {
        private static bool _godMode;
        private static float _speedMultiplier;
        private static float _damageMultiplier;
        private static float _teleporterMultiplier;
        private static uint _startMoney;
        private static bool _hooksInstalled;

        private static void EnsureHooks()
        {
            if (_hooksInstalled) return;
            _hooksInstalled = true;

            RecalculateStatsAPI.GetStatCoefficients += OnStatCoefficients;
            On.RoR2.HoldoutZoneController.Awake += OnHoldoutZoneAwake;
            On.RoR2.CharacterBody.Start += OnCharacterBodyStart;
            Stage.onStageStartGlobal += OnStageStart;
        }

        private static void RemoveHooks()
        {
            if (!_hooksInstalled) return;
            _hooksInstalled = false;

            RecalculateStatsAPI.GetStatCoefficients -= OnStatCoefficients;
            On.RoR2.HoldoutZoneController.Awake -= OnHoldoutZoneAwake;
            On.RoR2.CharacterBody.Start -= OnCharacterBodyStart;
            Stage.onStageStartGlobal -= OnStageStart;
        }

        // ── Hooks ──────────────────────────────────────────────

        private static void OnStatCoefficients(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body.isPlayerControlled) return;

            if (_speedMultiplier > 0f)
                args.moveSpeedMultAdd += _speedMultiplier - 1f;

            if (_damageMultiplier > 0f)
                args.damageMultAdd += _damageMultiplier - 1f;
        }

        private static void OnHoldoutZoneAwake(On.RoR2.HoldoutZoneController.orig_Awake orig, HoldoutZoneController self)
        {
            orig(self);
            if (_teleporterMultiplier > 1f)
            {
                self.baseChargeDuration /= _teleporterMultiplier;
            }
        }

        private static void OnCharacterBodyStart(On.RoR2.CharacterBody.orig_Start orig, CharacterBody self)
        {
            orig(self);
            if (_godMode && self.isPlayerControlled && self.healthComponent != null)
            {
                self.healthComponent.godMode = true;
            }
        }

        private static void OnStageStart(Stage stage)
        {
            if (_startMoney > 0)
            {
                ArchipelagoPlugin.Instance.StartCoroutine(GiveMoneyDelayed());
            }
        }

        private static System.Collections.IEnumerator GiveMoneyDelayed()
        {
            yield return new WaitForSeconds(1f);
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                if (player.master != null)
                {
                    player.master.GiveMoney(_startMoney);
                }
            }
            ChatMessage.SendColored($"[DEBUG] Gave ${_startMoney} to all players.", Color.yellow);
        }

        // ── Helpers ────────────────────────────────────────────

        private static void MarkStatsDirty()
        {
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var body = player.master?.GetBody();
                if (body != null) body.statsDirty = true;
            }
        }

        private static void SetGodModeOnAll(bool enabled)
        {
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var body = player.master?.GetBody();
                if (body?.healthComponent != null)
                    body.healthComponent.godMode = enabled;
            }
        }

        // ── Console Commands ───────────────────────────────────

        [ConCommand(commandName = "ap_debug", flags = ConVarFlags.SenderMustBeServer,
            helpText = "Show debug status or disable all. Syntax: ap_debug [off]")]
        private static void CmdDebug(ConCommandArgs args)
        {
            if (args.Count > 0 && args.GetArgString(0) == "off")
            {
                _godMode = false;
                _speedMultiplier = 0f;
                _damageMultiplier = 0f;
                _teleporterMultiplier = 0f;
                _startMoney = 0;
                SetGodModeOnAll(false);
                MarkStatsDirty();
                RemoveHooks();
                ChatMessage.SendColored("[DEBUG] All debug features disabled.", Color.yellow);
                return;
            }

            ChatMessage.SendColored(
                $"[DEBUG] God: {(_godMode ? "ON" : "OFF")} | " +
                $"Speed: {(_speedMultiplier > 0 ? $"{_speedMultiplier}x" : "OFF")} | " +
                $"Damage: {(_damageMultiplier > 0 ? $"{_damageMultiplier}x" : "OFF")} | " +
                $"Teleporter: {(_teleporterMultiplier > 1 ? $"{_teleporterMultiplier}x" : "OFF")} | " +
                $"Money: {(_startMoney > 0 ? $"${_startMoney}" : "OFF")}",
                Color.yellow);
        }

        [ConCommand(commandName = "ap_debug_god", flags = ConVarFlags.SenderMustBeServer,
            helpText = "Toggle god mode for all players.")]
        private static void CmdGodMode(ConCommandArgs args)
        {
            EnsureHooks();
            _godMode = !_godMode;
            SetGodModeOnAll(_godMode);
            ChatMessage.SendColored($"[DEBUG] God mode: {(_godMode ? "ON" : "OFF")}", Color.yellow);
        }

        [ConCommand(commandName = "ap_debug_teleporter", flags = ConVarFlags.SenderMustBeServer,
            helpText = "Speed up teleporter charging. Syntax: ap_debug_teleporter [multiplier]. Default: 4. Use 0 to disable.")]
        private static void CmdTeleporter(ConCommandArgs args)
        {
            EnsureHooks();
            _teleporterMultiplier = args.Count > 0 ? args.GetArgFloat(0) : 4f;
            if (_teleporterMultiplier <= 1f) _teleporterMultiplier = 0f;

            ChatMessage.SendColored(
                _teleporterMultiplier > 0
                    ? $"[DEBUG] Teleporter charge speed: {_teleporterMultiplier}x (applies next zone)"
                    : "[DEBUG] Teleporter charge speed: OFF",
                Color.yellow);
        }

        [ConCommand(commandName = "ap_debug_money", flags = ConVarFlags.SenderMustBeServer,
            helpText = "Give money at stage start. Syntax: ap_debug_money [amount]. Default: 50000. Use 0 to disable.")]
        private static void CmdMoney(ConCommandArgs args)
        {
            EnsureHooks();
            _startMoney = args.Count > 0 ? (uint)args.GetArgInt(0) : 50000u;

            if (_startMoney > 0)
            {
                // Also give immediately if mid-stage
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    if (player.master != null)
                        player.master.GiveMoney(_startMoney);
                }
                ChatMessage.SendColored($"[DEBUG] Stage start money: ${_startMoney} (also granted now)", Color.yellow);
            }
            else
            {
                ChatMessage.SendColored("[DEBUG] Stage start money: OFF", Color.yellow);
            }
        }

        [ConCommand(commandName = "ap_debug_speed", flags = ConVarFlags.SenderMustBeServer,
            helpText = "Multiply player move speed. Syntax: ap_debug_speed [multiplier]. Default: 3. Use 0 to disable.")]
        private static void CmdSpeed(ConCommandArgs args)
        {
            EnsureHooks();
            _speedMultiplier = args.Count > 0 ? args.GetArgFloat(0) : 3f;
            if (_speedMultiplier <= 1f) _speedMultiplier = 0f;
            MarkStatsDirty();

            ChatMessage.SendColored(
                _speedMultiplier > 0
                    ? $"[DEBUG] Speed multiplier: {_speedMultiplier}x"
                    : "[DEBUG] Speed multiplier: OFF",
                Color.yellow);
        }

        [ConCommand(commandName = "ap_debug_damage", flags = ConVarFlags.SenderMustBeServer,
            helpText = "Multiply player damage. Syntax: ap_debug_damage [multiplier]. Default: 10. Use 0 to disable.")]
        private static void CmdDamage(ConCommandArgs args)
        {
            EnsureHooks();
            _damageMultiplier = args.Count > 0 ? args.GetArgFloat(0) : 10f;
            if (_damageMultiplier <= 1f) _damageMultiplier = 0f;
            MarkStatsDirty();

            ChatMessage.SendColored(
                _damageMultiplier > 0
                    ? $"[DEBUG] Damage multiplier: {_damageMultiplier}x"
                    : "[DEBUG] Damage multiplier: OFF",
                Color.yellow);
        }

        // ── Portal Spawning ──────────────────────────────────────

        private static readonly System.Collections.Generic.Dictionary<string, string> PortalPrefabs = new System.Collections.Generic.Dictionary<string, string>
        {
            { "blue",      "RoR2/Base/PortalShop/PortalShop.prefab" },
            { "gold",      "RoR2/Base/PortalGoldshores/PortalGoldshores.prefab" },
            { "celestial", "RoR2/Base/PortalMS/PortalMS.prefab" },
            { "void",      "RoR2/DLC1/PortalVoid/PortalVoid.prefab" },
        };

        [ConCommand(commandName = "ap_debug_portal", flags = ConVarFlags.SenderMustBeServer,
            helpText = "Spawn a portal near the teleporter. Syntax: ap_debug_portal <blue|gold|celestial|void>")]
        private static void CmdPortal(ConCommandArgs args)
        {
            if (args.Count < 1)
            {
                ChatMessage.SendColored("[DEBUG] Syntax: ap_debug_portal <blue|gold|celestial|void>", Color.yellow);
                return;
            }

            string portalType = args.GetArgString(0).ToLower();
            if (!PortalPrefabs.TryGetValue(portalType, out string prefabPath))
            {
                ChatMessage.SendColored($"[DEBUG] Unknown portal type '{portalType}'. Options: blue, gold, celestial, void", Color.yellow);
                return;
            }

            var teleporter = TeleporterInteraction.instance;
            if (teleporter == null)
            {
                ChatMessage.SendColored("[DEBUG] No teleporter found on this stage.", Color.yellow);
                return;
            }

            var prefab = Addressables.LoadAssetAsync<GameObject>(prefabPath).WaitForCompletion();
            if (prefab == null)
            {
                ChatMessage.SendColored($"[DEBUG] Failed to load portal prefab for '{portalType}'.", Color.yellow);
                return;
            }

            Vector3 spawnPos = teleporter.transform.position + teleporter.transform.forward * 10f + Vector3.up * 1f;
            GameObject portal = GameObject.Instantiate(prefab, spawnPos, Quaternion.identity);
            NetworkServer.Spawn(portal);

            ChatMessage.SendColored($"[DEBUG] Spawned {portalType} portal near teleporter.", Color.yellow);
        }

        // ── Stage Warp ──────────────────────────────────────────

        [ConCommand(commandName = "ap_debug_stage", flags = ConVarFlags.SenderMustBeServer,
            helpText = "Warp to any stage by scene name. Syntax: ap_debug_stage <sceneName> (e.g. meridian, habitat, lemuriantemple)")]
        private static void CmdStage(ConCommandArgs args)
        {
            if (args.Count < 1)
            {
                ChatMessage.SendColored("[DEBUG] Syntax: ap_debug_stage <sceneName>", Color.yellow);
                return;
            }

            string sceneName = args.GetArgString(0).ToLower();
            var sceneDef = SceneCatalog.FindSceneDef(sceneName);
            if (sceneDef == null)
            {
                ChatMessage.SendColored($"[DEBUG] Unknown scene '{sceneName}'.", Color.yellow);
                return;
            }

            Run.instance.AdvanceStage(sceneDef);
            ChatMessage.SendColored($"[DEBUG] Warping to {sceneDef.nameToken} ({sceneName})...", Color.yellow);
        }
    }
}
