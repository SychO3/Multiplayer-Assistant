using MultiplayerAssistant.Chat;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewModdingAPI;
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
                // 获取农民当前的瓦片位置
                var point = new Vector2((int)(farmer.Position.X / 64f), (int)(farmer.Position.Y / 64f));
                
                // 获取建筑数据
                var buildingData = Game1.buildingData;
                string buildingType = cabinBlueprintName.Replace(" ", "");
                
                if (!buildingData.ContainsKey(buildingType))
                {
                    chatBox.textBoxEnter("/message " + farmer.Name + " Error: Building type not found.");
                    return;
                }
                
                var buildingInfo = buildingData[buildingType];
                var tilesWidth = buildingInfo.Size.X;
                var tilesHeight = buildingInfo.Size.Y;
                
                switch (farmer.facingDirection.Value)
                {
                    case 1: // Right
                        point.X++;
                        point.Y -= (tilesHeight / 2);
                        break;
                    case 2: // Down
                        point.X -= (tilesWidth / 2);
                        point.Y++;
                        break;
                    case 3: // Left
                        point.X -= tilesWidth;
                        point.Y -= (tilesHeight / 2);
                        break;
                    default: // 0 = Up
                        point.X -= (tilesWidth / 2);
                        point.Y -= tilesHeight;
                        break;
                }
                
                Game1.player.team.buildLock.RequestLock(delegate
                {
                    if (Game1.locationRequest == null)
                    {
                        var farm = Game1.getFarm();
                        // 使用新的建筑创建方法
                        Building building = Building.CreateInstanceFromId(buildingType, point);
                        if (building != null && farm.buildStructure(building))
                        {
                            chatBox.textBoxEnter(farmer.Name + " just built a " + cabinBlueprintName);
                        }
                        else
                        {
                            chatBox.textBoxEnter("/message " + farmer.Name + " Error: " + Game1.content.LoadString("Strings\\UI:Carpenter_CantBuild"));
                        }
                    }
                    Game1.player.team.buildLock.ReleaseLock();
                });
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
