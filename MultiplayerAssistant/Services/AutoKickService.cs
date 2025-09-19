using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using MultiplayerAssistant.Config;
using MultiplayerAssistant.Chat;

namespace MultiplayerAssistant.Services
{
    /// <summary>
    /// 自动踢出长时间未活动的玩家（软踢：发送警告与提示；不使用 Harmony，不调用非公开接口）。
    /// </summary>
    internal sealed class AutoKickService
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly EventDrivenChatBox chatBox;
        private readonly ModConfig config;

        // 中文说明：记录玩家最近一次活动时间（UTC）
        private readonly Dictionary<long, DateTime> playerLastActiveUtc = new();
        // 中文说明：记录已发过预警的玩家，避免刷屏
        private readonly HashSet<long> warnedPlayers = new();
        // 中文说明：上次周期性检查时间
        private DateTime lastCheckUtc = DateTime.UtcNow;
        // 中文说明：用于检测玩家移动的上一次已知坐标
        private readonly Dictionary<long, Vector2> lastKnownPositionByPlayer = new();

        public AutoKickService(IModHelper helper, IMonitor monitor, EventDrivenChatBox chatBox, ModConfig config)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.chatBox = chatBox;
            this.config = config;
        }

        public void Enable()
        {
            // 仅主机启用
            if (!Context.IsMainPlayer)
                return;

            // 订阅 SMAPI 事件
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Input.ButtonPressed += OnAnyActivity;
            helper.Events.Input.CursorMoved += OnAnyActivity;
            helper.Events.Display.MenuChanged += OnMenuChanged;
            helper.Events.Multiplayer.PeerConnected += OnPeerConnected;
            helper.Events.Multiplayer.PeerDisconnected += OnPeerDisconnected;
            chatBox.ChatReceived += OnChatReceived;

            monitor.Debug("AutoKickService 已启用（仅主机）", nameof(AutoKickService));
        }

        public void Disable()
        {
            if (!Context.IsMainPlayer)
                return;

            helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
            helper.Events.Input.ButtonPressed -= OnAnyActivity;
            helper.Events.Input.CursorMoved -= OnAnyActivity;
            helper.Events.Display.MenuChanged -= OnMenuChanged;
            helper.Events.Multiplayer.PeerConnected -= OnPeerConnected;
            helper.Events.Multiplayer.PeerDisconnected -= OnPeerDisconnected;
            chatBox.ChatReceived -= OnChatReceived;

            monitor.Debug("AutoKickService 已禁用", nameof(AutoKickService));
        }

        private void OnPeerConnected(object sender, PeerConnectedEventArgs e)
        {
            // 新连接的玩家，初始化为当前时间
            TouchActive(e.Peer.PlayerID, reason: "PeerConnected");
        }

        private void OnPeerDisconnected(object sender, PeerDisconnectedEventArgs e)
        {
            // 清理缓存
            playerLastActiveUtc.Remove(e.Peer.PlayerID);
            warnedPlayers.Remove(e.Peer.PlayerID);
            lastKnownPositionByPlayer.Remove(e.Peer.PlayerID);
        }

        private void OnChatReceived(object sender, ChatEventArgs e)
        {
            TouchActive(e.SourceFarmerId, reason: "Chat");
        }

        private void OnAnyActivity(object sender, EventArgs e)
        {
            // 本机活动：更新本机玩家（主机）的活跃时间
            if (Game1.player != null)
            {
                TouchActive(Game1.player.UniqueMultiplayerID, reason: "LocalInput");
            }
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (Game1.player != null)
            {
                TouchActive(Game1.player.UniqueMultiplayerID, reason: "MenuChanged");
            }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            // 每 10 秒检查一次
            if ((DateTime.UtcNow - lastCheckUtc).TotalSeconds < 10)
                return;
            lastCheckUtc = DateTime.UtcNow;

            if (!Context.IsMainPlayer)
                return;

            // 更新所有在线玩家位移活动
            foreach (var farmer in Game1.getOnlineFarmers())
            {
                // 跳过主机豁免
                if (config.AutoKickExemptHost && farmer.IsMainPlayer)
                    continue;

                // 白名单
                if (IsWhitelisted(farmer))
                    continue;

                // 位移活动检查
                var pos = farmer.Position;
                if (!lastKnownPositionByPlayer.TryGetValue(farmer.UniqueMultiplayerID, out var lastPos))
                {
                    lastKnownPositionByPlayer[farmer.UniqueMultiplayerID] = pos;
                    TouchActive(farmer.UniqueMultiplayerID, reason: "FirstSeen");
                }
                else if (lastPos != pos)
                {
                    lastKnownPositionByPlayer[farmer.UniqueMultiplayerID] = pos;
                    TouchActive(farmer.UniqueMultiplayerID, reason: "Move");
                }
            }

            // 不在世界、节日/剧情期间可选跳过（保守：仍然检查，若需可加配置）

            // 计算阈值
            int minutes = Math.Max(5, Math.Min(1440, config.AutoKickInactivityMinutes));
            var now = DateTime.UtcNow;
            foreach (var farmer in Game1.getOnlineFarmers())
            {
                if (config.AutoKickExemptHost && farmer.IsMainPlayer)
                    continue;
                if (IsWhitelisted(farmer))
                    continue;

                var id = farmer.UniqueMultiplayerID;
                if (!playerLastActiveUtc.TryGetValue(id, out var last))
                {
                    playerLastActiveUtc[id] = now;
                    continue;
                }

                var inactiveMinutes = (now - last).TotalMinutes;
                var remaining = minutes - inactiveMinutes;

                // 预警：剩余 <= 1 分钟 且未预警
                if (remaining <= 1 && remaining > 0 && !warnedPlayers.Contains(id))
                {
                    warnedPlayers.Add(id);
                    SendPrivateMessage(farmer.Name, $"You will be removed due to inactivity in {Math.Ceiling(remaining)} minute(s). 活动超时即将被移出。");
                    monitor.Warn($"预警未活动玩家：{farmer.Name}（{inactiveMinutes:F1}m）", nameof(AutoKickService));
                }

                // 超时
                if (inactiveMinutes >= minutes)
                {
                    TrySoftKick(farmer, inactiveMinutes);
                }
            }
        }

        private void TrySoftKick(Farmer farmer, double inactiveMinutes)
        {
            // 中文说明：公开 API 未提供直接踢出方法；采用软踢——私聊玩家并向主机公告。
            SendPrivateMessage(farmer.Name, "You were inactive too long. Please leave and rejoin later. 因长时间未活动，请离开并稍后再加入。");
            chatBox.globalInfoMessage($"[AutoKick] Player {farmer.Name} inactive for {inactiveMinutes:F1} minutes. 主机可在木匠铺或社交菜单管理玩家。");
            monitor.Info($"软踢提示：{farmer.Name} 已超过未活动阈值 {inactiveMinutes:F1}m。", nameof(AutoKickService));

            // 将其标记为已处理，避免重复刷屏
            playerLastActiveUtc[farmer.UniqueMultiplayerID] = DateTime.UtcNow;
            warnedPlayers.Remove(farmer.UniqueMultiplayerID);
        }

        private void SendPrivateMessage(string playerName, string message)
        {
            try
            {
                chatBox.textBoxEnter("/message " + playerName + " " + message);
            }
            catch (Exception ex)
            {
                monitor.Exception(ex, "发送私聊消息失败", nameof(AutoKickService));
            }
        }

        private bool IsWhitelisted(Farmer f)
        {
            try
            {
                if (config == null || config.AutoKickWhitelist == null || config.AutoKickWhitelist.Count == 0)
                    return false;

                // 支持名字或唯一ID匹配（忽略大小写）
                string id = f.UniqueMultiplayerID.ToString();
                return config.AutoKickWhitelist.Any(x => string.Equals(x, f.Name, StringComparison.OrdinalIgnoreCase) || string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private void TouchActive(long playerId, string reason)
        {
            playerLastActiveUtc[playerId] = DateTime.UtcNow;
            // 重置预警状态（再次活跃）
            warnedPlayers.Remove(playerId);
            monitor.Trace($"更新活跃时间：playerId={playerId}, reason={reason}", nameof(AutoKickService));
        }
    }
}
