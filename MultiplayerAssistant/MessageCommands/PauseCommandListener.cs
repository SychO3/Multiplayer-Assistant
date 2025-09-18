using MultiplayerAssistant.Chat;
using StardewModdingAPI;
using StardewValley;
using System.Linq;

namespace MultiplayerAssistant.MessageCommands
{
    /// <summary>
    /// 处理暂停游戏命令的监听器
    /// </summary>
    internal class PauseCommandListener
    {
        private EventDrivenChatBox chatBox;
        private readonly IMonitor monitor;

        public PauseCommandListener(EventDrivenChatBox chatBox, IMonitor monitor)
        {
            this.chatBox = chatBox;
            this.monitor = monitor;
        }

        public void Enable()
        {
            // 中文说明：启用暂停命令监听器，订阅聊天事件
            chatBox.ChatReceived += chatReceived;
            monitor.Debug("暂停命令监听器已启用", nameof(PauseCommandListener));
        }

        public void Disable()
        {
            // 中文说明：禁用暂停命令监听器，取消订阅聊天事件
            chatBox.ChatReceived -= chatReceived;
            monitor.Debug("暂停命令监听器已禁用", nameof(PauseCommandListener));
        }

        private void chatReceived(object sender, ChatEventArgs e)
        {
            var tokens = e.Message.ToLower().Split(' ');
            if (tokens.Length == 0)
            {
                return;
            }
            
            // 中文说明：私聊消息类型为 3，只处理私聊的 pause 命令
            if (e.ChatKind == 3 && tokens[0] == "pause")
            {
                // 中文说明：查找发送命令的玩家
                var sourceFarmer = Game1.otherFarmers.Values
                    .Where(farmer => farmer.UniqueMultiplayerID == e.SourceFarmerId)
                    .FirstOrDefault()?.Name ?? "未知玩家";
                
                monitor.Debug($"收到暂停命令：来自 {sourceFarmer}", nameof(PauseCommandListener));

                // 中文说明：切换游戏暂停状态
                Game1.netWorldState.Value.IsPaused = !Game1.netWorldState.Value.IsPaused;
                
                if (Game1.netWorldState.Value.IsPaused)
                {
                    chatBox.globalInfoMessage("游戏已暂停");
                    monitor.Info($"游戏已被 {sourceFarmer} 暂停", nameof(PauseCommandListener));
                }
                else
                {
                    chatBox.globalInfoMessage("游戏已恢复");
                    monitor.Info($"游戏已被 {sourceFarmer} 恢复", nameof(PauseCommandListener));
                }
            }
        }
    }
}
