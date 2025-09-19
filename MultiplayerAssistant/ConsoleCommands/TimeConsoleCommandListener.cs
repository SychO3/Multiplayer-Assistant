using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace MultiplayerAssistant.ConsoleCommands
{
	/// <summary>
	/// 控制台命令监听器：time（仅主机使用，控制台内）
	/// </summary>
	internal static class TimeConsoleCommandListener
	{
		private static bool isRegistered = false;

		public static void Register(IModHelper helper, IMonitor monitor)
		{
			if (isRegistered)
			{
				return;
			}

			helper.ConsoleCommands.Add(
				"time",
				"ServerBot 时间管理: time status|set HHMM|add MIN\n例如: time set 1300, time add -30",
				(name, args) =>
				{
					// 仅主机可用
					if (!Context.IsMainPlayer)
					{
						monitor.Warn("time 仅主机可用", nameof(TimeConsoleCommandListener));
						return;
					}

					// 帮助与状态
					if (args.Length == 0 || (args.Length >= 1 && (args[0].ToLower() == "help" || args[0].ToLower() == "?" || args[0].ToLower() == "h")))
					{
						monitor.Log("用法: time [status|set HHMM|add MIN]", LogLevel.Info);
						monitor.Log("status : 查看当前时间", LogLevel.Info);
						monitor.Log("set HHMM : 设置绝对时间(24小时制, 例: 0610, 1300)", LogLevel.Info);
						monitor.Log("add MIN  : 增加/减少分钟(例: 20 或 -30)", LogLevel.Info);
						return;
					}
					if (args.Length >= 1 && args[0].ToLower() == "status")
					{
						string fmt(int tod) { int h = tod / 100; int m = tod % 100; return h.ToString("D2") + ":" + m.ToString("D2"); }
						monitor.Log("当前时间: " + fmt(Game1.timeOfDay), LogLevel.Info);
						return;
					}

					int ClampTimeToRange(int tod)
					{
						if (tod < 600) tod = 600;
						if (tod > 2600) tod = 2600;
						int h = tod / 100; int m = tod % 100; m = (m / 10) * 10; if (m >= 60) { h += 1; m = 0; }
						return h * 100 + m;
					}
					string FormatTime(int tod) { int h = tod / 100; int m = tod % 100; return h.ToString("D2") + ":" + m.ToString("D2"); }

					if (args.Length >= 2 && args[0].ToLower() == "set")
					{
						if (!int.TryParse(args[1], out int hhmm))
						{
							monitor.Log("错误: 时间格式应为 HHMM, 例如 0610 或 1300。", LogLevel.Warn);
							return;
						}
						int newTime = ClampTimeToRange(hhmm);
						int oldTime = Game1.timeOfDay;
						Game1.timeOfDay = newTime;
						Game1.gameTimeInterval = 0;
						monitor.Info($"时间已被设置：{oldTime} -> {newTime} ({FormatTime(oldTime)} -> {FormatTime(newTime)})", nameof(TimeConsoleCommandListener));
						Game1.chatBox?.addMessage("[Server] 时间已设置为 " + FormatTime(newTime), Color.LimeGreen);
						return;
					}

					if (args.Length >= 2 && args[0].ToLower() == "add")
					{
						if (!int.TryParse(args[1], out int deltaMin))
						{
							monitor.Log("错误: add 后需跟分钟整数，例如 20 或 -30。", LogLevel.Warn);
							return;
						}
						int old = Game1.timeOfDay;
						int oh = old / 100; int om = old % 100;
						int totalMin = oh * 60 + om + deltaMin;
						if (totalMin < 360) totalMin = 360; // 06:00
						if (totalMin > 1560) totalMin = 1560; // 26:00
						int nh = totalMin / 60; int nm = totalMin % 60;
						nm = (nm / 10) * 10; if (nm >= 60) { nh += 1; nm = 0; }
						int newTod = nh * 100 + nm;
						newTod = ClampTimeToRange(newTod);
						Game1.timeOfDay = newTod;
						Game1.gameTimeInterval = 0;
						monitor.Info($"时间已调整：{old} -> {newTod} (变化 {deltaMin} 分)", nameof(TimeConsoleCommandListener));
						Game1.chatBox?.addMessage("[Server] 时间已调整为 " + FormatTime(newTod), Color.LimeGreen);
						return;
					}

					monitor.Log("未知用法: time [status|set HHMM|add MIN]", LogLevel.Warn);
				}
			);

			isRegistered = true;
			monitor.Debug("控制台命令 time 已注册", nameof(TimeConsoleCommandListener));
		}
	}
}


