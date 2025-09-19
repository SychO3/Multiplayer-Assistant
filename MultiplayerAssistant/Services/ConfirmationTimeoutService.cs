using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MultiplayerAssistant.Services
{
    /// <summary>
    /// 集中管理“确认超时”逻辑：可注册一个在指定秒数后触发的一次性回调。
    /// </summary>
    internal sealed class ConfirmationTimeoutService
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;

        private readonly List<TimeoutItem> items = new();

        private class TimeoutItem
        {
            public double DueUtc;
            public Action Callback;
            public bool Fired;
        }

        public ConfirmationTimeoutService(IModHelper helper, IMonitor monitor)
        {
            this.helper = helper;
            this.monitor = monitor;
        }

        public void Enable()
        {
            helper.Events.GameLoop.UpdateTicked += OnUpdate;
        }

        public void Disable()
        {
            helper.Events.GameLoop.UpdateTicked -= OnUpdate;
            items.Clear();
        }

        public void AddTimeoutSeconds(int seconds, Action callback)
        {
            if (seconds <= 0 || callback == null)
                return;
            items.Add(new TimeoutItem
            {
                DueUtc = DateTime.UtcNow.AddSeconds(seconds).Ticks,
                Callback = callback,
                Fired = false
            });
        }

        private void OnUpdate(object sender, UpdateTickedEventArgs e)
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!it.Fired && nowTicks >= it.DueUtc)
                {
                    it.Fired = true;
                    try { it.Callback?.Invoke(); }
                    catch (Exception ex) { monitor.Exception(ex, "确认超时回调异常", nameof(ConfirmationTimeoutService)); }
                }
            }
            // 清理已触发
            items.RemoveAll(x => x.Fired);
        }
    }
}


