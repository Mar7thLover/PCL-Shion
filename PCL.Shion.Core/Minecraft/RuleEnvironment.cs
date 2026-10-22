using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 规则求值所用的运行环境。
/// </summary>
/// <param name="OsName">windows / osx / linux。</param>
/// <param name="Arch">x86 / x86_64 / arm64。</param>
/// <param name="OsVersion">系统版本，形式与 Java 的 os.version 一致（如 10.0）。</param>
/// <param name="Features">已开启的特性名，未列出的特性视为关闭。</param>
internal sealed record RuleEnvironment(string OsName, string Arch, string OsVersion, IReadOnlySet<string> Features)
{
    public static RuleEnvironment ForCurrentPlatform(IReadOnlySet<string> features)
    {
        var version = Environment.OSVersion.Version;
        return new RuleEnvironment(PlatformInfo.OsName, PlatformInfo.Arch, $"{version.Major}.{version.Minor}", features);
    }
}
