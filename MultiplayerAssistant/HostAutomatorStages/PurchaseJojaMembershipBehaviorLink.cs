using MultiplayerAssistant.Config;
using StardewValley;

namespace MultiplayerAssistant.HostAutomatorStages
{
    internal class PurchaseJojaMembershipBehaviorLink : BehaviorLink
    {
        private ModConfig config;

        public PurchaseJojaMembershipBehaviorLink(ModConfig config, BehaviorLink next = null) : base(next) {
            this.config = config;
        }

        public override void Process(BehaviorState state)
        {
            // If the community center has been unlocked, the config specifies that the host
            // should purchase the joja membership, and the host has not yet purchased it...
            // 中文说明：1.6 后事件 ID 使用字符串
            var ccAvailable = Game1.player.eventsSeen.Contains("611439");
            var purchased = Game1.player.mailForTomorrow.Contains("JojaMember%&NL&%") || Game1.player.mailReceived.Contains("JojaMember");
            if (ccAvailable && config.PurchaseJojaMembership && !purchased)
            {
                // Then purchase it
                Game1.addMailForTomorrow("JojaMember", noLetter: true, sendToEveryone: true);
                // 中文说明：任务 ID 参数改为字符串
                Game1.player.removeQuest("26");
            }
            else
            {
                processNext(state);
            }
        }
    }
}
