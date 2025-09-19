using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MultiplayerAssistant.HostAutomatorStages
{
    internal class TransitionSleepBehaviorLink : BehaviorLink
    {
        private static MethodInfo info = typeof(GameLocation).GetMethod("doSleep", BindingFlags.Instance | BindingFlags.NonPublic);
        private static int hostSleepAutoConfirmTicks = 0;
        private static bool sleepExecutionInProgress = false;
        private static int sleepAttemptCount = 0;
        
        static TransitionSleepBehaviorLink()
        {
            // 检查 doSleep 方法是否存在
            if (info == null)
            {
                Console.WriteLine("[MultiplayerAssistant] 警告: 没有找到 doSleep 方法!");
                
                // 尝试查找所有可能的睡觉相关方法
                var methods = typeof(GameLocation).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                Console.WriteLine("[MultiplayerAssistant] GameLocation 中包含 'sleep' 的方法:");
                foreach (var method in methods)
                {
                    if (method.Name.ToLower().Contains("sleep"))
                    {
                        Console.WriteLine($"[MultiplayerAssistant]   - {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
                    }
                }
            }
            else
            {
                var parameters = info.GetParameters();
                Console.WriteLine($"[MultiplayerAssistant] 找到 doSleep 方法，参数: ({string.Join(", ", parameters.Select(p => p.ParameterType.Name))})");
            }
        }

        public TransitionSleepBehaviorLink(BehaviorLink next = null) : base(next)
        {
        }

        public override void Process(BehaviorState state)
        {
            bool shouldSleep = Utils.Sleeping.ShouldSleep(state.GetNumOtherPlayers());
            bool isSleeping = Utils.Sleeping.IsSleeping();
            
            if (Game1.IsMasterGame && Game1.ticks % 300 == 0) // 每5秒输出一次基本信息
            {
                int numOtherPlayers = state.GetNumOtherPlayers();
                bool othersInBed = Utils.Sleeping.OthersInBed(numOtherPlayers);
                int readyCount = ReadyCheckHelper.GetNumberReady("sleep");
                int totalPlayers = Game1.getOnlineFarmers().Count;
                
                Console.WriteLine($"[MultiplayerAssistant] 时间={Game1.timeOfDay}, shouldSleep={shouldSleep}, isSleeping={isSleeping}");
                Console.WriteLine($"[MultiplayerAssistant] numOtherPlayers={numOtherPlayers}, othersInBed={othersInBed}, readyCount={readyCount}, totalPlayers={totalPlayers}");
            }
            
            if (shouldSleep && !isSleeping)
            {
                if (state.HasBetweenTransitionSleepWaitTicks())
                {
                    state.DecrementBetweenTransitionSleepWaitTicks();
                }
                else if (Game1.currentLocation is FarmHouse)
                {
                    Game1.player.isInBed.Value = true;
                    Game1.player.sleptInTemporaryBed.Value = true;
                    Game1.player.timeWentToBed.Value = Game1.timeOfDay;
                    // 使用 ReadyCheckHelper 设置准备状态
                    ReadyCheckHelper.SetLocalReady("sleep", true);
                    Game1.dialogueUp = false;
                    
                    // 将主机添加到已宣布睡觉的农民列表
                    if (!Game1.player.team.announcedSleepingFarmers.Contains(Game1.player))
                        Game1.player.team.announcedSleepingFarmers.Add(Game1.player);

                    // 主机（ServerBot）需要正确响应客户端的睡觉请求
                    if (Game1.IsMasterGame)
                    {
                        Console.WriteLine($"[MultiplayerAssistant] 主机准备睡觉");
                        
                        // 主机也创建 ReadyCheckDialog，但立即自动确认
                        Game1.activeClickableMenu = new ReadyCheckDialog("sleep", allowCancel: true, delegate
                        {
                            Game1.player.isInBed.Value = true;
                            Game1.player.sleptInTemporaryBed.Value = true;
                            Console.WriteLine($"[MultiplayerAssistant] 主机确认睡觉，尝试执行睡觉流程");
                            
                            try 
                            {
                                // 尝试多种方式执行睡觉
                                Console.WriteLine($"[MultiplayerAssistant] 方法1: 调用 doSleep");
                                info.Invoke(Game1.currentLocation, new object[]{});
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[MultiplayerAssistant] doSleep 调用失败: {ex.Message}");
                                
                                try 
                                {
                                    Console.WriteLine($"[MultiplayerAssistant] 方法2: 尝试使用 Game1 的睡觉方法");
                                    // 尝试直接调用游戏的睡觉流程
                                    Game1.NewDay(0f);
                                }
                                catch (Exception ex2)
                                {
                                    Console.WriteLine($"[MultiplayerAssistant] Game1.NewDay 调用失败: {ex2.Message}");
                                    
                                    try 
                                    {
                                        Console.WriteLine($"[MultiplayerAssistant] 方法3: 设置游戏状态为睡觉");
                                        // 设置游戏状态，让游戏自然进入睡觉流程
                                        Game1.timeOfDay = 2600;  // 设置时间为睡觉时间
                                        // Game1.shouldTimePass 在1.6中可能是方法，暂时跳过
                                    }
                                    catch (Exception ex3)
                                    {
                                        Console.WriteLine($"[MultiplayerAssistant] 所有睡觉方法都失败了: {ex3.Message}");
                                    }
                                }
                            }
                        }, delegate (Farmer who)
                        {
                            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ReadyCheckDialog rcd)
                            {
                                rcd.closeDialog(who);
                            }
                            who.timeWentToBed.Value = 0;
                        });
                        
                        // 设置延迟确认计时器（约2秒后自动确认）
                        hostSleepAutoConfirmTicks = 120;
                        Console.WriteLine($"[MultiplayerAssistant] 主机将在2秒后自动确认睡觉");
                        
                        state.Sleep();
                    }
                    else
                    {
                        // 非主机玩家显示等待对话框
                        Game1.activeClickableMenu = new ReadyCheckDialog("sleep", allowCancel: true, delegate
                        {
                            Game1.player.isInBed.Value = true;
                            Game1.player.sleptInTemporaryBed.Value = true;
                            Console.WriteLine($"[MultiplayerAssistant] 非主机玩家确认睡觉，尝试执行睡觉流程");
                            
                            try 
                            {
                                Console.WriteLine($"[MultiplayerAssistant] 非主机调用 doSleep");
                                info.Invoke(Game1.currentLocation, new object[]{});
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[MultiplayerAssistant] 非主机 doSleep 调用失败: {ex.Message}");
                            }
                        }, delegate (Farmer who)
                        {
                            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ReadyCheckDialog rcd)
                            {
                                rcd.closeDialog(who);
                            }

                            who.timeWentToBed.Value = 0;
                        });
                        
                        state.Sleep();
                    }
                }
                else
                {
                    var farmHouse = Game1.getLocationFromName("FarmHouse") as FarmHouse;
                    var entryLocation = farmHouse.getEntryLocation();
                    var warp = new Warp(entryLocation.X, entryLocation.Y, farmHouse.NameOrUniqueName, entryLocation.X, entryLocation.Y, false);
                    Game1.player.warpFarmer(warp);
                    state.WarpToSleep();
                }
            } else if (!Utils.Sleeping.ShouldSleep(state.GetNumOtherPlayers()) && Utils.Sleeping.IsSleeping())
            {
                if (state.HasBetweenTransitionSleepWaitTicks())
                {
                    state.DecrementBetweenTransitionSleepWaitTicks();
                }
                else
                {
                    if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ReadyCheckDialog rcd)
                    {
                        rcd.closeDialog(Game1.player);
                    }
                    // 使用 ReadyCheckHelper 设置准备状态
                    ReadyCheckHelper.SetLocalReady("sleep", false);
                    
                    // 重置主机自动确认计时器
                    if (Game1.IsMasterGame)
                    {
                        hostSleepAutoConfirmTicks = 0;
                        Console.WriteLine($"[MultiplayerAssistant] 取消睡觉，重置主机自动确认计时器");
                    }
                    
                    state.CancelSleep();
                }
            }
            else
            {
                // 处理主机睡觉自动确认计时器
                if (Game1.IsMasterGame && hostSleepAutoConfirmTicks > 0)
                {
                    hostSleepAutoConfirmTicks--;
                    if (hostSleepAutoConfirmTicks == 0)
                    {
                        Console.WriteLine($"[MultiplayerAssistant] 主机自动确认计时器到期，尝试确认睡觉");
                        try 
                        {
                            if (Game1.activeClickableMenu is ReadyCheckDialog hostDialog)
                            {
                                Console.WriteLine($"[MultiplayerAssistant] 执行主机自动确认睡觉");
                                hostDialog.confirm();
                            }
                            else
                            {
                                Console.WriteLine($"[MultiplayerAssistant] 没有找到主机的 ReadyCheckDialog");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[MultiplayerAssistant] 主机自动确认睡觉时出错: {ex.Message}");
                        }
                    }
                }
                
                // 如果主机已经准备好睡觉，但还在等待其他玩家
                if (Game1.IsMasterGame && Utils.Sleeping.IsSleeping() && Game1.player.isInBed.Value)
                {
                    // 检查是否所有人都准备好了
                    int totalPlayers = 0;
                    foreach (var farmer in Game1.getOnlineFarmers())
                    {
                        totalPlayers++;
                    }
                    
                    int readyCount = ReadyCheckHelper.GetNumberReady("sleep");
                    Console.WriteLine($"[MultiplayerAssistant] 主机等待中... 准备睡觉的玩家数: {readyCount}/{totalPlayers}");
                    
                    if (readyCount >= totalPlayers && !sleepExecutionInProgress)
                    {
                        // 防止无限循环 - 限制尝试次数
                        if (sleepAttemptCount >= 3)
                        {
                            Console.WriteLine($"[MultiplayerAssistant] 已尝试 {sleepAttemptCount} 次睡觉，跳过此次尝试");
                            return;
                        }
                        
                        sleepExecutionInProgress = true;
                        sleepAttemptCount++;
                        
                        // 所有人都准备好了，执行睡觉
                        Console.WriteLine($"[MultiplayerAssistant] 所有玩家都准备好了，现在执行睡觉 (尝试 {sleepAttemptCount}/3)");
                        
                        try 
                        {
                            Console.WriteLine($"[MultiplayerAssistant] 等待状态执行 doSleep");
                            info.Invoke(Game1.currentLocation, new object[]{});
                            
                            // 等待几秒看看是否成功
                            System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ => {
                                if (Game1.timeOfDay < 2600) // 如果时间没有变化，说明睡觉失败了
                                {
                                    Console.WriteLine($"[MultiplayerAssistant] doSleep 似乎没有成功，尝试其他方法");
                                    try 
                                    {
                                        Console.WriteLine($"[MultiplayerAssistant] 尝试强制进入新的一天");
                                        Game1.NewDay(0f);
                                    }
                                    catch (Exception ex2)
                                    {
                                        Console.WriteLine($"[MultiplayerAssistant] NewDay 也失败了: {ex2.Message}");
                                    }
                                }
                                
                                // 重置状态
                                sleepExecutionInProgress = false;
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[MultiplayerAssistant] 等待状态 doSleep 调用失败: {ex.Message}");
                            
                            try 
                            {
                                Console.WriteLine($"[MultiplayerAssistant] 等待状态尝试 NewDay");
                                Game1.NewDay(0f);
                            }
                            catch (Exception ex2)
                            {
                                Console.WriteLine($"[MultiplayerAssistant] 等待状态 NewDay 调用失败: {ex2.Message}");
                            }
                            finally 
                            {
                                sleepExecutionInProgress = false;
                            }
                        }
                    }
                }
                
                state.ClearBetweenTransitionSleepWaitTicks();
                processNext(state);
            }
        }
    }
}
