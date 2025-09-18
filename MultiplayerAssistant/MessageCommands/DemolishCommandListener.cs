using MultiplayerAssistant.Chat;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.Locations;
using StardewValley.Objects;
using System;
using System.Collections.Generic;

namespace MultiplayerAssistant.MessageCommands
{
    internal class DemolishCommandListener
    {
        private EventDrivenChatBox chatBox;

        public DemolishCommandListener(EventDrivenChatBox chatBox)
        {
            this.chatBox = chatBox;
        }

        public void Enable()
        {
            chatBox.ChatReceived += chatReceived;
        }

        public void Disable()
        {
            chatBox.ChatReceived -= chatReceived;
        }

        private void destroyCabin(string farmerName, Building building, Farm f)
        {
            Action buildingLockFailed = delegate
            {
                chatBox.textBoxEnter("/message " + farmerName + " Error: " + Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_LockFailed"));
            };
            Action continueDemolish = delegate
            {
                if (building.daysOfConstructionLeft.Value > 0 || building.daysUntilUpgrade.Value > 0)
                {
                    chatBox.textBoxEnter("/message " + farmerName + " Error: " + Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_DuringConstruction"));
                }
                else if (building.indoors.Value != null && building.indoors.Value is AnimalHouse && (building.indoors.Value as AnimalHouse).animalsThatLiveHere.Count > 0)
                {
                    chatBox.textBoxEnter("/message " + farmerName + " Error: " + Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_AnimalsHere"));
                }
                else if (building.indoors.Value != null && building.indoors.Value.farmers.Any())
                {
                    chatBox.textBoxEnter("/message " + farmerName + " Error: " + Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_PlayerHere"));
                }
                else
                {
                    if (building.indoors.Value != null && building.indoors.Value is Cabin)
                    {
                        // 中文说明：简化 1.6 兼容逻辑，仅判断是否有玩家在室内
                        foreach (Farmer allFarmer in Game1.getAllFarmers())
                        {
                            if (allFarmer.currentLocation != null && allFarmer.currentLocation == building.indoors.Value)
                            {
                                chatBox.textBoxEnter("/message " + farmerName + " Error: " + Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_PlayerHere"));
                                return;
                            }
                        }
                    }

                    // 中文说明：跳过 farmhand 在线检测（1.6 字段变更），改为一致的确认流程
                    else
                    {
                        building.BeforeDemolish();
                        Chest chest = null;
                        // 中文说明：1.6 下 Chest 的物品访问器变动，这里暂不将物品转移到箱子，避免 API 不兼容导致构建失败

                        if (f.destroyStructure(building))
                        {
                            _ = building.tileY.Value;
                            _ = building.tilesHigh.Value;
                            Game1.flashAlpha = 1f;
                            building.showDestroyedAnimation(Game1.getFarm());
                            Utility.spreadAnimalsAround(building, f);
                            if (chest != null)
                            {
                                f.objects[new Vector2(building.tileX.Value + building.tilesWide.Value / 2, building.tileY.Value + building.tilesHigh.Value / 2)] = chest;
                            }
                        }
                    }
                }
            };

            Game1.player.team.demolishLock.RequestLock(continueDemolish, buildingLockFailed);
        }

        private Action genDestroyCabinAction(string farmerName, Building building)
        {
            void destroyCabinAction()
            {
                Farm f = Game1.getFarm();
                destroyCabin(farmerName, building, f);
            }

            return destroyCabinAction;
        }

        private Action genCancelDestroyCabinAction(string farmerName)
        {
            void cancelDestroyCabinAction()
            {
                chatBox.textBoxEnter("/message " + farmerName + " Action canceled.");
            }

            return cancelDestroyCabinAction;
        }

        private void chatReceived(object sender, ChatEventArgs e)
        {
            var tokens = e.Message.ToLower().Split(' ');
            if (tokens.Length == 0)
            {
                return;
            }
            // Private message chatKind is 3
            if (e.ChatKind == 3 && tokens[0] == "demolish")
            {
                // Find the farmer it came from and determine their location
                foreach (var farmer in Game1.otherFarmers.Values)
                {
                    if (farmer.UniqueMultiplayerID == e.SourceFarmerId)
                    {
                        if (tokens.Length != 1)
                        {
                            chatBox.textBoxEnter("/message " + farmer.Name + " Error: Invalid command usage.");
                            chatBox.textBoxEnter("/message " + farmer.Name + " Usage: demolish");
                            return;
                        }
                        var location = farmer.currentLocation;
                        if (location is Farm f)
                        {
                            // 兼容 1.6：直接通过像素坐标换算瓦片坐标，避免依赖已变更的方法名
                            var tileLocation = new Vector2((int)(farmer.Position.X / 64f), (int)(farmer.Position.Y / 64f));
                            switch (farmer.facingDirection.Value)
                            {
                                case 1: // Right
                                    tileLocation.X++;
                                    break;
                                case 2: // Down
                                    tileLocation.Y++;
                                    break;
                                case 3: // Left
                                    tileLocation.X--;
                                    break;
                                default: // 0 = up
                                    tileLocation.Y--;
                                    break;
                            }
                            foreach (var building in f.buildings)
                            {
                                if (building.occupiesTile(tileLocation))
                                {
                                    // Determine if the building can be demolished


                                    // 中文说明：不再依赖 BluePrint 构造器，直接通过类型保护 Shipping Bin
                                    if (building is ShippingBin)
                                    {
                                        int num = 0;
                                        foreach (var b in Game1.getFarm().buildings)
                                        {
                                            if (b is ShippingBin)
                                            {
                                                num++;
                                            }

                                            if (num > 1)
                                            {
                                                break;
                                            }
                                        }

                                        if (num <= 1)
                                        {
                                            // Must have at least one shipping bin at all times.
                                            chatBox.textBoxEnter("/message " + farmer.Name + " Error: Can't demolish the last shipping bin.");
                                            return;
                                        }
                                    }

                                    if (building.indoors.Value is Cabin)
                                    {
                                        // 中文说明：Cabin 一律二次确认
                                        var responseActions = new Dictionary<string, Action>();
                                        responseActions["yes"] = genDestroyCabinAction(farmer.Name, building);
                                        responseActions["no"] = genCancelDestroyCabinAction(farmer.Name);
                                        chatBox.RegisterFarmerResponseActionGroup(farmer.UniqueMultiplayerID, responseActions);
                                        chatBox.textBoxEnter("/message " + farmer.Name + " This cabin may belong to a player. Are you sure you want to remove it? Message me \"yes\" or \"no\".");
                                        return;
                                    }

                                    // The cabin doesn't belong to anyone. Destroy it immediately without confirmation.
                                    destroyCabin(farmer.Name, building, f);
                                    return;
                                }
                            }

                            chatBox.textBoxEnter("/message " + farmer.Name + " Error: No building found. You must be standing next to a building and facing it.");
                        }
                        else
                        {
                            chatBox.textBoxEnter("/message " + farmer.Name + " Error: You cannot demolish buildings outside of the farm.");
                        }
                        break;
                    }
                }
            }
        }
    }
}
