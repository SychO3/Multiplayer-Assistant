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
        private static bool initialized = false;
        private static Assembly assembly;
        private static Type readyCheckType;
        private static FieldInfo readyChecksFieldInfo;
        private static object readyChecks; // FarmerTeam.readyChecks instance
        private static MethodInfo readyChecksAddMethodInfo;
        private static PropertyInfo readyChecksItemPropertyInfo;
        private static FieldInfo readyPlayersFieldInfo; // field inside ReadyCheck holding NetFarmerCollection

        private static Dictionary<string, NetFarmerCollection> readyPlayersDictionary = new Dictionary<string, NetFarmerCollection>();

        private static bool EnsureInitialized()
        {
            if (initialized)
                return true;

            try
            {
                assembly = typeof(Game1).Assembly;
                // find the ReadyCheck type anywhere in the game assembly
                readyCheckType = assembly.GetTypes().FirstOrDefault(t => t.Name == "ReadyCheck");
                if (readyCheckType == null)
                    return false;

                // locate FarmerTeam.readyChecks field (NetStringDictionary<ReadyCheck, NetRef<ReadyCheck>>)
                readyChecksFieldInfo = typeof(FarmerTeam).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(f =>
                    {
                        var ft = f.FieldType;
                        if (!ft.IsGenericType) return false;
                        if (!ft.Name.Contains("NetStringDictionary")) return false;
                        var args = ft.GetGenericArguments();
                        return args.Length == 2 && (args[0] == readyCheckType || (args[0].IsGenericType && args[0].GetGenericArguments().FirstOrDefault() == readyCheckType));
                    });

                if (readyChecksFieldInfo == null)
                    return false;

                readyChecks = readyChecksFieldInfo.GetValue(Game1.player.team);
                if (readyChecks == null)
                    return false;

                var readyChecksType = readyChecks.GetType();
                readyChecksAddMethodInfo = readyChecksType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
                readyChecksItemPropertyInfo = readyChecksType.GetProperty("Item");
                if (readyChecksAddMethodInfo == null || readyChecksItemPropertyInfo == null)
                    return false;

                // inside ReadyCheck, find the NetFarmerCollection field (usually 'readyPlayers')
                readyPlayersFieldInfo = readyCheckType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(f => typeof(NetFarmerCollection).IsAssignableFrom(f.FieldType));

                if (readyPlayersFieldInfo == null)
                    return false;

                initialized = true;
                return true;
            }
            catch
            {
                initialized = false;
                return false;
            }
        }

        public static void OnDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            if (!EnsureInitialized()) return;

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
                if (!EnsureInitialized()) return false;
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

        public static int GetNumberReady(string checkName)
        {
            if (!EnsureInitialized()) return 0;
            // Ensure cache exists
            IsReady(checkName, Game1.player);
            if (readyPlayersDictionary.TryGetValue(checkName, out NetFarmerCollection readyPlayers) && readyPlayers != null)
            {
                // Try Count property first
                var countProp = typeof(NetFarmerCollection).GetProperty("Count");
                if (countProp != null)
                {
                    return (int)countProp.GetValue(readyPlayers);
                }

                // Fallback: enumerate
                int count = 0;
                var enumerator = readyPlayers.GetEnumerator();
                while (enumerator.MoveNext()) count++;
                return count;
            }
            return 0;
        }

        public static void SetReady(string checkName, Farmer player, bool ready)
        {
            if (!EnsureInitialized()) return;
            // Ensure we have the collection
            IsReady(checkName, player);
            if (!readyPlayersDictionary.TryGetValue(checkName, out NetFarmerCollection readyPlayers) || readyPlayers == null)
                return;

            bool isReady = readyPlayers.Contains(player);
            if (ready && !isReady)
            {
                // Try Add(Farmer)
                var addMethod = typeof(NetFarmerCollection).GetMethod("Add", new Type[] { typeof(Farmer) })
                                ?? typeof(NetFarmerCollection).GetMethod("Add", new Type[] { typeof(long) });
                if (addMethod != null)
                {
                    if (addMethod.GetParameters()[0].ParameterType == typeof(Farmer))
                        addMethod.Invoke(readyPlayers, new object[] { player });
                    else
                        addMethod.Invoke(readyPlayers, new object[] { player.UniqueMultiplayerID });
                }
            }
            else if (!ready && isReady)
            {
                var removeMethod = typeof(NetFarmerCollection).GetMethod("Remove", new Type[] { typeof(Farmer) })
                                   ?? typeof(NetFarmerCollection).GetMethod("Remove", new Type[] { typeof(long) });
                if (removeMethod != null)
                {
                    if (removeMethod.GetParameters()[0].ParameterType == typeof(Farmer))
                        removeMethod.Invoke(readyPlayers, new object[] { player });
                    else
                        removeMethod.Invoke(readyPlayers, new object[] { player.UniqueMultiplayerID });
                }
            }
        }
    }
}
