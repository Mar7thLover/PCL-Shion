using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Shion.Controls;
using PCL.Shion.Core.Online;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Download.Comp;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

using LocalFile = PCL.Shion.ModLocalComp.LocalCompFile;

namespace PCL.Shion.Pages.Instance;

/// <summary>
///     Mod 的 Jar-in-Jar（内嵌模组）管理页：展示缺失依赖、版本冲突、依赖关系与内嵌模组。与 WPF 版 PageInstanceCompJarInJar 对应。
/// </summary>
public partial class PageInstanceCompJarInJar : MyPageRight
{
    private ModJarInJarIndex _index = null!;
    private string? _instanceMc;
    private string _search = "";

    private static readonly IBrush BrushOk = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
    private static readonly IBrush BrushWarn = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
    private static readonly IBrush BrushError = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));

    public PageInstanceCompJarInJar()
    {
        InitializeComponent();
        PanScroll = PanBack;
        BtnBack.Click += (_, _) => GoBack();
        BtnEmptyBack.Click += (_, _) => GoBack();
        BtnRefresh.Click += (_, _) => RefreshList();
        SearchBox.TextChanged += (_, _) =>
        {
            _search = (SearchBox.Text ?? "").Trim();
            RefreshList();
        };
        Loaded += (_, _) => RefreshList();
    }

    private bool Match(LocalFile m)
    {
        if (_search.Length == 0) return true;
        return Contains(DisplayName(m)) || Contains(m.ModId) || Contains(m.FileName);
    }

    private bool Contains(string? s) =>
        !string.IsNullOrEmpty(s) && s.Contains(_search, StringComparison.OrdinalIgnoreCase);

    private bool AnyEmbeddedMatch(List<LocalFile>? embedded) =>
        embedded is not null && embedded.Any(e => Match(e) || AnyEmbeddedMatch(e.EmbeddedMods));

    private static void GoBack() => Navigator.Go(PageType.InstanceSetup, PageSubType.VersionMod);

    /// <summary>
    ///     后台内嵌解析完成后刷新当前页。
    /// </summary>
    public void OnJijResolved()
    {
        if (IsVisible) RefreshList();
    }

    #region 列表构建

    private void RefreshList()
    {
        var output = ModLocalComp.compResourceListLoader.output ?? new List<LocalFile>();
        if (!OutputBelongsToCurrentInstance(output))
        {
            GoBack();
            return;
        }

        var allMods = output
            .Where(m => !m.IsFolder && m.State != LocalFile.LocalFileStatus.Unavailable)
            .ToList();
        _instanceMc = PageInstanceLeft.McInstance?.Info?.VanillaName;
        _index = new ModJarInJarIndex(allMods, _instanceMc);
        PanLoad.IsVisible = false;

        PanWarnList.Children.Clear();
        foreach (var host in allMods)
        {
            if (host.State != LocalFile.LocalFileStatus.Fine) continue;
            if (Match(host)) AppendMissingWarning(host, null, host.DetectedLoader);
            foreach (var node in _index.GetLoadableEmbedded(host))
                if (Match(node))
                    AppendMissingWarning(node, host, node.JijLoader);
        }

        var hasWarning = PanWarnList.Children.Count > 0;
        CardWarnings.IsVisible = hasWarning;

        PanConflictList.Children.Clear();
        var conflictCount = 0;
        foreach (var (a, b, hard) in _index.FindActiveConflicts())
        {
            if (!Match(a) && !Match(b)) continue;
            PanConflictList.Children.Add(MakeConflictRow(a, b, hard));
            conflictCount++;
        }

        CardConflicts.IsVisible = conflictCount > 0;

        PanRelationList.Children.Clear();
        var relationCount = 0;
        foreach (var host in allMods)
        {
            var hostDeps = ModJarInJarIndex.BuildOwnDependencies(host, host.DetectedLoader)
                .Where(d => !ModDependencyIds.IsPlatform(d.DepId)).ToList();
            if (hostDeps.Count > 0 && (Match(host) || hostDeps.Any(d => Contains(d.DepId))))
            {
                PanRelationList.Children.Add(MakeRelationCard(host, null, hostDeps));
                relationCount++;
            }

            foreach (var node in _index.GetLoadableEmbedded(host))
            {
                var nodeDeps = ModJarInJarIndex.BuildOwnDependencies(node, node.JijLoader)
                    .Where(d => !ModDependencyIds.IsPlatform(d.DepId)).ToList();
                if (nodeDeps.Count == 0) continue;
                if (!Match(node) && !nodeDeps.Any(d => Contains(d.DepId))) continue;
                PanRelationList.Children.Add(MakeRelationCard(node, host, nodeDeps));
                relationCount++;
            }
        }

        SectionRelations.IsVisible = relationCount > 0;

        PanBundledList.Children.Clear();
        var bundledMods = allMods
            .Where(m => m.EmbeddedMods is { Count: > 0 } && (Match(m) || AnyEmbeddedMatch(m.EmbeddedMods)))
            .ToList();
        foreach (var mod in bundledMods)
            PanBundledList.Children.Add(MakeCard(mod, BuildBundledContent));
        SectionBundled.IsVisible = bundledMods.Count > 0;

        // 搜索无结果时不显示“没有模组”空态（避免误导为实例本身没有），仅留空白
        PanEmpty.IsVisible = !(hasWarning || conflictCount > 0 || relationCount > 0 || bundledMods.Count > 0 ||
                               _search.Length > 0);
    }

    private static MyCard MakeCard(LocalFile mod, Action<StackPanel, LocalFile> build, string? title = null)
    {
        var card = new MyCard
        {
            Title = title ?? DisplayName(mod),
            CanSwap = true,
            Margin = new Thickness(0, 0, 0, 10)
        };
        var stack = new StackPanel
        {
            Margin = new Thickness(20, MyCard.SwapedHeight, 18, 12),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        card.SwapControl = stack;
        card.InstallMethod = s => build(s, mod);
        card.IsSwapped = true;
        return card;
    }

    private void AppendMissingWarning(LocalFile mod, LocalFile? parent, string? loader)
    {
        var probed = ModJarInJarIndex.BuildOwnDependencies(mod, loader)
            .Where(d => !ModDependencyIds.IsPlatform(d.DepId) && !d.Optional)
            .Select(d => (d, status: _index.Analyze(mod, d)))
            .ToList();
        var missing = probed.Where(x => x.status == JijDepStatus.Missing).Select(x => x.d.DepId).ToList();
        var mismatch = probed.Where(x => x.status == JijDepStatus.VersionMismatch)
            .Select(x => Lang.Text("Instance.Resource.Mod.JarInJar.Warning.VersionMismatch.Detail",
                x.d.DepId, x.d.Raw ?? "*", string.Join(", ", _index.GetProviderVersions(mod, x.d.DepId))))
            .ToList();
        // 版本匹配的 provider 全部被禁用：运行时同样不可用，需提示（用户重新启用即可修复）
        var disabled = probed.Where(x => x.status == JijDepStatus.Disabled).Select(x => x.d.DepId).ToList();
        if (missing.Count == 0 && mismatch.Count == 0 && disabled.Count == 0) return;

        var name = parent is null
            ? DisplayName(mod)
            : DisplayName(mod) + " " + Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.From", DisplayName(parent));
        if (missing.Count > 0)
            PanWarnList.Children.Add(Text(
                Lang.Text("Instance.Resource.Mod.JarInJar.Warning.Missing", name, string.Join(", ", missing)),
                BrushError));
        if (mismatch.Count > 0)
            PanWarnList.Children.Add(Text(
                Lang.Text("Instance.Resource.Mod.JarInJar.Warning.VersionMismatch", name, string.Join(", ", mismatch)),
                BrushWarn));
        if (disabled.Count > 0)
            PanWarnList.Children.Add(Text(
                Lang.Text("Instance.Resource.Mod.JarInJar.Warning.Disabled", name, string.Join(", ", disabled)),
                BrushWarn));
    }

    private Panel MakeConflictRow(LocalFile a, LocalFile b, bool hard)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(Text(
            Lang.Text(hard
                ? "Instance.Resource.Mod.JarInJar.Conflict.Incompatible"
                : "Instance.Resource.Mod.JarInJar.Conflict.Discouraged"),
            hard ? BrushError : BrushWarn));
        row.Children.Add(TextRes(
            "  " + Lang.Text("Instance.Resource.Mod.JarInJar.Conflict.Pair", DisplayName(a), DisplayName(b)),
            "ColorBrush1"));
        return row;
    }

    private MyCard MakeRelationCard(LocalFile mod, LocalFile? parent, List<ModJarInJarIndex.DepRow> deps)
    {
        var title = parent is null
            ? DisplayName(mod)
            : DisplayName(mod) + "  " + Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.From", DisplayName(parent));
        return MakeCard(mod, (stack, owner) =>
        {
            foreach (var dep in deps) stack.Children.Add(BuildDependencyRow(owner, dep));
        }, title);
    }

    private void BuildBundledContent(StackPanel stack, LocalFile mod)
    {
        var selected = mod.State == LocalFile.LocalFileStatus.Fine
            ? _index.GetLoadableEmbedded(mod).ToHashSet()
            : new HashSet<LocalFile>();
        AppendTreeRows(stack, mod.EmbeddedMods, 0, selected);
    }

    private void AppendTreeRows(StackPanel stack, List<LocalFile>? embedded, int depth, HashSet<LocalFile> selected)
    {
        if (embedded is null) return;

        var ungrouped = embedded.Where(e => string.IsNullOrEmpty(e.ModId)).ToList();
        foreach (var g in embedded.Where(e => !string.IsNullOrEmpty(e.ModId))
                     .GroupBy(e => e.ModId, StringComparer.OrdinalIgnoreCase))
        {
            var versions = g.ToList();
            if (versions.Count == 1)
            {
                stack.Children.Add(BuildTreeRow(versions[0], depth, 0, McConstraintMatcher.MatchKind.None));
                AppendTreeRows(stack, versions[0].EmbeddedMods, depth + 1, selected);
                continue;
            }

            var scored = versions
                .Select(v => (v, kind: McConstraintMatcher.Match(v.FileName, v.Version, v.JijTargetMcVersion,
                    v.JijLoader, _instanceMc)))
                .OrderByDescending(x => (int)x.kind)
                .ToList();
            var selectedRep = versions.FirstOrDefault(selected.Contains);
            var rep = selectedRep ?? scored[0].v;
            var kind = selectedRep is null
                ? scored[0].kind
                : McConstraintMatcher.Match(rep.FileName, rep.Version, rep.JijTargetMcVersion, rep.JijLoader,
                    _instanceMc);
            var tooltip = string.Join("\n",
                versions.Select(v => string.IsNullOrWhiteSpace(v.Version) ? v.FileName : v.Version));
            stack.Children.Add(BuildTreeRow(rep, depth, versions.Count, kind, tooltip, selectedRep is not null));
            AppendTreeRows(stack, rep.EmbeddedMods, depth + 1, selected);
        }

        foreach (var e in ungrouped)
        {
            stack.Children.Add(BuildTreeRow(e, depth, 0, McConstraintMatcher.MatchKind.None));
            AppendTreeRows(stack, e.EmbeddedMods, depth + 1, selected);
        }
    }

    private Panel BuildTreeRow(LocalFile e, int depth, int versionCount, McConstraintMatcher.MatchKind kind,
        string? versionsTooltip = null, bool selected = false)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8 + depth * 20, 2, 0, 2)
        };
        row.Children.Add(TextRes("• " + DisplayName(e), "ColorBrush1", true));
        var ver = CleanPlaceholder(e.Version);
        if (!string.IsNullOrEmpty(ver))
            row.Children.Add(TextRes("  (" + ver + ")", "ColorBrush2"));
        if (!string.IsNullOrEmpty(e.JijLoader))
            row.Children.Add(TextRes("  [" + e.JijLoader + "]", "ColorBrush3"));
        var mc = CleanPlaceholder(e.JijTargetMcVersion);
        if (!string.IsNullOrEmpty(mc))
            row.Children.Add(TextRes("  (MC " + mc + ")", "ColorBrushInfo"));

        if (versionCount > 1)
        {
            var badge = TextRes("  " + Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Versions", versionCount),
                "ColorBrush2");
            if (!string.IsNullOrEmpty(versionsTooltip))
                ToolTip.SetTip(badge, versionsTooltip);
            row.Children.Add(badge);
        }

        if (selected || kind is McConstraintMatcher.MatchKind.Exact or McConstraintMatcher.MatchKind.Range)
            row.Children.Add(Text("  " + Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Current"), BrushOk));
        else if (kind == McConstraintMatcher.MatchKind.Incompatible)
            row.Children.Add(Text("  " + Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Incompatible"), BrushWarn));

        return row;
    }

    private Panel BuildDependencyRow(LocalFile mod, ModJarInJarIndex.DepRow dep)
    {
        var status = _index.Analyze(mod, dep);
        var optional = dep.Optional;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 0, 2) };
        var text = dep.Raw is null ? dep.DepId : dep.DepId + " " + dep.Raw;
        row.Children.Add(TextRes("• " + text, "ColorBrush1"));
        if (optional)
            row.Children.Add(TextRes("  " + Lang.Text("Instance.Resource.Mod.JarInJar.Dep.Optional"), "ColorBrush2"));
        var label = "  (" + StatusLabel(status) + ")";
        // 可选依赖缺失不算问题，用主题灰；其余：内嵌提供灰、已装/已禁/缺失为绿/橙/红语义色
        if (optional || status == JijDepStatus.Bundled)
            row.Children.Add(TextRes(label, "ColorBrush2"));
        else
            row.Children.Add(Text(label, StatusBrush(status)));
        return row;
    }

    private static string StatusLabel(JijDepStatus status) => Lang.Text(status switch
    {
        JijDepStatus.Installed => "Instance.Resource.Mod.JarInJar.Dep.Installed",
        JijDepStatus.Disabled => "Instance.Resource.Mod.JarInJar.Dep.Disabled",
        JijDepStatus.Bundled => "Instance.Resource.Mod.JarInJar.Dep.Bundled",
        JijDepStatus.VersionMismatch => "Instance.Resource.Mod.JarInJar.Dep.VersionMismatch",
        _ => "Instance.Resource.Mod.JarInJar.Dep.Missing"
    });

    private static IBrush StatusBrush(JijDepStatus status) => status switch
    {
        JijDepStatus.Installed => BrushOk,
        JijDepStatus.Disabled => BrushWarn,
        JijDepStatus.VersionMismatch => BrushWarn,
        _ => BrushError
    };

    #endregion

    #region 辅助

    // output 是否属于当前实例（mod 路径必在实例游戏目录 PathIndie 下）；空列表无从判断按匹配处理避免误弹
    private static bool OutputBelongsToCurrentInstance(List<LocalFile> output)
    {
        var inst = PageInstanceLeft.McInstance;
        if (inst is null || output is null) return false;
        try
        {
            var input = ModLocalComp.compResourceListLoader.input;
            if (input is null || input.compType != CompType.Mod ||
                !string.Equals(
                    Path.GetFullPath(input.gameVersion.PathInstance)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(inst.PathInstance)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
                return false;
            var first = output.FirstOrDefault(m => !string.IsNullOrEmpty(m.path));
            if (first is null) return true;
            var root = Path.GetFullPath(inst.PathIndie)
                           .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            return Path.GetFullPath(first.path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    // 未替换的版本占位符不显示
    private static string? CleanPlaceholder(string? v) =>
        string.IsNullOrEmpty(v) || v.Contains("${") ? null : v;

    private static string DisplayName(LocalFile m)
    {
        if (m.Comp is not null)
        {
            var title = CompDisplay.GetControlTitle(m.Comp, false);
            return title.Key + title.Value;
        }

        return string.IsNullOrWhiteSpace(m.Name) ? m.FileName : m.Name;
    }

    private static TextBlock Text(string text, IBrush brush, bool bold = false) => new()
    {
        Text = text,
        Foreground = brush,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        TextWrapping = TextWrapping.Wrap
    };

    private static TextBlock TextRes(string text, string resourceKey, bool bold = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, resourceKey);
        return tb;
    }

    #endregion
}
