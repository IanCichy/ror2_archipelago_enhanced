using RoR2.UI;
using System.Collections.Generic;
using static RoR2.UI.ObjectivePanelController;

namespace Archipelago.RiskOfRain2.UI;

/// <summary>
/// Provides functionality to track and manage an objective for completing a total number of location checks in an
/// Archipelago session.
/// </summary>
/// <remarks>This controller integrates with the objective panel system to display progress toward completing all
/// required location checks. Use the static properties and methods to update or control the objective's visibility and
/// progress. This class is intended for use in environments where objectives are tracked and displayed to the player,
/// such as in modded gameplay scenarios.</remarks>
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