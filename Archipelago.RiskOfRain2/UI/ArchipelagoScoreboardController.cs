using Archipelago.RiskOfRain2.Extensions;
using Archipelago.RiskOfRain2.Services;
using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Archipelago.RiskOfRain2.UI;

/// <summary>
/// Controls the display and behavior of the Archipelago scoreboard panel within the in-game HUD, providing session
/// progress, environment status, and item pool information for Archipelago-integrated runs.
/// </summary>
/// <remarks>The Archipelago scoreboard panel is dynamically added to the game's scoreboard and presents multiple
/// pages of information, including player progress, unlocked environments, and item pool status. The panel supports
/// paging via mouse scroll while the scoreboard is open. Register this controller to enable the panel; unregister to
/// remove it. The panel is intended for use in Archipelago-enabled sessions and is not visible in standard
/// gameplay.</remarks>
public class ArchipelagoScoreboardController : MonoBehaviour
{
    private TextMeshProUGUI apText;
    private GameObject panelRef;
    private List<GameObject> poolIcons = new List<GameObject>();
    private int currentPage = 0;
    private int totalPages = 3;
    private static bool hooked = false;
    private int lastPage = -1;
    private string cachedText;

    // Reuse the canonical stage group mapping from StageBlockerService (includes group 0 starting stages).
    private static Dictionary<string, int> StageGroups => StageBlockerService.StageLookup;

    // Scene name → display name, built from LocationNames static data.
    private static readonly Dictionary<string, string> DisplayNames;

    static ArchipelagoScoreboardController()
    {
        DisplayNames = new Dictionary<string, string>();
        foreach (var kvp in LocationExtensions.InternalSceneName)
        {
            if (LocationExtensions.LocationDisplayName.TryGetValue(kvp.Key, out string displayName))
            {
                DisplayNames[kvp.Value] = displayName;
            }
        }
    }

    public static void Register()
    {
        if (!hooked)
        {
            On.RoR2.UI.HUD.Awake += HUD_Awake;
            hooked = true;
        }
    }

    public static void Unregister()
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
        controller.panelRef = panelGO;
    }

    private void Update()
    {
        if (apText == null) return;

        // Dynamically add pool pages when item pool limiting is active
        if (ItemPoolService.IsActive && ItemPoolService.Instance != null)
            totalPages = 3 + ItemPoolService.Instance.GetNonEmptyTierCount();
        else
            totalPages = 3;

        // Mouse wheel to change pages while Tab scoreboard is open
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0f)
            currentPage = Mathf.Max(0, currentPage - 1);
        else if (scroll < 0f)
            currentPage = Mathf.Min(totalPages - 1, currentPage + 1);

        // Only rebuild when the page changes
        if (currentPage != lastPage)
        {
            lastPage = currentPage;
            ClearPoolIcons();
            cachedText = BuildText(currentPage, totalPages);

            // Add icons on pool pages
            if (currentPage >= 3 && ItemPoolService.IsActive && ItemPoolService.Instance != null)
            {
                BuildPoolIcons(currentPage - 3);
            }
        }

        apText.text = cachedText;
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
            default:
                // Pool pages (page 3+ maps to tier index)
                if (ItemPoolService.IsActive && ItemPoolService.Instance != null)
                    BuildPoolPage(sb, page - 3);
                break;
        }

        return sb.ToString();
    }

    private static void BuildOverviewPage(StringBuilder sb)
    {
        // Session info
        var player = ArchipelagoClient.ConnectedPlayerName;
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
        if (StageBlockerService.CompletedEnvironments.Contains(sceneKey))
            return $"<style=cIsHealing>\u2713 {name}</style>";       // green check — all checks done
        if (unlocked.Contains(sceneKey))
            return $"<color=#FFD700>\u25A1 {name}</color>";          // yellow hollow square — unlocked, checks remain
        return $"<style=cDeath>\u2717 {name}</style>";               // red X — locked
    }

    private static void BuildEnvironmentsPage(StringBuilder sb)
    {
        var unlocked = StageBlockerService.UnlockedEnvironments;
        var allEnvs = StageBlockerService.AllSessionEnvironments;

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

    private void ClearPoolIcons()
    {
        foreach (var obj in poolIcons)
            Destroy(obj);
        poolIcons.Clear();
    }

    private void BuildPoolIcons(int poolPageIndex)
    {
        var handler = ItemPoolService.Instance;
        if (handler == null || panelRef == null) return;

        int tierIndex = handler.GetTierIndexForPoolPage(poolPageIndex);
        if (tierIndex < 0) return;

        var items = handler.GetTierItems(tierIndex);
        float iconSize = 48f;
        float spacing = 4f;
        float iconsPerRow = 12f;
        float startX = 14f;
        float startY = -70f; // below the header + tier title lines

        for (int i = 0; i < items.Count; i++)
        {
            var (index, allowed) = items[i];

            Sprite iconSprite = null;
            if (tierIndex < 6)
            {
                var def = ItemCatalog.GetItemDef((ItemIndex)index);
                if (def != null) iconSprite = def.pickupIconSprite;
            }
            else
            {
                var def = EquipmentCatalog.GetEquipmentDef((EquipmentIndex)index);
                if (def != null) iconSprite = def.pickupIconSprite;
            }

            var iconGO = new GameObject("PoolIcon");
            iconGO.transform.SetParent(panelRef.transform, false);

            var image = iconGO.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;

            if (iconSprite != null)
            {
                image.sprite = iconSprite;
                image.color = allowed ? Color.white : new Color(0.15f, 0.15f, 0.15f, 0.5f);
            }
            else
            {
                Color tierColor;
                ColorUtility.TryParseHtmlString(ItemPoolService.TierHexColors[tierIndex], out tierColor);
                image.color = allowed ? tierColor : tierColor * 0.3f;
            }

            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 1);
            iconRect.anchorMax = new Vector2(0, 1);
            iconRect.pivot = new Vector2(0, 1);
            int col = i % (int)iconsPerRow;
            int row = i / (int)iconsPerRow;
            iconRect.anchoredPosition = new Vector2(
                startX + col * (iconSize + spacing),
                startY - row * (iconSize + spacing));
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);

            poolIcons.Add(iconGO);
        }
    }

    private static void BuildPoolPage(StringBuilder sb, int poolPageIndex)
    {
        var handler = ItemPoolService.Instance;
        if (handler == null) return;

        int tierIndex = handler.GetTierIndexForPoolPage(poolPageIndex);
        if (tierIndex < 0) return;

        var tier = handler.GetTierSummary()[tierIndex];
        string hex = ItemPoolService.TierHexColors[tierIndex];

        sb.AppendLine($"<color={hex}>── {ItemPoolService.TierNames[tierIndex]} Items: {tier.Current} / {tier.Total} ──</color>");
    }

    private static void BuildDetailsPage(StringBuilder sb)
    {
        var unlocked = StageBlockerService.UnlockedEnvironments;
        var allEnvs = StageBlockerService.AllSessionEnvironments;

        // Stage keys
        sb.AppendLine("<style=cIsUtility>── Stage Keys ──</style>");
        foreach (var kvp in StageBlockerService.StageUnlocks)
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