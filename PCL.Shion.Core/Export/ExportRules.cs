using System.Text;
using System.Text.RegularExpressions;

namespace PCL.Shion.Core.Export;

/// <summary>
/// 整合包导出规则的文本格式与匹配。规则以 <c>/</c> 或 <c>\</c> 作为路径分隔符，大小写不敏感，
/// 支持 <c>*</c>（不跨目录）、<c>**</c>（跨目录）、<c>?</c>、<c>[...]</c> 与 <c>{a,b}</c>；以 <c>!</c> 开头表示排除。
/// </summary>
public static class ExportRules
{
    private static readonly Dictionary<string, Regex> RegexCache = new();
    private static readonly object CacheLock = new();

    /// <summary>
    /// 对文本行进行标准化，忽略空行与以 <c>#</c>、<c>=</c> 开头的注释行。
    /// 当 <paramref name="addSuffixStarToFolderPath"/> 为真时，以路径分隔符结尾的文件夹规则会附加 <c>**</c>。
    /// </summary>
    public static IEnumerable<string> Standardize(IEnumerable<string> raw, bool addSuffixStarToFolderPath)
    {
        foreach (var line in raw)
        {
            var ignoreLine = line.Trim();
            if (string.IsNullOrEmpty(ignoreLine) || ignoreLine.StartsWith('#') || ignoreLine.StartsWith('='))
                continue;
            ignoreLine = ignoreLine.Replace('\\', '/');
            var isFolder = ignoreLine.EndsWith('/');
            yield return ignoreLine + (isFolder && addSuffixStarToFolderPath ? "**" : "");
        }
    }

    /// <summary>
    /// 转义文件名中的通配符，使其按字面匹配。
    /// </summary>
    public static string EscapeLikePattern(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            if (c is '[' or ']' or '*' or '?' or '{' or '}')
                sb.Append('[').Append(c).Append(']');
            else
                sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 判断相对路径是否匹配规则（不含前导 <c>!</c>）。
    /// </summary>
    public static bool Matches(string relativePath, string pattern)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        return GetRegex(pattern.Replace("#", "[0-9]").Replace('\\', '/')).IsMatch(normalizedPath);
    }

    private static Regex GetRegex(string pattern)
    {
        lock (CacheLock)
        {
            if (RegexCache.TryGetValue(pattern, out var cached))
                return cached;
            var regex = new Regex(ToRegexPattern(pattern), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            RegexCache[pattern] = regex;
            return regex;
        }
    }

    private static string ToRegexPattern(string glob)
    {
        var sb = new StringBuilder("^");
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            switch (c)
            {
                case '*':
                    if (i + 1 < glob.Length && glob[i + 1] == '*')
                    {
                        sb.Append(".*");
                        i++;
                    }
                    else
                    {
                        sb.Append("[^/]*");
                    }

                    break;
                case '?':
                    sb.Append("[^/]");
                    break;
                case '[':
                {
                    var close = glob.IndexOf(']', i + 1);
                    if (close < 0)
                    {
                        sb.Append(@"\[");
                        break;
                    }

                    var body = glob.Substring(i + 1, close - i - 1);
                    if (body.StartsWith('!'))
                        body = "^" + body[1..];
                    sb.Append('[').Append(body.Replace(@"\", @"\\")).Append(']');
                    i = close;
                    break;
                }
                case '{':
                {
                    var close = glob.IndexOf('}', i + 1);
                    if (close < 0)
                    {
                        sb.Append(@"\{");
                        break;
                    }

                    var alternatives = glob.Substring(i + 1, close - i - 1).Split(',')
                        .Select(Regex.Escape);
                    sb.Append("(?:").Append(string.Join("|", alternatives)).Append(')');
                    i = close;
                    break;
                }
                default:
                    sb.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        sb.Append('$');
        return sb.ToString();
    }
}
