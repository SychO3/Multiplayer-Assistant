using StardewModdingAPI;
using StardewModdingAPI.Events;
using HarmonyLib;

namespace MultiplayerAssistant;

public class ModEntry : Mod
{   
    private Harmony? _harmony;

    public override void Entry(IModHelper helper)
    {
        // 初始化 Harmony 并应用补丁（如果有）
        this._harmony = new Harmony(this.ModManifest.UniqueID);
        this._harmony.PatchAll();

        // 控制台命令：在 SMAPI 控制台输入 `mpa_ping`
        helper.ConsoleCommands.Add("mpa_ping", "Ping test for MultiplayerAssistant", this.OnPingCommand);

        // 事件示例
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.Monitor.Log("Multiplayer Assistant loaded. Ready to help!", LogLevel.Info);
    }

    private void OnPingCommand(string cmd, string[] args)
    {
        this.Monitor.Log("pong", LogLevel.Info);
    }

    
}