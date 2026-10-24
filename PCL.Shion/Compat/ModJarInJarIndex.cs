using System;
using System.Collections.Generic;
using System.Linq;
using PCL.Shion.Core.Mods;
using LocalCompFile = PCL.Shion.ModLocalComp.LocalCompFile;
using LocalFileStatus = PCL.Shion.ModLocalComp.LocalCompFile.LocalFileStatus;

namespace PCL.Shion;

/// <summary>
///     Mod 依赖在当前 mods 目录中的满足情况，对应 WPF 版 ModJarInJarDependency 的状态判定。
/// </summary>
public enum JijDepStatus
{
    /// <summary>已安装并启用。</summary>
    Installed,

    /// <summary>提供者存在但全部被禁用。</summary>
    Disabled,

    /// <summary>由其他 Mod 内嵌提供。</summary>
    Bundled,

    /// <summary>已安装，但版本不满足声明的范围。</summary>
    VersionMismatch,

    /// <summary>没有任何提供者。</summary>
    Missing
}

/// <summary>
///     平台级依赖的标识（Minecraft、加载器本身等），不参与 Mod 之间的依赖检查。
/// </summary>
public static class ModDependencyIds
{
    private static readonly HashSet<string> PlatformIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "minecraft", "java", "fabricloader", "fabric-loader", "quilt_loader", "quilt_base",
        "forge", "neoforge", "fml", "mcp", "cleanroom", "liteloader"
    };

    /// <summary>判断依赖标识是否为平台级依赖。</summary>
    public static bool IsPlatform(string? id) => id is not null && PlatformIds.Contains(id);
}

/// <summary>
///     一个 Mod 的依赖与当前 mods 目录中提供者的索引，对应 WPF 版 ModJarInJarIndex。
///     TODO(shion): 内嵌 Mod 的嵌套解析尚未移植，当前只基于顶层文件与已有的 EmbeddedMods 计算。
/// </summary>
public sealed class ModJarInJarIndex
{
    /// <summary>一条依赖声明。</summary>
    /// <param name="DepId">依赖的 Mod ID。</param>
    /// <param name="Raw">声明的版本范围，未声明时为 null。</param>
    /// <param name="Optional">是否为可选依赖。</param>
    public sealed record DepRow(string DepId, string? Raw, bool Optional);

    private readonly List<LocalCompFile> _hosts;
    private readonly string? _mcVersion;

    public ModJarInJarIndex(IEnumerable<LocalCompFile> mods, string? mcVersion)
    {
        _hosts = mods.Where(m => !m.IsFolder).ToList();
        _mcVersion = mcVersion;
    }

    /// <summary>
    ///     获取某个 Mod 中可以被加载的内嵌 Mod（未被禁用的）。
    /// </summary>
    public IEnumerable<LocalCompFile> GetLoadableEmbedded(LocalCompFile host) =>
        (host.EmbeddedMods ?? []).Where(e => e.State == LocalFileStatus.Fine);

    /// <summary>
    ///     读取 Mod 声明的依赖。加载器参数保留以与 WPF 版签名一致，当前未参与判断。
    /// </summary>
    public static List<DepRow> BuildOwnDependencies(LocalCompFile mod, string? loader) =>
        mod.Dependencies.Select(d => new DepRow(d.Id, d.VersionRange, !d.Required)).ToList();

    /// <summary>
    ///     判断依赖在当前 mods 目录中的满足情况。
    /// </summary>
    public JijDepStatus Analyze(LocalCompFile mod, DepRow dep)
    {
        var providers = Providers(dep.DepId).ToList();
        if (providers.Count == 0)
            return JijDepStatus.Missing;

        var enabledTop = providers.Where(p => !p.Bundled && p.File.State == LocalFileStatus.Fine).ToList();
        if (enabledTop.Count > 0)
        {
            // TODO(shion): 尚未解析版本范围，已安装的提供者一律视为满足。
            return JijDepStatus.Installed;
        }

        if (providers.Any(p => p.Bundled && p.File.State == LocalFileStatus.Fine))
            return JijDepStatus.Bundled;
        return JijDepStatus.Disabled;
    }

    /// <summary>
    ///     获取依赖提供者声明的版本，用于版本不匹配时的提示。
    /// </summary>
    public IEnumerable<string> GetProviderVersions(LocalCompFile mod, string depId) =>
        Providers(depId).Select(p => p.File.Version).Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).Distinct();

    /// <summary>
    ///     找出当前启用的、Mod ID 重复的顶层文件，返回两两冲突的组合。Hard 为 true 表示同一 Mod ID 的多个文件同时启用。
    /// </summary>
    public IEnumerable<(LocalCompFile A, LocalCompFile B, bool Hard)> FindActiveConflicts()
    {
        var groups = _hosts
            .Where(m => m.State == LocalFileStatus.Fine && !string.IsNullOrEmpty(m.ModId))
            .GroupBy(m => m.ModId!, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var list = group.ToList();
            if (list.Count < 2)
                continue;
            for (var i = 0; i < list.Count; i++)
                for (var j = i + 1; j < list.Count; j++)
                    yield return (list[i], list[j], true);
        }
    }

    /// <summary>
    ///     找出若禁用或删除 <paramref name="targets"/> 后会失去所需前置的其他顶层 Mod。
    /// </summary>
    public List<LocalCompFile> FindAffected(IReadOnlyCollection<LocalCompFile> targets)
    {
        var targetSet = targets.ToHashSet();
        var targetIds = targets.Select(t => t.ModId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new List<LocalCompFile>();
        foreach (var file in _hosts)
        {
            if (targetSet.Contains(file) || file.State != LocalFileStatus.Fine)
                continue;
            var required = BuildOwnDependencies(file, null)
                .Where(d => !d.Optional && !ModDependencyIds.IsPlatform(d.DepId) && targetIds.Contains(d.DepId))
                .ToList();
            if (required.Count == 0)
                continue;
            var broken = required.Any(d => !Providers(d.DepId)
                .Any(p => !targetSet.Contains(p.File) && p.File.State == LocalFileStatus.Fine));
            if (broken)
                result.Add(file);
        }

        return result;
    }

    private IEnumerable<(LocalCompFile File, bool Bundled)> Providers(string depId)
    {
        foreach (var host in _hosts)
            foreach (var (file, bundled) in Walk(host, false))
                if (IdMatches(file, depId))
                    yield return (file, bundled);
    }

    private static IEnumerable<(LocalCompFile File, bool Bundled)> Walk(LocalCompFile file, bool bundled)
    {
        yield return (file, bundled);
        foreach (var child in file.EmbeddedMods ?? [])
            foreach (var item in Walk(child, true))
                yield return item;
    }

    private static bool IdMatches(LocalCompFile file, string depId) =>
        string.Equals(file.ModId, depId, StringComparison.OrdinalIgnoreCase) ||
        file.Provides.Any(p => string.Equals(p, depId, StringComparison.OrdinalIgnoreCase));
}
