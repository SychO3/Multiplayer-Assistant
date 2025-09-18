using System;
using StardewModdingAPI;

namespace MultiplayerAssistant;

/// <summary>
/// 提供基于 <see cref="IMonitor"/> 的扩展方法，支持统一的上下文标签等功能。
/// </summary>
internal static class MonitorExtensions
{
    private static readonly object SyncRoot = new();
    private static string _defaultPrefix = "[MultiplayerAssistant]";
    private static volatile bool _loggingEnabled = false;

    public static bool IsLoggingEnabled => _loggingEnabled;

    public static void SetLoggingEnabled(bool enabled)
    {
        _loggingEnabled = enabled;
    }

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

        if (!IsLoggingEnabled)
            return;

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

        if (!IsLoggingEnabled)
            return;

        if (string.IsNullOrWhiteSpace(message))
            return;

        var formatted = $"{BuildPrefix(context)} {message}".Trim();
        monitor.LogOnce(formatted, level);
    }

    public static void VerboseLogWithContext(this IMonitor monitor, string message, string? context = null)
    {
        if (monitor == null)
            throw new ArgumentNullException(nameof(monitor));

        if (!IsLoggingEnabled)
            return;

        if (string.IsNullOrWhiteSpace(message))
            return;

        var formatted = $"{BuildPrefix(context)} {message}".Trim();
        monitor.VerboseLog(formatted);
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
