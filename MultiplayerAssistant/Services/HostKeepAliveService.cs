using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using MultiplayerAssistant.Config;

namespace MultiplayerAssistant.Services
{
    /// <summary>
    /// 主机保活服务：仅主机侧在世界就绪时维持生命与体力为最大值。
    /// </summary>
    internal sealed class HostKeepAliveService
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly ModConfig config;

        public HostKeepAliveService(IModHelper helper, IMonitor monitor, ModConfig config)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.config = config;
        }

        public void Enable()
        {
            if (!Context.IsMainPlayer || !config.EnableHostKeepAlive)
                return;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            monitor.Debug("HostKeepAliveService 已启用", nameof(HostKeepAliveService));
        }

        public void Disable()
        {
            helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
            monitor.Debug("HostKeepAliveService 已禁用", nameof(HostKeepAliveService));
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsMainPlayer || !Context.IsWorldReady)
                return;
            if (Game1.player is Farmer farmer)
            {
                // 中文说明：每 Tick 维持生命与体力上限，不输出频繁日志以免刷屏
                farmer.health = farmer.maxHealth;
                farmer.stamina = farmer.maxStamina.Value;
            }
        }
    }
}


