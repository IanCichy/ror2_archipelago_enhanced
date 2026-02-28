using System;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Net
{
    public class SyncLootPoolExpansion : INetMessage
    {
        public static event Action<string, int> OnLootPoolExpansionReceived;

        private string tierName;
        private int expansionCount;

        public SyncLootPoolExpansion()
        {
        }

        public SyncLootPoolExpansion(string tierName, int expansionCount)
        {
            this.tierName = tierName;
            this.expansionCount = expansionCount;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(tierName);
            writer.Write(expansionCount);
        }

        public void Deserialize(NetworkReader reader)
        {
            tierName = reader.ReadString();
            expansionCount = reader.ReadInt32();
        }

        public void OnReceived()
        {
            OnLootPoolExpansionReceived?.Invoke(tierName, expansionCount);
        }
    }
}
