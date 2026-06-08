using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using WolfKillCounter.Config;
using WolfKillCounter.Commands;

namespace WolfKillCounter
{


    public class WolfKillCounterModSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        IServerNetworkChannel serverChannel;
        WolfKillCounterConfig config;
        public WolfKillData Data;
        
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
            // Get ModSystem Config 
            config = new WolfKillCounterConfig(api);

            config.LoadWolfKillData(ref Data);
            //LoadLeaderboard();

            // Add function handler to trigger (function call) when an entity dies.
            api.Event.OnEntityDeath += OnEntityDeath;
            api.Event.PlayerJoin += OnPlayerJoin;
            // Update the event subscription to use a lambda function that calls the LoadWolfKillData method
            //api.Event.SaveGameLoaded += config.LoadWolfKillData;
            api.Event.GameWorldSave += config.SaveWolfKillData;
            
            Commands.Commands.RegisterCommands(api);
        }

        //public override void StartClientSide(ICoreClientAPI api)
        //{
        //    Mod.Logger.Notification("Hello from template mod client side: " + Lang.Get("wolfkillcounter:hello"));
        //}

        // function handler to catch entity death event.
        private void OnEntityDeath(Entity entity, DamageSource source)
        {
            try
            {
                if (entity.Code.Path.Contains("wolf"))
                {
                    // Debug logging for DamageSource
                    if (source == null || (source.SourceEntity == null && source.CauseEntity == null))
                    {
                        Mod.Logger.Debug("DamageSource is null for entity death.");
                        return;
                    }

                    Data.TotalKills++;
                    string playerName = null;
                    EntityPlayer sourcePlayer = null;

                    // Check for source of killer (should be a player), get their player name and increment their kill count.
                    if (source?.SourceEntity is EntityPlayer player)
                    {
                        playerName = player.Player.PlayerName;
                        sourcePlayer = player;
                    }
                    else if (source?.CauseEntity is EntityPlayer causePlayer)
                    {
                        playerName = causePlayer.Player.PlayerName;
                    }

                    if (playerName != null)
                    {
                        if (Data.NewKillCounts.TryGetValue(playerName, out var count))
                        {
                            count.Kills++;
                            if (Data.Leaderboard.ContainsKey(playerName))
                            {
                                Data.Leaderboard[playerName]++;
                            }
                            else
                            {
                                Data.Leaderboard.Add(playerName, 1);
                            }
                        }
                        else
                        {
                            Data.NewKillCounts.Add(playerName, new KillCountData { Kills = 1, Goal = 50, Deaths = 0 });
                            Data.Leaderboard.Add(playerName, 1);
                        }

                        // Check if the server kill goal has been reached and broadcast a message to all players.
                        if (Data.TotalKills == Data.ServerKillGoal)
                        {
                            BroadcastMessage(ServerKillGoal(Data.ServerKillGoal * 2));
                            Data.ServerKillGoal *= 2;
                        }

                        // Check if the player has reached their personal kill goal and broadcast a message to all players.
                        if (Data.NewKillCounts[playerName].Kills == Data.NewKillCounts[playerName].Goal)
                        {
                            int newGoal = CalculateGoal(Data.NewKillCounts[playerName].Goal);
                            BroadcastMessage(PlayerKillGoal(playerName, newGoal), sourcePlayer?.Player);
                            Data.NewKillCounts[playerName].Goal *= 2;
                        }
                    }

                    // Update Server description with Total Wolf kills
                }
                else if (source != null && entity.Code.Path == "player" && entity is EntityPlayer player && (source.SourceEntity.Code.Path.Contains("wolf") || source.CauseEntity.Code.Path.Contains("wolf")))
                {
                    string playerName = player.Player.PlayerName;

                    Mod.Logger.Notification("" + Data.NewKillCounts[playerName].Deaths);
                    Mod.Logger.Notification($"{playerName} has died to a Wolf! Skill Issue.\n");
                    Mod.Logger.Notification($"Total deaths to wolves: {Data.NewKillCounts[playerName].Deaths}");
                    // Ensure the player is in the wolfKillCount dictionary
                    if (!Data.NewKillCounts.ContainsKey(playerName))
                    {
                        Data.NewKillCounts.Add(playerName, new KillCountData { Kills = 0, Goal = 50, Deaths = 0 });
                    }

                    // Check if the death was caused by a wolf
                    Data.NewKillCounts[playerName].Deaths++;
                    Mod.Logger.Notification($"{playerName} has died to a Wolf! Skill Issue. Total deaths to wolves: {Data.NewKillCounts[playerName].Deaths}");
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"WolfKillCounter: Error in OnEntityDeath: {ex.Message}\n");
            }
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            // Check if the player is already in the wolfKillCount dictionary
            string playerName = player.PlayerName;

            if (!Data.NewKillCounts.ContainsKey(playerName))
            {
                Data.NewKillCounts.Add(playerName, new KillCountData { Kills = 0, Goal = 50, Deaths = 0 });
                Mod.Logger.Notification($"{playerName} has been added to the Dictionary.\n");
            }
        }

        
           
        // Helper function to calculate the next goal for a player after new format was created.
        public int CalculateGoal(int kills)
        {
            const int defaultGoal = 50;
            // Round up to the next multiple of 50 greater than kills
            return ((kills + defaultGoal - 1) / defaultGoal) * defaultGoal;
        }

        // Helper function to calculate the kill/death ratio of a player
        public string CalculateKD(KillCountData KDlist)
        {
            double kd = 0.0;

            // If the player has not died to a wolf yet, no KD.
            if (KDlist.Deaths == 0)
            {
                return "INFINITE";
            }
            kd = ((double) KDlist.Kills / (double) KDlist.Deaths);
            return System.String.Format("{0:F2}", kd);
        }  

        // Likely unnecessary now, but commented out just in case.
        // private void LoadLeaderboard()
        // {
        //     if (sapi.LoadModConfig<WolfKillData>("wolfkills.json") is WolfKillData data)
        //     {
        //         currentLeaderboard = data.Leaderboard ?? wolfKillCount
        //             .OrderByDescending(x => x.Value.Kills) // Use kills (index 0) for sorting
        //             .Take(5)
        //             .ToDictionary(x => x.Key, x => x.Value.Kills); // Use kills (index 0) as value
        //         sapi.Logger.Notification("WolfKillCounter: Loaded saved leaderboard data.");
        //     }
        //     else
        //     {
        //         currentLeaderboard = new Dictionary<string, int>();
        //         sapi.Logger.Notification("WolfKillCounter: No existing leaderboard data found. Starting fresh.");
        //     }
        // }

        // Function to check if total server kills reached a certain point
        private string ServerKillGoal(int newGoal)
        {
            string message = " *** WOLF SLAYERS UNITE! ***\n";
            message += "  -----------------------------------\n";
            message += $"| {Data.ServerKillGoal} reached!            |\n";
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

        // Function to add a player to the dictionary
        public void AddPlayer(string playerName, KillCountData data)
        {
            if (!Data.NewKillCounts.ContainsKey(playerName))
            {
                Data.NewKillCounts.Add(playerName, data);
                Mod.Logger.Notification($"{playerName} has been added to the Dictionary.\n");
            }
        }

        // Getters for External use
        public Dictionary<string, KillCountData> GetWolfKillCount() { return Data.NewKillCounts; }
        public Dictionary<string, int> GetCurrentLeaderboard() { return Data.Leaderboard; }
        public int GetTotalWolfKillCount() { return Data.TotalKills; }
        public int GetServerKillGoal() { return Data.ServerKillGoal; }
        public WolfKillCounterConfig GetConfig() { return config; }

        // Setters for External use
        public void SetWolfKillCount(Dictionary<string, KillCountData> newWolfKillCount) { Data.NewKillCounts = newWolfKillCount; }
        public void SetCurrentLeaderboard(Dictionary<string, int> newLeaderboard) { Data.Leaderboard = newLeaderboard; }
        public void SetTotalWolfKillCount(int newTotalWolfKillCount) { Data.TotalKills = newTotalWolfKillCount; }
        public void SetServerKillGoal(int newServerKillGoal) { Data.ServerKillGoal = newServerKillGoal; }
    }
}
