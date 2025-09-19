using MultiplayerAssistant.Chat;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Linq;
using System.Reflection;

namespace MultiplayerAssistant.MessageCommands
{
    internal class BuildCommandListener
    {
        private readonly EventDrivenChatBox chatBox;
        private readonly IMonitor monitor;

        public BuildCommandListener(IMonitor monitor, EventDrivenChatBox chatBox)
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
            if (e.ChatKind == 3 && tokens[0] == "build")
            {
                var farmer = Game1.otherFarmers.TryGetValue(e.SourceFarmerId, out var fmr) ? fmr : Game1.player;
                if (tokens.Length != 2)
                {
                    chatBox.textBoxEnter($"/message {farmer.Name} Usage: build [stone_cabin|plank_cabin|log_cabin]");
                    return;
                }

                string bpName = tokens[1] switch
                {
                    "stone_cabin" => "Stone Cabin",
                    "plank_cabin" => "Plank Cabin",
                    "log_cabin"   => "Log Cabin",
                    _ => null
                };

                if (bpName == null)
                {
                    chatBox.textBoxEnter($"/message {farmer.Name} Error: unknown building '{tokens[1]}'.");
                    return;
                }

                var farm = Game1.getFarm();
                // base tile in front of farmer
                var tile = new Vector2((int)(farmer.Position.X / 64f), (int)(farmer.Position.Y / 64f));
                switch (farmer.FacingDirection)
                {
                    case 1: tile.X += 1; break;
                    case 2: tile.Y += 1; break;
                    case 3: tile.X -= 1; break;
                    default: tile.Y -= 1; break;
                }

                monitor?.Log($"Build command by {farmer.Name}: '{bpName}'", LogLevel.Debug);
                // Build under team lock
                Game1.player.team.buildLock.RequestLock(delegate
                {
                    if (Game1.locationRequest != null) { Game1.player.team.buildLock.ReleaseLock(); return; }

                    string err = null;
                    bool ok = TryBuildWithReflection(farm, farmer, bpName, tile, out err);
                    if (ok)
                    {
                        chatBox.textBoxEnter($"{farmer.Name} just built a {bpName}");
                        monitor?.Log($"Build OK at {tile}: {bpName}", LogLevel.Info);
                    }
                    else
                    {
                        var msg = err ?? "can't place here.";
                        chatBox.textBoxEnter($"/message {farmer.Name} Error: {msg}");
                        monitor?.Log($"Build FAIL at {tile}: {bpName} ({msg})", LogLevel.Warn);
                    }

                    Game1.player.team.buildLock.ReleaseLock();
                });
            }
        }

        private static bool TryBuildWithReflection(Farm farm, Farmer who, string blueprintDisplayName, Vector2 nearTile, out string error)
        {
            error = null;
            try
            {
                var asm = typeof(Game1).Assembly;
                // find Blueprint type (either legacy 'BluePrint' or nested 'BlueprintsMenu.Blueprint')
                Type blueprintType = asm.GetTypes().FirstOrDefault(t => t.Name.Equals("BluePrint", StringComparison.Ordinal))
                                     ?? asm.GetTypes().FirstOrDefault(t => t.Name.Equals("Blueprint", StringComparison.Ordinal) && t.FullName.Contains("BlueprintsMenu"));
                if (blueprintType == null)
                {
                    error = "blueprint type not found";
                    return false;
                }

                // ctor(string displayName)
                object bp = Activator.CreateInstance(blueprintType, new object[] { blueprintDisplayName });
                if (bp == null)
                {
                    error = "failed to create blueprint";
                    return false;
                }

                // read width/height if available to adjust placement
                int tilesW = (int)(blueprintType.GetField("tilesWidth")?.GetValue(bp) ?? blueprintType.GetProperty("tilesWidth")?.GetValue(bp) ?? 0);
                int tilesH = (int)(blueprintType.GetField("tilesHeight")?.GetValue(bp) ?? blueprintType.GetProperty("tilesHeight")?.GetValue(bp) ?? 0);
                var place = new Vector2(nearTile.X - tilesW / 2f, nearTile.Y - tilesH / 2f);

                // try Farm.buildStructure(bp, pos, who, false)
                var farmType = farm.GetType();
                var methods = farmType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m => string.Equals(m.Name, "buildStructure", StringComparison.OrdinalIgnoreCase));
                foreach (var m in methods)
                {
                    var ps = m.GetParameters();
                    if (ps.Length >= 4 && ps[0].ParameterType == blueprintType && ps[1].ParameterType == typeof(Vector2))
                    {
                        var res = m.Invoke(farm, new object[] { bp, place, who, false });
                        if (res is bool b) return b;
                        return true;
                    }
                }

                // try Farm.tryToBuild(bp, pos, who) signature
                var tryMethod = farmType.GetMethod("tryToBuild", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (tryMethod != null)
                {
                    var ps = tryMethod.GetParameters();
                    object result;
                    if (ps.Length == 3)
                        result = tryMethod.Invoke(farm, new object[] { bp, place, who });
                    else if (ps.Length == 2)
                        result = tryMethod.Invoke(farm, new object[] { bp, place });
                    else
                        result = tryMethod.Invoke(farm, new object[] { bp, place, who, false });
                    if (result is bool bb) return bb;
                    return true;
                }

                error = "no compatible build method found";
                return false;
            }
            catch (TargetInvocationException tex)
            {
                error = tex.InnerException?.Message ?? tex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
