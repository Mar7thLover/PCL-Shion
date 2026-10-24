using System;
using System.Collections.Generic;
using System.Linq;
using PCL.Shion.Core.Download;
using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

/// <summary>
/// 启动页文字的强类型入口：XAML 里用 <c>{x:Static}</c>，代码里直接调用。
/// 字符串表是 <c>Assets\Strings\Launch.*.json</c>，键为 <c>Launch.</c> 加属性名。
/// </summary>
public static class LaunchStrings
{
    public static string SortName => Text(nameof(SortName));
    public static string SortVersion => Text(nameof(SortVersion));
    public static string SortLastPlayed => Text(nameof(SortLastPlayed));
    public static string InstanceDetails => Text(nameof(InstanceDetails));
    public static string NeverPlayed => Text(nameof(NeverPlayed));
    public static string Played(TimeSpan total, DateTimeOffset lastPlayed) =>
        Text(nameof(Played), Duration(total), lastPlayed.LocalDateTime);

    public static string RunningTitle => Text(nameof(RunningTitle));
    public static string RunningInfo(int processId, DateTimeOffset startedAt) =>
        Text(nameof(RunningInfo), processId, startedAt.LocalDateTime);
    public static string ShowLog => Text(nameof(ShowLog));
    public static string EndGame => Text(nameof(EndGame));
    public static string LogHeading(string instance) => Text(nameof(LogHeading), instance);

    public static string JavaCustom => Text(nameof(JavaCustom));
    public static string JavaCustomMissing(string path) => Text(nameof(JavaCustomMissing), path);
    public static string JavaDefault => Text(nameof(JavaDefault));
    public static string JavaDefaultMissing(string path) => Text(nameof(JavaDefaultMissing), path);
    public static string SettingsReadFailed(string message) => Text(nameof(SettingsReadFailed), message);
    public static string DownloadJava(int major) => Text(nameof(DownloadJava), major);
    public static string JavaDownloadFailed(string message) => Text(nameof(JavaDownloadFailed), message);

    public static string StatusStarted(string instance, int processId) => Text(nameof(StatusStarted), instance, processId);
    public static string StatusExited(string instance, int exitCode) => Text(nameof(StatusExited), instance, exitCode);
    public static string PreLaunchFailed(int exitCode) => Text(nameof(PreLaunchFailed), exitCode);
    public static string NotifyStarted => Text(nameof(NotifyStarted));
    public static string NotifyExited(int exitCode) => Text(nameof(NotifyExited), exitCode);

    public static string Completed => Text(nameof(Completed));
    public static string Pending(string stage) => Text(nameof(Pending), stage);
    public static string Progress(string stage, int completed, int total, string speed) =>
        Text(nameof(Progress), stage, completed, total, speed);

    public static string Stage(string stage) => stage switch
    {
        "version" => Text("StageVersion"),
        "files" => Text("StageFiles"),
        "assets" => Text("StageAssets"),
        "java" => Text("StageJava"),
        _ => stage,
    };

    public static string LogCompleting(int count) => Text(nameof(LogCompleting), count);
    public static string LogDownloadFailed(int count) => Text(nameof(LogDownloadFailed), count);
    public static string LogStillMissing(int count) => Text(nameof(LogStillMissing), count);
    public static string LogMore(int count) => Text(nameof(LogMore), count);
    public static string LogJavaDownloading(int major) => Text(nameof(LogJavaDownloading), major);
    public static string LogJavaInstalled(string path) => Text(nameof(LogJavaInstalled), path);

    public static string StatusDownloadFailed(int count, string files) => Text(nameof(StatusDownloadFailed), count, files);
    public static string StatusStillMissing(int count, string files) => Text(nameof(StatusStillMissing), count, files);
    public static string StatusJavaInstalled(string version) => Text(nameof(StatusJavaInstalled), version);

    /// <summary>把一个文件各地址的失败原因转成当前界面语言的文字，没有原因时为空。</summary>
    public static string DownloadErrors(IReadOnlyList<DownloadError>? errors) =>
        errors is null ? "" : string.Join("; ", errors.Select(DescribeError));

    private static string Duration(TimeSpan time) => ((int)time.TotalHours, time.Minutes) switch
    {
        (0, 0) => Text("DurationLessThanMinute"),
        (0, var minutes) => Text("DurationMinutes", minutes),
        (var hours, 0) => Text("DurationHours", hours),
        var (hours, minutes) => Text("DurationHoursMinutes", hours, minutes),
    };

    private static string DescribeError(DownloadError error)
    {
        var text = error.Kind switch
        {
            DownloadErrorKind.NoUrl => Text("ErrorNoUrl"),
            DownloadErrorKind.SizeMismatch => Text("ErrorSizeMismatch", error.Expected ?? "", error.Actual ?? ""),
            DownloadErrorKind.HashMismatch => Text("ErrorHashMismatch", error.Expected ?? "", error.Actual ?? ""),
            DownloadErrorKind.Timeout => Text("ErrorTimeout", error.Expected ?? ""),
            // 其余失败的说明取自系统（HTTP 状态、网络或磁盘错误），不在字符串表里
            _ => error.Actual ?? "",
        };
        return error.Url is null ? text : $"{error.Url}: {text}";
    }

    private static string Text(string name) => Localizer.Get($"Launch.{name}");

    private static string Text(string name, params object[] args) => Localizer.Format($"Launch.{name}", args);
}
