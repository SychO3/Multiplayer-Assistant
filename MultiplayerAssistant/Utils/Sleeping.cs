using MultiplayerAssistant.HostAutomatorStages;
using Netcode;
using StardewValley;
using StardewValley.Network;
using System;
using System.Collections.Generic;
using System.Reflection;
using StardewModdingAPI;
using MultiplayerAssistant;

namespace MultiplayerAssistant.Utils
{
    internal class Sleeping
    {
        public static bool IsSleeping(IMonitor monitor = null)
        {
            var res = ReadyCheckHelper.IsReady("sleep", Game1.player);
            monitor?.Debug($"是否已准备睡觉：{res}", nameof(Sleeping));
            return res;
        }
        public static bool OthersInBed(int numOtherPlayers, IMonitor monitor = null)
        {
            var ready = Game1.player.team.GetNumberReady("sleep");
            var mine = IsSleeping(monitor) ? 1 : 0;
            var res = ready == (numOtherPlayers + mine);
            monitor?.Debug($"他人是否已在床上：ready={ready}, others={numOtherPlayers}, me={mine}, res={res}", nameof(Sleeping));
            return res;
        }
        public static bool ShouldSleep(int numOtherPlayers, IMonitor monitor = null)
        {
            var res = numOtherPlayers > 0 && (Game1.timeOfDay >= 2530 || OthersInBed(numOtherPlayers, monitor));
            monitor?.Debug($"是否应睡觉：players={numOtherPlayers}, time={Game1.timeOfDay}, res={res}", nameof(Sleeping));
            return res;
        }
    }
}
