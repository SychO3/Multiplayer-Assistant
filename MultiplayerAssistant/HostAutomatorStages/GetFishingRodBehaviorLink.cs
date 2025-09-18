using StardewValley;

namespace MultiplayerAssistant.HostAutomatorStages
{
    internal class GetFishingRodBehaviorLink : BehaviorLink
    {
        private bool isGettingFishingRod;

        public GetFishingRodBehaviorLink(BehaviorLink next = null) : base(next)
        {
            isGettingFishingRod = false;
        }

        public override void Process(BehaviorState state)
        {
            //If we don't get the fishing rod, Willy isn't available
            // 中文说明：1.6 迁移后部分 ID 参数改为字符串，这里将事件与任务 ID 改为字符串
            if (!Game1.player.eventsSeen.Contains("739330") && Game1.player.hasQuest("13") && Game1.timeOfDay <= 1710 && !isGettingFishingRod && !Utility.isFestivalDay(Game1.Date.DayOfMonth, Game1.Date.Season))
            {
                Game1.warpFarmer("Beach", 38, 0, 1);
                isGettingFishingRod = true;
            }
            else if (isGettingFishingRod && Game1.player.eventsSeen.Contains("739330")) {
                Game1.warpFarmer("Farm", 64, 10, 1);
                isGettingFishingRod = false;
            }
            else
            {
                processNext(state);
            }
        }
    }
}
