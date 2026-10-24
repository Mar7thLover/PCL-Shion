using System.Runtime.InteropServices;

namespace PCL.Shion.Core.Platform;

/// <summary>
/// 当前运行平台。<see cref="OsName"/> 与 <see cref="Arch"/> 使用版本 JSON 规则里的取值。
/// </summary>
public static class PlatformInfo
{
    public static OsKind Os { get; } = DetectOs();

    /// <summary>规则用的系统名：windows / osx / linux。</summary>
    public static string OsName { get; } = Os switch
    {
        OsKind.Windows => "windows",
        OsKind.MacOs => "osx",
        _ => "linux"
    };

    /// <summary>规则用的架构名：x86 / x86_64 / arm64。</summary>
    public static string Arch { get; } = RuntimeInformation.OSArchitecture switch
    {
        Architecture.X86 => "x86",
        Architecture.Arm64 => "arm64",
        _ => "x86_64"
    };

    /// <summary>classpath 与 module path 中的路径分隔符。</summary>
    public static string ClasspathSeparator { get; } = Os == OsKind.Windows ? ";" : ":";

    private static OsKind DetectOs()
    {
        if (OperatingSystem.IsWindows())
            return OsKind.Windows;
        return OperatingSystem.IsMacOS() ? OsKind.MacOs : OsKind.Linux;
    }
}
