using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Common;
using WolfKillCounter.Config;

namespace WolfKillCounter.Commands
{
    internal class Commands
    {
        private static ICoreServerAPI sapi;
        public static void RegisterCommands(ICoreServerAPI api)
        {
            sapi = api;

            // List Leaderboard Command
            sapi.ChatCommands.Create("wkc")
                .WithDescription("Provides the Wolf Kill Counter commands")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => HandleWKCCommand(args, sapi))
                // List Wolf Kills Subcommand
                .BeginSubCommand("listWolfKills")
                .WithDescription("List the top 5 wolf killers")
                .RequiresPrivilege(Privilege.chat)
                .WithAlias("lwk")
                .HandleWith(args => ListWolfKills(args, sapi))
                .EndSubCommand()
                // Reset Leaderboard Command
                .BeginSubCommand("resetWolfLeaderboard")
                .WithDescription("Resets wolf leaderboard without affecting total kills.")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args => ResetLeaderboardCommand(args, sapi))
                .EndSubCommand()
                // Display Server Goal Command
                .BeginSubCommand("serverKillGoal")
                .WithDescription("Displays the server's kill goal.")
                .RequiresPrivilege(Privilege.chat)
                .WithAlias("skg")
                .HandleWith(args => DisplayServerGoal(args, sapi))
                .EndSubCommand()
                // Display Player Goal Command
                .BeginSubCommand("playerKillGoal")
                .WithDescription("Displays your kill goal.")
                .RequiresPrivilege(Privilege.chat)
                .WithAlias("pkg")
                .HandleWith(args => DisplayPlayerGoal(args, sapi))
                .EndSubCommand();
        }

        // Command function to print the Wolf Kills Leaderboard
        private TextCommandResult ListWolfKills(TextCommandCallingArgs args, ICoreServerAPI api)
        {

            string playerName = args.Caller.Player.PlayerName;
            api.Logger.Notification($"{playerName}: Printing Wolf Kill List Top 5");

            return TextCommandResult.Success(PrintList(playerName));
        }

        // Command function to reset the Wolf Kills Leaderboard
        private TextCommandResult ResetLeaderboardCommand(TextCommandCallingArgs args, ICoreServerAPI api)
        {
            WolfKillCounterModSystem modSystem = api.ModLoader.GetModSystem<WolfKillCounterModSystem>();
            modSystem.SetCurrentLeaderboard(new Dictionary<string, int>());
            modSystem.GetConfig().SaveWolfKillData(api);

            api.Logger.Notification("[WolfKillCounter] Resetting leaderboard.");

            return TextCommandResult.Success("Wolf kill leaderboard has been reset. Total kill count remains unchanged.");
        }

        private TextCommandResult DisplayServerGoal(TextCommandCallingArgs args, ICoreServerAPI api)
        {
            WolfKillCounterModSystem modSystem = api.ModLoader.GetModSystem<WolfKillCounterModSystem>();

            return TextCommandResult.Success($"Server Kill Goal: {modSystem.GetServerKillGoal()}.\n" +
                $"Server's Total Kills: {modSystem.GetTotalWolfKillCount()}");
        }

        private TextCommandResult DisplayPlayerGoal(TextCommandCallingArgs args, ICoreServerAPI api)
        {
            WolfKillCounterModSystem modSystem = api.ModLoader.GetModSystem<WolfKillCounterModSystem>();
            var wolfKillCount = modSystem.GetWolfKillCount();

            string playerName = args.Caller.Player.PlayerName;
            modSystem.Mod.Logger.Notification($"{playerName}: Printing personal kill goal.");

            if (wolfKillCount.ContainsKey(playerName))
            {
                modSystem.AddPlayer(playerName, new KillCountData { Kills = 0, Goal = 50, Deaths = 0 });
                modSystem.GetConfig().SaveWolfKillData(api);
            }

            return TextCommandResult.Success($"{playerName}'s Kill Goal: {modSystem.GetWolfKillCount()[playerName].Goal}.\n" +
                $"Your kills: {modSystem.GetWolfKillCount()[playerName].Kills}");
        }

        // Helper function to create the list string by sorting the dictionary and iterating through the top 5 elements in sortedDict.
        private string PrintList(string playerName)
        {
            WolfKillCounterModSystem modSystem = sapi.ModLoader.GetModSystem<WolfKillCounterModSystem>();
            Dictionary<string, KillCountData> wolfKillCount = modSystem.GetWolfKillCount();
            int totalWolfKillCount = modSystem.GetTotalWolfKillCount();
            Dictionary<string, int> currentLeaderboard = modSystem.GetCurrentLeaderboard();

            string list = $"WOLF EXTERMINATION LEADERBOARD\n";
            list += "=================================\n";

            int position = 1;

            foreach (var pair in GetTopFive(modSystem.GetCurrentLeaderboard()))
            {
                list += $"{position++}. {pair.Key}: {pair.Value} kills, {wolfKillCount[pair.Key].Deaths} Deaths, KD: {modSystem.CalculateKD(wolfKillCount[pair.Key])} \n";
            }

            list += "\n--------------------------------------------------------\n";
            list += $"Total Wolf Kills: {totalWolfKillCount}\n";
            list += $"Your Kills: {(wolfKillCount.ContainsKey(playerName) ? wolfKillCount[playerName].Kills : 0)}\n";
            list += $"Deaths by Wolf: {wolfKillCount[playerName].Deaths}\n";
            list += $"KD: {modSystem.CalculateKD(wolfKillCount[playerName])}\n";
            list += "=================================\n";
            return list;
        }

        // Helper function to get the top 5 players from the current leaderboard
        private Dictionary<string, int> GetTopFive(Dictionary<string, int> leaderboard)
        {
            return leaderboard.OrderByDescending(x => x.Value).Take(5).ToDictionary(x => x.Key, x => x.Value);
        }
    }
}
