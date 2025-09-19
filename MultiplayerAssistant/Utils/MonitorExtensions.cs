using System;
using StardewModdingAPI;

namespace MultiplayerAssistant;

/// <summary>
/// 提供基于 <see cref="IMonitor"/> 的扩展方法，支持统一的上下文标签等功能。
/// 适配最新SMAPI API，包含Alert级别日志和详细日志检查功能。
/// </summary>
internal static class MonitorExtensions
{
    private static readonly object SyncRoot = new();
    private static string _defaultPrefix = "[MultiplayerAssistant]";

    public static void SetDefaultPrefix(string? prefix)
    {
        lock (SyncRoot)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                _defaultPrefix = "[Log]";
                return;
            }

            var trimmed = prefix.Trim();
            _defaultPrefix = trimmed.StartsWith("[") ? trimmed : $"[{trimmed}]";
        }
    }

    public static void LogWithContext(this IMonitor monitor, string message, LogLevel level = LogLevel.Trace, string? context = null)
    {
        if (monitor == null)
            throw new ArgumentNullException(nameof(monitor));

        if (string.IsNullOrWhiteSpace(message))
            return;

        var formatted = $"{BuildPrefix(context)} {message}".Trim();
        monitor.Log(formatted, level);
    }

    public static void Trace(this IMonitor monitor, string message, string? context = null) => monitor.LogWithContext(message, LogLevel.Trace, context);

    public static void Debug(this IMonitor monitor, string message, string? context = null) => monitor.LogWithContext(message, LogLevel.Debug, context);

    public static void Info(this IMonitor monitor, string message, string? context = null) => monitor.LogWithContext(message, LogLevel.Info, context);

    public static void Warn(this IMonitor monitor, string message, string? context = null) => monitor.LogWithContext(message, LogLevel.Warn, context);

    public static void Error(this IMonitor monitor, string message, string? context = null) => monitor.LogWithContext(message, LogLevel.Error, context);

    public static void Alert(this IMonitor monitor, string message, string? context = null) => monitor.LogWithContext(message, LogLevel.Alert, context);

    public static void Exception(this IMonitor monitor, Exception exception, string? message = null, string? context = null)
    {
        if (monitor == null)
            throw new ArgumentNullException(nameof(monitor));
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        var summary = string.IsNullOrWhiteSpace(message) ? exception.Message : message;
        monitor.LogWithContext($"{summary}: {exception}", LogLevel.Error, context);
    }

    public static void LogOnceWithContext(this IMonitor monitor, string message, LogLevel level = LogLevel.Trace, string? context = null)
    {
        if (monitor == null)
            throw new ArgumentNullException(nameof(monitor));

        if (string.IsNullOrWhiteSpace(message))
            return;

        var formatted = $"{BuildPrefix(context)} {message}".Trim();
        monitor.LogOnce(formatted, level);
    }

    public static void VerboseLogWithContext(this IMonitor monitor, string message, string? context = null)
    {
        if (monitor == null)
            throw new ArgumentNullException(nameof(monitor));

        if (string.IsNullOrWhiteSpace(message))
            return;

        var formatted = $"{BuildPrefix(context)} {message}".Trim();
        monitor.VerboseLog(formatted);
    }

    /// <summary>
    /// 检查是否启用了详细日志记录。当需要执行复杂的日志处理逻辑时，可以先检查此属性避免不必要的计算。
    /// </summary>
    public static bool IsVerboseEnabled(this IMonitor monitor)
    {
        if (monitor == null)
            throw new ArgumentNullException(nameof(monitor));

        return monitor.IsVerbose;
    }

    /// <summary>
    /// 条件性详细日志记录 - 仅在启用详细日志时记录，避免不必要的字符串格式化开销。
    /// </summary>
    public static void VerboseLogConditional(this IMonitor monitor, Func<string> messageProvider, string? context = null)
    {
        if (monitor == null)
            throw new ArgumentNullException(nameof(monitor));

        if (messageProvider == null)
            throw new ArgumentNullException(nameof(messageProvider));

        // 只有在启用详细日志时才执行消息生成，避免性能开销
        if (monitor.IsVerbose)
        {
            var message = messageProvider();
            if (!string.IsNullOrWhiteSpace(message))
            {
                var formatted = $"{BuildPrefix(context)} {message}".Trim();
                monitor.VerboseLog(formatted);
            }
        }
    }

    /// <summary>
    /// 记录多人游戏相关的调试信息，自动添加多人游戏上下文标识。
    /// </summary>
    public static void MultiplayerDebug(this IMonitor monitor, string message, string? additionalContext = null)
    {
        var context = string.IsNullOrWhiteSpace(additionalContext) ? "Multiplayer" : $"Multiplayer.{additionalContext}";
        monitor.Debug(message, context);
    }

    /// <summary>
    /// 记录服务器机器人相关的信息，自动添加ServerBot上下文标识。
    /// </summary>
    public static void ServerBotInfo(this IMonitor monitor, string message, string? additionalContext = null)
    {
        var context = string.IsNullOrWhiteSpace(additionalContext) ? "ServerBot" : $"ServerBot.{additionalContext}";
        monitor.Info(message, context);
    }

    /// <summary>
    /// 记录服务器机器人相关的错误，自动添加ServerBot上下文标识。
    /// </summary>
    public static void ServerBotError(this IMonitor monitor, string message, string? additionalContext = null)
    {
        var context = string.IsNullOrWhiteSpace(additionalContext) ? "ServerBot" : $"ServerBot.{additionalContext}";
        monitor.Error(message, context);
    }

    /// <summary>
    /// 记录服务器机器人相关的异常，自动添加ServerBot上下文标识。
    /// </summary>
    public static void ServerBotException(this IMonitor monitor, Exception exception, string? message = null, string? additionalContext = null)
    {
        var context = string.IsNullOrWhiteSpace(additionalContext) ? "ServerBot" : $"ServerBot.{additionalContext}";
        monitor.Exception(exception, message, context);
    }

    private static string BuildPrefix(string? context)
    {
        var prefix = _defaultPrefix;
        if (string.IsNullOrWhiteSpace(prefix))
            prefix = "[Log]";

        if (string.IsNullOrWhiteSpace(context))
            return prefix;

        return $"{prefix}[{context}]";
    }
}
