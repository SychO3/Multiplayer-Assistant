using MultiplayerAssistant.HostAutomatorStages;
using Netcode;
using StardewValley;
using StardewValley.Network;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MultiplayerAssistant.Utils
{
    internal class Sleeping
    {
        public static bool IsSleeping()
        {
            return ReadyCheckHelper.IsReady("sleep", Game1.player);
        }
        public static bool OthersInBed(int numOtherPlayers)
        {
            // 中文说明：1.6 去除了 GetNumberReady，保守判断为：有其他玩家即可
            return numOtherPlayers > 0;
        }
        public static bool ShouldSleep(int numOtherPlayers)
        {
            return numOtherPlayers > 0 && (Game1.timeOfDay >= 2530 || OthersInBed(numOtherPlayers));
        }
    }
}
