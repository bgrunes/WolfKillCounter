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

namespace WolfKillCounter
{
    public class WolfKillData
    {
        // Players and their wolf kill counts
        public Dictionary<string, int[]> KillCounts { get; set; } = new Dictionary<string, int[]>();

        // Leaderboard of players and their wolf kill counts
        public Dictionary<string, int> Leaderboard { get; set; } = new Dictionary<string, int>();

        // Total wolf kills by all players
        public int TotalKills { get; set; } = 0;

        public int ServerKillGoal { get; set; } = 100;
    }
    public class WolfKillCounterModSystem : ModSystem
    {
        private ICoreAPI sapi;
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
            Mod.Logger.Notification("Wolf Kill Counter - " + api.Side);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            LoadWolfKillData();

            // Register command with API
            api.ChatCommands.Create("listWolfKills")
                .WithDescription("List the top 5 wolf killers")
                .RequiresPrivilege(Privilege.chat)
                .WithAlias("lwk")
                .HandleWith(ListWolfKills);

            // Reset Leaderboard command
            api.ChatCommands.Create("resetWolfLeaderboard")
                .WithDescription("Resets wolf leaderboard without affecting total kills.")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args => ResetLeaderboardCommand(args, sapi));

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
                    currentLeaderboard[playerName] = wolfKillCount[playerName][0];
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
            // Read raw JSON from the file
            string rawJson = System.IO.File.ReadAllText(SaveFilePath);
            var parsedJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(rawJson);

            // Initialize data structures
            wolfKillCount = new Dictionary<string, int[]>();
            currentLeaderboard = new Dictionary<string, int>();
            totalWolfKillCount = 0;
            serverKillGoal = 100;

            // Load TotalKills and ServerKillGoal
            if (parsedJson.TryGetValue("TotalKills", out object totalKillsObj) && totalKillsObj is long tk)
            {
                totalWolfKillCount = (int)tk;
            }

            if (parsedJson.TryGetValue("ServerKillGoal", out object goalObj) && goalObj is long goal)
            {
                serverKillGoal = (int)goal;
            }

            // Load Leaderboard
            if (parsedJson.TryGetValue("Leaderboard", out object leaderboardObj))
            {
                var leaderboardJson = leaderboardObj as JObject;
                if (leaderboardJson != null)
                {
                    foreach (var kvp in leaderboardJson)
                    {
                        currentLeaderboard[kvp.Key] = kvp.Value.ToObject<int>();
                    }
                }
            }

            // Load KillCounts with backward compatibility for the old format
            if (parsedJson.TryGetValue("KillCounts", out object killCountsObj))
            {
                var killCountsJson = killCountsObj as JObject;
                if (killCountsJson != null)
                {
                    foreach (var kvp in killCountsJson)
                    {
                        // Handle old format (just an integer for kills)
                        if (kvp.Value.Type == JTokenType.Integer)
                        {
                            int kills = kvp.Value.ToObject<int>();
                            int nextGoal = CalculateGoal(kills);  // Calculate next goal for the player
                            wolfKillCount[kvp.Key] = new int[] { kills, nextGoal };
                        }
                        // Handle the new format (array with kills and goal)
                        else if (kvp.Value.Type == JTokenType.Array)
                        {
                            int[] values = kvp.Value.ToObject<int[]>();
                            int kills = values.Length > 0 ? values[0] : 0;
                            int nextGoal = values.Length > 1 ? values[1] : CalculateGoal(kills);
                            wolfKillCount[kvp.Key] = new int[] { kills, nextGoal };
                        }
                    }

                    sapi.Logger.Notification("WolfKillCounter: KillCounts parsed and migrated if needed.");
                }
            }
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
                KillCounts = wolfKillCount,
                Leaderboard = currentLeaderboard,
                TotalKills = totalWolfKillCount,
                ServerKillGoal = serverKillGoal
            };

            sapi.StoreModConfig(data, "wolfkills.json");
            sapi.Logger.Notification("WolfKillCounter: Saved kill data.");
        }

        // Command function to print the Wolf Kills Leaderboard
        private TextCommandResult ListWolfKills(TextCommandCallingArgs args)
        {
            string playerName = args.Caller.Player.PlayerName;
            Mod.Logger.Notification($"{playerName}: Printing Wolf Kill List Top 5");
            Mod.Logger.Notification(wolfKillCount.ToString());

            return TextCommandResult.Success(PrintList(playerName));
        }

        // Command function to reset the Wolf Kills Leaderboard
        private TextCommandResult ResetLeaderboardCommand(TextCommandCallingArgs args, ICoreAPI api)
        {
            currentLeaderboard.Clear();
            SaveWolfKillData();

            return TextCommandResult.Success("Wolf kill leaderboard has been reset. Total kill count remains unchanged.");
        }

        // Helper function to create the list string by sorting the dictionary and iterating through the top 5 elements in sortedDict.
        private string PrintList(string playerName)
        {
            string list = $"WOLF EXTERMINATION LEADERBOARD\n";
            list +=        "=================================\n";

            int position = 1;
            var topFive = GetTopFive(currentLeaderboard);

            foreach (var pair in topFive) 
            {
                list += $"{position++}. {pair.Key}: {pair.Value} kills\n";
            }

            list += "\n--------------------------------------------------------\n";
            list += $"Total Wolf Kills: {totalWolfKillCount}\n";
            list += $"Your Kills: {(wolfKillCount.ContainsKey(playerName) ? wolfKillCount[playerName] : 0)}\n";
            list += "=================================\n";
            return list;
        }

        // Helper function to get the top 5 players from the current leaderboard
        private Dictionary<string, int> GetTopFive(Dictionary<string, int> leaderboard)
        {
            return leaderboard.OrderByDescending(x => x.Value).Take(5).ToDictionary(x => x.Key, x => x.Value);
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
