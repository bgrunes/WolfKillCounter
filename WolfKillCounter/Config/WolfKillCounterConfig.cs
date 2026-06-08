using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace WolfKillCounter.Config
{
    [ProtoContract]
    public class KillCountData
    {
        [ProtoMember(1)]
        public int Kills { get; set; } = 0;

        [ProtoMember(2)]
        public int Goal { get; set; } = 50;

        [ProtoMember(3)]
        public int Deaths { get; set; } = 0;
    }

    [ProtoContract]
    public class WolfKillData
    {
        // Players and their wolf kill counts (OUTDATED)
        [ProtoMember(1)]
        public Dictionary<string, int> KillCounts { get; set; } = new Dictionary<string, int>();

        // New Format
        [ProtoMember(2)]
        public Dictionary<string, KillCountData> NewKillCounts { get; set; } = new Dictionary<string, KillCountData>();

        // Leaderboard of players and their wolf kill counts
        [ProtoMember(3)]
        public Dictionary<string, int> Leaderboard { get; set; } = new Dictionary<string, int>();

        // Total wolf kills by all players
        [ProtoMember(4)]
        public int TotalKills { get; set; } = 0;

        [ProtoMember(5)]
        public int ServerKillGoal { get; set; } = 100;
    }
    public class WolfKillCounterConfig(ICoreServerAPI api)
    {

        //private string SaveFilePath => api.GetOrCreateDataPath("wolfkills.json");
        // Load the saved data from the json file
        public void LoadWolfKillData(ref WolfKillData data)
        {
            // Create new data if none exists
            if (api.WorldManager.SaveGame.GetData("wolfkilldata") is null)
            {
                // Fresh WolfKillData
                data = new WolfKillData() { };

                // Store the fresh data
                api.WorldManager.SaveGame.StoreData("wolfkilldata", data);
                api.Logger.Notification("[WolfKillCounter]: Created new save data in SaveGame");

                api.Logger.Notification("[WolfKillCounter]: Save Loaded.");
                return;
            }
            
            // New SaveGame format if data exists already, get and load the data
            // Read raw JSON from the file
            byte[] rawData = api.WorldManager.SaveGame.GetData("wolfkilldata");
            // Replace the line causing the error with the following line
            WolfKillData parsedData = rawData == null ? new WolfKillData() : SerializerUtil.Deserialize<WolfKillData>(rawData);

            data = new WolfKillData()
            {
                NewKillCounts = parsedData.NewKillCounts,
                Leaderboard = parsedData.Leaderboard,
                TotalKills = parsedData.TotalKills,
                ServerKillGoal = parsedData.ServerKillGoal,
            };

            api.Logger.Notification("[WolfKillCounter]: Save Loaded.");

        }

        // Save the current kill data to the json file
        public void SaveWolfKillData()
        {
            WolfKillCounterModSystem modSystem = api.ModLoader.GetModSystem<WolfKillCounterModSystem>();
            
            try
            {
                var data = new WolfKillData()
                {
                    NewKillCounts = modSystem.GetWolfKillCount(),
                    Leaderboard = modSystem.GetCurrentLeaderboard(),
                    TotalKills = modSystem.GetTotalWolfKillCount(),
                    ServerKillGoal = modSystem.GetServerKillGoal()
                };

                api.WorldManager.SaveGame.StoreData("wolfkilldata", data);
                api.Logger.Notification("WolfKillCounter: Saved kill data.");
            }
            catch (Exception ex)
            {
                api.Logger.Error($"WolfKillCounter: Failed to save kill data. Error: {ex.Message}\n");
            }
        }
    }
}
