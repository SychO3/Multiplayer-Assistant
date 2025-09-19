using MultiplayerAssistant.HostAutomatorStages;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiplayerAssistant.Utils
{
    internal class Festivals
    {
        public static int GetFestivalEndTime()
        {
            if (Game1.weatherIcon == 1)
            {
                var seasonKey = Game1.Date.Season.ToString().ToLower();
                var day = Game1.Date.DayOfMonth;
                return Convert.ToInt32(Game1.temporaryContent.Load<Dictionary<string, string>>("Data\\Festivals\\" + seasonKey + day)["conditions"].Split('/')[1].Split(' ')[1]);
            }

            return -1;
        }
        public static bool IsWaitingToAttend()
        {
            return ReadyCheckHelper.IsReady("festivalStart", Game1.player);
        }
        public static bool OthersWaitingToAttend(int numOtherPlayers)
        {
            return ReadyCheckHelper.GetNumberReady("festivalStart") == (numOtherPlayers + (IsWaitingToAttend() ? 1 : 0));
        }
        private static bool isTodayBeachNightMarket()
        {
            return Game1.Date.Season == StardewValley.Season.Winter && Game1.Date.DayOfMonth >= 15 && Game1.Date.DayOfMonth <= 17;
        }
        public static bool ShouldAttend(int numOtherPlayers)
        {
            return numOtherPlayers > 0 && OthersWaitingToAttend(numOtherPlayers) && Utility.isFestivalDay(Game1.Date.DayOfMonth, Game1.Date.Season) && !isTodayBeachNightMarket() && Game1.timeOfDay >= Utility.getStartTimeOfFestival() && Game1.timeOfDay <= GetFestivalEndTime();
        }

        public static bool IsWaitingToLeave()
        {
            return ReadyCheckHelper.IsReady("festivalEnd", Game1.player);
        }
        public static bool OthersWaitingToLeave(int numOtherPlayers)
        {
            return ReadyCheckHelper.GetNumberReady("festivalEnd") == (numOtherPlayers + (IsWaitingToLeave() ? 1 : 0));
        }
        public static bool ShouldLeave(int numOtherPlayers)
        {
            return Game1.isFestival() && OthersWaitingToLeave(numOtherPlayers);
        }
    }
}
