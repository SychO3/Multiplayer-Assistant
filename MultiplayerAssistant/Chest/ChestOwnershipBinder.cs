using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using System;
using System.Linq;

namespace MultiplayerAssistant.Features
{
    internal class ChestOwnershipBinder
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly Config.ModConfig config;
        private const string OwnerKey = "objectmanagermanager.MultiplayerAssistant/ChestOwner";
        private const string PublicKey = "objectmanagermanager.MultiplayerAssistant/ChestPublic";

        public ChestOwnershipBinder(IModHelper helper, IMonitor monitor, Config.ModConfig config)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.config = config;
        }

        public void Enable()
        {
            helper.Events.World.ObjectListChanged += OnObjectListChanged;
        }

        public void Disable()
        {
            helper.Events.World.ObjectListChanged -= OnObjectListChanged;
        }

        private void OnObjectListChanged(object sender, StardewModdingAPI.Events.ObjectListChangedEventArgs e)
        {
            if (!config.ChestLockEnabled) return;
            if (e.Added == null || e.Added.Count() == 0) return;
            foreach (var pair in e.Added)
            {
                if (pair.Value is Chest chest)
                {
                    var md = chest.modData;
                    if (!md.ContainsKey(OwnerKey))
                    {
                        // choose nearest farmer (host or other) in same location, within radius 2
                        Farmer nearest = Game1.player;
                        double best = double.MaxValue;
                        var pos = pair.Key;
                        // include host
                        if (Game1.player?.currentLocation == e.Location)
                        {
                            var d = DistanceSquared(pos, new Microsoft.Xna.Framework.Vector2((int)(Game1.player.Position.X/64f), (int)(Game1.player.Position.Y/64f)));
                            best = d; nearest = Game1.player;
                        }
                        // include others
                        foreach (var f in Game1.otherFarmers.Values)
                        {
                            if (f?.currentLocation == e.Location)
                            {
                                var d = DistanceSquared(pos, new Microsoft.Xna.Framework.Vector2((int)(f.Position.X/64f), (int)(f.Position.Y/64f)));
                                if (d < best)
                                {
                                    best = d; nearest = f;
                                }
                            }
                        }
                        md[OwnerKey] = nearest.UniqueMultiplayerID.ToString();
                        if (!md.ContainsKey(PublicKey)) md[PublicKey] = "false";
                        monitor?.Log($"Chest at {e.Location.NameOrUniqueName} {pos} bound to {nearest.Name}", LogLevel.Trace);
                    }
                }
            }
        }

        private static double DistanceSquared(Microsoft.Xna.Framework.Vector2 a, Microsoft.Xna.Framework.Vector2 b)
        {
            var dx = a.X - b.X; var dy = a.Y - b.Y; return dx * dx + dy * dy;
        }
    }
}
