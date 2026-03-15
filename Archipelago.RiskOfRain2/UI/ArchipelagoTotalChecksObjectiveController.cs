using System.Collections.Generic;
using RoR2.UI;
using static RoR2.UI.ObjectivePanelController;

namespace Archipelago.RiskOfRain2.UI
{
    public class ArchipelagoTotalChecksObjectiveController
    {
        public class TotalChecksObjectiveTracker : ObjectiveTracker
        {
            private int lastCurrentChecks = -1;
            private int lastTotalChecks = -1;

            public override string GenerateString()
            {
                lastCurrentChecks = CurrentChecks;
                lastTotalChecks = TotalChecks;
                return $"Complete location checks: <style=cIsUtility>{CurrentChecks}</style>/<style=cIsUtility>{TotalChecks}</style>";
            }

            public override bool IsDirty()
            {
                return CurrentChecks != lastCurrentChecks
                    || TotalChecks != lastTotalChecks;
            }
        }

        static ArchipelagoTotalChecksObjectiveController()
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
                    objectiveType = typeof(TotalChecksObjectiveTracker),
                    source = null
                });
            }
        }

        public static int CurrentChecks { get; set; }

        public static int TotalChecks { get; set; }

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