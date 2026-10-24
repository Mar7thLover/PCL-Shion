using System;
using Avalonia.Logging;
using Avalonia.Threading;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Select;

/// <summary>
///     选择页面的日志输出，对应 WPF 中 ModBase.Log 的调用。
/// </summary>
internal static class SelectLog
{
    public static void Info(string message)
    {
        Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(null, "[Select] {Message}", message);
    }

    public static void Error(string message, Exception? ex)
    {
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "[Select] {Message} {Exception}", message, ex);
    }

    /// <summary>
    ///     记录日志并以弹窗告知用户，对应 WPF 中 LogLevel.Feedback 的错误。
    /// </summary>
    public static void Feedback(string message, Exception? ex)
    {
        Error(message, ex);
        Dispatcher.UIThread.Post(() => _ = MsgBox.ShowAsync(message));
    }
}
