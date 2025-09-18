using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MultiplayerAssistant;

public class ModEntry : Mod
{   
    private Harmony? _harmony;

    public override void Entry(IModHelper helper)
    {
        // 初始化 Harmony 并应用补丁（如果有）
        this._harmony = new Harmony(this.ModManifest.UniqueID);
        this._harmony.PatchAll();

        // 初始化日志工具，后续模块可以直接调用 LogManager.* 输出日志
        LogManager.Initialize(this.Monitor, this.ModManifest.Name);

        // 控制台命令：在 SMAPI 控制台输入 `mpa_ping`
        helper.ConsoleCommands.Add("mpa_ping", "Ping test for MultiplayerAssistant", this.OnPingCommand);

        // 事件示例
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        LogManager.Info("Multiplayer Assistant loaded. Ready to help!");
    }

    private void OnPingCommand(string cmd, string[] args)
    {
        LogManager.Info("pong", "Console"); // "Console" 为上下文标签，可根据需要替换
    }

    
}
