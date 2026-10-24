using System.Text.RegularExpressions;

namespace PCL.Shion.Core.Java;

/// <summary>
/// 解析出的 Java 版本信息。
/// </summary>
public sealed record JavaVersionInfo(int MajorVersion, string Version, bool Is64Bit);

/// <summary>
/// 解析 Java 根目录的 release 文件与 java -version 的输出。
/// </summary>
public static partial class JavaVersionParser
{
    [GeneratedRegex(@"^(?:1\.)?(\d+)")]
    private static partial Regex MajorVersionPattern();

    [GeneratedRegex(@"\bversion ""([^""]+)""")]
    private static partial Regex VersionOutputPattern();

    /// <summary>
    /// 从版本号取主版本号：1.8.0_301 为 8，21.0.10 为 21，无法识别时返回 null。
    /// </summary>
    public static int? ParseMajorVersion(string version)
    {
        var match = MajorVersionPattern().Match(version);
        return match.Success && int.TryParse(match.Groups[1].Value, out var major) ? major : null;
    }

    /// <summary>
    /// 解析 release 文件内容，需要同时包含 JAVA_VERSION 与 OS_ARCH，否则返回 null。
    /// </summary>
    public static JavaVersionInfo? ParseReleaseFile(string content)
    {
        string? version = null;
        string? arch = null;
        foreach (var line in content.Split('\n'))
        {
            var separator = line.IndexOf('=');
            if (separator < 0) continue;
            var value = line[(separator + 1)..].Trim().Trim('"');
            switch (line[..separator].Trim())
            {
                case "JAVA_VERSION":
                    version = value;
                    break;
                case "OS_ARCH":
                    arch = value;
                    break;
            }
        }
        if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(arch)) return null;
        // x86_64、amd64、aarch64 等 64 位架构名都带 64，32 位为 x86、i586 等
        return Create(version, arch.Contains("64"));
    }

    /// <summary>
    /// 解析 java -version 的输出（stderr 与 stdout 合并），无法识别时返回 null。
    /// </summary>
    public static JavaVersionInfo? ParseVersionOutput(string output)
    {
        var match = VersionOutputPattern().Match(output);
        if (!match.Success) return null;
        return Create(match.Groups[1].Value, output.Contains("64-Bit", StringComparison.OrdinalIgnoreCase));
    }

    private static JavaVersionInfo? Create(string version, bool is64Bit) =>
        ParseMajorVersion(version) is { } major ? new JavaVersionInfo(major, version, is64Bit) : null;
}
