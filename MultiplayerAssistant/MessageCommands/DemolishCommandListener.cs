using DedicatedServer.Chat;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;

namespace DedicatedServer.MessageCommands
{
    internal class DemolishCommandListener
    {
        private readonly EventDrivenChatBox chatBox;
        private readonly IMonitor monitor;

        public DemolishCommandListener(IMonitor monitor, EventDrivenChatBox chatBox)
        {
            this.monitor = monitor;
            this.chatBox = chatBox;
        }

        public void Enable() => chatBox.ChatReceived += chatReceived;
        public void Disable() => chatBox.ChatReceived -= chatReceived;

        private void chatReceived(object sender, ChatEventArgs e)
        {
            var tokens = e.Message.ToLower().Split(' ');
            if (tokens.Length == 0) return;
            if (e.ChatKind == 3 && tokens[0] == "demolish")
            {
                var farmer = Game1.otherFarmers.TryGetValue(e.SourceFarmerId, out var fmr) ? fmr : Game1.player;
                monitor?.Log($"Demolish command by {farmer.Name}", LogLevel.Debug);
                var farm = Game1.getFarm();
                var tile = new Microsoft.Xna.Framework.Vector2((int)(farmer.Position.X / 64f), (int)(farmer.Position.Y / 64f));
                // 在面前一格查找建筑
                switch (farmer.FacingDirection)
                {
                    case 1: tile.X += 1; break;
                    case 2: tile.Y += 1; break;
                    case 3: tile.X -= 1; break;
                    default: tile.Y -= 1; break;
                }

                Building target = null;
                foreach (var b in farm.buildings)
                {
                    if (b.occupiesTile(tile)) { target = b; break; }
                }

                if (target == null)
                {
                    chatBox.textBoxEnter($"/message {farmer.Name} Error: no building in front of you.");
                    return;
                }

                // 避免破坏最后一个出货箱
                if (target is ShippingBin)
                {
                    int bins = 0;
                    foreach (var b in farm.buildings) if (b is ShippingBin) bins++;
                    if (bins <= 1)
                    {
                        chatBox.textBoxEnter($"/message {farmer.Name} Error: can't demolish the last shipping bin.");
                        return;
                    }
                }

                Game1.player.team.demolishLock.RequestLock(delegate
                {
                    bool ok = farm.destroyStructure(target);
                    if (ok)
                    {
                        Game1.flashAlpha = 1f;
                        target.showDestroyedAnimation(farm);
                        Utility.spreadAnimalsAround(target, farm);
                        chatBox.textBoxEnter($"{farmer.Name} demolished a {target.buildingType.Value}.");
                        monitor?.Log($"Demolish OK: {target.buildingType.Value} at {tile}", LogLevel.Info);
                    }
                    else
                    {
                        chatBox.textBoxEnter($"/message {farmer.Name} Error: can't demolish this building.");
                        monitor?.Log("Demolish FAIL: cannot demolish target", LogLevel.Warn);
                    }
                }, delegate
                {
                    chatBox.textBoxEnter($"/message {farmer.Name} Error: demolish lock failed.");
                    monitor?.Log("Demolish FAIL: demolish lock failed", LogLevel.Warn);
                });
            }
        }
    }
}
