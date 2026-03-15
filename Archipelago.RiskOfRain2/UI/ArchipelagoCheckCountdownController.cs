using System.Collections.Generic;
using RoR2.UI;
using static RoR2.UI.ObjectivePanelController;

namespace Archipelago.RiskOfRain2.UI
{
    public class ArchipelagoCheckCountdownController
    {
        public class CheckCountdownTracker : ObjectiveTracker
        {
            private int lastItemsPickedUp = -1;
            private int lastItemStep = -1;
            private int lastShrinesUsed = -1;
            private int lastShrineStep = -1;

            public override string GenerateString()
            {
                var parts = new List<string>();

                if (ShowItemCountdown && ItemStep > 0)
                {
                    int remaining = ItemStep - ItemsPickedUp;
                    if (remaining > 0)
                        parts.Add($"Next item check in <style=cIsHealing>{remaining}</style> pickup(s)");
                }

                if (ShowShrineCountdown && ShrineStep > 0)
                {
                    int remaining = ShrineStep - ShrinesUsed;
                    if (remaining > 0)
                        parts.Add($"Next shrine check in <style=cShrine>{remaining}</style> use(s)");
                }

                lastItemsPickedUp = ItemsPickedUp;
                lastItemStep = ItemStep;
                lastShrinesUsed = ShrinesUsed;
                lastShrineStep = ShrineStep;

                if (parts.Count == 0)
                    return "No checks remaining";

                return string.Join(" | ", parts);
            }

            public override bool IsDirty()
            {
                return ItemsPickedUp != lastItemsPickedUp
                    || ItemStep != lastItemStep
                    || ShrinesUsed != lastShrinesUsed
                    || ShrineStep != lastShrineStep;
            }
        }

        static ArchipelagoCheckCountdownController()
        {
            ObjectivePanelController.collectObjectiveSources += ObjectivePanelController_collectObjectiveSources;
        }

        public static void disable()
        {
            ObjectivePanelController.collectObjectiveSources -= ObjectivePanelController_collectObjectiveSources;
        }

        private static void ObjectivePanelController_collectObjectiveSources(RoR2.CharacterMaster arg1, List<ObjectiveSourceDescriptor> arg2)
        {
            if (addObjective)
            {
                arg2.Add(new ObjectiveSourceDescriptor()
                {
                    master = arg1,
                    objectiveType = typeof(CheckCountdownTracker),
                    source = null
                });
            }
        }

        /// <summary>Number of items picked up since last check (0 to ItemStep-1).</summary>
        public static int ItemsPickedUp { get; set; }

        /// <summary>Items required per check.</summary>
        public static int ItemStep { get; set; }

        /// <summary>Number of shrines used since last check (0 to ShrineStep-1).</summary>
        public static int ShrinesUsed { get; set; }

        /// <summary>Shrines required per check.</summary>
        public static int ShrineStep { get; set; }

        /// <summary>Whether to show the item countdown (always true when playing AP).</summary>
        public static bool ShowItemCountdown { get; set; }

        /// <summary>Whether to show the shrine countdown (explore mode only).</summary>
        public static bool ShowShrineCountdown { get; set; }

        private static bool addObjective;

        public static void AddObjective()
        {
            addObjective = true;
        }

        public static void RemoveObjective()
        {
            addObjective = false;
        }

        /// <summary>
        /// Update the countdown from raw pickup count and step values (as sent by net messages).
        /// </summary>
        public static void UpdateItemCountdown(int pickupCount, int step)
        {
            ItemsPickedUp = pickupCount;
            ItemStep = step;
        }

        /// <summary>
        /// Update the countdown from raw shrine count and step values (as sent by net messages).
        /// </summary>
        public static void UpdateShrineCountdown(int shrineCount, int step)
        {
            ShrinesUsed = shrineCount;
            ShrineStep = step;
        }
    }
}
