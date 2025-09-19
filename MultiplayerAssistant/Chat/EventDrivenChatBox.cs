using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using StardewModdingAPI;
using MultiplayerAssistant;

namespace MultiplayerAssistant.Chat
{
    internal class EventDrivenChatBox : ChatBox
    {
        public event EventHandler<ChatEventArgs> ChatReceived;
        private readonly Dictionary<long, Dictionary<string, Tuple<List<string>, Action>>> farmerResponseActions = new Dictionary<long, Dictionary<string, Tuple<List<string>, Action>>>();
        private readonly IMonitor monitor;

        public EventDrivenChatBox(IMonitor monitor) : base()
        {
            // 中文说明：注入 IMonitor 以启用统一的调试日志输出
            this.monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            ChatReceived += tryResponseAction;
            this.monitor.Debug("EventDrivenChatBox 初始化并订阅 ChatReceived 事件", nameof(EventDrivenChatBox));
        }

        private void tryResponseAction(object sender, ChatEventArgs e)
        {
            if (e.ChatKind == 3 &&
                    farmerResponseActions.TryGetValue(e.SourceFarmerId, out var responseActionsForFarmer) &&
                    responseActionsForFarmer.TryGetValue(e.Message.ToLower(), out var responseAction)) {
                // Remove all response actions grouped with this response. This must be done
                // before executing the action, which could in-turn overwrite some of these
                // grouped responses. Otherwise, the overwritten one would be deleted.
                foreach (var groupedResponse in responseAction.Item1)
                {
                    responseActionsForFarmer.Remove(groupedResponse);
                }

                // Execute the action if not null
                if (responseAction.Item2 != null)
                {
                    // 中文说明：命中预设回复选项，执行对应动作
                    this.monitor.Debug($"触发回应动作：farmer={e.SourceFarmerId}, key='{e.Message.ToLower()}'", nameof(EventDrivenChatBox));
                    responseAction.Item2();
                }
            }
        }

        public override void receiveChatMessage(long sourceFarmer, int chatKind, LocalizedContentManager.LanguageCode language, string message)
        {
            base.receiveChatMessage(sourceFarmer, chatKind, language, message);
            if (ChatReceived != null)
            {
                var args = new ChatEventArgs
                {
                    SourceFarmerId = sourceFarmer,
                    ChatKind = chatKind,
                    LanguageCode = language,
                    Message = message
                };
                // 中文说明：收到聊天消息后转发事件，便于指令监听器订阅
                this.monitor.Debug($"收到聊天消息：farmer={sourceFarmer}, kind={chatKind}, len={message?.Length ?? 0}", nameof(EventDrivenChatBox));
                ChatReceived(this, args);
            }
        }

        public void RegisterFarmerResponseActionGroup(long farmerId, Dictionary<string, Action> responseActions)
        {
            // 中文说明：为特定农夫注册一组响应（互斥组），命中任一响应将移除该组
            Dictionary<string, Tuple<List<string>, Action>> responseActionsForFarmer;
            if (farmerResponseActions.TryGetValue(farmerId, out responseActionsForFarmer))
            {
                // Remove existing response groups for these farmer / responses. That is,
                // remove each of the responses as well as each of the responses grouped
                // with any of these responses.
                foreach (var response in responseActions.Keys)
                {
                    if (responseActionsForFarmer.TryGetValue(response, out var responseActionGroup))
                    {
                        foreach (var groupedResponse in responseActionGroup.Item1)
                        {
                            responseActionsForFarmer.Remove(groupedResponse);
                        }
                    }
                }
            }
            else
            {
                // The farmer does not yet have any response groups recorded; initialize
                // an empty dictionary for them
                farmerResponseActions.Add(farmerId, new Dictionary<string, Tuple<List<string>, Action>>());
                responseActionsForFarmer = farmerResponseActions[farmerId];
            }

            // Construct list of grouped response actions
            var responseGroup = new List<string>();
            foreach (var response in responseActions.Keys)
            {
                responseGroup.Add(response);
            }

            // Register all of the response actions
            foreach (var responseAction in responseActions)
            {
                responseActionsForFarmer[responseAction.Key] = new Tuple<List<string>, Action>(responseGroup, responseAction.Value);
            }

            this.monitor.Debug($"注册回应动作组：farmer={farmerId}, keys=[{string.Join(",", responseActions.Keys)}]", nameof(EventDrivenChatBox));
        }

        public void RemoveResponsesForFarmer(long farmerId)
        {
            if (farmerResponseActions.TryGetValue(farmerId, out var responseActionsForFarmer))
            {
                responseActionsForFarmer.Clear();
                farmerResponseActions.Remove(farmerId);
                this.monitor.Debug($"清理回应动作组：farmer={farmerId}", nameof(EventDrivenChatBox));
            }
        }
    }
}
