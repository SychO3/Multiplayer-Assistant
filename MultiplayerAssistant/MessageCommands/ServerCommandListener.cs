using MultiplayerAssistant.Chat;
using MultiplayerAssistant.Config;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiplayerAssistant.MessageCommands
{
    /// <summary>
    /// 处理服务器命令的监听器，主要用于管理建筑移动权限
    /// </summary>
    internal class ServerCommandListener
    {
        private EventDrivenChatBox chatBox;
        private ModConfig config;
        private IModHelper helper;
        private readonly IMonitor monitor;

        public ServerCommandListener(IModHelper helper, ModConfig config, EventDrivenChatBox chatBox, IMonitor monitor)
        {
            this.helper  = helper;
            this.config  = config;
            this.chatBox = chatBox;
            this.monitor = monitor;
            // 中文说明：初始化服务器命令监听器
            monitor.Debug("服务器命令监听器初始化完成", nameof(ServerCommandListener));
        }

        public void Enable()
        {
            // 中文说明：启用服务器命令监听器
            chatBox.ChatReceived += chatReceived;
            monitor.Debug("服务器命令监听器已启用", nameof(ServerCommandListener));
        }

        public void Disable()
        {
            // 中文说明：禁用服务器命令监听器
            chatBox.ChatReceived -= chatReceived;
            monitor.Debug("服务器命令监听器已禁用", nameof(ServerCommandListener));
        }

        private void chatReceived(object sender, ChatEventArgs e)
        {
            var tokens = e.Message.Split(' ');

            if (tokens.Length == 0) { return; }

            tokens[0] = tokens[0].ToLower();

            // 中文说明：作为主机，你可以在聊天框中运行命令，使用斜杠(/)作为前缀
            // 参见: https://stardewcommunitywiki.com/Multiplayer
            var moveBuildPermissionCommand = new List<string>() { "mbp", "movebuildpermission", "movepermissiong" };
           
            if( (ChatBox.privateMessage == e.ChatKind               ) &&
                (moveBuildPermissionCommand.Any(tokens[0].Equals) ) )
            {
                // 中文说明：收到建筑移动权限命令
                monitor.Debug($"收到建筑移动权限命令：{e.Message}", nameof(ServerCommandListener));
                
                string newBuildPermission;

                if (2 == tokens.Length)
                {
                    newBuildPermission = tokens[1].ToLower();
                }
                else
                {
                    newBuildPermission = "";
                }

                var sourceFarmer = Game1.otherFarmers.Values
                    .Where( farmer => farmer.UniqueMultiplayerID == e.SourceFarmerId)
                    .FirstOrDefault()?
                    .Name ?? Game1.player.Name;

                var moveBuildPermissionParameter = new List<string>() { "off", "owned", "on" };

                if (moveBuildPermissionParameter.Any(newBuildPermission.Equals))
                {
                    if (config.MoveBuildPermission == newBuildPermission)
                    {
                        // 中文说明：参数已经是当前值
                        monitor.Warn($"参数已经是当前值：{config.MoveBuildPermission}，玩家：{sourceFarmer}", nameof(ServerCommandListener));
                        chatBox.textBoxEnter("/message " + sourceFarmer + " Error: The parameter is already " + config.MoveBuildPermission);
                    }
                    else
                    {
                        // 中文说明：更新建筑移动权限配置
                        monitor.Info($"更新建筑移动权限：{config.MoveBuildPermission} -> {newBuildPermission}，操作者：{sourceFarmer}", nameof(ServerCommandListener));
                        config.MoveBuildPermission = newBuildPermission;
                        chatBox.textBoxEnter(sourceFarmer + " Changed MoveBuildPermission to " + config.MoveBuildPermission);
                        chatBox.textBoxEnter("/mbp " + config.MoveBuildPermission);
                        helper.WriteConfig(config);
                    }
                }
                else
                {
                    // 中文说明：无效的参数值
                    monitor.Warn($"无效的建筑移动权限参数：{newBuildPermission}，玩家：{sourceFarmer}", nameof(ServerCommandListener));
                    chatBox.textBoxEnter("/message " + sourceFarmer + " Error: Only the following parameter are valid: " + String.Join(", ", moveBuildPermissionParameter.ToArray()));
                }
            }
        }
    }
}
