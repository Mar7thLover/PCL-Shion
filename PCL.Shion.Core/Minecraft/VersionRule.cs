namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 版本 JSON 中 rules 数组的一个元素。
/// </summary>
/// <param name="Allow">true 为 allow，false 为 disallow。</param>
/// <param name="OsName">os.name，null 表示不限。</param>
/// <param name="OsArch">os.arch（正则，需匹配整个架构名），null 表示不限。</param>
/// <param name="OsVersion">os.version（正则，查找匹配），null 表示不限。</param>
/// <param name="Features">features 条件：特性名与期望的开关状态。</param>
public sealed record VersionRule(
    bool Allow,
    string? OsName,
    string? OsArch,
    string? OsVersion,
    IReadOnlyDictionary<string, bool> Features);
