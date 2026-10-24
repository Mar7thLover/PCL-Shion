using PCL.Shion.Core.Launch;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Test;

/// <summary>
/// 借用系统 shell 充当 Java 的启动计划：Windows 用 cmd，其他系统用 sh。
/// </summary>
internal static class ShellPlan
{
    public static bool IsWindows => PlatformInfo.Os == OsKind.Windows;

    public static string Shell => IsWindows ? "cmd.exe" : "/bin/sh";

    /// <summary>
    /// 脚本先停顿约一秒再执行，让测试有时间在输出和退出之前订阅事件。
    /// </summary>
    public static LaunchPlan Create(
        string windowsScript,
        string unixScript,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        string[] arguments = IsWindows
            ? ["/c", $"ping -n 2 127.0.0.1 >nul& {windowsScript}"]
            : ["-c", $"sleep 1; {unixScript}"];
        return new LaunchPlan(
            Shell,
            arguments,
            workingDirectory ?? Path.GetTempPath(),
            Path.GetTempPath(),
            [],
            environment ?? new Dictionary<string, string>(),
            null);
    }
}
