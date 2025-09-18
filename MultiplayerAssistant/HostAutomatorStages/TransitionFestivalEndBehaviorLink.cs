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
    internal class TransitionFestivalEndBehaviorLink : BehaviorLink
    {
        public TransitionFestivalEndBehaviorLink(BehaviorLink next = null) : base(next)
        {
        }

        public override void Process(BehaviorState state)
        {
            // 中文说明：1.6 兼容，Utils.Festivals 方法签名调整且不再直接调用 SetLocalReady
            if (Utils.Festivals.ShouldLeave(state.GetNumOtherPlayers()) && !Utils.Festivals.IsWaitingToLeave())
            {
                if (state.HasBetweenTransitionFestivalEndWaitTicks())
                {
                    state.DecrementBetweenTransitionFestivalEndWaitTicks();
                } else
                {
                    // 中文说明：ReadyCheckDialog 负责就绪状态
                    Game1.activeClickableMenu = new ReadyCheckDialog("festivalEnd", allowCancel: true, delegate (Farmer who)
                    {
                        Game1.currentLocation.currentEvent.forceEndFestival(who);
                        state.DisableFestivalChatBox();
                    });
                    state.WaitForFestivalEnd();
                }
            } else if (!Utils.Festivals.ShouldLeave(state.GetNumOtherPlayers()) && Utils.Festivals.IsWaitingToLeave())
            {
                if (state.HasBetweenTransitionFestivalEndWaitTicks())
                {
                    state.DecrementBetweenTransitionFestivalEndWaitTicks();
                } else
                {
                    if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ReadyCheckDialog rcd)
                    {
                        rcd.closeDialog(Game1.player);
                    }
                    // 中文说明：对话框关闭后状态自然更新
                    state.StopWaitingForFestivalEnd();
                }
            }
            else
            {
                state.ClearBetweenTransitionFestivalEndWaitTicks();
                processNext(state);
            }
        }
    }
}
