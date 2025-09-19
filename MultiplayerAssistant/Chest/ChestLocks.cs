using MultiplayerAssistant.Config;
using MultiplayerAssistant.Chat;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.Objects;
using System;
using System.Linq;

namespace MultiplayerAssistant.Features
{
    internal static class ChestLocks
    {
        private static IMonitor Monitor;
        private static ModConfig Config;
        private static EventDrivenChatBox ChatBox;
        private static bool Patched = false;

        private const string OwnerKey = "objectmanagermanager.MultiplayerAssistant/ChestOwner";
        private const string PublicKey = "objectmanagermanager.MultiplayerAssistant/ChestPublic";
        private const string AllowKey = "objectmanagermanager.MultiplayerAssistant/ChestAllow"; // semicolon list of UIDs/names

        public static void Configure(IMonitor monitor, ModConfig config, EventDrivenChatBox chatBox)
        {
            Monitor = monitor;
            Config = config;
            ChatBox = chatBox;
            // Note: To fully enforce locks server-side, a Harmony patch to Chest.checkForAction is recommended.
            // In this build, we only provide metadata + chat commands. Enforcement can be added if Harmony is available.
        }

        private static bool IsInScope(StardewValley.Objects.Chest chest, Farmer who)
        {
            string scope = (Config.ChestLockScope ?? "placed-by-owner").ToLower();
            if (scope == "placed-by-owner") return true; // always enforced

            var loc = who.currentLocation;
            if (loc == null) return true;

            if (scope == "inside")
            {
                // enforce only when inside a cabin/farmhouse
                return (loc is StardewValley.Locations.Cabin) || (loc is StardewValley.Locations.FarmHouse);
            }
            else if (scope == "nearby")
            {
                if (loc is Farm)
                {
                    var farm = Game1.getFarm();
                    var tile = chest.TileLocation;
                    foreach (var b in farm.buildings)
                    {
                        if (b.isCabin)
                        {
                            var rect = new Microsoft.Xna.Framework.Rectangle(b.tileX.Value, b.tileY.Value, b.tilesWide.Value, b.tilesHigh.Value);
                            var expand = Config.ChestLockNearbyRadius;
                            rect.Inflate(expand, expand);
                            if (rect.Contains((int)tile.X, (int)tile.Y))
                                return true;
                        }
                    }
                    return false;
                }
                // not on farm -> don't enforce nearby
                return false;
            }

            // unknown scope: enforce by default
            return true;
        }

        public static bool TryAllow(StardewValley.Objects.Chest chest, Farmer owner, Farmer target)
        {
            if (chest == null || target == null) return false;
            if (!Config.ChestLockEnabled) return false;
            var md = chest.modData;
            if (!md.ContainsKey(OwnerKey)) md[OwnerKey] = owner.UniqueMultiplayerID.ToString();
            if (!(owner.IsMainPlayer || md[OwnerKey] == owner.UniqueMultiplayerID.ToString())) return false;
            var id = target.UniqueMultiplayerID.ToString();
            var list = (md.TryGetValue(AllowKey, out var s) && !string.IsNullOrEmpty(s)) ? s.Split(';').ToList() : new System.Collections.Generic.List<string>();
            if (!list.Any(x => x == id || string.Equals(x, target.Name, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(id);
                md[AllowKey] = string.Join(";", list);
            }
            return true;
        }

        public static bool TryRevoke(StardewValley.Objects.Chest chest, Farmer owner, Farmer target)
        {
            if (chest == null || target == null) return false;
            if (!Config.ChestLockEnabled) return false;
            var md = chest.modData;
            if (!md.ContainsKey(OwnerKey)) md[OwnerKey] = owner.UniqueMultiplayerID.ToString();
            if (!(owner.IsMainPlayer || md[OwnerKey] == owner.UniqueMultiplayerID.ToString())) return false;
            if (md.TryGetValue(AllowKey, out var s) && !string.IsNullOrEmpty(s))
            {
                var id = target.UniqueMultiplayerID.ToString();
                var list = s.Split(';').ToList();
                list = list.Where(x => !(x == id || string.Equals(x, target.Name, StringComparison.OrdinalIgnoreCase))).ToList();
                md[AllowKey] = string.Join(";", list);
            }
            return true;
        }

        public static string GetStatus(StardewValley.Objects.Chest chest)
        {
            if (chest == null) return "No chest.";
            var md = chest.modData;
            md.TryGetValue(OwnerKey, out var owner);
            md.TryGetValue(PublicKey, out var isPublic);
            md.TryGetValue(AllowKey, out var allow);
            return $"owner={owner ?? "?"}, public={isPublic ?? "false"}, allow=[{allow ?? ""}]";
        }

        public static bool TrySetPublic(StardewValley.Objects.Chest chest, bool isPublic, Farmer who)
        {
            if (chest == null) return false;
            if (!Config.ChestLockEnabled || !Config.ChestLockAllowPublicToggle) return false;
            var md = chest.modData;
            if (!md.ContainsKey(OwnerKey))
                md[OwnerKey] = who.UniqueMultiplayerID.ToString();
            // only owner or host may change
            if (!(who.IsMainPlayer || md[OwnerKey] == who.UniqueMultiplayerID.ToString()))
                return false;
            md[PublicKey] = isPublic ? "true" : "false";
            return true;
        }

        public static bool TryTransfer(StardewValley.Objects.Chest chest, Farmer from, Farmer to)
        {
            if (chest == null) return false;
            if (!Config.ChestLockEnabled) return false;
            var md = chest.modData;
            if (!md.ContainsKey(OwnerKey))
                md[OwnerKey] = from.UniqueMultiplayerID.ToString();
            if (!(from.IsMainPlayer || md[OwnerKey] == from.UniqueMultiplayerID.ToString()))
                return false;
            md[OwnerKey] = to.UniqueMultiplayerID.ToString();
            return true;
        }
    }
}
