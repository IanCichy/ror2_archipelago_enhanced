using System.Collections.Generic;
using Archipelago.RiskOfRain2.Handlers;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Archipelago.RiskOfRain2.UI
{
    public class ItemPoolViewerController : MonoBehaviour
    {
        private static bool hooked = false;
        private static readonly string[] TierNames = { "White", "Green", "Red", "Boss", "Lunar", "Void", "Equipment" };
        private static readonly Color[] TierColors =
        {
            new Color(1f, 1f, 1f),           // White
            new Color(0.467f, 1f, 0.125f),    // Green #77FF20
            new Color(0.898f, 0.325f, 0.247f), // Red #E5533F
            new Color(1f, 1f, 0f),            // Boss/Yellow
            new Color(0.188f, 0.498f, 1f),    // Lunar #307FFF
            new Color(0.769f, 0.333f, 0.878f), // Void #C455E0
            new Color(1f, 0.502f, 0f),        // Equipment #FF8000
        };

        private GameObject panelGO;
        private TextMeshProUGUI headerText;
        private Transform gridContainer;
        private int currentTierPage = 0;
        private int lastTierPage = -1;
        private bool isVisible = false;
        private bool poolDirty = true;

        private List<GameObject> iconObjects = new List<GameObject>();

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
                CreateViewer(self);
            }
            catch (System.Exception e)
            {
                Log.LogError($"Failed to create Item Pool Viewer: {e}");
            }
        }

        private static void CreateViewer(HUD hud)
        {
            // Create root panel (not parented to scoreboardPanel — independent toggle via F2)
            var rootGO = new GameObject("ItemPoolViewerRoot");
            rootGO.transform.SetParent(hud.transform, false);

            // Dark background
            var bg = rootGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.9f);
            bg.raycastTarget = false;

            // Center on screen
            var rootRect = rootGO.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(800f, 600f);

            // Ignore parent layout
            var layoutElement = rootGO.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            // Header text
            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(rootGO.transform, false);
            var headerText = headerGO.AddComponent<TextMeshProUGUI>();
            headerText.fontSize = 22;
            headerText.alignment = TextAlignmentOptions.Center;
            headerText.enableWordWrapping = false;
            headerText.richText = true;
            headerText.raycastTarget = false;

            var headerRect = headerGO.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.anchoredPosition = new Vector2(0, -8);
            headerRect.sizeDelta = new Vector2(0, 40);

            // Use same font as other HUD text
            var existingText = hud.GetComponentInChildren<TextMeshProUGUI>();
            if (existingText != null)
                headerText.font = existingText.font;

            // Instruction text at bottom
            var instrGO = new GameObject("Instructions");
            instrGO.transform.SetParent(rootGO.transform, false);
            var instrText = instrGO.AddComponent<TextMeshProUGUI>();
            instrText.fontSize = 14;
            instrText.alignment = TextAlignmentOptions.Center;
            instrText.enableWordWrapping = false;
            instrText.richText = true;
            instrText.raycastTarget = false;
            instrText.text = "<style=cSub>Scroll to change tier | F2 to close</style>";
            if (existingText != null)
                instrText.font = existingText.font;

            var instrRect = instrGO.GetComponent<RectTransform>();
            instrRect.anchorMin = new Vector2(0, 0);
            instrRect.anchorMax = new Vector2(1, 0);
            instrRect.pivot = new Vector2(0.5f, 0);
            instrRect.anchoredPosition = new Vector2(0, 8);
            instrRect.sizeDelta = new Vector2(0, 30);

            // Grid container for icons
            var gridGO = new GameObject("IconGrid");
            gridGO.transform.SetParent(rootGO.transform, false);

            var gridRect = gridGO.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0, 0);
            gridRect.anchorMax = new Vector2(1, 1);
            gridRect.offsetMin = new Vector2(16, 44);  // bottom padding (instructions)
            gridRect.offsetMax = new Vector2(-16, -52); // top padding (header)

            var gridLayout = gridGO.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(64, 64);
            gridLayout.spacing = new Vector2(6, 6);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 10;

            // Attach controller
            var controller = rootGO.AddComponent<ItemPoolViewerController>();
            controller.panelGO = rootGO;
            controller.headerText = headerText;
            controller.gridContainer = gridGO.transform;

            // Subscribe to pool changes
            ItemPoolHandler.OnPoolChanged += controller.OnPoolChanged;

            // Start hidden
            rootGO.SetActive(false);
        }

        private void OnPoolChanged()
        {
            poolDirty = true;
        }

        private void OnDestroy()
        {
            ItemPoolHandler.OnPoolChanged -= OnPoolChanged;
        }

        private void Update()
        {
            // F2 toggles visibility
            if (Input.GetKeyDown(KeyCode.F2))
            {
                isVisible = !isVisible;
                panelGO.SetActive(isVisible);
                if (isVisible)
                {
                    poolDirty = true; // Force rebuild when opening
                }
            }

            if (!isVisible) return;
            if (!ItemPoolHandler.IsActive || ItemPoolHandler.Instance == null)
            {
                panelGO.SetActive(false);
                isVisible = false;
                return;
            }

            // Scroll wheel to change tier page
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0f)
                currentTierPage = Mathf.Max(0, currentTierPage - 1);
            else if (scroll < 0f)
                currentTierPage = Mathf.Min(TierNames.Length - 1, currentTierPage + 1);

            // Rebuild only when page changes or pool state changes
            if (currentTierPage != lastTierPage || poolDirty)
            {
                lastTierPage = currentTierPage;
                poolDirty = false;
                RebuildGrid();
            }
        }

        private void RebuildGrid()
        {
            // Clear old icons
            foreach (var obj in iconObjects)
            {
                Destroy(obj);
            }
            iconObjects.Clear();

            var handler = ItemPoolHandler.Instance;
            if (handler == null) return;

            var tierSummary = handler.GetTierSummary();
            var tier = tierSummary[currentTierPage];
            var tierColor = TierColors[currentTierPage];
            var hexColor = ColorUtility.ToHtmlStringRGB(tierColor);

            // Update header
            headerText.text = $"<color=#{hexColor}>{TierNames[currentTierPage]} Items</color>: " +
                              $"<color=#{hexColor}>{tier.Current}</color> / {tier.Total}  " +
                              $"<style=cSub>({currentTierPage + 1}/{TierNames.Length})</style>";

            // Get items for this tier
            var items = handler.GetTierItems(currentTierPage);

            foreach (var (index, allowed) in items)
            {
                var iconGO = new GameObject("ItemIcon");
                iconGO.transform.SetParent(gridContainer, false);

                var image = iconGO.AddComponent<Image>();
                image.raycastTarget = false;

                Texture2D iconTexture = null;

                if (currentTierPage < 6) // Item tiers
                {
                    var itemDef = ItemCatalog.GetItemDef((ItemIndex)index);
                    if (itemDef != null)
                    {
                        var pickupIndex = PickupCatalog.FindPickupIndex((ItemIndex)index);
                        var pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
                        if (pickupDef != null && pickupDef.iconTexture != null)
                        {
                            iconTexture = pickupDef.iconTexture as Texture2D;
                        }
                    }
                }
                else // Equipment tier
                {
                    var equipDef = EquipmentCatalog.GetEquipmentDef((EquipmentIndex)index);
                    if (equipDef != null)
                    {
                        var pickupIndex = PickupCatalog.FindPickupIndex((EquipmentIndex)index);
                        var pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
                        if (pickupDef != null && pickupDef.iconTexture != null)
                        {
                            iconTexture = pickupDef.iconTexture as Texture2D;
                        }
                    }
                }

                if (iconTexture != null)
                {
                    image.sprite = Sprite.Create(iconTexture,
                        new Rect(0, 0, iconTexture.width, iconTexture.height),
                        new Vector2(0.5f, 0.5f));
                }

                // Tint: full color if allowed, dark if locked
                image.color = allowed ? Color.white : new Color(0.2f, 0.2f, 0.2f, 0.6f);

                iconObjects.Add(iconGO);
            }
        }
    }
}
