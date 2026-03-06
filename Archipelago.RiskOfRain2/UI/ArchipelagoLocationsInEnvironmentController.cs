using Archipelago.RiskOfRain2.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2API.Utils;
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
                if (CurrentChests > 0)     lines.Add($"  <style=cIsHealing>Chests: {CurrentChests} remaining</style>");
                if (CurrentShrines > 0)    lines.Add($"  <style=cShrine>Shrines: {CurrentShrines} remaining</style>");
                if (CurrentScavangers > 0) lines.Add($"  <style=cIsDamage>Scav: {CurrentScavangers} remaining</style>");
                if (CurrentScanners > 0)   lines.Add($"  <style=cIsUtility>Scanner: {CurrentScanners} remaining</style>");
                if (CurrentNewts > 0)      lines.Add($"  <style=cKeywordName>Altar: {CurrentNewts} remaining</style>");

                if (lines.Count == 0)
                    return $"{CurrentScene}\n  <style=cIsHealing>All checks complete!</style>";

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
