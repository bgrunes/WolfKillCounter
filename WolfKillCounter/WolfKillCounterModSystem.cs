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
        public Dictionary<string, int> KillCounts { get; set; } = new Dictionary<string, int>();

        // Total wolf kills by all players
        public int TotalKills { get; set; } = 0;
    }
    public class WolfKillCounterModSystem : ModSystem
    {
        private ICoreAPI sapi;
        private string SaveFilePath => sapi.GetOrCreateDataPath("wolfkills.json");


        // List of players and their total wolf kills from the first startup of this mod.
        Dictionary<string, int> wolfKillCount = new Dictionary<string, int>();
        int totalWolfKillCount = 0;
        


        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            Mod.Logger.Notification("Hello from template mod: " + api.Side);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            LoadWolfKillData();

            Mod.Logger.Notification("Hello from template mod server side: " + Lang.Get("wolfkillcounter:hello"));

            // Register command with API
            api.ChatCommands.Create("listWolfKills")
                .WithDescription("List the top 5 wolf killers")
                .RequiresPrivilege(Privilege.chat)
                .WithAlias("lwk")
                .HandleWith(listWolfKills);

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

                if (source?.SourceEntity is EntityPlayer player)
                    playerName = player.Player.PlayerName;
                else if (source?.CauseEntity is EntityPlayer causePlayer)
                    playerName = causePlayer.Player.PlayerName;
                if (wolfKillCount.ContainsKey(playerName))
                {
                    wolfKillCount[playerName]++;
                }
                else if (playerName != null)
                {
                    wolfKillCount.Add(playerName, 1);
                }
            }
        }

        private void LoadWolfKillData()
        {
            if (sapi.LoadModConfig<WolfKillData>("wolfkills.json") is WolfKillData data)
            {
                wolfKillCount = data.KillCounts ?? new Dictionary<string, int>();
                totalWolfKillCount = data.TotalKills;
                sapi.Logger.Notification("WolfKillCounter: Loaded saved kill data.");
            }
            else
            {
                wolfKillCount = new Dictionary<string, int>();
                totalWolfKillCount = 0;
                sapi.Logger.Notification("WolfKillCounter: No existing data found. Starting fresh.");
            }
        }

        private void SaveWolfKillData()
        {
            var data = new WolfKillData()
            {
                KillCounts = wolfKillCount,
                TotalKills = totalWolfKillCount
            };

            sapi.StoreModConfig(data, "wolfkills.json");
            sapi.Logger.Notification("WolfKillCounter: Saved kill data.");
        }

        // Command function to print the Wolf Kills Leaderboard
        private TextCommandResult listWolfKills(TextCommandCallingArgs args)
        {
            string playerName = args.Caller.Player.PlayerName;
            Mod.Logger.Notification($"{playerName}: Printing Wolf Kill List Top 5");
            Mod.Logger.Notification(wolfKillCount.ToString());

            return TextCommandResult.Success(printList());
        }

        // Helper function to create the list string by sorting the dictionary and iterating through the top 5 elements in sortedDict.
        private string printList()
        {
            string list = $"Wolf Kill Leaderboard\n";
            list +=        "--------------------------\n";

            int position = 1;
            var sortedDict = wolfKillCount
                .OrderByDescending(pair => pair.Value)
                .Take(5)
                .ToList();

            foreach (var pair in sortedDict) 
            {
                list += $"{position}. {pair.Key}: {pair.Value} kills\n";
            }

            list += $"Total Wolf Kills: {totalWolfKillCount}\n";
            return list;
        }
    }
}
