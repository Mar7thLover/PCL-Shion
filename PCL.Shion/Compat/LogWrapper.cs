using System;
using Avalonia.Logging;

namespace PCL.Shion;

/// <summary>
///     与 WPF 版 PCL.Core.Logging.LogWrapper 对应的日志入口，写入 Avalonia 的日志系统（与 Shion 其它部分的日志一致）。
/// </summary>
public static class LogWrapper
{
    public static void Info(string msg) => Write(LogEventLevel.Information, null, msg);

    public static void Debug(string msg) => Write(LogEventLevel.Debug, null, msg);

    public static void Warn(Exception? ex, string msg) => Write(LogEventLevel.Warning, ex, msg);

    public static void Error(Exception? ex, string msg) => Write(LogEventLevel.Error, ex, msg);

    private static void Write(LogEventLevel level, Exception? ex, string msg)
    {
        // ParametrizedLogger 是结构体，TryGet 返回的是可空值，需要先解包再调用 Log
        if (Logger.TryGet(level, LogArea.Control) is not { } logger)
            return;
        if (ex is null)
            logger.Log(null, "{Message}", msg);
        else
            logger.Log(null, "{Message}：{Error}", msg, ex);
    }
}
