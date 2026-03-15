using System.Collections.Generic;
using Archipelago.RiskOfRain2.Handlers;
using RoR2.UI;
using static RoR2.UI.ObjectivePanelController;

namespace Archipelago.RiskOfRain2.UI
{
    public class ArchipelagoItemPoolObjectiveController
    {
        public class ItemPoolObjectiveTracker : ObjectiveTracker
        {
            private string lastText = "";

            public override string GenerateString()
            {
                if (!ItemPoolHandler.IsActive || ItemPoolHandler.Instance == null)
                {
                    lastText = "Item Pool: <style=cSub>disabled</style>";
                    return lastText;
                }

                var tiers = ItemPoolHandler.Instance.GetTierSummary();
                var parts = new List<string>();
                foreach (var t in tiers)
                {
                    if (t.Total == 0) continue;
                    string color = GetTierColor(t.Name);
                    parts.Add($"<color={color}>{t.Name[0]}</color> {t.Current}/{t.Total}");
                }

                lastText = "Pool: " + string.Join(" | ", parts);
                return lastText;
            }

            public override bool IsDirty()
            {
                // Rebuild every frame is cheap since it's just string concat.
                // Could optimize with dirty flag from ItemPoolHandler.OnPoolChanged if needed.
                return true;
            }

            private static string GetTierColor(string tierName)
            {
                switch (tierName)
                {
                    case "White": return "#FFFFFF";
                    case "Green": return "#77FF20";
                    case "Red": return "#E5533F";
                    case "Boss": return "#FFFF00";
                    case "Lunar": return "#307FFF";
                    case "Void": return "#C455E0";
                    case "Equipment": return "#FF8000";
                    default: return "#FFFFFF";
                }
            }
        }

        static ArchipelagoItemPoolObjectiveController()
        {
            ObjectivePanelController.collectObjectiveSources += ObjectivePanelController_collectObjectiveSources;
        }

        private static void ObjectivePanelController_collectObjectiveSources(RoR2.CharacterMaster arg1, List<ObjectiveSourceDescriptor> arg2)
        {
            if (addObjective)
            {
                arg2.Add(new ObjectiveSourceDescriptor()
                {
                    master = arg1,
                    objectiveType = typeof(ItemPoolObjectiveTracker),
                    source = null
                });
            }
        }

        private static bool addObjective;

        public static void AddObjective()
        {
            addObjective = true;
        }

        public static void RemoveObjective()
        {
            addObjective = false;
        }
    }
}
