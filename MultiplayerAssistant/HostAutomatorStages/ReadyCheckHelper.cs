using MultiplayerAssistant;
using Netcode;
using StardewModdingAPI;
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
        private static IMonitor? monitor;
        private static readonly Assembly assembly = typeof(Game1).Assembly;
        private static readonly Type? readyCheckType = assembly.GetType("StardewValley.ReadyCheck");
        private static readonly Type netRefType = typeof(NetRef<>);
        private static readonly Type? readyCheckNetRefType = readyCheckType != null ? netRefType.MakeGenericType(readyCheckType) : null;
        private static readonly Type netStringDictionaryType = typeof(NetStringDictionary<,>);
        private static readonly Type? readyCheckDictionaryType = readyCheckNetRefType != null && readyCheckType != null ? netStringDictionaryType.MakeGenericType(readyCheckType, readyCheckNetRefType) : null;

        private static readonly FieldInfo? readyChecksFieldInfo = typeof(FarmerTeam).GetField("readyChecks", BindingFlags.NonPublic | BindingFlags.Instance);
        private static object? readyChecks = null;

        private static readonly MethodInfo? readyChecksAddMethodInfo = readyCheckDictionaryType != null && readyCheckType != null ? readyCheckDictionaryType.GetMethod("Add", new Type[] { typeof(string), readyCheckType }) : null;
        private static readonly PropertyInfo? readyChecksItemPropertyInfo = readyCheckDictionaryType?.GetProperty("Item");

        private static readonly FieldInfo? readyPlayersFieldInfo = readyCheckType?.GetField("readyPlayers", BindingFlags.NonPublic | BindingFlags.Instance);

        private static Dictionary<string, NetFarmerCollection> readyPlayersDictionary = new();

        public static void Initialize(IMonitor monitor)
        {
            ReadyCheckHelper.monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            Debug("ReadyCheckHelper 初始化完成");
        }

        public static void OnDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            if (readyCheckType == null || readyChecksFieldInfo == null || readyChecksItemPropertyInfo == null)
            {
                Warn("ReadyCheck 反射信息缺失，无法刷新缓存");
                return;
            }

            Debug("OnDayStarted 触发，准备刷新 ReadyCheck 缓存");
            if (readyChecks == null)
            {
                readyChecks = readyChecksFieldInfo.GetValue(Game1.player.team);
                Debug("已获取 FarmerTeam.readyChecks 引用");
            }

            try
            {
                Game1.player?.team?.announcedSleepingFarmers?.Clear();
                Debug("已清空 announcedSleepingFarmers");

                object? sleepReadyCheck = null;
                try
                {
                    sleepReadyCheck = Activator.CreateInstance(readyCheckType, new object[] { "sleep" });
                    readyChecksAddMethodInfo?.Invoke(readyChecks, new object[] { "sleep", sleepReadyCheck });
                }
                catch (Exception ex)
                {
                    Trace($"使用缓存 ReadyCheck(sleep)：{ex.Message}");
                    sleepReadyCheck = readyChecksItemPropertyInfo.GetValue(readyChecks, new object[] { "sleep" });
                }

                var readyPlayers = (NetFarmerCollection?)(readyPlayersFieldInfo?.GetValue(sleepReadyCheck));
                readyPlayers?.Clear();
                Debug("已重置 sleep ReadyCheck 的 readyPlayers 集合");

                if (readyPlayersDictionary.ContainsKey("sleep"))
                {
                    readyPlayersDictionary["sleep"] = readyPlayers ?? new NetFarmerCollection();
                }
            }
            catch (Exception ex)
            {
                Warn($"刷新 sleep ReadyCheck 时出现异常：{ex.Message}");
            }

            for (int i = 0; i < 10; ++i)
            {
                Game1.getFarm().mailbox();
            }

            if (!Game1.player.eventsSeen.Contains("295672") && Game1.netWorldState.Value.MuseumPieces.Count() >= 60)
            {
                Game1.player.eventsSeen.Add("295672");
                Debug("自动解锁下水道事件 (295672)");
            }

            var targetLevel = Game1.getFarm().buildings.Where(o => o.isCabin).Select(o => ((Cabin)o.indoors.Value).upgradeLevel).DefaultIfEmpty(0).Max();
            if (targetLevel > Game1.player.HouseUpgradeLevel)
            {
                Game1.player.HouseUpgradeLevel = targetLevel;
                Game1.player.performRenovation("FarmHouse");
                Debug($"同步房屋升级等级：{targetLevel}");
            }

            var newReadyPlayersDictionary = new Dictionary<string, NetFarmerCollection>();
            foreach (var checkName in readyPlayersDictionary.Keys)
            {
                Trace($"刷新 ReadyCheck：{checkName}");
                object? readyCheck = null;
                try
                {
                    if (readyCheckType != null)
                    {
                        readyCheck = Activator.CreateInstance(readyCheckType, new object[] { checkName });
                        readyChecksAddMethodInfo?.Invoke(readyChecks, new object[] { checkName, readyCheck });
                    }
                }
                catch (Exception ex)
                {
                    Trace($"复用现有 ReadyCheck {checkName}：{ex.Message}");
                    readyCheck = readyChecksItemPropertyInfo?.GetValue(readyChecks, new object[] { checkName });
                }

                var readyPlayers = (NetFarmerCollection?)(readyPlayersFieldInfo?.GetValue(readyCheck));
                newReadyPlayersDictionary.Add(checkName, readyPlayers ?? new NetFarmerCollection());
            }

            readyPlayersDictionary = newReadyPlayersDictionary;
            Trace($"当前监控 ReadyCheck 数量：{readyPlayersDictionary.Count}");
        }

        public static void WatchReadyCheck(string checkName)
        {
            readyPlayersDictionary.TryAdd(checkName, null);
            Debug($"记录监听 ReadyCheck：{checkName}");
        }

        public static bool IsReady(string checkName, Farmer player)
        {
            if (readyCheckType == null || readyChecksItemPropertyInfo == null)
            {
                Warn($"ReadyCheck 反射信息缺失，无法判断 {checkName}");
                return false;
            }

            if (readyPlayersDictionary.TryGetValue(checkName, out var readyPlayers) && readyPlayers != null)
            {
                Trace($"从缓存读取 ReadyCheck：{checkName}, player={player?.Name}");
                return readyPlayers.Contains(player);
            }

            object? readyCheck = null;
            try
            {
                if (readyCheckType != null)
                {
                    readyCheck = Activator.CreateInstance(readyCheckType, new object[] { checkName });
                    readyChecksAddMethodInfo?.Invoke(readyChecks, new object[] { checkName, readyCheck });
                }
            }
            catch (Exception ex)
            {
                Trace($"复用 ReadyCheck {checkName}：{ex.Message}");
                readyCheck = readyChecksItemPropertyInfo?.GetValue(readyChecks, new object[] { checkName });
            }

            readyPlayers = (NetFarmerCollection?)(readyPlayersFieldInfo?.GetValue(readyCheck));
            var rc2 = readyPlayers ?? new NetFarmerCollection();
            if (readyPlayersDictionary.ContainsKey(checkName))
            {
                readyPlayersDictionary[checkName] = rc2;
            }
            else
            {
                readyPlayersDictionary.Add(checkName, rc2);
            }

            var result = rc2.Contains(player);
            Trace($"刷新 ReadyCheck 状态：{checkName}, player={player?.Name}, result={result}");
            return result;
        }

        private static void Debug(string message)
        {
            if (monitor != null)
                monitor.Debug(message, nameof(ReadyCheckHelper));
        }

        private static void Trace(string message)
        {
            if (monitor != null)
                monitor.Trace(message, nameof(ReadyCheckHelper));
        }

        private static void Warn(string message)
        {
            if (monitor != null)
                monitor.Warn(message, nameof(ReadyCheckHelper));
        }
    }
}
