using System;
using System.Text.RegularExpressions;
using PCL.Shion.Pages.Homepage;
using PCL.Shion.UI;

namespace PCL.Shion;

/// <summary>
///     与 WPF 版 ModBase 的日志入口对应。日志写入 <see cref="LogWrapper" />，需要提示用户时使用 Shion 的提示与弹窗服务。
/// </summary>
public static class ModBase
{
    /// <summary>
    ///     日志的提示级别，数值与 WPF 版保持一致。
    /// </summary>
    public enum LogLevel
    {
        /// <summary>不提示，只记录日志。</summary>
        Normal = 0,

        /// <summary>只提示开发者与调试模式用户。</summary>
        Debug = 2,

        /// <summary>弹出提示所有用户。</summary>
        Hint = 3,

        /// <summary>弹窗，不要求反馈。</summary>
        Msgbox = 4,

        /// <summary>弹窗，要求反馈。</summary>
        Feedback = 5
    }

    /// <summary>
    ///     输出一条普通日志，并按级别向用户提示。
    /// </summary>
    /// <param name="text">日志内容。</param>
    /// <param name="level">提示级别。</param>
    /// <param name="title">弹窗标题，为空时使用默认标题。</param>
    /// <param name="userSummary">面向用户的提示内容，为空时使用去掉标签后的日志内容。</param>
    public static void Log(string text, LogLevel level = LogLevel.Normal, string? title = null,
        string? userSummary = null)
    {
        switch (level)
        {
            case LogLevel.Hint or LogLevel.Msgbox:
                LogWrapper.Warn(null, text);
                break;
            case LogLevel.Feedback:
                LogWrapper.Error(null, text);
                break;
            case LogLevel.Debug:
                LogWrapper.Debug(text);
                break;
            default:
                LogWrapper.Info(text);
                break;
        }

        if (level == LogLevel.Normal)
            return;

        var userMessage = string.IsNullOrWhiteSpace(userSummary)
            ? Regex.Replace(text, @"\[[^\]]+?\] ", "")
            : userSummary;
        ShowToUser(level, text, userMessage, title);
    }

    /// <summary>
    ///     输出一条错误日志，并按级别向用户提示。
    /// </summary>
    /// <param name="ex">发生的异常。</param>
    /// <param name="desc">错误描述，仅用于日志和错误详情。</param>
    /// <param name="level">提示级别，默认只记录调试日志。</param>
    /// <param name="title">弹窗标题，为空时使用默认标题。</param>
    /// <param name="userSummary">面向用户的提示内容，不会写入日志。</param>
    public static void Log(Exception ex, string desc, LogLevel level = LogLevel.Debug, string? title = null,
        string? userSummary = null)
    {
        switch (level)
        {
            case LogLevel.Hint or LogLevel.Msgbox:
                LogWrapper.Warn(ex, desc);
                break;
            case LogLevel.Debug:
                LogWrapper.Debug($"{desc}:{ex}");
                break;
            default:
                LogWrapper.Error(ex, desc);
                break;
        }

        if (level == LogLevel.Normal)
            return;

        var userMessage = string.IsNullOrWhiteSpace(userSummary)
            ? $"{desc}：{ex.Message}"
            : userSummary;
        ShowToUser(level, desc + "：" + ex, userMessage, title);
    }

    private static void ShowToUser(LogLevel level, string detail, string userMessage, string? title)
    {
        switch (level)
        {
            case LogLevel.Debug:
                if (HomepageSupport.ModeDebug)
                    HintService.Hint("[调试模式] " + detail, HintType.Info, false);
                break;
            case LogLevel.Hint:
                HintService.Hint(userMessage, HintType.Error, false);
                break;
            case LogLevel.Msgbox:
                _ = MsgBox.ShowAsync(userMessage, title, isWarn: true);
                break;
            case LogLevel.Feedback:
                // TODO(shion): WPF 版会弹出反馈提示并可直接提交反馈，Shion 暂未接入反馈流程，这里只显示弹窗。
                _ = MsgBox.ShowAsync(userMessage, title, isWarn: true);
                break;
        }
    }
}
