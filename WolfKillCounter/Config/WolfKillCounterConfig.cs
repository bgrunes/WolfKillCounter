using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        // Players and their wolf kill counts
        [ProtoMember(1)]
        public Dictionary<string, int> KillCounts { get; set; } = new Dictionary<string, int>();

        // Old Dictionary format, REFORMAT USE ONLY
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
    public class WolfKillCounterConfig
    {
        ICoreServerAPI sapi;
        
        private string SaveFilePath => sapi.GetOrCreateDataPath("wolfkills.json");
        // Load the saved data from the json file
        public WolfKillData LoadWolfKillData(ICoreServerAPI api)
        {
            sapi = api;
            
            // If the old json file exists, get the data and switch to new saving format
            if (api.LoadModConfig("wolfkills.json") != null)
            {
                // Get Mod System
                WolfKillCounterModSystem wolfKillCounter = api.ModLoader.GetModSystem<WolfKillCounterModSystem>();

                // Get the file data
                var data = api.LoadModConfig<WolfKillData>("wolfkills.json");
                
                // Load Leaderboard and TotalKills
                var currentLeaderboard = data.Leaderboard;
                var totalWolfKillCount = data.TotalKills;
                var serverKillGoal = wolfKillCounter.CalculateGoal(totalWolfKillCount);

                Dictionary<string, KillCountData> wolfKillCount = new Dictionary<string, KillCountData>();
                // Load KillCounts from the old format and convert it to the new format
                if (data.KillCounts != null)
                {
                    foreach (var kvp in data.KillCounts)
                    {
                        wolfKillCount.Add(kvp.Key, new KillCountData { Kills = kvp.Value, Goal = wolfKillCounter.CalculateGoal((int)kvp.Value), Deaths = 0 });
                    }
                }

                api.Logger.Notification("[WolfKillCounter]: KillCounts parsed and migrated if needed.");

                // Try to Delete the JSON file, otherwise Error.
                try
                {
                    System.IO.File.Delete(SaveFilePath);
                    api.Logger.Notification("[WolfKillCounter]: Deleted old json file for SaveData Migration.");
                }
                catch (Exception)
                {
                    api.Logger.Error($"[WolfKillCounter] - Error: File not deleted! Make sure the JSON file is manually deleted.\n");
                }

                WolfKillData loadData = new WolfKillData()
                {
                    NewKillCounts = wolfKillCount,
                    Leaderboard = currentLeaderboard,
                    TotalKills = totalWolfKillCount,
                    ServerKillGoal = serverKillGoal
                };

                api.Logger.Notification("[WolfKillCounter]: Save Loaded.");
                return loadData;
                
            }
            // New SaveGame format if data exists already, get and load the data
            else if (api.WorldManager.SaveGame.GetData("wolfkilldata") is Byte[] rawData)
            {
                // Read raw JSON from the file
                rawData = api.WorldManager.SaveGame.GetData("wolfkilldata");
                // Replace the line causing the error with the following line
                WolfKillData parsedData = rawData == null ? new WolfKillData() : SerializerUtil.Deserialize<WolfKillData>(rawData);

                // Load TotalKills and ServerKillGoal
                var totalWolfKillCount = parsedData.TotalKills;
                var serverKillGoal = parsedData.ServerKillGoal;

                // Load Leaderboard
                var currentLeaderboard = parsedData.Leaderboard;

                // Load KillCounts with backward compatibility for the old format
                var wolfKillCount = parsedData.NewKillCounts;

                var loadData = new WolfKillData()
                {
                    NewKillCounts = wolfKillCount,
                    Leaderboard = currentLeaderboard,
                    TotalKills = totalWolfKillCount,
                    ServerKillGoal = serverKillGoal
                };

                api.Logger.Notification("[WolfKillCounter]: Save Loaded.");
                return loadData;
            }
            // No SaveGame data exists, create new save data
            else
            {
                // Fresh WolfKillData
                var loadData = new WolfKillData()
                {
                    NewKillCounts = new Dictionary<string, KillCountData>(),
                    Leaderboard = new Dictionary<string, int>(),
                    TotalKills = 0,
                    ServerKillGoal = 100
                };

                // Store the fresh data
                api.WorldManager.SaveGame.StoreData("wolfkilldata", loadData);
                api.Logger.Notification("[WolfKillCounter]: Created new save data in SaveGame");

                api.Logger.Notification("[WolfKillCounter]: Save Loaded.");
                return loadData;
            }

        }

        // Save the current kill data to the json file
        public void SaveWolfKillData(ICoreServerAPI api)
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
