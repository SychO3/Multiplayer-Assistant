using MultiplayerAssistant.Chat;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiplayerAssistant.MessageCommands
{
    /// <summary>
    /// 处理建造命令的监听器，允许玩家通过聊天命令建造小屋
    /// </summary>
    internal class BuildCommandListener
    {
        private readonly IMonitor monitor;
        private Dictionary<string, Action<EventDrivenChatBox, Farmer>> buildingActions;
        private string validBuildingNamesList;

        private EventDrivenChatBox chatBox;

        public BuildCommandListener(EventDrivenChatBox chatBox, IMonitor monitor)
        {
            this.chatBox = chatBox;
            this.monitor = monitor;
            
            // 中文说明：初始化建造命令映射表
            buildingActions = new Dictionary<string, Action<EventDrivenChatBox, Farmer>>
            {
                {"stone_cabin", genBuildCabin("Stone Cabin")},
                {"plank_cabin", genBuildCabin("Plank Cabin")},
                {"log_cabin", genBuildCabin("Log Cabin")},
            };
            validBuildingNamesList = genValidBuildingNamesList();
            
            monitor.Debug("建造命令监听器初始化完成", nameof(BuildCommandListener));
        }

        private Action<EventDrivenChatBox, Farmer> genBuildCabin(string cabinBlueprintName)
        {
            void buildCabin(EventDrivenChatBox chatBox, Farmer farmer)
            {
                // 中文说明：按方案A（apis_en 推荐流程）适配 1.6：
                // 通过打开 CarpenterMenu 进入官方建造界面，由玩家（主机）选择相应蓝图并放置。
                // 优点：兼容性最佳，不依赖 BluePrint 或内部 buildLock 签名。

                monitor.Debug($"处理建造命令：{cabinBlueprintName}，玩家：{farmer.Name}", nameof(BuildCommandListener));

                // 1) 打开木匠铺 UI（CarpenterMenu）
                try
                {
                    // 1.6 构造签名需要提供 builder 与位置，这里使用 Robin 与 ScienceHouse
                    Game1.activeClickableMenu = new CarpenterMenu("Robin", Game1.getLocationFromName("ScienceHouse"));
                    // 2) 提示玩家选择目标蓝图并放置
                    chatBox.textBoxEnter("/message " + farmer.Name + " Opened Carpenter menu. Please select '" + cabinBlueprintName + "' and place it in front of you.");
                    // 中文提示
                    chatBox.textBoxEnter("/message " + farmer.Name + " 已为你打开木匠铺界面，请选择 '" + cabinBlueprintName + "' 并在你面前一格放置。");
                    
                    monitor.Info($"已为玩家 {farmer.Name} 打开木匠铺界面，建造：{cabinBlueprintName}", nameof(BuildCommandListener));
                }
                catch (Exception ex)
                {
                    // 兼容性兜底：若直接打开失败，则给出操作指引
                    chatBox.textBoxEnter("/message " + farmer.Name + " Error: Unable to open Carpenter menu automatically on SDV 1.6. Please visit Robin and select '" + cabinBlueprintName + "'.");
                    monitor.Error($"无法自动打开木匠铺界面：{ex.Message}", nameof(BuildCommandListener));
                }
            }
            return buildCabin;
        }

        private string genValidBuildingNamesList()
        {
            string str = "";
            var buildingActionsEnumerable = buildingActions.Keys.ToArray();
            for (int i = 0; i < buildingActionsEnumerable.Length; i++)
            {
                str += "\"" + buildingActionsEnumerable[i] + "\"";
                if (i + 1 < buildingActionsEnumerable.Length)
                {
                    str += ", ";
                }
                if (i + 1 == buildingActionsEnumerable.Length - 1)
                {
                    str += "and ";
                }
            }
            return str;
        }

        private void pmValidBuildingNames(Farmer farmer)
        {
            var str = "/message " + farmer.Name + " Valid building names include " + validBuildingNamesList;
            chatBox.textBoxEnter(str);
        }

        public void Enable()
        {
            // 中文说明：启用建造命令监听器
            chatBox.ChatReceived += chatReceived;
            monitor.Debug("建造命令监听器已启用", nameof(BuildCommandListener));
        }

        public void Disable()
        {
            // 中文说明：禁用建造命令监听器
            chatBox.ChatReceived -= chatReceived;
            monitor.Debug("建造命令监听器已禁用", nameof(BuildCommandListener));
        }

        private void chatReceived(object sender, ChatEventArgs e)
        {
            // 中文说明：私聊消息类型为 3
            var tokens = e.Message.ToLower().Split(' ');
            if (tokens.Length == 0)
            {
                return;
            }
            if (e.ChatKind == 3 && tokens[0] == "build")
            {
                monitor.Debug($"收到建造命令：{e.Message}", nameof(BuildCommandListener));
                // 中文说明：查找发送命令的玩家并确定其位置
                foreach (var farmer in Game1.otherFarmers.Values)
                {
                    if (farmer.UniqueMultiplayerID == e.SourceFarmerId)
                    {
                        if (tokens.Length != 2)
                        {
                            // 中文说明：命令格式错误
                            monitor.Warn($"无效的建造命令格式，玩家：{farmer.Name}", nameof(BuildCommandListener));
                            chatBox.textBoxEnter("/message " + farmer.Name + " Error: Invalid command usage.");
                            chatBox.textBoxEnter("/message " + farmer.Name + " Usage: build [building_name]");
                            pmValidBuildingNames(farmer);
                            return;
                        }
                        var buildingName = tokens[1];
                        if (buildingActions.TryGetValue(buildingName, out var action))
                        {
                            var location = farmer.currentLocation;
                            if (location is Farm f)
                            {
                                // 中文说明：在农场中执行建造操作
                                monitor.Info($"玩家 {farmer.Name} 在农场执行建造：{buildingName}", nameof(BuildCommandListener));
                                action(chatBox, farmer);
                            }
                            else
                            {
                                // 中文说明：不在农场中，无法建造
                                monitor.Warn($"玩家 {farmer.Name} 不在农场中，无法建造", nameof(BuildCommandListener));
                                chatBox.textBoxEnter("/message " + farmer.Name + " Error: You cannot place buildings outside of the farm!");
                            }
                        }
                        else
                        {
                            // 中文说明：无法识别的建筑名称
                            monitor.Warn($"无法识别的建筑名称：{buildingName}，玩家：{farmer.Name}", nameof(BuildCommandListener));
                            chatBox.textBoxEnter("/message " + farmer.Name + " Error: Unrecognized building name \"" + buildingName + "\"");
                            pmValidBuildingNames(farmer);
                        }
                        break;
                    }
                }
            }
        }
    }
}
