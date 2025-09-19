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
        private Services.AutoKickService autoKickService = null;
        private Services.HostKeepAliveService hostKeepAliveService = null;
        private Services.ConfirmationTimeoutService confirmationTimeoutService = null;

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
            
			// 中文说明：1.6 可能更改了 FindSaveGames 的签名，使用健壮的反射调用以适配多种参数形式
			List<Farmer> farmers = null;
			try
			{
				var candidateMethods = typeof(LoadGameMenu)
					.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
					.Where(m => m.Name == "FindSaveGames")
					.ToArray();
				monitor.Debug($"检测到 LoadGameMenu.FindSaveGames 重载数量：{candidateMethods.Length}", nameof(StartFarmStage));

				object result = null;
				foreach (var m in candidateMethods)
				{
					var ps = m.GetParameters();
					var sig = string.Join(", ", ps.Select(p => $"{p.ParameterType.Name}{(p.IsOptional ? "?" : string.Empty)}"));
					monitor.Debug($"尝试调用重载：FindSaveGames({sig})", nameof(StartFarmStage));
					try
					{
						object[] args;
						if (ps.Length == 0)
						{
							args = Array.Empty<object>();
						}
						else
						{
							// 为可选参数提供默认值；值类型给定默认实例；引用类型给 null
							args = ps.Select(p =>
							{
								if (p.HasDefaultValue) return p.DefaultValue;
								if (p.IsOptional) return Type.Missing;
								var t = p.ParameterType;
								return t.IsValueType ? Activator.CreateInstance(t) : null;
							}).ToArray();
						}

						// 只调用静态方法。若未来变为实例方法，这里直接跳过以避免构造菜单实例的副作用。
						if (!m.IsStatic)
						{
							monitor.Debug("跳过非静态重载，以避免构造 LoadGameMenu 实例", nameof(StartFarmStage));
							continue;
						}

						result = m.Invoke(null, args);
						if (result != null)
							break;
					}
					catch (TargetParameterCountException tpex)
					{
						monitor.Debug($"参数数量不匹配：{tpex.Message}", nameof(StartFarmStage));
						continue;
					}
					catch (TargetInvocationException tiex)
					{
						monitor.Debug($"重载执行抛出：{tiex.InnerException?.Message ?? tiex.Message}", nameof(StartFarmStage));
						continue;
					}
					catch (Exception innerEx)
					{
						monitor.Debug($"FindSaveGames 重载调用失败：{innerEx.Message}", nameof(StartFarmStage));
						continue;
					}
				}

				if (result is List<Farmer> list)
				{
					farmers = list;
				}
				else if (result is IEnumerable<Farmer> enumerable)
				{
					farmers = enumerable.ToList();
				}
			}
			catch (Exception ex)
			{
				monitor.Error($"反射调用 LoadGameMenu.FindSaveGames 失败：{ex.Message}", nameof(StartFarmStage));
				return;
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
                monitor.Log($"Hosting {hostedFarmer.slotName} on co-op", LogLevel.Debug);
                
                // Mechanisms pulled from CoopMenu.HostFileSlot
                Game1.multiplayerMode = 2;
                SaveGame.Load(hostedFarmer.slotName);
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
                // 中文说明：1.6 中 catPerson 为只读，这里不直接设置宠物偏好，交由后续命名/选择流程处理

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
            var chatBox = new EventDrivenChatBox(monitor);
            monitor.Debug("替换游戏内 ChatBox 为 EventDrivenChatBox（带调试日志）", nameof(StartFarmStage));
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
            // 中文说明：1.6 可能已从 NetInt 变为普通 int，去掉 .Value 访问
            Game1.netWorldState.Value.CurrentPlayerLimit = int.MaxValue;
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

            // 中文说明：1.6 中 MiningLevel 为只读，使用经验机制将采矿等级提升到 10（经验值采用足够大的数值以确保达到 10 级）
            Game1.player.gainExperience(3, 20000);

            automatedHost = new AutomatedHost(helper, monitor, config, chatBox);
            automatedHost.Enable();

            buildCommandListener = new BuildCommandListener(chatBox, monitor);
            buildCommandListener.Enable();
            demolishCommandListener = new DemolishCommandListener(helper, chatBox, monitor);
            demolishCommandListener.Enable();
            pauseCommandListener = new PauseCommandListener(chatBox, monitor);
            pauseCommandListener.Enable();
            serverCommandListener = new ServerCommandListener(helper, config, chatBox, monitor);
            serverCommandListener.Enable();

            // 注册控制台命令：time（仅在存档加载完成后确保环境就绪）
            MultiplayerAssistant.ConsoleCommands.TimeConsoleCommandListener.Register(helper, monitor);

            // 启用自动踢出未活动玩家服务（仅主机侧）
            if (Context.IsMainPlayer && config.EnableAutoKickInactivePlayers)
            {
                autoKickService = new Services.AutoKickService(helper, monitor, chatBox, config);
                autoKickService.Enable();
            }

            // 启用主机保活服务（仅主机侧）
            if (Context.IsMainPlayer && config.EnableHostKeepAlive)
            {
                hostKeepAliveService = new Services.HostKeepAliveService(helper, monitor, config);
                hostKeepAliveService.Enable();
            }

            // 启用集中确认超时服务（仅主机侧）
            if (Context.IsMainPlayer)
            {
                confirmationTimeoutService = new Services.ConfirmationTimeoutService(helper, monitor);
                confirmationTimeoutService.Enable();
                Services.ServiceHub.ConfirmationTimeout = confirmationTimeoutService;
            }
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

            if (autoKickService != null)
            {
                autoKickService.Disable();
                autoKickService = null;
            }

            if (hostKeepAliveService != null)
            {
                hostKeepAliveService.Disable();
                hostKeepAliveService = null;
            }

            if (confirmationTimeoutService != null)
            {
                confirmationTimeoutService.Disable();
                Services.ServiceHub.ConfirmationTimeout = null;
                confirmationTimeoutService = null;
            }
        }
    }
}
