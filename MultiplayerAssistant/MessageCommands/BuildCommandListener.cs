using MultiplayerAssistant.Chat;
using Microsoft.Xna.Framework;
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
    internal class BuildCommandListener
    {
        private static Dictionary<string, Action<EventDrivenChatBox, Farmer>> buildingActions = new Dictionary<string, Action<EventDrivenChatBox, Farmer>>
        {
            {"stone_cabin", genBuildCabin("Stone Cabin")},
            {"plank_cabin", genBuildCabin("Plank Cabin")},
            {"log_cabin", genBuildCabin("Log Cabin")},
        };
        private static readonly string validBuildingNamesList = genValidBuildingNamesList();

        private EventDrivenChatBox chatBox;

        public BuildCommandListener(EventDrivenChatBox chatBox)
        {
            this.chatBox = chatBox;
        }

        private static Action<EventDrivenChatBox, Farmer> genBuildCabin(string cabinBlueprintName)
        {
            void buildCabin(EventDrivenChatBox chatBox, Farmer farmer)
            {
                // 中文说明：按方案A（apis_en 推荐流程）适配 1.6：
                // 通过打开 CarpenterMenu 进入官方建造界面，由玩家（主机）选择相应蓝图并放置。
                // 优点：兼容性最佳，不依赖 BluePrint 或内部 buildLock 签名。

                // 1) 打开木匠铺 UI（CarpenterMenu）
                try
                {
                    // 1.6 构造签名需要提供 builder 与位置，这里使用 Robin 与 ScienceHouse
                    Game1.activeClickableMenu = new CarpenterMenu("Robin", Game1.getLocationFromName("ScienceHouse"));
                    // 2) 提示玩家选择目标蓝图并放置
                    chatBox.textBoxEnter("/message " + farmer.Name + " Opened Carpenter menu. Please select '" + cabinBlueprintName + "' and place it in front of you.");
                    // 中文提示
                    chatBox.textBoxEnter("/message " + farmer.Name + " 已为你打开木匠铺界面，请选择 '" + cabinBlueprintName + "' 并在你面前一格放置。");
                }
                catch (Exception)
                {
                    // 兼容性兜底：若直接打开失败，则给出操作指引
                    chatBox.textBoxEnter("/message " + farmer.Name + " Error: Unable to open Carpenter menu automatically on SDV 1.6. Please visit Robin and select '" + cabinBlueprintName + "'.");
                }
            }
            return buildCabin;
        }

        private static string genValidBuildingNamesList()
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
            chatBox.ChatReceived += chatReceived;
        }

        public void Disable()
        {
            chatBox.ChatReceived -= chatReceived;
        }

        private void chatReceived(object sender, ChatEventArgs e)
        {
            // Private message chatKind is 3
            var tokens = e.Message.ToLower().Split(' ');
            if (tokens.Length == 0)
            {
                return;
            }
            if (e.ChatKind == 3 && tokens[0] == "build")
            {
                // Find the farmer it came from and determine their location
                foreach (var farmer in Game1.otherFarmers.Values)
                {
                    if (farmer.UniqueMultiplayerID == e.SourceFarmerId)
                    {
                        if (tokens.Length != 2)
                        {
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
                                action(chatBox, farmer);
                            }
                            else
                            {
                                chatBox.textBoxEnter("/message " + farmer.Name + " Error: You cannot place buildings outside of the farm!");
                            }
                        }
                        else
                        {
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
