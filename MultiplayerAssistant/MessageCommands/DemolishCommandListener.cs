using MultiplayerAssistant.Chat;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.Locations;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiplayerAssistant.MessageCommands
{
    /// <summary>
    /// 处理拆除建筑命令的监听器
    /// </summary>
    internal class DemolishCommandListener
    {
        private readonly IModHelper helper;
        private EventDrivenChatBox chatBox;
        private readonly IMonitor monitor;

        public DemolishCommandListener(IModHelper helper, EventDrivenChatBox chatBox, IMonitor monitor)
        {
            this.helper = helper;
            this.chatBox = chatBox;
            this.monitor = monitor;
            // 中文说明：初始化拆除命令监听器
            monitor.Debug("拆除命令监听器初始化完成", nameof(DemolishCommandListener));
        }

        public void Enable()
        {
            // 中文说明：启用拆除命令监听器
            chatBox.ChatReceived += chatReceived;
            monitor.Debug("拆除命令监听器已启用", nameof(DemolishCommandListener));
        }

        public void Disable()
        {
            // 中文说明：禁用拆除命令监听器
            chatBox.ChatReceived -= chatReceived;
            monitor.Debug("拆除命令监听器已禁用", nameof(DemolishCommandListener));
        }

        private void destroyCabin(string farmerName, Building building, Farm f)
        {
            // 中文说明：准备拆除建筑
            monitor.Debug($"准备拆除建筑，玩家：{farmerName}，建筑类型：{building.GetType().Name}", nameof(DemolishCommandListener));
            
            Action buildingLockFailed = delegate
            {
                monitor.Warn($"无法获取建筑锁，玩家：{farmerName}", nameof(DemolishCommandListener));
                chatBox.textBoxEnter("/message " + farmerName + " Error: " + Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_LockFailed"));
            };
            Action continueDemolish = delegate
            {
                if (building.daysOfConstructionLeft.Value > 0 || building.daysUntilUpgrade.Value > 0)
                {
                    // 中文说明：建筑正在建造或升级中
                    monitor.Warn($"建筑正在建造/升级中，无法拆除，玩家：{farmerName}", nameof(DemolishCommandListener));
                    chatBox.textBoxEnter("/message " + farmerName + " Error: " + Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_DuringConstruction"));
                }
                else if (building.indoors.Value != null && building.indoors.Value is AnimalHouse && (building.indoors.Value as AnimalHouse).animalsThatLiveHere.Count > 0)
                {
                    // 中文说明：建筑内有动物
                    monitor.Warn($"建筑内有动物，无法拆除，玩家：{farmerName}", nameof(DemolishCommandListener));
                    chatBox.textBoxEnter("/message " + farmerName + " Error: " + Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_AnimalsHere"));
                }
                else if (building.indoors.Value != null && building.indoors.Value.farmers.Any())
                {
                    // 中文说明：建筑内有玩家
                    monitor.Warn($"建筑内有玩家，无法拆除，玩家：{farmerName}", nameof(DemolishCommandListener));
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
                            // 中文说明：成功拆除建筑
                            monitor.Info($"成功拆除建筑，玩家：{farmerName}，建筑类型：{building.GetType().Name}", nameof(DemolishCommandListener));
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
                        else
                        {
                            // 中文说明：拆除失败
                            monitor.Error($"拆除建筑失败，玩家：{farmerName}", nameof(DemolishCommandListener));
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
            // 中文说明：私聊消息类型为 3
            if (!Context.IsMainPlayer)
            {
                return;
            }
            if (e.ChatKind == 3 && tokens[0] == "demolish")
            {
                monitor.Debug($"收到拆除命令：{e.Message}", nameof(DemolishCommandListener));
                // 中文说明：查找发送命令的玩家并确定其位置
                foreach (var farmer in Game1.otherFarmers.Values)
                {
                    if (farmer.UniqueMultiplayerID == e.SourceFarmerId)
                    {
                        // 帮助与状态
                        if (tokens.Length == 2 && (tokens[1] == "help" || tokens[1] == "?" || tokens[1] == "h"))
                        {
                            chatBox.textBoxEnter("/message " + farmer.Name + " Usage: demolish [help]");
                            chatBox.textBoxEnter("/message " + farmer.Name + " 说明: 面向目标建筑一格并站在其旁边，然后发送 demolish。");
                            chatBox.textBoxEnter("/message " + farmer.Name + " 注意: 拆除小屋需要二次确认并在30秒内回复 yes/no。");
                            return;
                        }

                        if (tokens.Length != 1)
                        {
                            // 中文说明：命令格式错误
                            monitor.Warn($"无效的拆除命令格式，玩家：{farmer.Name}", nameof(DemolishCommandListener));
                            chatBox.textBoxEnter("/message " + farmer.Name + " Error: Invalid command usage.");
                            chatBox.textBoxEnter("/message " + farmer.Name + " Usage: demolish [help]");
                            return;
                        }
                        var location = farmer.currentLocation;
                        if (location is Farm f)
                        {
                            // 中文说明：兼容 1.6 - 直接通过像素坐标换算瓦片坐标，避免依赖已变更的方法名
                            var tileLocation = new Vector2((int)(farmer.Position.X / 64f), (int)(farmer.Position.Y / 64f));
                            monitor.Debug($"玩家 {farmer.Name} 位置：{tileLocation}", nameof(DemolishCommandListener));
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
                                    // 中文说明：找到目标建筑
                                    monitor.Info($"找到建筑：{building.GetType().Name}，位置：{tileLocation}", nameof(DemolishCommandListener));
                                    
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
                                            // 中文说明：必须保留至少一个运输箱
                                            monitor.Warn($"不能拆除最后一个运输箱，玩家：{farmer.Name}", nameof(DemolishCommandListener));
                                            chatBox.textBoxEnter("/message " + farmer.Name + " Error: Can't demolish the last shipping bin.");
                                            return;
                                        }
                                    }

                                    if (building.indoors.Value is Cabin)
                                    {
                                        // 中文说明：Cabin 一律二次确认
                                        monitor.Info($"检测到小屋，需要二次确认，玩家：{farmer.Name}", nameof(DemolishCommandListener));
                                        var responseActions = new Dictionary<string, Action>();
                                        responseActions["yes"] = genDestroyCabinAction(farmer.Name, building);
                                        responseActions["no"] = genCancelDestroyCabinAction(farmer.Name);
                                        chatBox.RegisterFarmerResponseActionGroup(farmer.UniqueMultiplayerID, responseActions);
                                        var ownerId = (building.indoors.Value as Cabin).OwnerId;
                                        string ownerName = null;
                                        if (ownerId != 0)
                                        {
                                            var owner = Game1.getAllFarmers().FirstOrDefault(p => p.UniqueMultiplayerID == ownerId);
                                            ownerName = owner?.Name;
                                        }
                                        var ownerInfo = ownerName != null ? $" (owner: {ownerName})" : string.Empty;
                                        chatBox.textBoxEnter("/message " + farmer.Name + " This cabin may belong to a player" + ownerInfo + ". Are you sure you want to remove it? Message me \"yes\" or \"no\".");
                                        // 中文说明：通过集中服务在 30 秒后自动提示超时并清理回应
                                        Services.ServiceHub.ConfirmationTimeout?.AddTimeoutSeconds(30, () =>
                                        {
                                            chatBox.textBoxEnter("/message " + farmer.Name + " Demolish cabin confirmation timed out.");
                                            chatBox.RemoveResponsesForFarmer(farmer.UniqueMultiplayerID);
                                        });
                                        return;
                                    }

                                    // 中文说明：非小屋建筑，直接拆除无需确认
                                    monitor.Info($"直接拆除非小屋建筑，玩家：{farmer.Name}", nameof(DemolishCommandListener));
                                    destroyCabin(farmer.Name, building, f);
                                    return;
                                }
                            }

                            // 中文说明：未找到建筑
                            monitor.Warn($"未找到可拆除的建筑，玩家：{farmer.Name}，目标位置：{tileLocation}", nameof(DemolishCommandListener));
                            chatBox.textBoxEnter("/message " + farmer.Name + " Error: No building found. You must be standing next to a building and facing it.");
                        }
                        else
                        {
                            // 中文说明：不在农场中，无法拆除
                            monitor.Warn($"玩家 {farmer.Name} 不在农场中，无法拆除建筑", nameof(DemolishCommandListener));
                            chatBox.textBoxEnter("/message " + farmer.Name + " Error: You cannot demolish buildings outside of the farm.");
                        }
                        break;
                    }
                }
            }
        }
    }
}
