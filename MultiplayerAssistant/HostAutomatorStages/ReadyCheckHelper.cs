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
        private static Type readyCheckNetRefType = (readyCheckType != null) ? netRefType.MakeGenericType(readyCheckType) : null;
        private static Type netStringDictionaryType = typeof(NetStringDictionary<,>);
        private static Type readyCheckDictionaryType = (readyCheckNetRefType != null && readyCheckType != null) ? netStringDictionaryType.MakeGenericType(readyCheckType, readyCheckNetRefType) : null;

        private static FieldInfo readyChecksFieldInfo = typeof(FarmerTeam).GetField("readyChecks", BindingFlags.NonPublic | BindingFlags.Instance);
        private static object readyChecks = null;

        private static MethodInfo readyChecksAddMethodInfo = (readyCheckDictionaryType != null && readyCheckType != null) ? readyCheckDictionaryType.GetMethod("Add", new Type[] { typeof(string), readyCheckType }) : null;
        private static PropertyInfo readyChecksItemPropertyInfo = readyCheckDictionaryType?.GetProperty("Item");

        private static FieldInfo readyPlayersFieldInfo = readyCheckType?.GetField("readyPlayers", BindingFlags.NonPublic | BindingFlags.Instance);

        private static Dictionary<string, NetFarmerCollection> readyPlayersDictionary = new Dictionary<string, NetFarmerCollection>();

        public static void OnDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            // 基本防御：若反射类型不可用则直接返回，避免空引用
            if (readyCheckType == null || readyChecksFieldInfo == null || readyChecksItemPropertyInfo == null)
            {
                return;
            }
            if (readyChecks == null)
            {
                readyChecks = readyChecksFieldInfo.GetValue(Game1.player.team);
            }

            // 新的一天开始时，清理上一晚遗留的睡觉就绪与公告状态，避免早晨误判为仍在睡觉导致循环弹窗
            try
            {
                // 1) 清空 announcedSleepingFarmers
                Game1.player?.team?.announcedSleepingFarmers?.Clear();

                // 2) 清空 ReadyCheck("sleep") 的 readyPlayers 集合
                object sleepReadyCheck = null;
                try
                {
                    sleepReadyCheck = Activator.CreateInstance(readyCheckType, new object[] { "sleep" });
                    readyChecksAddMethodInfo?.Invoke(readyChecks, new object[] { "sleep", sleepReadyCheck });
                }
                catch (Exception)
                {
                    sleepReadyCheck = readyChecksItemPropertyInfo.GetValue(readyChecks, new object[] { "sleep" });
                }
                var readyPlayers = (NetFarmerCollection)(readyPlayersFieldInfo?.GetValue(sleepReadyCheck));
                readyPlayers?.Clear();

                // 同步我们维护的缓存字典
                if (readyPlayersDictionary.ContainsKey("sleep"))
                {
                    readyPlayersDictionary["sleep"] = readyPlayers ?? new NetFarmerCollection();
                }
            }
            catch
            {
                // 忽略清理异常，避免影响游戏流程
            }

            //Checking mailbox sometimes gives some gold, but it's compulsory to unlock some events
            for (int i = 0; i < 10; ++i) {
                Game1.getFarm().mailbox();
            }

            //Unlocks the sewer（1.6 事件ID改为字符串）
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
                    if (readyCheckType != null)
                    {
                        readyCheck = Activator.CreateInstance(readyCheckType, new object[] { checkName });
                        readyChecksAddMethodInfo?.Invoke(readyChecks, new object[] { checkName, readyCheck });
                    }
                }
                catch (Exception)
                {
                    readyCheck = readyChecksItemPropertyInfo.GetValue(readyChecks, new object[] { checkName });
                }

                NetFarmerCollection readyPlayers = (NetFarmerCollection) (readyPlayersFieldInfo?.GetValue(readyCheck));
                // 为空则给一个空集合，避免可空性警告
                var rc = readyPlayers ?? new NetFarmerCollection();
                newReadyPlayersDictionary.Add(checkName, rc);
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
            if (readyCheckType == null || readyChecksItemPropertyInfo == null)
            {
                return false;
            }
            if (readyPlayersDictionary.TryGetValue(checkName, out NetFarmerCollection readyPlayers) && readyPlayers != null)
            {
                return readyPlayers.Contains(player);
            }

            object readyCheck = null;
            try
            {
                if (readyCheckType != null)
                {
                    readyCheck = Activator.CreateInstance(readyCheckType, new object[] { checkName });
                    readyChecksAddMethodInfo?.Invoke(readyChecks, new object[] { checkName, readyCheck });
                }
            }
            catch (Exception)
            {
                readyCheck = readyChecksItemPropertyInfo.GetValue(readyChecks, new object[] { checkName });
            }

            readyPlayers = (NetFarmerCollection) (readyPlayersFieldInfo?.GetValue(readyCheck));
            var rc2 = readyPlayers ?? new NetFarmerCollection();
            if (readyPlayersDictionary.ContainsKey(checkName))
            {
                readyPlayersDictionary[checkName] = rc2;
            } else
            {
                readyPlayersDictionary.Add(checkName , rc2);
            }

            return rc2.Contains(player);
        }
    }
}
