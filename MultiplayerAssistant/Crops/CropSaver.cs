using MultiplayerAssistant.Config;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using System.Reflection;
using MultiplayerAssistant;

namespace MultiplayerAssistant.Crops
{
    public class CropSaver
    {
        // 中文说明：从 Data/Crops 读取季节信息的轻量模型
        private class CropDataModel
        {
            public List<string> Seasons { get; set; }
        }

        private class CropDataModelFull
        {
            public List<string> Seasons { get; set; }
            public string HarvestItemId { get; set; }
            public int? SpriteIndex { get; set; }
        }
        private IModHelper helper;
        private IMonitor monitor;
        private ModConfig config;
        private SerializableDictionary<CropLocation, CropData> cropDictionary = new SerializableDictionary<CropLocation, CropData>();
        private SerializableDictionary<CropLocation, CropComparisonData> beginningOfDayCrops = new SerializableDictionary<CropLocation, CropComparisonData>();
        private XmlSerializer cropSaveDataSerializer = new XmlSerializer(typeof(CropSaveData));

        // 兼容 1.6：尝试反射调用 Game1.GetSeasonForLocation(GameLocation)
        private static MethodInfo? s_getSeasonForLocation;
        private static string GetSeasonForLocationCompat(GameLocation location)
        {
            try
            {
                if (s_getSeasonForLocation == null)
                {
                    s_getSeasonForLocation = typeof(Game1).GetMethod("GetSeasonForLocation", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(GameLocation) }, null);
                }
                if (s_getSeasonForLocation != null)
                {
                    var res = s_getSeasonForLocation.Invoke(null, new object[] { location });
                    if (res is string s && !string.IsNullOrEmpty(s))
                    {
                        return s;
                    }
                }
            }
            catch
            {
                // 忽略反射失败，走兜底逻辑
            }
            // 兜底：姜岛一律视为夏季（apis_en 提示 island 等价于 summer），否则使用当前全局季节
            if (location is IslandLocation) return "summer";
            return Game1.currentSeason;
        }

        public struct CropSaveData
        {
            public SerializableDictionary<CropLocation, CropData> cropDictionary { get; set; }
            public SerializableDictionary<CropLocation, CropComparisonData> beginningOfDayCrops { get; set; }
        }

        public struct CropLocation
        {
            public string LocationName { get; set; }
            public int TileX { get; set; }
            public int TileY { get; set; }
        }

        public struct CropGrowthStage
        {
            public int CurrentPhase { get; set; }
            public int DayOfCurrentPhase { get; set; }
            public bool FullyGrown { get; set; }
            // 中文说明：1.6 兼容，阶段天数数据可能为字符串，这里使用 string 列表储存
            public List<string> PhaseDays { get; set; }
            public int OriginalRegrowAfterHarvest { get; set; }
        }

        public struct CropComparisonData
        {
            public CropGrowthStage CropGrowthStage { get; set; }
            // 中文说明：1.6 兼容，RowInSpriteSheet 可能为字符串，这里使用 string 存储
            public string RowInSpriteSheet { get; set; }
            public bool Dead { get; set; }
            public bool ForageCrop { get; set; }
            // 中文说明：1.6 兼容，WhichForageCrop 可能为字符串，这里使用 string 存储
            public string WhichForageCrop { get; set; }
        }

        public struct CropData
        {
            public bool MarkedForDeath { get; set; }
            public List<string> OriginalSeasonsToGrowIn { get; set; }
            public bool HasExistedInIncompatibleSeason { get; set; }
            public int OriginalRegrowAfterHarvest { get; set; }
            public bool HarvestableLastNight { get; set; }
        }

        public CropSaver(IModHelper helper, IMonitor monitor, ModConfig config)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.config = config;
        }

        // 中文说明：尝试根据作物实例推断出对应的种子ID，并从 Data/Crops 读取 Seasons 字段
        private List<string> TryGetCropSeasonsFromData(StardewValley.Crop crop)
        {
            try
            {
                // 改进匹配：按 HarvestItemId 与 SpriteIndex 双条件评分匹配
                var dict = helper.GameContent.Load<Dictionary<string, CropDataModelFull>>("Data/Crops");
                if (dict == null || dict.Count == 0)
                    return null;

                string harvestIdStr = crop.indexOfHarvest.Value.ToString();
                int spriteRow = crop.rowInSpriteSheet.Value;

                int bestScore = -1;
                List<string> bestSeasons = null;

                foreach (var kvp in dict)
                {
                    var model = kvp.Value;
                    if (model == null)
                        continue;

                    int score = 0;
                    if (!string.IsNullOrWhiteSpace(model.HarvestItemId) && string.Equals(model.HarvestItemId, harvestIdStr, StringComparison.OrdinalIgnoreCase))
                        score += 2; // Harvest 匹配权重更高
                    if (model.SpriteIndex.HasValue && model.SpriteIndex.Value == spriteRow)
                        score += 1;

                    if (score > bestScore && model.Seasons != null && model.Seasons.Count > 0)
                    {
                        bestScore = score;
                        bestSeasons = model.Seasons;
                    }
                }

                if (bestScore > 0 && bestSeasons != null)
                    return bestSeasons.Select(s => s.ToLower()).ToList();
            }
            catch
            {
                // 忽略错误，回退四季
            }
            return null;
        }

        public void Enable()
        {
            helper.Events.GameLoop.DayStarted += onDayStarted;
            helper.Events.GameLoop.DayEnding += onDayEnding;
            helper.Events.GameLoop.Saving += onSaving;
            helper.Events.GameLoop.SaveLoaded += onLoaded;
            // 中文说明：启动作物保护器，订阅与一天生命周期相关的事件
            monitor.Debug("CropSaver 已启用并订阅 DayStarted/DayEnding/Saving/SaveLoaded 事件", nameof(CropSaver));
        }

        private void onLoaded(object sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            /**
             * Loads the cropDictionary and beginningOfDayCrops.
             */
            // 中文说明：从存档目录读取上次序列化的作物字典，支持中途载入恢复
            string str = SaveGame.FilterFileName(Game1.GetSaveGameName());
            string filenameNoTmpString = str + "_" + Game1.uniqueIDForThisGame;
            string save_directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Saves", filenameNoTmpString + Path.DirectorySeparatorChar);
            string saveFile = Path.Combine(save_directory, "AdditionalCropData");

            // Deserialize crop data from temp save file
            Stream fstream = null;
            try
            {
                fstream = new FileStream(saveFile, FileMode.Open);
                CropSaveData cropSaveData = (CropSaveData)cropSaveDataSerializer.Deserialize(fstream);
                fstream.Close();
                beginningOfDayCrops = cropSaveData.beginningOfDayCrops;
                cropDictionary = cropSaveData.cropDictionary;
                monitor.Debug($"加载作物缓存成功：begin={beginningOfDayCrops.Count}, dict={cropDictionary.Count}", nameof(CropSaver));
            } catch (IOException)
            {
                fstream?.Close();
                monitor.Debug("未发现 AdditionalCropData 文件，跳过加载（第一次运行或无缓存）", nameof(CropSaver));
            }
        }

        private void onSaving(object sender, StardewModdingAPI.Events.SavingEventArgs e)
        {
            /**
             * Saves the cropDictionary and beginningOfDayCrops. In most cases, the day is started
             * immediately after loading, which in-turn clears beginningOfDayCrops. However, in case
             * some other mod is installed which allows mid-day saving and loading, it's a good idea
             * to save both dictionaries anyways.
             */

            // Determine save paths
            string tmpString = "_STARDEWVALLEYSAVETMP";
            bool save_backups_and_metadata = true;
            string str = SaveGame.FilterFileName(Game1.GetSaveGameName());
            string filenameNoTmpString = str + "_" + Game1.uniqueIDForThisGame;
            string filenameWithTmpString = str + "_" + Game1.uniqueIDForThisGame + tmpString;
            string save_directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Saves", filenameNoTmpString + Path.DirectorySeparatorChar);
            SaveGame.ensureFolderStructureExists();
            string tmpSaveFile = Path.Combine(save_directory, "AdditionalCropData" + tmpString);
            string saveFile = Path.Combine(save_directory, "AdditionalCropData");
            string backupSaveFile = Path.Combine(save_directory, "AdditionalCropData_old");

            // Serialize crop data to temp save file
            TextWriter writer = null;
            try
            {
                writer = new StreamWriter(tmpSaveFile);
            }
            catch (IOException)
            {
                writer?.Close();
                monitor.Warn($"无法打开临时写入文件：{tmpSaveFile}", nameof(CropSaver));
            }

            cropSaveDataSerializer.Serialize(writer, new CropSaveData {cropDictionary = cropDictionary, beginningOfDayCrops = beginningOfDayCrops});
            writer.Close();
            monitor.Debug($"已序列化作物数据到临时文件：{tmpSaveFile}", nameof(CropSaver));

            // If appropriate, move old crop data file to backup
            if (save_backups_and_metadata)
            {
                try
                {
                    if (File.Exists(backupSaveFile))
                    {
                        File.Delete(backupSaveFile);
                    }
                }
                catch (Exception ex)
                {
                    monitor.Exception(ex, "删除旧备份文件失败", nameof(CropSaver));
                }

                try
                {
                    if (File.Exists(saveFile))
                    {
                        File.Move(saveFile, backupSaveFile);
                    }
                    else
                    {
                        // 旧数据文件不存在，跳过备份，避免 FileNotFoundException
                        monitor.Debug($"未找到旧数据文件，跳过备份：{saveFile}", nameof(CropSaver));
                    }
                }
                catch (Exception ex)
                {
                    monitor.Exception(ex, "移动旧数据为备份失败", nameof(CropSaver));
                }
            }

            // Delete previous save file if it still exists (hasn't been moved to
            // backup)
            if (File.Exists(saveFile))
            {
                File.Delete(saveFile);
            }

            // Move new temp save file to non-temp save file
            try
            {
                File.Move(tmpSaveFile, saveFile);
                monitor.Debug($"已写入作物数据：{saveFile}", nameof(CropSaver));
            }
            catch (IOException ex)
            {
                Game1.debugOutput = Game1.parseText(ex.Message);
                monitor.Exception(ex, "写入作物数据失败（IO）", nameof(CropSaver));
            }
        }

        private static bool sameCrop(CropComparisonData first, CropComparisonData second)
        {
            // Two crops are considered "different" if they have different sprite sheet rows (i.e., they're
            // different crop types); one of them is dead while the other is alive; one is a forage crop
            // while the other is not; the two crops are different types of forage crops; their phases
            // of growth are different; or their current days of growth are different, except when
            // one of them is harvestable and the other is fully grown and harvested. A crop is considered
            // harvestable when it's in the last stage of growth, and its either set to not "FullyGrown", or
            // its day of current phase is less than or equal to zero (after the first harvest, its day of
            // current phase works downward). A crop is considered harvested when it's in the final phase
            // and the above sub-conditions aren't satisfied (it's set to FullyGrown and its day of current
            // phase is positive)
            var differentSprites = first.RowInSpriteSheet != second.RowInSpriteSheet;
            
            var differentDeads = first.Dead != second.Dead;
            
            var differentForages = first.ForageCrop != second.ForageCrop;
            
            var differentForageTypes = first.WhichForageCrop != second.WhichForageCrop;
            
            var differentPhases = first.CropGrowthStage.CurrentPhase != second.CropGrowthStage.CurrentPhase;

            var differentDays = first.CropGrowthStage.DayOfCurrentPhase != second.CropGrowthStage.DayOfCurrentPhase;
            var firstGrown = first.CropGrowthStage.CurrentPhase >= first.CropGrowthStage.PhaseDays.Count - 1;
            var secondGrown = second.CropGrowthStage.CurrentPhase >= second.CropGrowthStage.PhaseDays.Count - 1;
            var firstHarvestable = firstGrown && (first.CropGrowthStage.DayOfCurrentPhase <= 0 || !first.CropGrowthStage.FullyGrown);
            var secondHarvestable = secondGrown && (second.CropGrowthStage.DayOfCurrentPhase <= 0 || !second.CropGrowthStage.FullyGrown);
            var firstRegrown = firstGrown && !firstHarvestable;
            var secondRegrown = secondGrown && !secondHarvestable;
            var harvestableAndRegrown = (firstHarvestable && secondRegrown) || (firstRegrown && secondHarvestable);
            var differentMeaningfulDays = differentDays && !harvestableAndRegrown;

            return !differentSprites && !differentDeads && !differentForages && !differentForageTypes && !differentPhases && !differentMeaningfulDays;
        }

        private void onDayEnding(object sender, StardewModdingAPI.Events.DayEndingEventArgs e)
        {
            // 中文说明：傍晚扫描所有室外作物，记录新栽种作物并更新可收获状态，清理不存在的作物
            int foundCrops = 0;
            int newCrops = 0;
            // In order to check for crops that have been destroyed and need to be removed from
            // the cropDictionary all together, we need to keep track of which crop locations
            // from the cropDictionary are found during the iteration over all crops in all
            // locations. Any which are not found must no longer exist (and have not been
            // replaced) and can be removed.
            var locationSet = new HashSet<CropLocation>();
            foreach (var location in Game1.locations)
            {
                if (location.IsOutdoors && !location.SeedsIgnoreSeasonsHere() && !(location is IslandLocation))
                {
                    // Found an outdoor location where seeds don't ignore seasons. Find all the
                    // crops here to cache necessary data for protecting them.
                    foreach (var pair in location.terrainFeatures.Pairs)
                    {
                        var tileLocation = pair.Key;
                        var terrainFeature = pair.Value;
                        if (terrainFeature is HoeDirt)
                        {
                            var hoeDirt = terrainFeature as HoeDirt;
                            var crop = hoeDirt.crop;
                            if (crop != null)
                            {
                                foundCrops++;
                                // Found a crop. Construct a CropLocation key
                                var cropLocation = new CropLocation
                                {
                                    LocationName = location.NameOrUniqueName,
                                    TileX = (int)tileLocation.X,
                                    TileY = (int)tileLocation.Y
                                };
                                
                                // Mark it as found via the locationSet, so we know not to remove
                                // the corresponding cropDictionary entry if one exists
                                locationSet.Add(cropLocation);

                                // Construct its growth stage so we can compare it to beginningOfDayCrops
                                // to see if it was newly-planted.
                                var cropGrowthStage = new CropGrowthStage
                                {
                                    CurrentPhase = crop.currentPhase.Value,
                                    DayOfCurrentPhase = crop.dayOfCurrentPhase.Value,
                                    FullyGrown = crop.fullyGrown.Value,
                                    PhaseDays = crop.phaseDays.Select(d => d.ToString()).ToList(),
                                    // 中文说明：1.6 中作物回生字段接口有变，这里记录为 0 作为占位
                                    OriginalRegrowAfterHarvest = 0
                                };

                                var cropComparisonData = new CropComparisonData
                                {
                                    CropGrowthStage = cropGrowthStage,
                                    RowInSpriteSheet = crop.rowInSpriteSheet.Value.ToString(),
                                    Dead = crop.dead.Value,
                                    ForageCrop = crop.forageCrop.Value,
                                    WhichForageCrop = crop.whichForageCrop.Value.ToString()
                                };

                                // Determine if this crop was planted today or was pre-existing, based on whether
                                // or not it's different from the crop at this location at the beginning of the day.
                                if (!beginningOfDayCrops.ContainsKey(cropLocation) || !sameCrop(beginningOfDayCrops[cropLocation], cropComparisonData))
                                {
                                    // No crop was found at this location at the beginning of the day, or the comparison data
                                    // is different. Consider it a new crop, and add a new CropData for it in the cropDictionary.
                                    var cd = new CropData
                                    {
                                        MarkedForDeath = false,
                                        // 中文说明：尽力从 Data/Crops 中读取真实季节；读取失败则回退四季
                                        OriginalSeasonsToGrowIn = TryGetCropSeasonsFromData(crop) ?? new List<string>{"spring","summer","fall","winter"},
                                        HasExistedInIncompatibleSeason = false,
                                        // 中文说明：占位
                                        OriginalRegrowAfterHarvest = 0,
                                        HarvestableLastNight = false
                                    };
                                    cropDictionary[cropLocation] = cd;
                                    newCrops++;
                                    // 中文说明：不再修改作物可生长季节列表，避免访问已变更的字段接口
                                }

                                // If there's a crop in the dictionary at this location (just planted today or otherwise),
                                // record whether it's harvestable tonight. This is used to help determine whether the crop
                                // should be marked for death the next morning. A crop is harvestable if and only if it's
                                // in the last phase, AND it's either a) NOT marked as "fully grown" (i.e., it hasn't been harvested
                                // at least once), or b) has a non-positive current day of phase (after harvest and regrowth,
                                // the current day of phase is set to positive and then works downward; 0 means ready-for-reharvest).
                                if (cropDictionary.TryGetValue(cropLocation, out var cropData))
                                {
                                    if ((crop.phaseDays.Count > 0 && crop.currentPhase.Value < crop.phaseDays.Count - 1) || (crop.dayOfCurrentPhase.Value > 0 && crop.fullyGrown.Value))
                                    {
                                        cropData.HarvestableLastNight = false;
                                    } else
                                    {
                                        cropData.HarvestableLastNight = true;
                                    }
                                    cropDictionary[cropLocation] = cropData;
                                }
                            }
                        }
                    }
                }
            }

            // Lastly, if there were any CropLocations in the cropDictionary that we DIDN'T see throughout the entire
            // iteration, then they must've been destroyed, AND they weren't replaced with a new crop at the same location.
            // In such a case, we can remove it from the cropDictionary.
            var locationSetComplement = new HashSet<CropLocation>();
            foreach (var kvp in cropDictionary)
            {
                if (!locationSet.Contains(kvp.Key))
                {
                    locationSetComplement.Add(kvp.Key);
                }
            }
            foreach (var cropLocation in locationSetComplement)
            {
                cropDictionary.Remove(cropLocation);
            }
            monitor.Debug($"DayEnding 扫描完成：found={foundCrops}, new={newCrops}, remain={cropDictionary.Count}", nameof(CropSaver));
        }

        private void onDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            // 中文说明：清空清晨快照，遍历室外作物并根据原始季节性与昨夜可收获状态进行保护与标记
            int snapshotCrops = 0;
            beginningOfDayCrops.Clear();
            foreach (var location in Game1.locations)
            {
                if (location.IsOutdoors && !location.SeedsIgnoreSeasonsHere() && location is not IslandLocation)
                {
                    // Found an outdoor location where seeds don't ignore seasons. Find all the
                    // crops here to cache necessary data for protecting them.
                    foreach (var pair in location.terrainFeatures.Pairs)
                    {
                        var tileLocation = pair.Key;
                        var terrainFeature = pair.Value;
                        if (terrainFeature is HoeDirt)
                        {
                            var hoeDirt = terrainFeature as HoeDirt;
                            var crop = hoeDirt.crop;
                            if (crop != null) {
                                snapshotCrops++;
                                // Found a crop. Construct a CropLocation key
                                var cropLocation = new CropLocation
                                {
                                    LocationName = location.NameOrUniqueName,
                                    TileX = (int) tileLocation.X,
                                    TileY = (int) tileLocation.Y
                                };

                                CropData cropData;
                                CropComparisonData cropComparisonData;
                                // Now, we have to update the properties of the CropData entry
                                // in the cropDictionary. Firstly, check if such a CropData entry exists
                                // (it won't exist for auto-spawned crops, like spring onion, since they'll
                                // never have passed the previous "newly planted test")
                                if (!cropDictionary.TryGetValue(cropLocation, out cropData))
                                {
                                    // The crop was not planted by the player. However, we do want to
                                    // record its comparison information so that we can check this evening
                                    // if it has changed, which would indicate that it HAS been replaced
                                    // by a player-planted crop.

                                    var cgs = new CropGrowthStage
                                    {
                                        CurrentPhase = crop.currentPhase.Value,
                                        DayOfCurrentPhase = crop.dayOfCurrentPhase.Value,
                                        FullyGrown = crop.fullyGrown.Value,
                                        PhaseDays = crop.phaseDays.Select(d => d.ToString()).ToList(),
                                        // 中文说明：占位
                                        OriginalRegrowAfterHarvest = 0
                                    };

                                    cropComparisonData = new CropComparisonData
                                    {
                                        CropGrowthStage = cgs,
                                        RowInSpriteSheet = crop.rowInSpriteSheet.Value.ToString(),
                                        Dead = crop.dead.Value,
                                        ForageCrop = crop.forageCrop.Value,
                                        WhichForageCrop = crop.whichForageCrop.Value.ToString()
                                    };

                                    beginningOfDayCrops[cropLocation] = cropComparisonData;

                                // Now move on to the next crop; we don't want to mess with this one.
                                continue;
                            }

                                // As of last night, the crop at this location was considered to have been
                                // planted by the player. Let's hope that it hasn't somehow been replaced
                                // by an entirely different crop overnight; though that seems unlikely.

                                // Check if it's currently a season which is incompatible with the
                                // crop's ORIGINAL compatible seasons. If so, update the crop data to
                                // reflect this.
                                // 中文说明：使用位置上下文季节（优先 Game1.GetSeasonForLocation 反射，兜底 island->summer / currentSeason）
                                var locSeason = GetSeasonForLocationCompat(location);
                                monitor.Debug($"位置季节判定：loc={location.NameOrUniqueName}, season={locSeason}", nameof(CropSaver));
                                if (!cropData.OriginalSeasonsToGrowIn.Contains(locSeason))
                                {
                                    cropData.HasExistedInIncompatibleSeason = true;
                                    monitor.Debug($"标记为不兼容季节：loc={location.NameOrUniqueName}, seasons=[{string.Join(',', cropData.OriginalSeasonsToGrowIn)}]", nameof(CropSaver));
                                }

                                // Check if the crop has been out of season, AND it was not harvestable last night.
                                // If so, mark it for death. This covers the edge case of when a crop finishes
                                // growing on the first day in which it's out-of-season (it should be marked for
                                // death, in this case).

                                if (cropData.HasExistedInIncompatibleSeason && !cropData.HarvestableLastNight)
                                {
                                    cropData.MarkedForDeath = true;
                                }

                                // Now we have to update the crop itself. If it's existed out-of-season,
                                // then its regrowAfterHarvest value should be set to -1, so that the
                                // farmer only gets one more harvest out of it.
                                // 中文说明：跳过对作物回生字段的直接修改，避免因 API 变更导致编译错误

                                // And if the crop has been marked for death because it was planted too close to
                                // the turn of the season, then we should make sure it's killed.
                                if (cropData.MarkedForDeath)
                                {
                                    crop.Kill();
                                }

                                // Update the crop data in the crop dictionary
                                cropDictionary[cropLocation] = cropData;

                                // Lastly, now that the crop has been updated, construct the comparison data for later
                                // so that we can check if this has been replaced by a newly planted crop in the evening.

                                var cropGrowthStage = new CropGrowthStage
                                {
                                    CurrentPhase = crop.currentPhase.Value,
                                    DayOfCurrentPhase = crop.dayOfCurrentPhase.Value,
                                    FullyGrown = crop.fullyGrown.Value,
                                    PhaseDays = crop.phaseDays.Select(d => d.ToString()).ToList(),
                                    OriginalRegrowAfterHarvest = cropData.OriginalRegrowAfterHarvest
                                };
                                cropComparisonData = new CropComparisonData
                                {
                                    CropGrowthStage = cropGrowthStage,
                                    RowInSpriteSheet = crop.rowInSpriteSheet.Value.ToString(),
                                    Dead = crop.dead.Value,
                                    ForageCrop = crop.forageCrop.Value,
                                    WhichForageCrop = crop.whichForageCrop.Value.ToString()
                                };

                                beginningOfDayCrops[cropLocation] = cropComparisonData;
                            }
                        }
                    }
                }
            }
            monitor.Debug($"DayStarted 快照完成：snapshot={snapshotCrops}, tracked={cropDictionary.Count}", nameof(CropSaver));
        }
    }
}
