using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Navigation;
using PCL.Shion.Services;
using PCL.Shion.ViewModels;

namespace PCL.Shion.Pages.Select;

/// <summary>
///     实例卡片的种类，顺序与 WPF 中 McInstanceCardType 的显示顺序一致。
/// </summary>
internal enum SelectCardKind
{
    Star,
    Api,
    OriginalLike,
    Error,
    Hidden
}

/// <summary>
///     列表中的一个实例。
/// </summary>
internal sealed record SelectInstanceEntry(GameInstance Instance, bool IsStar, bool IsHidden)
{
    public string Name => Instance.Name;

    /// <summary>
    ///     列表第二行的描述，与 WPF 中 McInstance.Desc 对应。
    /// </summary>
    public string Desc => new InstanceItem(Instance).Description;
}

/// <summary>
///     一个实例卡片及其中的实例。
/// </summary>
internal sealed record SelectCard(SelectCardKind Kind, IReadOnlyList<SelectInstanceEntry> Items);

/// <summary>
///     实例列表的扫描、分类与收藏、隐藏状态，对应 WPF 中的 ModInstanceList。
/// </summary>
internal static class SelectInstances
{
    private static readonly IComparer<string?> VersionComparer = Comparer<string?>.Create(CompareVersion);

    private static IReadOnlyList<SelectCard> cards = [];

    /// <summary>
    ///     当前 .minecraft 目录下的实例卡片，由 <see cref="Loader" /> 在后台扫描得出。
    /// </summary>
    public static IReadOnlyList<SelectCard> Cards => cards;

    /// <summary>
    ///     实例列表的加载器，输入为 .minecraft 目录。
    /// </summary>
    public static readonly ModLoader.LoaderTask<string, int> Loader = new("Minecraft 实例列表", task =>
    {
        cards = Classify(task.input ?? "");
    });

    /// <summary>
    ///     重新扫描实例。<paramref name="force" /> 为 false 时，目录未变化则不重复扫描。
    /// </summary>
    public static void Reload(bool force)
    {
        Loader.Start(App.Services.Settings.MinecraftRoot, force);
    }

    /// <summary>
    ///     设置或取消收藏，对应 WPF 中的 States.Instance.Starred。
    /// </summary>
    public static void SetStarred(string directory, bool starred)
    {
        var settings = App.Services.Settings;
        settings.StarredInstances.RemoveAll(item => IsSameDirectory(item, directory));
        if (starred)
            settings.StarredInstances.Add(directory);
        settings.Save();
    }

    /// <summary>
    ///     清除实例在收藏与隐藏中的记录，实例被删除后调用。
    /// </summary>
    public static void Forget(string directory)
    {
        var settings = App.Services.Settings;
        settings.StarredInstances.RemoveAll(item => IsSameDirectory(item, directory));
        settings.HiddenInstances.RemoveAll(item => IsSameDirectory(item, directory));
        settings.Save();
    }

    private static IReadOnlyList<SelectCard> Classify(string root)
    {
        var settings = App.Services.Settings;
        var starred = new HashSet<string>(settings.StarredInstances, StringComparer.OrdinalIgnoreCase);
        var hidden = new HashSet<string>(settings.HiddenInstances, StringComparer.OrdinalIgnoreCase);
        var entries = InstanceScanner.Scan(root)
            .Select(instance => new SelectInstanceEntry(
                instance,
                starred.Contains(instance.Directory),
                hidden.Contains(instance.Directory)))
            .ToList();

        var result = new List<SelectCard>();

        void Add(SelectCardKind kind, IEnumerable<SelectInstanceEntry> items)
        {
            var sorted = Sort(items);
            if (sorted.Count > 0)
                result.Add(new SelectCard(kind, sorted));
        }

        bool IsRegular(SelectInstanceEntry entry) => !entry.IsStar && !entry.IsHidden && entry.Instance.Error is null;

        Add(SelectCardKind.Star, entries.Where(entry => entry.IsStar && !entry.IsHidden));
        Add(SelectCardKind.Api, entries.Where(entry => IsRegular(entry) && entry.Instance.Loader is not null));
        Add(SelectCardKind.OriginalLike, entries.Where(entry => IsRegular(entry) && entry.Instance.Loader is null));
        Add(SelectCardKind.Error,
            entries.Where(entry => !entry.IsStar && !entry.IsHidden && entry.Instance.Error is not null));
        Add(SelectCardKind.Hidden, entries.Where(entry => entry.IsHidden));
        return result;
    }

    private static List<SelectInstanceEntry> Sort(IEnumerable<SelectInstanceEntry> items) =>
        App.Services.Settings.InstanceSort switch
        {
            InstanceSortMode.Name => items.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            InstanceSortMode.Version => items
                .OrderByDescending(entry => entry.Instance.MinecraftVersion, VersionComparer).ToList(),
            _ => items.OrderByDescending(entry => entry.Instance.LastPlayed ?? DateTimeOffset.MinValue).ToList(),
        };

    private static int CompareVersion(string? left, string? right)
    {
        var leftParts = VersionParts(left);
        var rightParts = VersionParts(right);
        for (var i = 0; i < Math.Max(leftParts.Length, rightParts.Length); i++)
        {
            var a = i < leftParts.Length ? leftParts[i] : -1;
            var b = i < rightParts.Length ? rightParts[i] : -1;
            if (a != b)
                return a.CompareTo(b);
        }

        return string.CompareOrdinal(left, right);
    }

    private static long[] VersionParts(string? version) =>
        Regex.Matches(version ?? "", @"\d+")
            .Select(match => long.TryParse(match.Value, out var value) ? value : 0)
            .ToArray();

    private static bool IsSameDirectory(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
