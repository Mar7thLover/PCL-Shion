namespace PCL.Shion.Core.Mods;

/// <summary>
/// 检查一组 Mod 里的重复与缺失的前置。
/// </summary>
public static class ModAnalyzer
{
    // 由游戏、Java 与加载器本身提供，不会出现在 mods 目录里
    private static readonly HashSet<string> BuiltInIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "minecraft", "java", "forge", "neoforge", "fml", "mcp", "fabricloader", "quilt_loader"
    };

    /// <summary>
    /// 只分析已启用且读到了 ID 的 Mod，先列出重复，再列出缺失的前置。
    /// 前置只检查是否存在，不比对版本范围；可选依赖与内置依赖不算缺失。
    /// </summary>
    public static IReadOnlyList<ModIssue> Analyze(IReadOnlyList<LocalMod> mods)
    {
        var enabled = mods.Where(mod => mod is { Enabled: true, Id: not null }).ToList();
        return [.. FindDuplicates(enabled), .. FindMissingDependencies(enabled)];
    }

    private static IEnumerable<ModIssue> FindDuplicates(List<LocalMod> enabled)
    {
        return enabled
            .GroupBy(mod => mod.Id!)
            .Where(group => group.Count() > 1)
            .Select(group => new ModIssue(ModIssueKind.Duplicate, group.Key, group.ToList(), null));
    }

    private static IEnumerable<ModIssue> FindMissingDependencies(List<LocalMod> enabled)
    {
        var available = enabled.Select(mod => mod.Id!).Concat(enabled.SelectMany(mod => mod.Provides)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return enabled
            .SelectMany(mod => mod.Dependencies
                .Where(dependency => dependency.Required && !BuiltInIds.Contains(dependency.Id) && !available.Contains(dependency.Id))
                .Select(dependency => (Mod: mod, DependencyId: dependency.Id)))
            .GroupBy(item => (ModId: item.Mod.Id!, item.DependencyId), item => item.Mod)
            .Select(group => new ModIssue(ModIssueKind.MissingDependency, group.Key.ModId, group.ToList(), group.Key.DependencyId));
    }
}
