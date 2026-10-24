using System;

namespace PCL.Shion;

/// <summary>
///     判断内嵌 Mod 声明的 Minecraft 版本与实例版本的匹配程度，对应 WPF 版 McConstraintMatcher。
///     TODO(shion): 完整的版本范围语法（如 [1.20,1.21)）尚未移植，当前支持精确匹配与 “1.20.*” 形式的通配。
/// </summary>
public static class McConstraintMatcher
{
    /// <summary>匹配程度，数值越大越匹配。</summary>
    public enum MatchKind
    {
        /// <summary>无法判断（未声明或包含未替换的占位符）。</summary>
        None = 0,

        /// <summary>与实例版本不兼容。</summary>
        Incompatible = 1,

        /// <summary>实例版本落在声明的通配范围内。</summary>
        Range = 2,

        /// <summary>与实例版本完全一致。</summary>
        Exact = 3
    }

    /// <summary>
    ///     计算一个 Mod 的声明版本与实例 Minecraft 版本的匹配程度。
    /// </summary>
    public static MatchKind Match(string? fileName, string? version, string? targetMcVersion, string? loader,
        string? instanceMcVersion)
    {
        if (string.IsNullOrWhiteSpace(targetMcVersion) || targetMcVersion.Contains("${", StringComparison.Ordinal))
            return MatchKind.None;
        if (string.IsNullOrWhiteSpace(instanceMcVersion))
            return MatchKind.None;

        var target = targetMcVersion.Trim();
        var instance = instanceMcVersion.Trim();
        if (string.Equals(target, instance, StringComparison.OrdinalIgnoreCase))
            return MatchKind.Exact;

        if (target.EndsWith(".*", StringComparison.Ordinal) || target.EndsWith(".x", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = target[..^2];
            if (instance.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                instance.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                return MatchKind.Range;
        }

        return MatchKind.Incompatible;
    }
}
