using System.Globalization;
using System.Text.RegularExpressions;

namespace PCL.Shion.Core.Online;

/// <summary>
/// 资源列表使用的 Minecraft 版本判断与比较。
/// </summary>
public static partial class CompVersions
{
    /// <summary>
    /// 版本字符串是否符合 Minecraft 原版格式，例如 1.x、26.x。
    /// </summary>
    public static bool IsFormatFit(string? version)
    {
        if (version is null)
            return false;
        if (FormatOneRegex().IsMatch(version))
            return true;
        var major = ParseLeadingNumber(LeadingVersionRegex().Match(version).Value);
        return major > 25d;
    }

    /// <summary>
    /// 尝试将版本字符串转换为 Drop 序数。若无法转换则返回 -1。
    /// </summary>
    public static int VersionToDrop(string? version, bool allowSnapshot = false)
    {
        if (string.IsNullOrEmpty(version))
            return -1;

        var lower = version.ToLowerInvariant();

        if (lower.StartsWith("1.rv", StringComparison.Ordinal)) return 90;
        if (lower.StartsWith("3d shareware", StringComparison.Ordinal)) return 140;

        if (!allowSnapshot && lower.Contains('-'))
            return -1;

        var baseVer = BeforeFirst(lower, "-");
        var segments = baseVer.Split('.');

        // XXwYY 的快照，按年份估算
        if (allowSnapshot && segments.Length == 1 && segments[0].Length >= 3
            && segments[0][2] == 'w'
            && char.IsDigit(segments[0][0]) && char.IsDigit(segments[0][1]))
        {
            var year = int.Parse(segments[0][..2], CultureInfo.InvariantCulture);
            if (year <= 16) return Math.Max(year * 10 - 70, 20);
            if (year == 17) return 120;
            return Math.Min(130 + (year - 18) * 10, 210);
        }

        if (segments.Length < 2)
            return -1;
        var major = (int)Math.Round(ParseLeadingNumber(segments[0]));
        var minor = (int)Math.Round(ParseLeadingNumber(segments[1]));
        if (major == 1) return minor * 10;
        if (major >= 25) return major * 10 + minor;
        if (major == 2) return 50; // 2.0 愚人节
        return -1;
    }

    /// <summary>
    /// 将 Drop 序数转换为版本字符串。
    /// </summary>
    public static string DropToVersion(int drop)
    {
        if (drop >= 250) return $"{drop / 10}.{drop % 10}";
        return $"1.{drop / 10}";
    }

    /// <summary>
    /// 比较两个版本名，若 left 较新则返回 1，相同则返回 0，right 较新则返回 -1。
    /// 无法比较两个预发布版的大小。
    /// </summary>
    /// <param name="unknownVersion">表示未知版本的文本，未知版本排在最前之外的其他版本之后。为 null 时不做特殊处理。</param>
    public static int CompareVersion(string left, string right, string? unknownVersion = null)
    {
        if (unknownVersion is not null && (left == unknownVersion || right == unknownVersion))
        {
            if (left == unknownVersion && right != unknownVersion) return 1;
            if (left == unknownVersion && right == unknownVersion) return 0;
            return -1;
        }

        left = left.ToLowerInvariant();
        right = right.ToLowerInvariant();
        var lefts = Tokenize(left);
        var rights = Tokenize(right);
        var i = 0;
        while (true)
        {
            if (lefts.Count - 1 < i && rights.Count - 1 < i)
            {
                var ordinal = string.CompareOrdinal(left, right);
                return ordinal > 0 ? 1 : ordinal < 0 ? -1 : 0;
            }

            var leftValue = lefts.Count - 1 < i ? "0" : lefts[i];
            var rightValue = rights.Count - 1 < i ? "0" : rights[i];
            if (leftValue == rightValue)
            {
                i += 1;
                continue;
            }

            var leftNum = ParseLeadingNumber(MapPreRelease(leftValue));
            var rightNum = ParseLeadingNumber(MapPreRelease(rightValue));
            if (leftNum == 0d && rightNum == 0d)
            {
                var ordinal = string.CompareOrdinal(leftValue, rightValue);
                if (ordinal > 0) return 1;
                if (ordinal < 0) return -1;
            }
            else if (leftNum > rightNum)
            {
                return 1;
            }
            else if (leftNum < rightNum)
            {
                return -1;
            }

            i += 1;
        }
    }

    /// <summary>
    /// 比较两个版本名是否 left &gt;= right。
    /// </summary>
    public static bool CompareVersionGe(string left, string right) => CompareVersion(left, right) >= 0;

    /// <summary>
    /// 版本名比较器。
    /// </summary>
    public sealed class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y) => CompareVersion(x ?? "", y ?? "");
    }

    private static List<string> Tokenize(string text)
    {
        var replaced = text.Replace("快照", "snapshot").Replace("预览版", "pre");
        var result = new List<string>();
        foreach (Match match in TokenRegex().Matches(replaced))
            result.Add(match.Value);
        return result;
    }

    private static string MapPreRelease(string value) => value switch
    {
        "rc" => "-1",
        "pre" => "-2",
        "snapshot" => "-3",
        "experimental" => "-4",
        _ => value
    };

    /// <summary>
    /// 与 VB 的 Val 相同：读取开头的数字，读不到则返回 0。
    /// </summary>
    public static double ParseLeadingNumber(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0d;
        var match = LeadingNumberRegex().Match(text);
        return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0d;
    }

    private static string BeforeFirst(string text, string separator)
    {
        var index = text.IndexOf(separator, StringComparison.Ordinal);
        return index < 0 ? text : text[..index];
    }

    [GeneratedRegex(@"^1\.\d")]
    private static partial Regex FormatOneRegex();

    [GeneratedRegex(@"^[2-9]\d\.\d+")]
    private static partial Regex LeadingVersionRegex();

    [GeneratedRegex(@"[a-z]+|[0-9]+")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"^\s*[-+]?\d+(\.\d+)?")]
    private static partial Regex LeadingNumberRegex();
}
