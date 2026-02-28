using System;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Net
{
    public class SyncSurvivorConfig : INetMessage
    {
        public static event Action<bool, long, int, int> OnSurvivorConfigReceived;

        private bool enabled;
        private long seed;
        private int totalUnlocks;
        private int currentUnlocks;

        public SyncSurvivorConfig()
        {
        }

        public SyncSurvivorConfig(bool enabled, long seed, int totalUnlocks, int currentUnlocks)
        {
            this.enabled = enabled;
            this.seed = seed;
            this.totalUnlocks = totalUnlocks;
            this.currentUnlocks = currentUnlocks;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(enabled);
            writer.Write(seed);
            writer.Write(totalUnlocks);
            writer.Write(currentUnlocks);
        }

        public void Deserialize(NetworkReader reader)
        {
            enabled = reader.ReadBoolean();
            seed = reader.ReadInt64();
            totalUnlocks = reader.ReadInt32();
            currentUnlocks = reader.ReadInt32();
        }

        public void OnReceived()
        {
            OnSurvivorConfigReceived?.Invoke(enabled, seed, totalUnlocks, currentUnlocks);
        }
    }
}
