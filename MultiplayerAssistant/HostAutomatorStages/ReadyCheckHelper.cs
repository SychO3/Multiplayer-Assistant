using Netcode;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MultiplayerAssistant.HostAutomatorStages
{
    internal class ReadyCheckHelper
    {
        private static Assembly assembly = typeof(Game1).Assembly;
        private static Type readyCheckType = assembly.GetType("StardewValley.ReadyCheck");
        private static Type netRefType = typeof(NetRef<>);
        private static Type readyCheckNetRefType = netRefType.MakeGenericType(readyCheckType);
        private static Type netStringDictionaryType = typeof(NetStringDictionary<,>);
        private static Type readyCheckDictionaryType = netStringDictionaryType.MakeGenericType(readyCheckType, readyCheckNetRefType);

        private static FieldInfo readyChecksFieldInfo = typeof(FarmerTeam).GetField("readyChecks", BindingFlags.NonPublic | BindingFlags.Instance);
        private static object readyChecks = null;

        private static MethodInfo readyChecksAddMethodInfo = readyCheckDictionaryType.GetMethod("Add", new Type[] { typeof(string), readyCheckType });
        private static PropertyInfo readyChecksItemPropertyInfo = readyCheckDictionaryType.GetProperty("Item");

        private static FieldInfo readyPlayersFieldInfo = readyCheckType.GetField("readyPlayers", BindingFlags.NonPublic | BindingFlags.Instance);

        private static Dictionary<string, NetFarmerCollection> readyPlayersDictionary = new Dictionary<string, NetFarmerCollection>();

        public static void OnDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            if (readyChecks == null)
            {
                readyChecks = readyChecksFieldInfo.GetValue(Game1.player.team);
            }

            //Checking mailbox sometimes gives some gold, but it's compulsory to unlock some events
            for (int i = 0; i < 10; ++i) {
                Game1.getFarm().mailbox();
            }

            //Unlocks the sewer
            if (!Game1.player.eventsSeen.Contains("295672") && Game1.netWorldState.Value.MuseumPieces.Count() >= 60) {
                Game1.player.eventsSeen.Add("295672");
            }

            //Upgrade farmhouse to match highest level cabin
            var targetLevel = Game1.getFarm().buildings.Where(o => o.isCabin).Select(o => ((Cabin)o.indoors.Value).upgradeLevel).DefaultIfEmpty(0).Max();
            if (targetLevel > Game1.player.HouseUpgradeLevel) {
                Game1.player.HouseUpgradeLevel = targetLevel;
                Game1.player.performRenovation("FarmHouse");
            }
            

            Dictionary<string, NetFarmerCollection> newReadyPlayersDictionary = new Dictionary<string, NetFarmerCollection>();
            foreach (var checkName in readyPlayersDictionary.Keys)
            {
                object readyCheck = null;
                try
                {
                    readyCheck = Activator.CreateInstance(readyCheckType, new object[] { checkName });
                    readyChecksAddMethodInfo.Invoke(readyChecks, new object[] { checkName, readyCheck });
                }
                catch (Exception)
                {
                    readyCheck = readyChecksItemPropertyInfo.GetValue(readyChecks, new object[] { checkName });
                }

                NetFarmerCollection readyPlayers = (NetFarmerCollection) readyPlayersFieldInfo.GetValue(readyCheck);
                newReadyPlayersDictionary.Add(checkName, readyPlayers);
            }
            readyPlayersDictionary = newReadyPlayersDictionary;
        }

        public static void WatchReadyCheck(string checkName)
        {
            readyPlayersDictionary.TryAdd(checkName, null);
        }

        // Prerequisite: OnDayStarted() must have been called at least once prior to this method being called.
        public static bool IsReady(string checkName, Farmer player)
        {
            if (readyPlayersDictionary.TryGetValue(checkName, out NetFarmerCollection readyPlayers) && readyPlayers != null)
            {
                return readyPlayers.Contains(player);
            }

            object readyCheck = null;
            try
            {
                readyCheck = Activator.CreateInstance(readyCheckType, new object[] { checkName });
                readyChecksAddMethodInfo.Invoke(readyChecks, new object[] { checkName, readyCheck });
            }
            catch (Exception)
            {
                readyCheck = readyChecksItemPropertyInfo.GetValue(readyChecks, new object[] { checkName });
            }

            readyPlayers = (NetFarmerCollection) readyPlayersFieldInfo.GetValue(readyCheck);
            if (readyPlayersDictionary.ContainsKey(checkName))
            {
                readyPlayersDictionary[checkName] = readyPlayers;
            } else
            {
                readyPlayersDictionary.Add(checkName , readyPlayers);
            }

            return readyPlayers.Contains(player);
        }

        // 使用反射设置准备状态，因为 SetLocalReady 在 1.6 中已被移除
        public static void SetLocalReady(string checkName, bool ready)
        {
            try
            {
                // 尝试使用 SetLocalReady 方法（如果存在）
                var setLocalReadyMethod = typeof(FarmerTeam).GetMethod("SetLocalReady", BindingFlags.Public | BindingFlags.Instance);
                if (setLocalReadyMethod != null)
                {
                    setLocalReadyMethod.Invoke(Game1.player.team, new object[] { checkName, ready });
                    return;
                }

                // 备用方案：使用 ToggleReady
                if (readyChecks == null)
                {
                    readyChecks = readyChecksFieldInfo.GetValue(Game1.player.team);
                }

                object readyCheck = null;
                try
                {
                    readyCheck = readyChecksItemPropertyInfo.GetValue(readyChecks, new object[] { checkName });
                }
                catch
                {
                    // 如果不存在，创建一个新的
                    readyCheck = Activator.CreateInstance(readyCheckType, new object[] { checkName });
                    readyChecksAddMethodInfo.Invoke(readyChecks, new object[] { checkName, readyCheck });
                }

                if (readyCheck != null)
                {
                    // 检查当前状态
                    bool currentlyReady = IsReady(checkName, Game1.player);
                    if (currentlyReady != ready)
                    {
                        // 只有在状态不同时才切换
                        var toggleMethod = readyCheck.GetType().GetMethod("ToggleReady", BindingFlags.Public | BindingFlags.Instance);
                        if (toggleMethod != null)
                        {
                            toggleMethod.Invoke(readyCheck, new object[] { Game1.player });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不中断执行
                Game1.log.Error($"Failed to set ready status: {ex.Message}");
            }
        }

        // 获取已准备好的玩家数量
        public static int GetNumberReady(string checkName)
        {
            try
            {
                // 尝试使用 GetNumberReady 方法（如果存在）
                var getNumberReadyMethod = typeof(FarmerTeam).GetMethod("GetNumberReady", BindingFlags.Public | BindingFlags.Instance);
                if (getNumberReadyMethod != null)
                {
                    return (int)getNumberReadyMethod.Invoke(Game1.player.team, new object[] { checkName });
                }

                // 备用方案：直接访问 readyPlayers
                if (readyPlayersDictionary.TryGetValue(checkName, out NetFarmerCollection readyPlayers) && readyPlayers != null)
                {
                    return readyPlayers.Count;
                }

                // 如果没有缓存，尝试获取
                if (readyChecks == null)
                {
                    readyChecks = readyChecksFieldInfo.GetValue(Game1.player.team);
                }

                object readyCheck = readyChecksItemPropertyInfo.GetValue(readyChecks, new object[] { checkName });
                if (readyCheck != null)
                {
                    readyPlayers = (NetFarmerCollection)readyPlayersFieldInfo.GetValue(readyCheck);
                    if (readyPlayers != null)
                    {
                        return readyPlayers.Count;
                    }
                }
            }
            catch (Exception ex)
            {
                Game1.log.Error($"Failed to get number ready: {ex.Message}");
            }
            return 0;
        }
    }
}
