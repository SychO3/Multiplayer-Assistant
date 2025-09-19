using MultiplayerAssistant.Chat;
using MultiplayerAssistant.Config;
using MultiplayerAssistant.Crops;
using MultiplayerAssistant.MessageCommands;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

// TODO move config value checking to the ModEntry, or another dedicated class, to be performed
// prior to any updates / Execute() calls. Also make sure to check validity of newly added fields, like
// the cave type selection and the PetName

namespace MultiplayerAssistant.HostAutomatorStages
{
    internal class StartFarmStage : HostAutomatorStage
    {
        private IMonitor monitor;
        private ModConfig config;
        private CropSaver cropSaver = null;
        private AutomatedHost automatedHost = null;
        private BuildCommandListener buildCommandListener = null;
        private DemolishCommandListener demolishCommandListener = null;
        private PauseCommandListener pauseCommandListener = null;
        private ServerCommandListener serverCommandListener = null;
        private ChestCommandListener chestCommandListener = null;
        private MultiplayerAssistant.Utils.InviteCodeWriter inviteCodeWriter = null;
        private MultiplayerAssistant.Utils.RelationshipProtector relationshipProtector = null;
        private JojaAutoUpgrader jojaAutoUpgrader = null;
        private MultiplayerAssistant.Features.ChestOwnershipBinder chestOwnershipBinder = null;
        private MultiplayerAssistant.Utils.AntiCheatHooks antiCheatHooks = null;
        private bool createdNewFarm = false;

        public StartFarmStage(IModHelper helper, IMonitor monitor, ModConfig config) : base(helper)
        {
            this.monitor = monitor;
            this.config = config;
            helper.Events.GameLoop.SaveLoaded += onSaveLoaded;
            if (config.EnableCropSaver) {
                cropSaver = new CropSaver(helper, monitor, config);
                cropSaver.Enable();
            }
            helper.Events.GameLoop.DayStarted += ReadyCheckHelper.OnDayStarted;
            helper.Events.GameLoop.ReturnedToTitle += onReturnToTitle;
        }

        private void logConfigError(string error)
        {
            monitor.Log($"Error in MultiplayerAssistant mod config file. {error}", LogLevel.Error);
        }

        private void exit(int statusCode)
        {
            monitor.Log("Exiting...", LogLevel.Error);
            Environment.Exit(statusCode);
        }

        public override void Execute(object sender, UpdateTickedEventArgs e)
        {
            if (Game1.activeClickableMenu is not TitleMenu menu)
            {
                return;
            }
            
            // Try to find a save folder whose farm name matches config.FarmName (1.6-safe, no reflection)
            string slotName = TryFindSaveSlotByFarmName(config.FarmName);
            if (slotName != null)
            {
                monitor.Log($"Hosting {slotName} on co-op", LogLevel.Debug);

                // Mechanisms pulled from CoopMenu.HostFileSlot
                Game1.multiplayerMode = 2;
                SaveGame.Load(slotName);
                Game1.exitActiveMenu();
            }
            else
            {
                monitor.Log($"Failed to find farm slot. Creating new farm \"{config.FarmName}\" and hosting on co-op", LogLevel.Debug);
                // Mechanism pulled from CoopMenu.HostNewFarmSlot; CharacterCustomization class; and AdvancedGameOptions class
                Game1.resetPlayer();

                // Starting cabins
                if (config.StartingCabins < 0 || config.StartingCabins > 3)
                {
                    logConfigError("Starting cabins must be an integer in [0, 3]");
                    exit(-1);
                }
                Game1.startingCabins = config.StartingCabins;

                // Cabin layout
                if (config.CabinLayout != "nearby" && config.CabinLayout != "separate")
                {
                    logConfigError("Cabin layout must be either \"nearby\" or \"separate\"");
                    exit(-1);
                }
                if (config.CabinLayout == "separate")
                {
                    Game1.cabinsSeparate = true;
                }
                else
                {
                    Game1.cabinsSeparate = false;
                }

                // Profit margin
                if (config.ProfitMargin != "normal" && config.ProfitMargin != "75%" && config.ProfitMargin != "50%" && config.ProfitMargin != "25%")
                {
                    logConfigError("Profit margin must be one of \"normal\", \"75%\", \"50%\", or \"25%\"");
                    exit(-1);
                }
                if (config.ProfitMargin == "normal")
                {
                    Game1.player.difficultyModifier = 1f;
                }
                else if (config.ProfitMargin == "75%")
                {
                    Game1.player.difficultyModifier = 0.75f;
                }
                else if (config.ProfitMargin == "50%")
                {
                    Game1.player.difficultyModifier = 0.5f;
                }
                else
                {
                    Game1.player.difficultyModifier = 0.25f;
                }

                // Money style
                if (config.MoneyStyle != "shared" && config.MoneyStyle != "separate")
                {
                    logConfigError("Money style must be either \"shared\" or \"separate\"");
                    exit(-1);
                }
                if (config.MoneyStyle == "separate")
                {
                    Game1.player.team.useSeparateWallets.Value = true;
                }
                else
                {
                    Game1.player.team.useSeparateWallets.Value = false;
                }

                // Farm name
                Game1.player.farmName.Value = config.FarmName;

                // Pet species
                if (config.PetSpecies != null && config.PetSpecies != "dog" && config.PetSpecies != "cat")
                {
                    logConfigError("PetSpecies must be either \"dog\" or \"cat\"");
                    exit(-1);
                }
                if (config.AcceptPet && config.PetSpecies == null)
                {
                    logConfigError("PetSpecies must be specified if AcceptPet is true");
                }
                // Pet species selection changed in 1.6; skip explicit cat/dog toggle here.

                // Pet breed
                if (config.PetBreed.HasValue && (config.PetBreed < 0 || config.PetBreed > 2))
                {
                    logConfigError("PetBreed must be an integer in [0, 2]");
                    exit(-1);
                }
                if (config.AcceptPet && !config.PetBreed.HasValue)
                {
                    logConfigError("PetBreed must be specified if AcceptPet is true");
                }
                // Pet breed handling changed in 1.6; skip setting here.

                // Farm type
                if (config.FarmType != "standard" && config.FarmType != "riverland" && config.FarmType != "forest" && config.FarmType != "hilltop" && config.FarmType != "wilderness" && config.FarmType != "fourcorners" && config.FarmType != "beach" && config.FarmType != "meadowlands")
                {
                    logConfigError("Farm type must be one of \"standard\", \"riverland\", \"forest\", \"hilltop\", \"wilderness\", \"fourcorners\", \"beach\", or \"meadowlands\"");
                    exit(-1);
                }
                if (config.FarmType == "standard")
                {
                    Game1.whichFarm = 0;
                }
                else if (config.FarmType == "riverland")
                {
                    Game1.whichFarm = 1;
                }
                else if (config.FarmType == "forest")
                {
                    Game1.whichFarm = 2;
                }
                else if (config.FarmType == "hilltop")
                {
                    Game1.whichFarm = 3;
                }
                else if (config.FarmType == "wilderness")
                {
                    Game1.whichFarm = 4;
                }
                else if (config.FarmType == "fourcorners")
                {
                    Game1.whichFarm = 5;
                }
                else if (config.FarmType == "beach")
                {
                    Game1.whichFarm = 6;
                }
                else if (config.FarmType == "meadowlands")
                {
                    Game1.whichFarm = 7; // SDV 1.6 new farm type
                }

                // Community center bundles type
                if (config.CommunityCenterBundles != "normal" && config.CommunityCenterBundles != "remixed")
                {
                    logConfigError("Community center bundles must be either \"normal\" or \"remixed\"");
                    exit(-1);
                }
                if (config.CommunityCenterBundles == "normal")
                {
                    Game1.bundleType = Game1.BundleType.Default;
                }
                else
                {
                    Game1.bundleType = Game1.BundleType.Remixed;
                }

                // Guarantee year 1 completable flag
                Game1.game1.SetNewGameOption("YearOneCompletable", config.GuaranteeYear1Completable);

                // Mine rewards type
                if (config.MineRewards != "normal" && config.MineRewards != "remixed")
                {
                    logConfigError("Mine rewards must be either \"normal\" or \"remixed\"");
                    exit(-1);
                }
                if (config.MineRewards == "normal")
                {
                    Game1.game1.SetNewGameOption("MineChests", Game1.MineChestType.Default);
                }
                else
                {
                    Game1.game1.SetNewGameOption("MineChests", Game1.MineChestType.Remixed);
                }

                // Monsters spawning at night on farm
                Game1.spawnMonstersAtNight = config.SpawnMonstersOnFarmAtNight;
                Game1.game1.SetNewGameOption("SpawnMonstersAtNight", config.SpawnMonstersOnFarmAtNight);

                // Random seed
                Game1.startingGameSeed = config.RandomSeed;

                // Configuration is done; Set server bot constants
                Game1.player.Name = "ServerBot";
                Game1.player.displayName = Game1.player.Name;
                Game1.player.favoriteThing.Value = "Farms";
                Game1.player.isCustomized.Value = true;
                Game1.multiplayerMode = 2;

                // Start game
                menu.createdNewCharacter(true);
                createdNewFarm = true;
            }

            Disable();
        }

        private static string TryFindSaveSlotByFarmName(string farmName)
        {
            try
            {
                // Base saves path (works cross-platform)
                var savesBase = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Saves");
                if (!System.IO.Directory.Exists(savesBase))
                    return null;

                foreach (var dir in System.IO.Directory.EnumerateDirectories(savesBase))
                {
                    // In each save folder, look for SaveGameInfo (temp or regular)
                    var infoPath = System.IO.Path.Combine(dir, "SaveGameInfo");
                    var tmpInfoPath = infoPath + "_STARDEWVALLEYSAVETMP";
                    var usePath = System.IO.File.Exists(infoPath) ? infoPath : (System.IO.File.Exists(tmpInfoPath) ? tmpInfoPath : null);
                    if (usePath == null)
                        continue;

                    // Read small XML and look for farmName
                    string xml = System.IO.File.ReadAllText(usePath);
                    if (xml.IndexOf("<farmName>", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // naive parse to avoid tight coupling
                        var startTag = "<farmName>";
                        var endTag = "</farmName>";
                        int si = xml.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
                        int ei = xml.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);
                        if (si >= 0 && ei > si)
                        {
                            si += startTag.Length;
                            var value = xml.Substring(si, ei - si).Trim();
                            if (string.Equals(value, farmName, StringComparison.Ordinal))
                            {
                                return System.IO.Path.GetFileName(dir);
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private void onSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            monitor.Log("Save loaded; installing event-driven chat box and enabling listeners.", LogLevel.Info);
            Game1.onScreenMenus.Remove(Game1.chatBox);
            var chatBox = new EventDrivenChatBox();
            Game1.chatBox = chatBox;
            Game1.onScreenMenus.Add(chatBox);
            // Ensure game keeps running when window not focused (server-friendly)
            try { Game1.options.pauseWhenOutOfFocus = false; } catch { }
            // Update the player limits (remove them)
            // This breaks the game since there are loops which iterate in the range
            // (1, ..., HighestPlayerLimit). I think the only loops regarding this
            // value are around loading / creating cellar maps on world load...
            // maybe we just have to sacrifice cellar-per-player. Or maybe we have to
            // update the value dynamically, and load new cellars whenever a new player
            // joins? Unclear...
            //Game1.netWorldState.Value.HighestPlayerLimit.Value = int.MaxValue;
            if (config.PlayerLimit > 0)
            {
                Game1.netWorldState.Value.CurrentPlayerLimit = config.PlayerLimit;
                monitor.Log($"Applied PlayerLimit from config: {config.PlayerLimit}", LogLevel.Info);
            }
            else
            {
                Game1.netWorldState.Value.CurrentPlayerLimit = int.MaxValue;
            }
            // NOTE: It will be very difficult, if not impossible, to remove the
            // cabin-per-player requirement. This requirement is very much built in
            // to much of the multiplayer networking connect / disconnect logic, and,
            // more importantly, every cabin has a SINGLE "farmhand" assigned to it.
            // Indeed, it's a 1-to-1 relationship---multiple farmers can't be assigned
            // to the same cabin. And this is a property of the cabin interface, so
            // it can't even be extended / modified. The most viable way to remove the
            // cabin-per-player requirement would be to create "invisible cabins"
            // which all sit on top of the farmhouse (for instance). They'd have
            // to be invisible (so that only the farmhouse is rendered), and
            // somehow they'd have to be made so that you can't collide with them
            // (though maybe this could be solved naturally by placing it to overlap
            // with the farmhouse in just the right position). Whenever a player enters
            // one of these cabins automatically (e.g., by warping home after passing out),
            // they'd have to be warped out of it immediately back into the farmhouse, since
            // these cabins should NOT be enterable in general (this part might be impossible
            // to do seamlessly, but it could theoretically be done in some manner). The mailbox
            // for the farmhouse would have to somehow be used instead of the cabin's mailbox (this
            // part might be totally impossible). And there would always have to be at least one
            // unclaimed invisible cabin at all times (every time one is claimed by a joining player,
            // create a new one). This would require a lot of work, and the mailbox part might
            // be totally impossible.

            // The command movebuildpermission is a standard command.
            // The server must be started, the value is set accordingly after each start
            chatBox.textBoxEnter("/mbp " + config.MoveBuildPermission);
            monitor.Log($"Applied MoveBuildPermission: {config.MoveBuildPermission}", LogLevel.Debug);

            // Mining level is read-only in 1.6; skip overriding.

            // Configure sleep behavior based on config
            MultiplayerAssistant.Utils.Sleeping.Configure(config.AutoSleepTime, config.ForceReturnToBed);

            automatedHost = new AutomatedHost(helper, monitor, config, chatBox);
            automatedHost.Enable();

            buildCommandListener = new BuildCommandListener(monitor, chatBox);
            buildCommandListener.Enable();
            demolishCommandListener = new DemolishCommandListener(monitor, chatBox);
            demolishCommandListener.Enable();
            pauseCommandListener = new PauseCommandListener(chatBox);
            pauseCommandListener.Enable();
            chestCommandListener = new ChestCommandListener(chatBox);
            chestCommandListener.Enable();
            serverCommandListener = new ServerCommandListener(helper, config, chatBox);
            serverCommandListener.Enable();

            // Enable chest locks (Harmony patch) if configured
            MultiplayerAssistant.Features.ChestLocks.Configure(monitor, config, chatBox);
            if (config.ChestLockEnabled)
            {
                chestOwnershipBinder = new MultiplayerAssistant.Features.ChestOwnershipBinder(helper, monitor, config);
                chestOwnershipBinder.Enable();
            }

            // Anti-cheat naming sanitizer
            if (config.AntiCheatSanitizeNames)
            {
                antiCheatHooks = new MultiplayerAssistant.Utils.AntiCheatHooks(helper, monitor, config);
                antiCheatHooks.Enable();
            }

            // Invite code writer
            if (config.WriteInviteCodeFile)
            {
                inviteCodeWriter = new MultiplayerAssistant.Utils.InviteCodeWriter(helper, monitor, config);
                inviteCodeWriter.Enable();
            }

            // Relationship protector (freeze host heart loss)
            if (config.FreezeHostHeartLoss)
            {
                relationshipProtector = new MultiplayerAssistant.Utils.RelationshipProtector(helper, monitor, config);
                relationshipProtector.Enable();
            }

            if (config.JojaAutoUpgradesEnabled)
            {
                jojaAutoUpgrader = new JojaAutoUpgrader(helper, monitor, config, chatBox);
                jojaAutoUpgrader.Enable();
            }

            // Post-load setup: extra cabins and buildings
            try
            {
                BuildConfiguredCabins();
                BuildConfiguredBuildings();
            }
            catch (Exception ex)
            {
                monitor.Log($"Error while building configured structures: {ex.Message}", LogLevel.Warn);
            }

            // First-day initialization for newly created farm
            helper.Events.GameLoop.DayStarted += onDayStartedInit;
        }

        private void onDayStartedInit(object sender, DayStartedEventArgs e)
        {
            this.helper.Events.GameLoop.DayStarted -= onDayStartedInit;
            if (!createdNewFarm)
                return;

            var dataKey = "objectmanagermanager.MultiplayerAssistant/InitApplied";
            if (Game1.player.modData.ContainsKey(dataKey))
                return;

            try
            {
                if (config.StartGold > 0)
                {
                    Game1.player.Money += config.StartGold * 100; // 1g == 100 money units
                    monitor.Log($"Granted start gold: {config.StartGold}g", LogLevel.Info);
                }

                ApplyStartSkills();

                if (config.StartMail != null)
                {
                    foreach (var m in config.StartMail)
                    {
                        if (!string.IsNullOrWhiteSpace(m))
                            Game1.addMailForTomorrow(m, noLetter: true, sendToEveryone: true);
                    }
                }

                ApplyStartWeather();

                Game1.player.modData[dataKey] = "true";
                monitor.Log("Initial farm setup applied.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                monitor.Log($"Error during initial setup: {ex.Message}", LogLevel.Warn);
            }
        }

        private static readonly int[] SkillLevelXp = new int[] { 0, 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000 };
        private void ApplyStartSkills()
        {
            if (config.StartSkills == null) return;
            TrySetSkill(0, config.StartSkills.Farming);
            TrySetSkill(1, config.StartSkills.Fishing);
            TrySetSkill(2, config.StartSkills.Foraging);
            TrySetSkill(3, config.StartSkills.Mining);
            TrySetSkill(4, config.StartSkills.Combat);
        }

        private void TrySetSkill(int skillIndex, int targetLevel)
        {
            targetLevel = Math.Max(0, Math.Min(10, targetLevel));
            int currentXp = 0;
            if (Game1.player.experiencePoints != null && Game1.player.experiencePoints.Length > skillIndex)
                currentXp = Game1.player.experiencePoints[skillIndex];
            int targetXp = SkillLevelXp[Math.Max(0, Math.Min(targetLevel, SkillLevelXp.Length - 1))];
            int diff = targetXp - currentXp;
            if (diff > 0)
                Game1.player.gainExperience(skillIndex, diff);
        }

        private void ApplyStartWeather()
        {
            if (string.IsNullOrWhiteSpace(config.StartWeatherDay1)) return;
            string w = config.StartWeatherDay1.Trim().ToLower();
            if (w == "sunny") Game1.weatherForTomorrow = Game1.weather_sunny;
            else if (w == "rain") Game1.weatherForTomorrow = Game1.weather_rain;
            else if (w == "storm") Game1.weatherForTomorrow = Game1.weather_lightning;
            else if (w == "wind") Game1.weatherForTomorrow = Game1.weather_debris;
        }

        private void BuildConfiguredCabins()
        {
            if (config.ExtraCabins <= 0 && (config.CabinPositions == null || config.CabinPositions.Count == 0)) return;
            string displayName = config.CabinStyle?.ToLower() switch
            {
                "plank" => "Plank Cabin",
                "log" => "Log Cabin",
                _ => "Stone Cabin"
            };

            int built = 0;
            var farm = Game1.getFarm();

            if (config.CabinPositions != null)
            {
                foreach (var pos in config.CabinPositions)
                {
                    if (TryBuildBlueprint(farm, displayName, new Microsoft.Xna.Framework.Vector2(pos.X, pos.Y)))
                        built++;
                }
            }
            int remaining = Math.Max(0, config.ExtraCabins - built);
            var baseTile = new Microsoft.Xna.Framework.Vector2(70, 15);
            for (int i = 0; i < remaining; i++)
            {
                var offset = new Microsoft.Xna.Framework.Vector2(i * 4, 0);
                TryBuildBlueprint(farm, displayName, baseTile + offset);
            }
            if (built > 0 || remaining > 0)
                monitor.Log($"Extra cabins requested: built ~{built + remaining} using style '{displayName}'.", LogLevel.Info);
        }

        private void BuildConfiguredBuildings()
        {
            // reserved for future extension (StartBuildings)
        }

        private bool TryBuildBlueprint(Farm farm, string blueprintDisplayName, Microsoft.Xna.Framework.Vector2 nearTile)
        {
            try
            {
                var asm = typeof(Game1).Assembly;
                var blueprintType = asm.GetTypes().FirstOrDefault(t => t.Name.Equals("BluePrint", StringComparison.Ordinal))
                                   ?? asm.GetTypes().FirstOrDefault(t => t.Name.Equals("Blueprint", StringComparison.Ordinal) && t.FullName.Contains("BlueprintsMenu"));
                if (blueprintType == null) return false;
                var bp = Activator.CreateInstance(blueprintType, new object[] { blueprintDisplayName });
                int tilesW = (int)(blueprintType.GetField("tilesWidth")?.GetValue(bp) ?? blueprintType.GetProperty("tilesWidth")?.GetValue(bp) ?? 0);
                int tilesH = (int)(blueprintType.GetField("tilesHeight")?.GetValue(bp) ?? blueprintType.GetProperty("tilesHeight")?.GetValue(bp) ?? 0);
                var place = new Microsoft.Xna.Framework.Vector2(nearTile.X - tilesW / 2f, nearTile.Y - tilesH / 2f);

                var farmType = farm.GetType();
                var methods = farmType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m => string.Equals(m.Name, "buildStructure", StringComparison.OrdinalIgnoreCase));
                foreach (var m in methods)
                {
                    var ps = m.GetParameters();
                    if (ps.Length >= 4 && ps[0].ParameterType == blueprintType && ps[1].ParameterType == typeof(Microsoft.Xna.Framework.Vector2))
                    {
                        var res = m.Invoke(farm, new object[] { bp, place, Game1.player, false });
                        if (res is bool b1) return b1; else return true;
                    }
                }

                var tryMethod = farmType.GetMethod("tryToBuild", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (tryMethod != null)
                {
                    var ps = tryMethod.GetParameters();
                    object result;
                    if (ps.Length == 3) result = tryMethod.Invoke(farm, new object[] { bp, place, Game1.player });
                    else if (ps.Length == 2) result = tryMethod.Invoke(farm, new object[] { bp, place });
                    else result = tryMethod.Invoke(farm, new object[] { bp, place, Game1.player, false });
                    if (result is bool bb) return bb; else return true;
                }
            }
            catch { }
            return false;
        }

        private void onReturnToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            automatedHost?.Disable();
            automatedHost = null;
            buildCommandListener?.Disable();
            buildCommandListener = null;
            demolishCommandListener?.Disable();
            demolishCommandListener = null;
            pauseCommandListener?.Disable();
            pauseCommandListener = null;
            serverCommandListener?.Disable();
            serverCommandListener = null;
            chestCommandListener?.Disable();
            chestCommandListener = null;
            inviteCodeWriter?.Disable();
            inviteCodeWriter = null;
            relationshipProtector?.Disable();
            relationshipProtector = null;
            jojaAutoUpgrader?.Disable();
            jojaAutoUpgrader = null;
            chestOwnershipBinder?.Disable();
            chestOwnershipBinder = null;
            antiCheatHooks?.Disable();
            antiCheatHooks = null;
        }
    }
}
