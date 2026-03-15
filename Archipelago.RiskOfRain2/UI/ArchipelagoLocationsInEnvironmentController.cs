using Archipelago.RiskOfRain2.Handlers;
using System.Collections.Generic;
using RoR2.UI;
using static RoR2.UI.ObjectivePanelController;

namespace Archipelago.RiskOfRain2.UI
{
    public class ArchipelagoLocationsInEnvironmentController
    {
        public class ChecksInEnvironment : ObjectiveTracker
        {
            public override string GenerateString()
            {
                var lines = new List<string>();
                if (CurrentChests > 0)     lines.Add($"  <color=#E2E2E2>Chests: {CurrentChests} remaining</color>");
                if (CurrentShrines > 0)    lines.Add($"  <color=#F2C94C>Shrines: {CurrentShrines} remaining</color>");
                if (CurrentScavangers > 0) lines.Add($"  <color=#BB86FC>Scav: {CurrentScavangers} remaining</color>");
                if (CurrentScanners > 0)   lines.Add($"  <color=#6FCF97>Scanner: {CurrentScanners} remaining</color>");
                if (CurrentNewts > 0)      lines.Add($"  <color=#56B4E9>Newt Altar: {CurrentNewts} remaining</color>");

                if (lines.Count == 0)
                    return $"{CurrentScene}\n  <style=cIsHealing>All AP checks complete on this stage!</style>";

                return $"{CurrentScene}\n{string.Join("\n", lines)}";
            }

            public override bool IsDirty()
            {
                return true;
            }
        }

        static ArchipelagoLocationsInEnvironmentController()
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
                    objectiveType = typeof(ChecksInEnvironment),
                    source = null
                });
            }
        }

        internal static LocationHandler.LocationInformationTemplate count = new LocationHandler.LocationInformationTemplate();

        public static string CurrentScene { get; set; }
        public static int CurrentChests { get; set; }
        public static int CurrentShrines { get; set; }
        public static int CurrentScavangers { get; set; }
        public static int CurrentScanners { get; set; }
        public static int CurrentNewts { get; set; }

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