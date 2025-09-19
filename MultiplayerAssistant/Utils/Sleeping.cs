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
        private static int autoSleepTime = 2530;
        private static bool forceReturnToBed = true;

        public static void Configure(int autoSleepTimeConfig, bool forceReturn)
        {
            // sanitize time as HHMM rounded to 10s; fallback to 2530 if invalid
            if (autoSleepTimeConfig < 600 || autoSleepTimeConfig > 2600)
                autoSleepTime = 2530;
            else
                autoSleepTime = autoSleepTimeConfig - (autoSleepTimeConfig % 10);
            forceReturnToBed = forceReturn;
        }

        public static bool ForceReturnToBed() => forceReturnToBed;
        public static bool IsSleeping()
        {
            return ReadyCheckHelper.IsReady("sleep", Game1.player);
        }
        public static bool OthersInBed(int numOtherPlayers)
        {
            return ReadyCheckHelper.GetNumberReady("sleep") == (numOtherPlayers + (IsSleeping() ? 1 : 0));
        }
        public static bool ShouldSleep(int numOtherPlayers)
        {
            // don't sleep during active festival window
            var isFestivalWindow = Utility.isFestivalDay(Game1.Date.DayOfMonth, Game1.Date.Season)
                                   && Game1.timeOfDay >= Utility.getStartTimeOfFestival();
            if (isFestivalWindow)
                return false;

            return numOtherPlayers > 0 && (Game1.timeOfDay >= autoSleepTime || OthersInBed(numOtherPlayers));
        }
    }
}
