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
            // 中文说明：基于 1.6，使用团队 announcedSleepingFarmers 判断是否有“其他玩家”上床（排除本地玩家）
            var team = Game1.player?.team;
            if (team?.announcedSleepingFarmers == null || team.announcedSleepingFarmers.Count == 0)
                return false;

            foreach (var farmer in team.announcedSleepingFarmers)
            {
                if (farmer != null && farmer != Game1.player)
                {
                    return true;
                }
            }
            return false;
        }
        public static bool ShouldSleep(int numOtherPlayers)
        {
            // 规则：
            // 1) 总是需要至少一个“其他玩家”在线；
            // 2) 满足以下其一：
            //    a. 很晚（>= 2530）
            //    b. 任意时间只要有“其他玩家”宣布上床（允许白天主动睡觉）
            if (numOtherPlayers <= 0)
                return false;

            if (Game1.timeOfDay >= 2530)
                return true;

            if (OthersInBed(numOtherPlayers))
                return true;

            return false;
        }
    }
}
