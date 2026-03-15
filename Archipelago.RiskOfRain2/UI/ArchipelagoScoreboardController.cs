using Archipelago.RiskOfRain2.Handlers;
using Archipelago.RiskOfRain2.Lookup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Archipelago.RiskOfRain2.UI
{
    public class ArchipelagoScoreboardController : MonoBehaviour
    {
        private TextMeshProUGUI apText;
        private int currentPage = 0;
        private int totalPages = 3;
        private static bool hooked = false;

        // Stage group mapping: scene name → AP stage key (1-4).
        // Mirrors StageBlockerHandler.stageLookup but static for scoreboard access.
        // AP Stage 1 = game ordered stage 2 (first advancement after starting stages).
        private static readonly Dictionary<string, int> StageGroups = new()
        {
            // Starting stages (group 0) — no Stage Key required
            { "blackbeach", 0 }, { "blackbeach2", 0 }, { "golemplains", 0 }, { "golemplains2", 0 },
            { "lakes", 0 }, { "snowyforest", 0 },
            { "village", 0 }, { "villagenight", 0 }, { "lakesnight", 0 },
            // Group 1 (Stage Key 1 = game Stage 2)
            { "ancientloft", 1 }, { "foggyswamp", 1 }, { "goolake", 1 },
            { "lemuriantemple", 1 }, { "nest", 1 },
            // Group 2 (Stage Key 2 = game Stage 3)
            { "frozenwall", 2 }, { "sulfurpools", 2 }, { "wispgraveyard", 2 },
            { "habitat", 2 }, { "habitatfall", 2 },
            { "ironalluvium", 2 }, { "ironalluvium2", 2 },
            // Group 3 (Stage Key 3 = game Stage 4)
            { "dampcavesimple", 3 }, { "rootjungle", 3 }, { "shipgraveyard", 3 },
            { "meridian", 3 }, { "repurposedcrater", 3 }, { "conduitcanyon", 3 },
            // Group 4 (Stage Key 4 = game Stage 5)
            { "skymeadow", 4 }, { "helminthroost", 4 }, { "solutionalhaunt", 4 },
        };

        // Scene name → display name, built from LocationNames static data.
        private static readonly Dictionary<string, string> DisplayNames;

        static ArchipelagoScoreboardController()
        {
            DisplayNames = new Dictionary<string, string>();
            foreach (var kvp in LocationNames.cachedLocationsNames)
            {
                if (LocationNames.locationsNames.TryGetValue(kvp.Key, out string displayName))
                {
                    DisplayNames[kvp.Value] = displayName;
                }
            }
        }

        public static void Hook()
        {
            if (!hooked)
            {
                On.RoR2.UI.HUD.Awake += HUD_Awake;
                hooked = true;
            }
        }

        public static void Unhook()
        {
            if (hooked)
            {
                On.RoR2.UI.HUD.Awake -= HUD_Awake;
                hooked = false;
            }
        }

        private static void HUD_Awake(On.RoR2.UI.HUD.orig_Awake orig, HUD self)
        {
            orig(self);
            try
            {
                CreatePanel(self);
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to create Archipelago scoreboard panel: {e}");
            }
        }

        private static void CreatePanel(HUD hud)
        {
            var scoreboardPanel = hud.scoreboardPanel;
            if (scoreboardPanel == null) return;

            // Create container as child of scoreboardPanel so it toggles with Tab
            var panelGO = new GameObject("ArchipelagoScoreboardPanel");
            panelGO.transform.SetParent(scoreboardPanel.transform, false);

            // Dark background for readability
            var bg = panelGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);
            bg.raycastTarget = false;

            // Center the panel on screen
            var rect = panelGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(700f, 600f);

            // Ignore parent layout so we float independently
            var layoutElement = panelGO.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            // Text element
            var textGO = new GameObject("APText");
            textGO.transform.SetParent(panelGO.transform, false);
            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.fontSize = 16;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.richText = true;
            text.raycastTarget = false;

            // Match the text area to the panel with padding
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 8);
            textRect.offsetMax = new Vector2(-12, -8);

            // Use the same font as other scoreboard text
            var existingText = scoreboardPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (existingText != null)
                text.font = existingText.font;

            // Auto-size the panel height to content
            var fitter = panelGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Attach our MonoBehaviour for per-frame updates + page input
            var controller = panelGO.AddComponent<ArchipelagoScoreboardController>();
            controller.apText = text;
        }

        private void Update()
        {
            if (apText == null) return;

            // Mouse wheel to change pages while Tab scoreboard is open
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0f)
                currentPage = Mathf.Max(0, currentPage - 1);
            else if (scroll < 0f)
                currentPage = Mathf.Min(totalPages - 1, currentPage + 1);

            apText.text = BuildText(currentPage, totalPages);
        }

        private static string BuildText(int page, int totalPages)
        {
            var sb = new StringBuilder();

            // Header + page indicator
            sb.AppendLine($"<style=cIsUtility>══ Archipelago ══</style>  <style=cSub>({page + 1}/{totalPages}) scroll to page</style>");
            sb.AppendLine();

            switch (page)
            {
                case 0:
                    BuildOverviewPage(sb);
                    break;
                case 1:
                    BuildEnvironmentsPage(sb);
                    break;
                case 2:
                    BuildDetailsPage(sb);
                    break;
            }

            return sb.ToString();
        }

        private static void BuildOverviewPage(StringBuilder sb)
        {
            // Session info
            var player = ArchipelagoClient.connectedPlayerName;
            sb.AppendLine($"Player: <style=cIsHealing>{(string.IsNullOrEmpty(player) ? "?" : player)}</style>");

            // Victory condition
            var victory = ArchipelagoClient.victoryCondition;
            sb.AppendLine($"Victory: <style=cIsDamage>{(string.IsNullOrEmpty(victory) ? "unknown" : victory)}</style>");

            // Check progress
            var current = ArchipelagoTotalChecksObjectiveController.CurrentChecks;
            var total = ArchipelagoTotalChecksObjectiveController.TotalChecks;
            sb.AppendLine($"Checks: <style=cIsHealing>{current}</style>/<style=cIsHealing>{total}</style>");

            // Check countdown config
            var itemStep = ArchipelagoCheckCountdownController.ItemStep;
            var shrineStep = ArchipelagoCheckCountdownController.ShrineStep;
            sb.Append($"Checks every: <style=cIsHealing>{itemStep}</style> pickup(s)");
            if (ArchipelagoCheckCountdownController.ShowShrineCountdown)
                sb.Append($" | <style=cShrine>{shrineStep}</style> shrine(s)");
            sb.AppendLine();
        }

        private static string FormatEnv(string sceneKey, HashSet<string> unlocked)
        {
            var name = DisplayNames.TryGetValue(sceneKey, out string dn) ? dn : sceneKey;
            if (StageBlockerHandler.CompletedEnvironments.Contains(sceneKey))
                return $"<style=cIsHealing>\u2713 {name}</style>";       // green check — all checks done
            if (unlocked.Contains(sceneKey))
                return $"<color=#FFD700>\u25A1 {name}</color>";          // yellow hollow square — unlocked, checks remain
            return $"<style=cDeath>\u2717 {name}</style>";               // red X — locked
        }

        private static void BuildEnvironmentsPage(StringBuilder sb)
        {
            var unlocked = StageBlockerHandler.UnlockedEnvironments;
            var allEnvs = StageBlockerHandler.AllSessionEnvironments;

            // Starting stages listed horizontally at the top
            var starting = StageGroups
                .Where(e => e.Value == 0 && allEnvs.Contains(e.Key))
                .Select(e => e.Key)
                .ToList();

            if (starting.Count > 0)
            {
                sb.AppendLine("<style=cIsUtility>── Starting Stages ──</style>");
                // Two per line to avoid overflow
                for (int i = 0; i < starting.Count; i += 2)
                {
                    sb.Append($"<pos=0>{FormatEnv(starting[i], unlocked)}");
                    if (i + 1 < starting.Count)
                        sb.Append($"<pos=350>{FormatEnv(starting[i + 1], unlocked)}");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            // Stage 1-4 in 4 columns with more room
            var columns = new List<string>[4];
            for (int g = 0; g < 4; g++)
            {
                int group = g + 1;
                columns[g] = StageGroups
                    .Where(e => e.Value == group && allEnvs.Contains(e.Key))
                    .Select(e => FormatEnv(e.Key, unlocked))
                    .ToList();
            }

            int[] positions = { 0, 175, 350, 525 };
            string[] headers = { "Stage 1", "Stage 2", "Stage 3", "Stage 4" };

            sb.AppendLine("<style=cIsUtility>── Stage Key Stages ──</style>");
            for (int g = 0; g < 4; g++)
                sb.Append($"<pos={positions[g]}>{headers[g]}");
            sb.AppendLine();

            int maxRows = 0;
            for (int g = 0; g < 4; g++)
                maxRows = Math.Max(maxRows, columns[g].Count);

            for (int row = 0; row < maxRows; row++)
            {
                for (int g = 0; g < 4; g++)
                {
                    if (row < columns[g].Count)
                        sb.Append($"<pos={positions[g]}>{columns[g][row]}");
                }
                sb.AppendLine();
            }
        }
        // Hidden realms and special stages to show on the details page.
        private static readonly Dictionary<string, string> HiddenRealms = new()
        {
            { "bazaar", "Bazaar Between Time" },
            { "arena", "Void Fields" },
            { "goldshores", "Gilded Coast" },
            { "mysteryspace", "A Moment, Fractured" },
            { "limbo", "A Moment, Whole" },
            { "artifactworld", "Bulwark's Ambry" },
        };

        private static readonly Dictionary<string, string> SpecialStages = new()
        {
            { "moon2", "Commencement" },
            { "voidstage", "Void Locus" },
            { "voidraid", "The Planetarium" },
            { "meridian", "Prime Meridian" },
            { "solutionalhaunt", "Solutional Haunt" },
            { "solusweb", "Neural Sanctum" },
        };

        private static void BuildDetailsPage(StringBuilder sb)
        {
            var unlocked = StageBlockerHandler.UnlockedEnvironments;
            var allEnvs = StageBlockerHandler.AllSessionEnvironments;

            // Stage keys
            sb.AppendLine("<style=cIsUtility>── Stage Keys ──</style>");
            foreach (var kvp in StageBlockerHandler.stageUnlocks)
            {
                var icon = kvp.Value
                    ? "<style=cIsHealing>\u2713</style>"
                    : "<style=cDeath>\u2717</style>";
                sb.AppendLine($"  {icon} {kvp.Key}");
            }
            sb.AppendLine();

            // Hidden realms
            sb.AppendLine("<style=cIsUtility>── Hidden Realms ──</style>");
            foreach (var kvp in HiddenRealms)
            {
                if (!allEnvs.Contains(kvp.Key)) continue;
                sb.AppendLine($"  {FormatEnv(kvp.Key, unlocked)}");
            }
            sb.AppendLine();

            // Special / victory stages
            sb.AppendLine("<style=cIsUtility>── Special Stages ──</style>");
            foreach (var kvp in SpecialStages)
            {
                if (!allEnvs.Contains(kvp.Key)) continue;
                sb.AppendLine($"  {FormatEnv(kvp.Key, unlocked)}");
            }
        }
    }
}
