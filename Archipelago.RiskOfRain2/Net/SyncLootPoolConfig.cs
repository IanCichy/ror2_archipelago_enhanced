using System;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Net
{
    public class SyncLootPoolConfig : INetMessage
    {
        public static event Action<bool, long, int, int, int, int, int, int, int> OnLootPoolConfigReceived;

        private bool enabled;
        private long seed;
        private int startWhite;
        private int startGreen;
        private int startRed;
        private int startBoss;
        private int startLunar;
        private int startEquip;
        private int itemsPerExpansion;

        public SyncLootPoolConfig()
        {
        }

        public SyncLootPoolConfig(bool enabled, long seed, int startWhite, int startGreen,
            int startRed, int startBoss, int startLunar, int startEquip, int itemsPerExpansion)
        {
            this.enabled = enabled;
            this.seed = seed;
            this.startWhite = startWhite;
            this.startGreen = startGreen;
            this.startRed = startRed;
            this.startBoss = startBoss;
            this.startLunar = startLunar;
            this.startEquip = startEquip;
            this.itemsPerExpansion = itemsPerExpansion;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(enabled);
            writer.Write(seed);
            writer.Write(startWhite);
            writer.Write(startGreen);
            writer.Write(startRed);
            writer.Write(startBoss);
            writer.Write(startLunar);
            writer.Write(startEquip);
            writer.Write(itemsPerExpansion);
        }

        public void Deserialize(NetworkReader reader)
        {
            enabled = reader.ReadBoolean();
            seed = reader.ReadInt64();
            startWhite = reader.ReadInt32();
            startGreen = reader.ReadInt32();
            startRed = reader.ReadInt32();
            startBoss = reader.ReadInt32();
            startLunar = reader.ReadInt32();
            startEquip = reader.ReadInt32();
            itemsPerExpansion = reader.ReadInt32();
        }

        public void OnReceived()
        {
            OnLootPoolConfigReceived?.Invoke(enabled, seed, startWhite, startGreen,
                startRed, startBoss, startLunar, startEquip, itemsPerExpansion);
        }
    }
}
