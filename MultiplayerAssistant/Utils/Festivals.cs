using MultiplayerAssistant.HostAutomatorStages;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using MultiplayerAssistant;

namespace MultiplayerAssistant.Utils
{
    internal class Festivals
    {
        private static int getFestivalEndTime(IMonitor monitor = null)
        {
            // 中文说明：优先通过 API 判断今日是否为节日，而不是依赖 weatherIcon 数值
            if (Utility.isFestivalDay(Game1.dayOfMonth, Game1.currentSeason))
            {
                try
                {
                    var dict = Game1.temporaryContent.Load<Dictionary<string, string>>("Data\\Festivals\\" + Game1.currentSeason + Game1.dayOfMonth);
                    // conditions 形如 "location/start X end Y ..."，此处取 end 的时间数值
                    var endToken = dict["conditions"].Split('/')[1].Split(' ');
                    var endTime = Convert.ToInt32(endToken[1]);
                    monitor?.Debug($"节日结束时间解析：{endTime}", nameof(Festivals));
                    return endTime;
                }
                catch (Exception ex)
                {
                    monitor?.Exception(ex, "解析节日结束时间失败", nameof(Festivals));
                }
            }

            return -1;
        }
        public static bool IsWaitingToAttend(IMonitor monitor = null)
        {
            var res = ReadyCheckHelper.IsReady("festivalStart", Game1.player);
            monitor?.Debug($"是否已准备参加节日：{res}", nameof(Festivals));
            return res;
        }
        public static bool OthersWaitingToAttend(int numOtherPlayers, IMonitor monitor = null)
        {
            var ready = Game1.player.team.GetNumberReady("festivalStart");
            var mine = IsWaitingToAttend(monitor) ? 1 : 0;
            var res = ready == (numOtherPlayers + mine);
            monitor?.Debug($"他人是否已全部准备参加节日：ready={ready}, others={numOtherPlayers}, me={mine}, res={res}", nameof(Festivals));
            return res;
        }
        private static bool isTodayBeachNightMarket()
        {
            return Game1.currentSeason.Equals("winter") && Game1.dayOfMonth >= 15 && Game1.dayOfMonth <= 17;
        }
        public static bool ShouldAttend(int numOtherPlayers, IMonitor monitor = null)
        {
            var res = numOtherPlayers > 0
                      && OthersWaitingToAttend(numOtherPlayers, monitor)
                      && Utility.isFestivalDay(Game1.dayOfMonth, Game1.currentSeason)
                      && !isTodayBeachNightMarket()
                      && Game1.timeOfDay >= Utility.getStartTimeOfFestival()
                      && Game1.timeOfDay <= getFestivalEndTime(monitor);
            monitor?.Debug($"是否应参加节日：players={numOtherPlayers}, now={Game1.timeOfDay}, res={res}", nameof(Festivals));
            return res;
        }

        public static bool IsWaitingToLeave(IMonitor monitor = null)
        {
            var res = ReadyCheckHelper.IsReady("festivalEnd", Game1.player);
            monitor?.Debug($"是否已准备离开节日：{res}", nameof(Festivals));
            return res;
        }
        public static bool OthersWaitingToLeave(int numOtherPlayers, IMonitor monitor = null)
        {
            var ready = Game1.player.team.GetNumberReady("festivalEnd");
            var mine = IsWaitingToLeave(monitor) ? 1 : 0;
            var res = ready == (numOtherPlayers + mine);
            monitor?.Debug($"他人是否已全部准备离开节日：ready={ready}, others={numOtherPlayers}, me={mine}, res={res}", nameof(Festivals));
            return res;
        }
        public static bool ShouldLeave(int numOtherPlayers, IMonitor monitor = null)
        {
            var res = Game1.isFestival() && OthersWaitingToLeave(numOtherPlayers, monitor);
            monitor?.Debug($"是否应离开节日：players={numOtherPlayers}, res={res}", nameof(Festivals));
            return res;
        }
    }
}
