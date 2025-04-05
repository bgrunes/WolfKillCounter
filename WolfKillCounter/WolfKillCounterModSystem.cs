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
    
    public class KillCountData
    {
        public int Kills { get; set; } = 0;
        public int Goal { get; set; } = 50;
        public int Deaths { get; set; } = 0;
    }
    public class WolfKillData
    {
        // Players and their wolf kill counts
        public Dictionary<string, int> KillCounts { get; set; } = new Dictionary<string, int>();

        // Old Dictionary format, REFORMAT USE ONLY
        public Dictionary<string, KillCountData> NewKillCounts { get; set; } = new Dictionary<string, KillCountData>();

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
        Dictionary<string, KillCountData> wolfKillCount = new Dictionary<string, KillCountData>();
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
            serverKillGoal = 10;
            totalWolfKillCount = 9;

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
                EntityPlayer sourcePlayer = null;
                Console.WriteLine(playerName);

                // Check for source of killer (should be a player), get their player name and increment their kill count.
                if (source?.SourceEntity is EntityPlayer player)
                {
                    playerName = player.Player.PlayerName;
                    sourcePlayer = player;
                }
                else if (source?.CauseEntity is EntityPlayer causePlayer)
                    playerName = causePlayer.Player.PlayerName;
                if (wolfKillCount.ContainsKey(playerName))
                {
                    wolfKillCount[playerName].Kills++;
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
                    wolfKillCount.Add(playerName, new KillCountData{ Kills = 1, Goal = 50, Deaths = 0 });
                    currentLeaderboard.Add(playerName, 1);
                }

                // Check if the server kill goal has been reached and broadcast a message to all players.
                if (totalWolfKillCount == serverKillGoal)
                {
                    BroadcastMessage(ServerKillGoal(serverKillGoal * 2));
                    serverKillGoal *= 2;
                }

                // Check if the player has reached their personal kill goal and broadcast a message to all players.
                if (wolfKillCount[playerName].Kills == wolfKillCount[playerName].Goal)
                {
                    int newGoal = CalculateGoal(wolfKillCount[playerName].Goal);
                    BroadcastMessage(PlayerKillGoal(playerName, newGoal), sourcePlayer.Player);
                    wolfKillCount[playerName].Goal *= 2;
                }
            }
            else if (entity.Code.Path == "player")
            {
                if ((source.SourceEntity.Code.Path.Contains("wolf") || source.CauseEntity.Code.Path.Contains("wolf")) && entity is EntityPlayer player)
                {
                    string playerName = player.Player.PlayerName;

                    wolfKillCount[playerName].Deaths++;
                    Mod.Logger.Notification("" + wolfKillCount[playerName].Deaths);
                    Mod.Logger.Notification($"{playerName} has died to a Wolf! Skill Issue.\n");
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
                        wolfKillCount.Add(kvp.Key, new KillCountData{ Kills = kvp.Value, Goal = CalculateGoal((int)kvp.Value), Deaths = 0});
                    }
                }

                sapi.Logger.Notification("[WolfKillCounter]: KillCounts parsed and migrated if needed.");
                
                // Try to Delete the JSON file, otherwise Error.
                try 
                {
                    System.IO.File.Delete(SaveFilePath);
                }
                catch (Exception)
                {
                    sapi.Logger.Error($"[WolfKillCounter] - Error: File not deleted! Make sure the JSON file is manually deleted.\n");
                }
                sapi.Logger.Notification("[WolfKillCounter]: Deleted old json file for SaveData Migration.");
            }
            // New SaveGame format if data exists already, get and load the data
            else if (sapi.WorldManager.SaveGame.GetData("wolfkilldata") is Byte[] rawData)
            {
                // Read raw JSON from the file
                rawData = sapi.WorldManager.SaveGame.GetData("wolfkilldata");
                // Replace the line causing the error with the following line
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
                    NewKillCounts = new Dictionary<string, KillCountData>(),
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

        // Helper function to calculate the kill/death ratio of a player
        private string CalculateKD(KillCountData KDlist)
        {
            double kd = 0.0;

            // If the player has not died to a wolf yet, no KD.
            if (KDlist.Deaths == 0)
            {
                return "∞";
            }
            kd =(double) (KDlist.Kills / KDlist.Deaths);
            return System.String.Format("{0:F2}", kd);
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

            return TextCommandResult.Success($"{playerName}'s Kill Goal: {wolfKillCount[playerName].Goal}.\n" +
                $"Your kills: {wolfKillCount[playerName].Kills}");
        }

        // Helper function to create the list string by sorting the dictionary and iterating through the top 5 elements in sortedDict.
        private string PrintList(string playerName)
        {
            string list = $"WOLF EXTERMINATION LEADERBOARD\n";
            list += "=================================\n";

            int position = 1;

            foreach (var pair in GetTopFive(currentLeaderboard))
            {
                list += $"{position++}. {pair.Key}: {pair.Value} kills, {wolfKillCount[pair.Key].Deaths} Deaths, KD: {CalculateKD(wolfKillCount[pair.Key])} \n";
            }

            list += "\n--------------------------------------------------------\n";
            list += $"Total Wolf Kills: {totalWolfKillCount}\n";
            list += $"Your Kills: {(wolfKillCount.ContainsKey(playerName) ? wolfKillCount[playerName].Kills : 0)}\n";
            list += $"Deaths by Wolf: {wolfKillCount[playerName].Deaths}\n";
            list += $"KD: {CalculateKD(wolfKillCount[playerName])}\n";
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
                    .OrderByDescending(x => x.Value.Kills) // Use kills (index 0) for sorting
                    .Take(5)
                    .ToDictionary(x => x.Key, x => x.Value.Kills); // Use kills (index 0) as value
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
            message += $"| {serverKillGoal} reached!            |\n";
            message +=  "| Server Pack Triumphs!             |\n";
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

        private void BroadcastMessage(string message)
        {
            sapi.SendMessageToGroup(
                GlobalConstants.GeneralChatGroup,
                message,
                EnumChatType.Notification
            );
        }

        private void BroadcastMessage(string message, IPlayer player)
        {
            sapi.SendMessage(
                player,
                0,
                message,
                EnumChatType.OwnMessage
            );
        }
    }
}
