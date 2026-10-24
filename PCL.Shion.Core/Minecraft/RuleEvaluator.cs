using System.Text.RegularExpressions;

namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 版本 JSON 规则的求值。
/// </summary>
internal static class RuleEvaluator
{
    /// <summary>
    /// 规则列表为空时放行；否则以最后一条命中的规则为准，没有任何规则命中则不放行。
    /// </summary>
    public static bool IsAllowed(IReadOnlyList<VersionRule> rules, RuleEnvironment environment)
    {
        if (rules.Count == 0)
            return true;

        var allowed = false;
        foreach (var rule in rules)
        {
            if (Matches(rule, environment))
                allowed = rule.Allow;
        }
        return allowed;
    }

    private static bool Matches(VersionRule rule, RuleEnvironment environment)
    {
        if (rule.OsName is not null && rule.OsName != environment.OsName)
            return false;
        // 架构名必须整体匹配，否则 x86 会命中 x86_64
        if (rule.OsArch is not null && !Regex.IsMatch(environment.Arch, $"^(?:{rule.OsArch})$"))
            return false;
        if (rule.OsVersion is not null && !Regex.IsMatch(environment.OsVersion, rule.OsVersion))
            return false;

        foreach (var (feature, expected) in rule.Features)
        {
            if (environment.Features.Contains(feature) != expected)
                return false;
        }
        return true;
    }
}
