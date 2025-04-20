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
using ProtoBuf;
using WolfKillCounter.Config;

namespace WolfKillCounter
{


    public class WolfKillCounterModSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        IServerNetworkChannel serverChannel;
        WolfKillCounterConfig config;

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
            // Get ModSystem Config 
            config = new WolfKillCounterConfig();

            config.LoadWolfKillData(sapi);
            LoadLeaderboard();

            // Add function handler to trigger (function call) when an entity dies.
            api.Event.OnEntityDeath += OnEntityDeath;
            api.Event.PlayerJoin += OnPlayerJoin;
            // Update the event subscription to use a lambda function that calls the LoadWolfKillData method
            api.Event.SaveGameLoaded += () => config.LoadWolfKillData(sapi);
            api.Event.GameWorldSave += () => config.SaveWolfKillData(sapi);
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

                    totalWolfKillCount++;
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
                        else
                        {
                            wolfKillCount.Add(playerName, new KillCountData { Kills = 1, Goal = 50, Deaths = 0 });
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
                            BroadcastMessage(PlayerKillGoal(playerName, newGoal), sourcePlayer?.Player);
                            wolfKillCount[playerName].Goal *= 2;
                        }
                    }

                    // Update Server description with Total Wolf kills
                }
                else if (source != null && entity.Code.Path == "player" && entity is EntityPlayer player && (source.SourceEntity.Code.Path.Contains("wolf") || source.CauseEntity.Code.Path.Contains("wolf")))
                {
                    string playerName = player.Player.PlayerName;

                    Mod.Logger.Notification("" + wolfKillCount[playerName].Deaths);
                    Mod.Logger.Notification($"{playerName} has died to a Wolf! Skill Issue.\n");
                    Mod.Logger.Notification($"Total deaths to wolves: {wolfKillCount[playerName].Deaths}");
                    // Ensure the player is in the wolfKillCount dictionary
                    if (!wolfKillCount.ContainsKey(playerName))
                    {
                        wolfKillCount.Add(playerName, new KillCountData { Kills = 0, Goal = 50, Deaths = 0 });
                    }

                    // Check if the death was caused by a wolf
                    wolfKillCount[playerName].Deaths++;
                    Mod.Logger.Notification($"{playerName} has died to a Wolf! Skill Issue. Total deaths to wolves: {wolfKillCount[playerName].Deaths}");
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

            if (!wolfKillCount.ContainsKey(playerName))
            {
                wolfKillCount.Add(playerName, new KillCountData { Kills = 0, Goal = 50, Deaths = 0 });
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
                return "∞";
            }
            kd = ((double) KDlist.Kills / (double) KDlist.Deaths);
            return System.String.Format("{0:F2}", kd);
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

        // Function to add a player to the dictionary
        public void AddPlayer(string playerName, KillCountData data)
        {
            if (!wolfKillCount.ContainsKey(playerName))
            {
                wolfKillCount.Add(playerName, data);
                Mod.Logger.Notification($"{playerName} has been added to the Dictionary.\n");
            }
        }

        // Getters for External use
        public Dictionary<string, KillCountData> GetWolfKillCount() { return wolfKillCount; }
        public Dictionary<string, int> GetCurrentLeaderboard() { return currentLeaderboard; }
        public int GetTotalWolfKillCount() { return totalWolfKillCount; }
        public int GetServerKillGoal() { return serverKillGoal; }
        public WolfKillCounterConfig GetConfig() { return config; }

        // Setters for External use
        public void SetWolfKillCount(Dictionary<string, KillCountData> newWolfKillCount) { wolfKillCount = newWolfKillCount; }
        public void SetCurrentLeaderboard(Dictionary<string, int> newLeaderboard) { currentLeaderboard = newLeaderboard; }
        public void SetTotalWolfKillCount(int newTotalWolfKillCount) { totalWolfKillCount = newTotalWolfKillCount; }
        public void SetServerKillGoal(int newServerKillGoal) { serverKillGoal = newServerKillGoal; }
    }
}
