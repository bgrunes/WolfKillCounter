using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Vintagestory.API.Util;

namespace WolfKillCounter
{
    public class WolfKillData
    {
        // Players and their wolf kill counts
        public Dictionary<string, int> KillCounts { get; set; } = new Dictionary<string, int>();

        // Old Dictionary format, REFORMAT USE ONLY
        public Dictionary<string, int[]> NewKillCounts { get; set; } = new Dictionary<string, int[]>();

        // Leaderboard of players and their wolf kill counts
        public Dictionary<string, int> Leaderboard { get; set; } = new Dictionary<string, int>();

        // Total wolf kills by all players
        public int TotalKills { get; set; } = 0;

        public int ServerKillGoal { get; set; } = 100;
    }
    public class WolfKillCounterModSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        IServerNetworkChannel serverChannel;
        private string SaveFilePath => sapi.GetOrCreateDataPath("wolfkills.json");

        // List of players and their total wolf kills from the first startup of this mod.
        Dictionary<string, int[]> wolfKillCount = new Dictionary<string, int[]>();
        Dictionary<string, int> currentLeaderboard = new Dictionary<string, int>();
        int totalWolfKillCount = 0;

        // Server kill goal
        int serverKillGoal = 0;
        
        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            Mod.Logger.Notification(": " + api.Side);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Mod.Logger.Notification(": " + Lang.Get("wolfkillcounter:version"));
            sapi = api;

            sapi.Logger.Notification($"Wolf Kill Counter: {SaveFilePath}");
            LoadWolfKillData();
            LoadLeaderboard();

            // List Leaderboard Command
            api.ChatCommands.Create("listWolfKills")
                .WithDescription("List the top 5 wolf killers")
                .RequiresPrivilege(Privilege.chat)
                .WithAlias("lwk")
                .HandleWith(ListWolfKills);

            // Reset Leaderboard Command
            api.ChatCommands.Create("resetWolfLeaderboard")
                .WithDescription("Resets wolf leaderboard without affecting total kills.")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args => ResetLeaderboardCommand(args, sapi));

            // Display Server Goal Command
            api.ChatCommands.Create("serverKillGoal")
                .WithDescription("Displays the server's kill goal.")
                .RequiresPrivilege(Privilege.chat)
                .WithAlias("skg")
                .HandleWith(args => DisplayServerGoal(args, sapi));

            // Display Player Goal Command
            api.ChatCommands.Create("playerKillGoal")
                .WithDescription("Displays your kill goal.")
                .RequiresPrivilege(Privilege.chat)
                .WithAlias("pkg")
                .HandleWith(args => DisplayPlayerGoal(args, sapi));

            // Add function handler to trigger (function call) when an entity dies.
            api.Event.OnEntityDeath += OnEntityDeath;
            api.Event.SaveGameLoaded += LoadWolfKillData;
            api.Event.GameWorldSave += SaveWolfKillData;
        }

        //public override void StartClientSide(ICoreClientAPI api)
        //{
        //    Mod.Logger.Notification("Hello from template mod client side: " + Lang.Get("wolfkillcounter:hello"));
        //}

        // function handler to catch entity death event.
        private void OnEntityDeath(Entity entity, DamageSource source)
        {
            //Console.WriteLine(entity.Code.Path);
            if (entity.Code.Path.Contains("wolf"))
            {
                totalWolfKillCount++;
                string playerName = null;
                Console.WriteLine(playerName);

                // Check for source of killer (should be a player), get their player name and increment their kill count.
                if (source?.SourceEntity is EntityPlayer player)
                    playerName = player.Player.PlayerName;
                else if (source?.CauseEntity is EntityPlayer causePlayer)
                    playerName = causePlayer.Player.PlayerName;
                if (wolfKillCount.ContainsKey(playerName))
                {
                    wolfKillCount[playerName][0]++;
                    if (currentLeaderboard.ContainsKey(playerName))
                    {
                        currentLeaderboard[playerName]++;
                    }
                    else
                    {
                        currentLeaderboard.Add(playerName, 1);
                    }
                }
                else if (playerName != null)
                {
                    wolfKillCount.Add(playerName, new int[]{ 1, 50 });
                    currentLeaderboard.Add(playerName, 1);
                }

                // Check if the server kill goal has been reached and broadcast a message to all players.
                if (totalWolfKillCount == serverKillGoal)
                {
                    serverChannel.BroadcastPacket(ServerKillGoal(serverKillGoal * 2));
                    serverKillGoal *= 2;
                }

                // Check if the player has reached their personal kill goal and broadcast a message to all players.
                if (wolfKillCount[playerName][0] == wolfKillCount[playerName][1])
                {
                    int newGoal = CalculateGoal(wolfKillCount[playerName][0]);
                    serverChannel.BroadcastPacket(PlayerKillGoal(playerName, newGoal));
                    wolfKillCount[playerName][1] *= 2;
                }
            }
        }

        // Load the saved data from the json file
        private void LoadWolfKillData()
        {
            // If the old json file exists, get the data and switch to new saving format
            if (sapi.GetOrCreateDataPath("wolfkills.json") != null) 
            {
                // Get the file data
                var data = sapi.LoadModConfig<WolfKillData>("wolfkills.json");
                
                // Load Leaderboard and TotalKills
                currentLeaderboard = data.Leaderboard;
                totalWolfKillCount = data.TotalKills;
                serverKillGoal = CalculateGoal(totalWolfKillCount);

                // Load KillCounts from the old format and convert it to the new format
                if (data.KillCounts != null)
                {
                    foreach (var kvp in data.KillCounts)
                    {
                        wolfKillCount.Add(kvp.Key, new int[] { kvp.Value, CalculateGoal((int)kvp.Value)});
                    }
                }

                sapi.Logger.Notification("[WolfKillCounter]: KillCounts parsed and migrated if needed.");
                
                // Try to Delete the JSON file, otherwise Error.
                try 
                {
                    System.IO.File.Delete(SaveFilePath);
                }
                catch (Exception error) 
                {
                    sapi.Logger.Error($"[WolfKillCounter] - Error: File not deleted! Make sure the JSON file is manually deleted.\n{error}");
                }
                sapi.Logger.Notification("[WolfKillCounter]: Deleted old json file for SaveData Migration.");
            }
            // New SaveGame format if data exists already, get and load the data
            else if (sapi.WorldManager.SaveGame.GetData("wolfkilldata") is Byte[] rawData)
            {
                // Read raw JSON from the file
                rawData = sapi.WorldManager.SaveGame.GetData("wolfkilldata");
                WolfKillData parsedData = rawData == null ? new WolfKillData() : SerializerUtil.Deserialize<WolfKillData>(rawData);
                
                // Load TotalKills and ServerKillGoal
                totalWolfKillCount = parsedData.TotalKills;
                serverKillGoal = parsedData.ServerKillGoal;

                // Load Leaderboard
                currentLeaderboard = parsedData.Leaderboard;

                // Load KillCounts with backward compatibility for the old format
                wolfKillCount = parsedData.NewKillCounts;
            }
            // No SaveGame data exists, create new save data
            else
            {
                // Fresh WolfKillData
                var data = new WolfKillData()
                {
                    NewKillCounts = new Dictionary<string, int[]>(),
                    Leaderboard = new Dictionary<string, int>(),
                    TotalKills = 0,
                    ServerKillGoal = 100
                };

                // Store the fresh data
                sapi.WorldManager.SaveGame.StoreData("wolfkilldata", data);
                sapi.Logger.Notification("[WolfKillCounter]: Created new save data in SaveGame");
            }
                sapi.Logger.Notification("[WolfKillCounter]: Save Loaded.");
        }
           
        // Helper function to calculate the next goal for a player after new format was created.
        private int CalculateGoal(int kills)
        {
            const int defaultGoal = 50;
            // Round up to the next multiple of 50 greater than kills
            return ((kills + defaultGoal - 1) / defaultGoal) * defaultGoal;
        }

        // Save the current kill data to the json file
        private void SaveWolfKillData()
        {
            var data = new WolfKillData()
            {
                NewKillCounts = wolfKillCount,
                Leaderboard = currentLeaderboard,
                TotalKills = totalWolfKillCount,
                ServerKillGoal = serverKillGoal
            };

            sapi.WorldManager.SaveGame.StoreData("wolfkilldata", data);
            //sapi.StoreModConfig(data, "wolfkills.json");
            sapi.Logger.Notification("WolfKillCounter: Saved kill data.");
        }

        // Command function to print the Wolf Kills Leaderboard
        private TextCommandResult ListWolfKills(TextCommandCallingArgs args)
        {
            string playerName = args.Caller.Player.PlayerName;
            Mod.Logger.Notification($"{playerName}: Printing Wolf Kill List Top 5");

            return TextCommandResult.Success(PrintList(playerName));
        }

        // Command function to reset the Wolf Kills Leaderboard
        private TextCommandResult ResetLeaderboardCommand(TextCommandCallingArgs args, ICoreAPI api)
        {
            currentLeaderboard.Clear();
            SaveWolfKillData();

            return TextCommandResult.Success("Wolf kill leaderboard has been reset. Total kill count remains unchanged.");
        }

        private TextCommandResult DisplayServerGoal(TextCommandCallingArgs args, ICoreAPI api)
        {
            return TextCommandResult.Success($"Server Kill Goal: {serverKillGoal}.\n" +
                $"Server's Total Kills: {totalWolfKillCount}");
        }

        private TextCommandResult DisplayPlayerGoal(TextCommandCallingArgs args, ICoreAPI api)
        {
            string playerName = args.Caller.Player.PlayerName;
            Mod.Logger.Notification($"{playerName}: Printing personal kill goal.");

            return TextCommandResult.Success($"{playerName}'s Kill Goal: {wolfKillCount[playerName][1]}.\n" +
                $"Your kills: {wolfKillCount[playerName][0]}");
        }

        // Helper function to create the list string by sorting the dictionary and iterating through the top 5 elements in sortedDict.
        private string PrintList(string playerName)
        {
            string list = $"WOLF EXTERMINATION LEADERBOARD\n";
            list += "=================================\n";

            int position = 1;

            foreach (var pair in GetTopFive(currentLeaderboard))
            {
                list += $"{position++}. {pair.Key}: {pair.Value} kills\n";
            }

            list += "\n--------------------------------------------------------\n";
            list += $"Total Wolf Kills: {totalWolfKillCount}\n";
            list += $"Your Kills: {(wolfKillCount.ContainsKey(playerName) ? wolfKillCount[playerName][0] : 0)}\n";
            list += "=================================\n";
            return list;
        }

        // Helper function to get the top 5 players from the current leaderboard
        private Dictionary<string, int> GetTopFive(Dictionary<string, int> leaderboard)
        {
            return leaderboard.OrderByDescending(x => x.Value).Take(5).ToDictionary(x => x.Key, x => x.Value);
        }

        private void LoadLeaderboard()
        {
            if (sapi.LoadModConfig<WolfKillData>("wolfkills.json") is WolfKillData data)
            {
                currentLeaderboard = data.Leaderboard ?? wolfKillCount
                    .OrderByDescending(x => x.Value[0]) // Use kills (index 0) for sorting
                    .Take(5)
                    .ToDictionary(x => x.Key, x => x.Value[0]); // Use kills (index 0) as value
                sapi.Logger.Notification("WolfKillCounter: Loaded saved leaderboard data.");
            }
            else
            {
                currentLeaderboard = new Dictionary<string, int>();
                sapi.Logger.Notification("WolfKillCounter: No existing leaderboard data found. Starting fresh.");
            }
        }

        // Function to check if total server kills reached a certain point
        private string ServerKillGoal(int newGoal)
        {
            string message = " *** WOLF SLAYERS UNITE! ***\n";
            message += "  -----------------------------------\n";
            message += $"| {serverKillGoal} reached!          |\n";
            message +=  "| Server Pack Triumphs!              |\n";
            message += $"| New server goal: {newGoal} kills!  |\n";
            message += "  -----------------------------------\n";

            return message;
        }

        private string PlayerKillGoal(string playerName, int newGoal)
        {
            string message = " *** WOLF HUNTER EXTRAORDINAIRE! ***\n";
            message += "  ---------------------------------------------\n";
            message += $"| {playerName} surpassed {newGoal / 2} kills!  |\n";
            message += $"| New personal goal: {newGoal} kills!          |\n";
            message += "  ---------------------------------------------\n";

            return message;
        }
    }
}
