using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace WolfKillCounter.Commands
{
    internal class Commands
    {
        private static ICoreServerAPI sapi;
        public static void RegisterCommands(ICoreServerAPI api)
        {
            sapi = api;
            
            // List Leaderboard Command
            sapi.ChatCommands.Create("listWolfKills")
                .WithDescription("List the top 5 wolf killers")
                .RequiresPrivilege(Privilege.chat)
                .WithAlias("lwk")
                .HandleWith(args => ListWolfKills(args, sapi));

            // Reset Leaderboard Command
            sapi.ChatCommands.Create("resetWolfLeaderboard")
                .WithDescription("Resets wolf leaderboard without affecting total kills.")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args => ResetLeaderboardCommand(args, sapi));

            // Display Server Goal Command
            sapi.ChatCommands.Create("serverKillGoal")
                .WithDescription("Displays the server's kill goal.")
                .RequiresPrivilege(Privilege.chat)
                .WithAlias("skg")
                .HandleWith(args => DisplayServerGoal(args, sapi));

            // Display Player Goal Command
            sapi.ChatCommands.Create("playerKillGoal")
                .WithDescription("Displays your kill goal.")
                .RequiresPrivilege(Privilege.chat)
                .WithAlias("pkg")
                .HandleWith(args => DisplayPlayerGoal(args, sapi));
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
            WolfKillCounterModSystem wolfKillCounter = api.ModLoader.GetModSystem<WolfKillCounterModSystem>();
            wolfKillCounter.currentLeaderboard.Clear();
            wolfKillCounter.SaveWolfKillData();

            api.Logger.Notification("[WolfKillCounter] Resetting leaderboard.");

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

            if (!wolfKillCount.ContainsKey(playerName))
            {
                wolfKillCount.Add(playerName, new KillCountData { Kills = 0, Goal = 50, Deaths = 0 });
            }

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
    }
}
