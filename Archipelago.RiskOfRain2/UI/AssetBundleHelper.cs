using UnityEngine;

namespace Archipelago.RiskOfRain2.UI;

/// <summary>
/// Provides helper methods for loading and accessing AssetBundles and their assets within the application.
/// </summary>
/// <remarks>This class is intended for internal use to manage the lifecycle and retrieval of assets from a
/// specific AssetBundle. It is not intended to be used directly by external consumers.</remarks>
internal class AssetBundleHelper
{
    public static AssetBundle localAssetBundle { get; private set; }
    internal static void LoadBundle()
    {
        localAssetBundle = AssetBundle.LoadFromFile(
            System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(ArchipelagoPlugin.Instance.Info.Location), "connectbundle"));

        if (localAssetBundle == null)
        {
            Debug.LogError("Failed to load AssetBundle!");
            return;
        }
    }

    internal static GameObject LoadPrefab(string name)
    {
        return localAssetBundle?.LoadAsset<GameObject>(name);
    }
}