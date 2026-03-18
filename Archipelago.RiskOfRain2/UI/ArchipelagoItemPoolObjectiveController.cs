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
                for (int i = 0; i < tiers.Length; i++)
                {
                    if (tiers[i].Total == 0) continue;
                    string color = ItemPoolHandler.TierHexColors[i];
                    parts.Add($"<color={color}>{tiers[i].Name[0]}</color> {tiers[i].Current}/{tiers[i].Total}");
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
