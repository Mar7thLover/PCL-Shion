namespace PCL.Shion.Core.Mods;

/// <summary>
/// Mod 组合中的问题类型。
/// </summary>
public enum ModIssueKind
{
    /// <summary>多个已启用的文件使用同一个 Mod ID。</summary>
    Duplicate,

    /// <summary>必需的前置 Mod 没有启用的版本。</summary>
    MissingDependency
}

/// <summary>
/// <see cref="ModAnalyzer"/> 发现的一个问题。
/// </summary>
/// <param name="Kind">问题类型。</param>
/// <param name="ModId">重复的 Mod ID，或缺少前置的那个 Mod 的 ID。</param>
/// <param name="Mods">涉及的 Mod 文件。</param>
/// <param name="MissingDependencyId">缺少的前置 Mod 的 ID，仅 <see cref="ModIssueKind.MissingDependency"/> 有值。</param>
public sealed record ModIssue(ModIssueKind Kind, string ModId, IReadOnlyList<LocalMod> Mods, string? MissingDependencyId);
