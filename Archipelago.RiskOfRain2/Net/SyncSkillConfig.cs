using System;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Net
{
    public class SyncSkillConfig : INetMessage
    {
        public static event Action<bool, long, int, int, int> OnSkillConfigReceived;

        private bool enabled;
        private long seed;
        private int totalUnlocks;
        private int currentUnlocks;
        private int startingSkills;

        public SyncSkillConfig()
        {
        }

        public SyncSkillConfig(bool enabled, long seed, int totalUnlocks, int currentUnlocks, int startingSkills)
        {
            this.enabled = enabled;
            this.seed = seed;
            this.totalUnlocks = totalUnlocks;
            this.currentUnlocks = currentUnlocks;
            this.startingSkills = startingSkills;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(enabled);
            writer.Write(seed);
            writer.Write(totalUnlocks);
            writer.Write(currentUnlocks);
            writer.Write(startingSkills);
        }

        public void Deserialize(NetworkReader reader)
        {
            enabled = reader.ReadBoolean();
            seed = reader.ReadInt64();
            totalUnlocks = reader.ReadInt32();
            currentUnlocks = reader.ReadInt32();
            startingSkills = reader.ReadInt32();
        }

        public void OnReceived()
        {
            OnSkillConfigReceived?.Invoke(enabled, seed, totalUnlocks, currentUnlocks, startingSkills);
        }
    }
}
