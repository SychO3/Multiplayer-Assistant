using StardewValley;

namespace MultiplayerAssistant.HostAutomatorStages
{
    internal class UnlockCommunityCenterBehaviorLink : BehaviorLink
    {
        private bool isUnlocking;

        public UnlockCommunityCenterBehaviorLink(BehaviorLink next = null) : base(next)
        {
            isUnlocking = false;
        }

        public override void Process(BehaviorState state)
        {
            // 中文说明：1.6 迁移：事件 ID 使用字符串；Stats 字段为 PascalCase；isFestivalDay 使用 Season 枚举
            if (!Game1.player.eventsSeen.Contains("611439") && Game1.stats.DaysPlayed > 4 && Game1.timeOfDay >= 800 && Game1.timeOfDay <= 1300 && !Game1.IsRainingHere(Game1.getLocationFromName("Town")) && !isUnlocking && !Utility.isFestivalDay(Game1.Date.DayOfMonth, Game1.season))
            {
                Game1.warpFarmer("Town", 0, 54, 1);
                isUnlocking = true;
            }
            else if (isUnlocking && Game1.player.eventsSeen.Contains("611439")) {
                Game1.warpFarmer("Farm", 64, 10, 1);
                isUnlocking = false;
            }
            else
            {
                processNext(state);
            }
        }
    }
}
