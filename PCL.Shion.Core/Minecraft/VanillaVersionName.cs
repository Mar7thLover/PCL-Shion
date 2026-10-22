using System.Text.RegularExpressions;

namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 从版本 ID 中识别原版 Minecraft 的版本号。
/// </summary>
internal static partial class VanillaVersionName
{
    /// <summary>
    /// 取版本 ID 开头的原版版本号（正式版、pre/rc/snapshot、周快照以及 alpha/beta 等旧版），
    /// 其后必须是结尾或分隔符。无法识别时返回 null。
    /// </summary>
    public static string? TryExtract(string id)
    {
        var match = VersionPrefix().Match(id);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"^(?:\d+\.\d+(?:\.\d+)?(?:-(?:pre|rc|snapshot-)\d+)?|\d{2}w\d{2}[a-z]|[abc]\d+\.\d+(?:\.\d+)?(?:_\d+)?[a-z]?|rd-\d+|inf-\d+)(?=$|[-_ +])")]
    private static partial Regex VersionPrefix();
}
