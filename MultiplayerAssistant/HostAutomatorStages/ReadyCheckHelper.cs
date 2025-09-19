using Netcode;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.Menus;
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
            Console.WriteLine($"[MultiplayerAssistant] OnDayStarted 被调用 - 清理所有准备状态");
            
            // 重置睡觉尝试计数器
            ResetSleepAttemptCounter();
            
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

        // 使用多种方式检查玩家是否准备好
        public static bool IsReady(string checkName, Farmer player)
        {
            // 首先检查自定义的 ModData 状态（用于主机）
            string key = MOD_DATA_PREFIX + checkName;
            if (player.modData.ContainsKey(key))
            {
                return player.modData[key] == "true";
            }
            
            // 如果是睡觉状态，检查游戏原生的睡觉状态
            if (checkName == "sleep")
            {
                // 检查玩家是否在床上且已设置睡觉时间
                bool isInBed = player.isInBed.Value;
                bool hasTimeWentToBed = player.timeWentToBed.Value > 0;
                bool isAnnounced = Game1.player.team.announcedSleepingFarmers.Contains(player);
                
                Console.WriteLine($"[MultiplayerAssistant] 检查玩家 {player.Name} 原生睡觉状态:");
                Console.WriteLine($"[MultiplayerAssistant]   isInBed={isInBed}, timeWentToBed={player.timeWentToBed.Value}, isAnnounced={isAnnounced}");
                
                return isInBed && hasTimeWentToBed;
            }
            
            // 其他状态默认为 false
            return false;
        }

        // 使用 ModData 设置准备状态
        public static void SetLocalReady(string checkName, bool ready)
        {
            try
            {
                string key = MOD_DATA_PREFIX + checkName;
                Game1.player.modData[key] = ready.ToString().ToLower();
                
                // 添加详细的调试信息
                Console.WriteLine($"[MultiplayerAssistant] 玩家 {Game1.player.Name} 设置准备状态 {checkName} = {ready}");
                Console.WriteLine($"[MultiplayerAssistant] 当前时间: {Game1.timeOfDay}, 是否为主机: {Game1.IsMasterGame}");
                
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
                return IsReady(checkName, Game1.player) ? 1 : 0;
            }

            int count = 0;

            // 遍历所有在线玩家，使用 IsReady 方法检查状态
            foreach (Farmer farmer in Game1.getOnlineFarmers())
            {
                if (IsReady(checkName, farmer))
                {
                    count++;
                    Console.WriteLine($"[MultiplayerAssistant] 玩家 {farmer.Name} 已准备好 {checkName}");
                }
            }
            
            Console.WriteLine($"[MultiplayerAssistant] 总共准备好的玩家: {count}/{Game1.getOnlineFarmers().Count}");
            return count;
        }

        // 重置睡觉尝试计数器（通过反射访问 TransitionSleepBehaviorLink 的私有字段）
        private static void ResetSleepAttemptCounter()
        {
            try
            {
                var sleepLinkType = typeof(TransitionSleepBehaviorLink);
                var sleepAttemptCountField = sleepLinkType.GetField("sleepAttemptCount", BindingFlags.NonPublic | BindingFlags.Static);
                var sleepExecutionInProgressField = sleepLinkType.GetField("sleepExecutionInProgress", BindingFlags.NonPublic | BindingFlags.Static);
                
                if (sleepAttemptCountField != null)
                {
                    sleepAttemptCountField.SetValue(null, 0);
                    Console.WriteLine($"[MultiplayerAssistant] 重置睡觉尝试计数器");
                }
                
                if (sleepExecutionInProgressField != null)
                {
                    sleepExecutionInProgressField.SetValue(null, false);
                    Console.WriteLine($"[MultiplayerAssistant] 重置睡觉执行状态");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MultiplayerAssistant] 重置睡觉状态失败: {ex.Message}");
            }
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
            
            Console.WriteLine($"[MultiplayerAssistant] 清理准备状态，找到 {keysToRemove.Count} 个状态需要清理");
            
            // 移除所有准备状态
            foreach (var key in keysToRemove)
            {
                Console.WriteLine($"[MultiplayerAssistant] 移除准备状态: {key}");
                Game1.player.modData.Remove(key);
            }
            
            // 检查是否还有活跃的睡觉对话框需要关闭
            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ReadyCheckDialog rcd)
            {
                Console.WriteLine($"[MultiplayerAssistant] 发现活跃的睡觉对话框，正在关闭");
                Game1.exitActiveMenu();
            }
        }
    }
}