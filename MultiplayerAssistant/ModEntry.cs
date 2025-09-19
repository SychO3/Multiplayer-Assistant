using MultiplayerAssistant.Config;
using MultiplayerAssistant.HostAutomatorStages;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MultiplayerAssistant
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        // TODO ModConfig value checking. But perhaps this actually should be done in the SelectFarmStage; if the
        // farm with the name given by the config exists, then none of the rest of the config values really matter,
        // except for the bat / mushroom decision and the pet name (the parts accessed mid-game rather than just at
        // farm creation).

        // TODO Add more config options, like the ability to disable the crop saver (perhaps still keep track of crops
        // in case it's enabled later, but don't alter them).

        // TODO Remove player limit (if the existing attempts haven't already succeeded in doing that).
        
        // TODO Make the host invisible to everyone else
        
        // TODO Consider what the automated host should do when another player proposes to them.

        private WaitCondition titleMenuWaitCondition;
        private ModConfig config;
        private bool farmStageEnabled;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            // 使用统一的日志扩展，设置默认前缀并开启调试日志
            // 中文说明：为保证各功能点都能输出 DEBUG 级别日志，便于线上排查问题
            MonitorExtensions.SetDefaultPrefix("MultiplayerAssistant");
            MonitorExtensions.SetLoggingEnabled(true);
            this.Monitor.Debug("Entry 开始，读取配置...", nameof(ModEntry));

            this.config = helper.ReadConfig<ModConfig>();
            this.Monitor.Debug("配置读取完成", nameof(ModEntry));

            // 控制台命令的注册已迁移至 TimeConsoleCommandListener，并在存档加载时注册

            // ensure that the game environment is in a stable state before the mod starts executing
            this.titleMenuWaitCondition = new WaitCondition(() => Game1.activeClickableMenu is StardewValley.Menus.TitleMenu, 5, this.Monitor);
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            this.Monitor.Debug("已订阅 GameLoop.UpdateTicked 事件", nameof(ModEntry));
        }

        /// <summary>
        /// Event handler to wait until a specific condition is met before executing.
        /// </summary>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            // 中文说明：等待进入标题菜单的稳定阶段后，再启动自动化开荒流程
            if (!this.farmStageEnabled && this.titleMenuWaitCondition.IsMet())
            {
                this.farmStageEnabled = true; // 标记为已启用，避免重复触发
                this.Monitor.Debug("TitleMenu 条件达成，启动 StartFarmStage", nameof(ModEntry));
                new StartFarmStage(this.Helper, Monitor, config).Enable();
            }
            // 中文说明：仅在存档载入完成后，对主机玩家维持无限体力与生命，避免影响到客机玩家
            // 最新 API 用法：Context.IsWorldReady 仍为推荐判断方式
            if (Context.IsWorldReady) 
            {
                // 仅主机执行该逻辑，确保 ServerBot 角色定位（房主）
                if (this.config != null && this.config.EnableHostKeepAlive && Context.IsMainPlayer && Game1.player is Farmer farmer)
                {
                    // 中文说明：每 Tick 维持生命与体力上限，不输出频繁日志以免刷屏
                    farmer.health = farmer.maxHealth; // int 赋值保持不变
                    farmer.stamina = farmer.maxStamina.Value; // 1.6 中 maxStamina 为 NetInt，取 Value 并转为 float 隐式
                }
            }
        }

        /// <summary>
        /// Represents wait condition.
        /// </summary>
        private class WaitCondition
        {
            private readonly System.Func<bool> condition;
            private int waitCounter;
            private readonly IMonitor monitor;

            public WaitCondition(System.Func<bool> condition, int initialWait, IMonitor monitor)
            {
                this.condition = condition;
                this.waitCounter = initialWait;
                this.monitor = monitor;
            }

            public bool IsMet()
            {
                if (this.waitCounter <= 0 && this.condition())
                {
                    // 中文说明：等待计数完毕且条件达成，输出一次调试日志（仅一次）
                    // 使用 LogOnce 避免重复
                    this.monitor.LogOnceWithContext(
                        "WaitCondition 达成",
                        LogLevel.Debug,
                        nameof(WaitCondition)
                    );
                    return true;
                }

                this.waitCounter--;
                return false;
            }
        }
    }
}
