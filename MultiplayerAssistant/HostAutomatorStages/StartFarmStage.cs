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

        public StartFarmStage(IModHelper helper, IMonitor monitor, ModConfig config) : base(helper)
        {
            this.monitor = monitor;
            this.config = config;
            // 初始化 ReadyCheckHelper
            ReadyCheckHelper.Initialize(monitor);
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
            
            // 在1.6中，尝试使用不同的方法获取存档信息
            List<Farmer> farmers = null;
            try 
            {
                // 首先尝试不带参数的调用（兼容旧版本）
                MethodInfo info = typeof(LoadGameMenu).GetMethod("FindSaveGames", BindingFlags.Static | BindingFlags.NonPublic);
                if (info != null)
                {
                    // 检查方法参数
                    var parameters = info.GetParameters();
                    if (parameters.Length == 0)
                    {
                        // 无参数版本
                        object result = info.Invoke(null, Array.Empty<object>());
                        farmers = result as List<Farmer>;
                    }
                    else if (parameters.Length == 1)
                    {
                        // 可能需要一个参数（如路径）
                        object result = info.Invoke(null, new object[] { null });
                        farmers = result as List<Farmer>;
                    }
                    else
                    {
                        monitor.Debug($"FindSaveGames 方法需要 {parameters.Length} 个参数，无法自动适配");
                    }
                }
            }
            catch (Exception ex)
            {
                monitor.Debug($"通过反射调用 FindSaveGames 失败: {ex.Message}");
            }
            if (farmers == null)
            {
                return;
            }

            Farmer hostedFarmer = null;
            foreach (Farmer farmer in farmers)
            {
                if (!farmer.slotCanHost)
                {
                    continue;
                }
                if (farmer.farmName.Value == config.FarmName)
                {
                    hostedFarmer = farmer;
                    break;
                }
            }

            if (hostedFarmer != null)
            {
                monitor.Log($"Hosting {hostedFarmer.slotName} on co-op");
                
                // Mechanisms pulled from CoopMenu.HostFileSlot
                Game1.multiplayerMode = 2;
                SaveGame.Load(hostedFarmer.slotName);
                Game1.exitActiveMenu();
            }
            else
            {
                monitor.Log($"Failed to find farm slot. Creating new farm \"{config.FarmName}\" and hosting on co-op");
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
                // 在 1.6 中 catPerson 是只读的，需要使用其他方式设置宠物类型
                if (config.PetSpecies == "cat")
                {
                    // 记录选择但不直接设置 catPerson
                    monitor.Debug("玩家选择了猫作为宠物");
                }
                else
                {
                    monitor.Debug("玩家选择了狗作为宠物");
                }

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
                if (config.PetBreed.HasValue)
                {
                    Game1.player.whichPetBreed = config.PetBreed.Value.ToString();
                } else
                {
                    Game1.player.whichPetBreed = "0";
                }

                // Farm type
                if (config.FarmType != "standard" && config.FarmType != "riverland" && config.FarmType != "forest" && config.FarmType != "hilltop" && config.FarmType != "wilderness" && config.FarmType != "fourcorners" && config.FarmType != "beach")
                {
                    logConfigError("Farm type must be one of \"standard\", \"riverland\", \"forest\", \"hilltop\", \"wilderness\", \"fourcorners\", or \"beach\"");
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
            }

            Disable();
        }

        private void onSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            Game1.onScreenMenus.Remove(Game1.chatBox);
            var chatBox = new EventDrivenChatBox();
            Game1.chatBox = chatBox;
            Game1.onScreenMenus.Add(chatBox);
            // Update the player limits (remove them)
            // This breaks the game since there are loops which iterate in the range
            // (1, ..., HighestPlayerLimit). I think the only loops regarding this
            // value are around loading / creating cellar maps on world load...
            // maybe we just have to sacrifice cellar-per-player. Or maybe we have to
            // update the value dynamically, and load new cellars whenever a new player
            // joins? Unclear...
            //Game1.netWorldState.Value.HighestPlayerLimit.Value = int.MaxValue;
            Game1.netWorldState.Value.CurrentPlayerLimit = int.MaxValue; // 在1.6中CurrentPlayerLimit已经是int类型
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

            //We set bot mining lvl to 10 so he doesn't lvlup passively
            // 在 1.6 中 MiningLevel 是只读的，需要使用其他方式设置技能等级
            try
            {
                // 尝试使用经验值来设置等级
                Game1.player.experiencePoints[3] = 15000; // Mining skill
                monitor.Debug("设置挖矿技能等级为 10");
            }
            catch (Exception ex)
            {
                monitor.Debug($"无法设置挖矿技能等级: {ex.Message}");
            }

            automatedHost = new AutomatedHost(helper, monitor, config, chatBox);
            automatedHost.Enable();

            buildCommandListener = new BuildCommandListener(chatBox);
            buildCommandListener.Enable();
            demolishCommandListener = new DemolishCommandListener(chatBox);
            demolishCommandListener.Enable();
            pauseCommandListener = new PauseCommandListener(chatBox);
            pauseCommandListener.Enable();
            serverCommandListener = new ServerCommandListener(helper, config, chatBox);
            serverCommandListener.Enable();
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
        }
    }
}
