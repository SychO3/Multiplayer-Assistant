using System;
using StardewModdingAPI;

namespace MultiplayerAssistant;

/// <summary>
/// 日志工具类，在整个 Mod 中复用同一个 <see cref="IMonitor"/> 实例。
/// 调用 <see cref="Initialize"/> 完成初始化后，就可以在任意位置调用 <see cref="Info"/>、<see cref="Warn"/> 等方法输出日志。
/// </summary>
internal static class LogManager
{
    private static readonly object SyncRoot = new();
    private static IMonitor? _monitor;
    private static string _defaultPrefix = "[MultiplayerAssistant]";

    /// <summary>
    /// 在 <c>Entry</c> 中调用一次，用 Mod 的 <see cref="IMonitor"/> 初始化日志工具。
    /// </summary>
    public static void Initialize(IMonitor monitor, string? defaultPrefix = null)
    {
        if (monitor == null)
            throw new ArgumentNullException(nameof(monitor));

        lock (SyncRoot)
        {
            _monitor = monitor;
            if (!string.IsNullOrWhiteSpace(defaultPrefix))
                _defaultPrefix = $"[{defaultPrefix}]";
        }
    }

    private static IMonitor Monitor => _monitor ?? throw new InvalidOperationException("LogManager.Initialize must be called before logging.");

    /// <summary>
    /// 通用日志方法，其他快捷方法最终都会调用这里。
    /// <paramref name="context"/> 可选，用来区分模块来源，例如 "Network"、"UI"。
    /// </summary>
    public static void Log(string message, LogLevel level = LogLevel.Trace, string? context = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var prefix = BuildPrefix(context);
        Monitor.Log($"{prefix} {message}", level);
    }

    // 以下快捷方法按日志等级划分，正常使用时直接调用其中一个即可。
    public static void Trace(string message, string? context = null) => Log(message, LogLevel.Trace, context);
    public static void Debug(string message, string? context = null) => Log(message, LogLevel.Debug, context);
    public static void Info(string message, string? context = null) => Log(message, LogLevel.Info, context);
    public static void Warn(string message, string? context = null) => Log(message, LogLevel.Warn, context);
    public static void Error(string message, string? context = null) => Log(message, LogLevel.Error, context);

    /// <summary>
    /// 捕获异常时可以调用，自动输出异常信息和堆栈。
    /// </summary>
    public static void Exception(Exception ex, string? message = null, string? context = null)
    {
        if (ex == null)
            throw new ArgumentNullException(nameof(ex));

        var summary = string.IsNullOrWhiteSpace(message) ? ex.Message : message;
        Log($"{summary}: {ex}", LogLevel.Error, context);
    }

    /// <summary>
    /// 构建统一的前缀，形如 [ModName][Context]。
    /// </summary>
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
