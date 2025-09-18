using MultiplayerAssistant.Chat;
using MultiplayerAssistant.Config;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiplayerAssistant;

namespace MultiplayerAssistant.HostAutomatorStages
{
    internal class AutomatedHost
    {
        private IModHelper helper;
        private BehaviorChain behaviorChain;
        private BehaviorState behaviorState;
        private readonly IMonitor _monitor;

        public AutomatedHost(IModHelper helper, IMonitor monitor, ModConfig config, EventDrivenChatBox chatBox)
        {
            behaviorChain = new BehaviorChain(helper, monitor, config, chatBox);
            behaviorState = new BehaviorState(monitor, chatBox);
            this.helper = helper;
            _monitor = monitor; // 保存监视器以统一使用 MonitorExtensions 扩展方法
        }

        public void Enable()
        {
            // 使用扩展日志：启用阶段，订阅游戏循环事件
            _monitor.Debug("启用 AutomatedHost：订阅 UpdateTicked / DayStarted 事件。", "AutomatedHost");
            helper.Events.GameLoop.UpdateTicked += OnUpdate;
            helper.Events.GameLoop.DayStarted += OnNewDay;
        }

        public void Disable()
        {
            // 使用扩展日志：禁用阶段，取消订阅游戏循环事件
            _monitor.Debug("禁用 AutomatedHost：取消订阅 UpdateTicked / DayStarted 事件。", "AutomatedHost");
            helper.Events.GameLoop.UpdateTicked -= OnUpdate;
            helper.Events.GameLoop.DayStarted -= OnNewDay;
        }

        private void OnNewDay(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            // 使用扩展日志：新的一天开始，重置行为状态
            _monitor.Debug("新的一天开始，重置行为状态。", "AutomatedHost");
            behaviorState.NewDay();
        }

        private void OnUpdate(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            behaviorChain.Process(behaviorState);
        }
    }
}
