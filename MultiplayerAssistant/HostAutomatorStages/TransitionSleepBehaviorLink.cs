using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
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

        public TransitionSleepBehaviorLink(BehaviorLink next = null) : base(next)
        {
        }

        public override void Process(BehaviorState state)
        {
            // 中文说明：1.6 兼容，Sleeping 工具方法不再需要 Monitor 参数
            if (Utils.Sleeping.ShouldSleep(state.GetNumOtherPlayers()) && !Utils.Sleeping.IsSleeping())
            {
                if (state.HasBetweenTransitionSleepWaitTicks())
                {
                    state.DecrementBetweenTransitionSleepWaitTicks();
                }
                else if (Game1.currentLocation is FarmHouse)
                {
                    // 若已经有 ReadyCheckDialog 打开（无论是否为 sleep），不要重复创建，避免无限弹窗
                    if (Game1.activeClickableMenu is ReadyCheckDialog)
                    {
                        state.Sleep();
                        return;
                    }
                    // 如果已经宣布睡觉，则不要再次创建对话框，等待就绪同步
                    if (Game1.player.team.announcedSleepingFarmers.Contains(Game1.player))
                    {
                        state.Sleep();
                        return;
                    }

                    Game1.player.isInBed.Value = true;
                    Game1.player.sleptInTemporaryBed.Value = true;
                    Game1.player.timeWentToBed.Value = Game1.timeOfDay;
                    // 中文说明：1.6 不再直接调用 SetLocalReady，由 ReadyCheckDialog 负责就绪状态
                    Game1.dialogueUp = false;
                    Game1.activeClickableMenu = new ReadyCheckDialog("sleep", allowCancel: true, delegate
                    {
                        Game1.player.isInBed.Value = true;
                        Game1.player.sleptInTemporaryBed.Value = true;
                        // 接受后触发实际的睡觉过渡，避免所有人就绪后仍停留在等待界面
                        info.Invoke(Game1.currentLocation, new object[] { });
                    }, delegate (Farmer who)
                    {
                        if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ReadyCheckDialog rcd)
                        {
                            rcd.closeDialog(who);
                        }

                        who.timeWentToBed.Value = 0;
                        // 取消时同步撤销已宣布状态，避免保持 OthersInBed 为 true 导致循环弹窗
                        if (who.team.announcedSleepingFarmers.Contains(who))
                            who.team.announcedSleepingFarmers.Remove(who);
                    });

                    if (!Game1.player.team.announcedSleepingFarmers.Contains(Game1.player))
                        Game1.player.team.announcedSleepingFarmers.Add(Game1.player);

                    state.Sleep();
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
                    // 中文说明：ReadyCheckDialog 关闭后状态自然更新
                    state.CancelSleep();
                }
            }
            else
            {
                state.ClearBetweenTransitionSleepWaitTicks();
                processNext(state);
            }
        }
    }
}
