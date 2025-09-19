using Netcode;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;

namespace MultiplayerAssistant.HostAutomatorStages
{
    internal class ReadyCheckHelper
    {
        // 使用 ModData 存储准备状态
        private const string MOD_DATA_PREFIX = "MultiplayerAssistant.Ready.";
        private static IMonitor monitor;
        
        // 初始化监视器
        public static void Initialize(IMonitor mon)
        {
            monitor = mon;
        }

        public static void OnDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            // 清理所有准备状态
            ClearAllReadyStates();
            
            // 检查邮箱（有时会给一些金币，但解锁某些事件是必须的）
            for (int i = 0; i < 10; ++i) {
                Game1.getFarm().mailbox();
            }

            // 解锁下水道
            if (!Game1.player.hasRustyKey)
            {
                Game1.player.hasRustyKey = true;
            }

            // 立即升级农舍到目标等级
            int targetLevel = Math.Max(Math.Min(3, Game1.getFarm().getNumberBuildingsConstructed("Cabin")), 1);
            if (Game1.player.HouseUpgradeLevel < targetLevel)
            {
                Game1.player.HouseUpgradeLevel = targetLevel;
                Game1.player.performRenovation("FarmHouse");
            }
        }

        public static void WatchReadyCheck(string checkName)
        {
            // 在新系统中不需要预先注册
        }

        // 使用 ModData 检查玩家是否准备好
        public static bool IsReady(string checkName, Farmer player)
        {
            // 对于主机（ServerBot），总是返回 true
            if (Game1.IsMasterGame && player == Game1.player)
            {
                return true;
            }

            string key = MOD_DATA_PREFIX + checkName;
            return player.modData.ContainsKey(key) && player.modData[key] == "true";
        }

        // 使用 ModData 设置准备状态
        public static void SetLocalReady(string checkName, bool ready)
        {
            try
            {
                string key = MOD_DATA_PREFIX + checkName;
                Game1.player.modData[key] = ready.ToString().ToLower();
                
                // 通过网络同步给其他玩家
                if (Context.IsMultiplayer)
                {
                    // 触发一个同步事件
                    monitor?.Log($"设置准备状态 {checkName} = {ready}", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                // 在1.6中，使用控制台输出作为替代
                Console.WriteLine($"[MultiplayerAssistant] Failed to set ready status: {ex.Message}");
            }
        }

        // 获取准备好的玩家数量
        public static int GetNumberReady(string checkName)
        {
            if (!Context.IsMultiplayer)
            {
                return 1; // 单人游戏总是返回1
            }

            int count = 0;
            string key = MOD_DATA_PREFIX + checkName;

            // 遍历所有在线玩家
            foreach (Farmer farmer in Game1.getOnlineFarmers())
            {
                if (farmer.modData.ContainsKey(key) && farmer.modData[key] == "true")
                {
                    count++;
                }
                // 主机（ServerBot）总是准备好的
                else if (Game1.IsMasterGame && farmer == Game1.player)
                {
                    count++;
                }
            }

            return count;
        }

        // 清理所有准备状态
        private static void ClearAllReadyStates()
        {
            var keysToRemove = new List<string>();
            
            // 查找所有准备状态键
            foreach (var key in Game1.player.modData.Keys)
            {
                if (key.StartsWith(MOD_DATA_PREFIX))
                {
                    keysToRemove.Add(key);
                }
            }
            
            // 移除所有准备状态
            foreach (var key in keysToRemove)
            {
                Game1.player.modData.Remove(key);
            }
        }
    }
}