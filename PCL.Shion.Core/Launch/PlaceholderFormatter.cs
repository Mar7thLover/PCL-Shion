using System.Text.RegularExpressions;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 替换启动参数中的 ${名称} 占位符。
/// </summary>
internal static partial class PlaceholderFormatter
{
    /// <summary>
    /// 未知的占位符原样保留。
    /// </summary>
    public static string Apply(string template, IReadOnlyDictionary<string, string> values)
    {
        return Placeholder().Replace(
            template,
            match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
    }

    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex Placeholder();
}
