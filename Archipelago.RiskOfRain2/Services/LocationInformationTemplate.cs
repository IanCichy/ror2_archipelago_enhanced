using Archipelago.RiskOfRain2.Extensions;
using Archipelago.RiskOfRain2.UI;
using RoR2;

namespace Archipelago.RiskOfRain2.Services;

/// <summary>
/// Stores per-type location counts for an environment and provides indexed access
/// via <see cref="LocationCheckService.LocationTypes"/>.
/// </summary>
internal class LocationInformationTemplate
{
    private int[] data = new int[(int)LocationCheckService.LocationTypes.MAX];

    public int this[int i]
    {
        get => data[i];
        set => data[i] = value;
    }

    public int this[LocationCheckService.LocationTypes type]
    {
        get => data[(int)type];
        set => data[(int)type] = value;
    }

    /// <returns>The sum of all locations in the template.</returns>
    public int Total()
    {
        int sum = 0;
        for (int type = 0; type < (int)LocationCheckService.LocationTypes.MAX; type++) sum += data[type];
        return sum;
    }

    public string Scene()
    {
        SceneDef scene = LocationCheckService.GetLocationScene();
        if (LocationExtensions.LocationDisplayName.ContainsKey(LocationCheckService.CurrentSceneIndex))
        {
            ArchipelagoLocationsInEnvironmentController.CurrentScene = $"{LocationExtensions.LocationDisplayName[LocationCheckService.CurrentSceneIndex]}";
            return $"{LocationExtensions.LocationDisplayName[LocationCheckService.CurrentSceneIndex]}";
        }
        ArchipelagoLocationsInEnvironmentController.CurrentScene = $"Environment Location";
        return $"Environment Location";
    }

    public LocationInformationTemplate Copy()
    {
        LocationInformationTemplate copy = new LocationInformationTemplate();
        for (int type = 0; type < (int)LocationCheckService.LocationTypes.MAX; type++) copy[type] = data[type];
        return copy;
    }
}
