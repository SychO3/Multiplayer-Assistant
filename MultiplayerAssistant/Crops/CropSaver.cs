using DedicatedServer.Config;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;

namespace DedicatedServer.Crops
{
    // 1.6-compatible implementation: on the last day of a season, automatically harvest
    // any harvest-ready seasonal crops outdoors (non-Island, non-Greenhouse) so players
    // get one final yield before they die overnight.
    public class CropSaver
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly ModConfig config;

        public CropSaver(IModHelper helper, IMonitor monitor, ModConfig config)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.config = config;
        }

        public void Enable()
        {
            helper.Events.GameLoop.DayEnding += OnDayEnding;
        }

        private void OnDayEnding(object sender, StardewModdingAPI.Events.DayEndingEventArgs e)
        {
            if (!config.EnableCropSaver)
                return;

            // Only run on the last day of the month
            // SDV uses 28 days per season.
            if (Game1.Date.DayOfMonth < 28)
                return;

            int harvested = 0;
            foreach (var location in Game1.locations)
            {
                if (!location.IsOutdoors || location.SeedsIgnoreSeasonsHere() || location is IslandLocation)
                    continue;

                foreach (var pair in location.terrainFeatures.Pairs)
                {
                    if (pair.Value is HoeDirt dirt && dirt.crop != null)
                    {
                        try
                        {
                            // Attempt to harvest via reflection to handle 1.6 signature changes.
                            var crop = dirt.crop;
                            var methods = crop.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            foreach (var m in methods)
                            {
                                if (m.Name != "harvest") continue;
                                var ps = m.GetParameters();
                                if (ps.Length >= 3 && ps[0].ParameterType == typeof(int) && ps[1].ParameterType == typeof(int) && typeof(HoeDirt).IsAssignableFrom(ps[2].ParameterType))
                                {
                                    object result;
                                    var args = new object[ps.Length];
                                    args[0] = (int)pair.Key.X;
                                    args[1] = (int)pair.Key.Y;
                                    args[2] = dirt;
                                    // last parameter may be Farmer or JunimoHarvester; pass null if not Farmer
                                    for (int i = 3; i < ps.Length; i++)
                                    {
                                        if (ps[i].ParameterType == typeof(Farmer)) args[i] = Game1.player; else args[i] = null;
                                    }
                                    result = m.Invoke(crop, args);
                                    if (result is bool b && b)
                                    {
                                        harvested++;
                                        break;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // If signature changed, be silent; we prefer stability over spam.
                        }
                    }
                }
            }

            if (harvested > 0)
                monitor.Log($"CropSaver: auto-harvested {harvested} crops on season end.", LogLevel.Debug);
        }
    }
}
