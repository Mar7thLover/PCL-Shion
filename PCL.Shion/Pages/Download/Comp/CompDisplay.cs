using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PCL.Shion.Controls;
using PCL.Shion.Core.Online;
using PCL.Shion.Localization;

namespace PCL.Shion.Pages.Download.Comp;

/// <summary>
/// 资源列表卡片的文字与版本段的生成。
/// </summary>
public static class CompDisplay
{
    private const string NoIconLogo = "avares://PCL.Shion/Assets/Images/Icons/NoIcon.png";

    /// <summary>
    /// 所有正式版的 Minecraft Drop 序数，从高到低排列；未知时为 null。
    /// </summary>
    public static List<int>? AllDrops { get; set; }

    /// <summary>
    /// 为工程创建列表卡片。
    /// </summary>
    /// <param name="project">工程。</param>
    /// <param name="showMcVersionDesc">是否显示游戏版本段。</param>
    /// <param name="showLoaderDesc">是否显示加载器段。</param>
    /// <param name="showQuickDownload">是否显示快速下载按钮。</param>
    public static MyVirtualizingElement<MyCompItem> CreateItem(CompProject project, bool showMcVersionDesc,
        bool showLoaderDesc, bool showQuickDownload = false)
    {
        return new MyVirtualizingElement<MyCompItem>(() =>
                BuildCard(project, showMcVersionDesc, showLoaderDesc, showQuickDownload))
            { Height = 64 };
    }

    /// <summary>
    /// 立即构建工程的列表卡片控件。
    /// </summary>
    public static MyCompItem BuildCard(CompProject project, bool showMcVersionDesc, bool showLoaderDesc,
        bool showQuickDownload)
    {
        var gameVersionDescription = GetGameVersionDescription(project);
        var (fullDesc, partDesc) = GetLoaderDescription(project);
        {
            var newItem = new MyCompItem { Tag = project };
            newItem.Logo = string.IsNullOrEmpty(project.LogoUrl) ? NoIconLogo : project.LogoUrl;

            var title = GetControlTitle(project, true);
            newItem.Title = title.Key;
            newItem.SubTitle = title.Value;

            newItem.Tags = project.TagKeys.Select(key => Lang.Text(key)).Distinct().ToList();
            newItem.Description = project.Description.Replace("\r", "").Replace("\n", "");
            newItem.ShowDownloadBtn = showQuickDownload;

            newItem.VersionText = (showMcVersionDesc, showLoaderDesc) switch
            {
                (true, true) =>
                    $"{(string.IsNullOrEmpty(partDesc) ? "" : partDesc + " ")}{gameVersionDescription}",
                (true, false) => gameVersionDescription,
                (false, true) => fullDesc,
                _ => ""
            };
            newItem.ShowVersionArea = showMcVersionDesc || showLoaderDesc;

            newItem.SourceText = project.FromCurseForge ? "CurseForge" : "Modrinth";

            if (project.LastUpdate is { } lastUpdate)
            {
                newItem.TimeText = Lang.TimeSpan(lastUpdate - DateTime.Now, 1);
                newItem.ShowTimeArea = true;
            }
            else
            {
                newItem.ShowTimeArea = false;
            }

            newItem.DownloadText = Lang.CompactNumber(project.DownloadCount);
            return newItem;
        }
    }

    /// <summary>
    /// 游戏版本段，将 Drop 序数合并为版本区间文字。
    /// </summary>
    public static string GetGameVersionDescription(CompProject project)
    {
        var drops = project.Drops;
        if (drops.Count == 0)
            return Lang.Text("Download.Comp.Detail.CompItem.SnapshotOnly");

        var allDrops = AllDrops;
        var segments = new List<string>();
        var isOld = false;

        for (var i = 0; i < drops.Count; i++)
        {
            int startDrop = drops[i], endDrop = drops[i];

            if (startDrop < 100)
            {
                if (segments.Count > 0 && !isOld) break;
                isOld = true;
            }

            for (var ii = i + 1; ii < drops.Count; ii++)
            {
                if (allDrops is null || allDrops.IndexOf(drops[ii]) != allDrops.IndexOf(endDrop) + 1) break;
                endDrop = drops[ii];
                i = ii;
            }

            var startName = CompVersions.DropToVersion(startDrop);
            var endName = CompVersions.DropToVersion(endDrop);

            if (startDrop == endDrop)
            {
                segments.Add(startName);
            }
            else if (allDrops is { Count: > 0 } && startDrop >= allDrops[0])
            {
                if (endDrop < 100)
                {
                    segments.Clear();
                    segments.Add(Lang.Text("Download.Comp.Detail.CompItem.AllVersions"));
                    break;
                }

                segments.Add(endName + "+");
            }
            else if (endDrop < 100)
            {
                segments.Add(startName + "-");
                break;
            }
            else if (allDrops is null || allDrops.IndexOf(endDrop) - allDrops.IndexOf(startDrop) == 1)
            {
                segments.Add($"{startName}, {endName}");
            }
            else
            {
                segments.Add($"{startName}~{endName}");
            }
        }

        return string.Join(", ", segments);
    }

    /// <summary>
    /// 加载器段：完整描述与简短描述。
    /// </summary>
    public static (string FullDesc, string PartDesc) GetLoaderDescription(CompProject project)
    {
        var loaders = project.ModLoaders;
        return loaders.Count switch
        {
            0 => (Lang.Text("Download.Comp.Type.Unknown"), ""),
            1 => (Lang.Text("Download.Comp.Type.Only", loaders[0].ToString()), loaders[0].ToString()),
            _ => GetMultiLoaderDescription(project)
        };
    }

    private static (string, string) GetMultiLoaderDescription(CompProject project)
    {
        var newestDrop = project.Drops.Count > 0 ? project.Drops[0] : 0;
        var loaders = project.ModLoaders;
        var isAny = loaders.Contains(CompLoaderType.Forge) &&
                    (newestDrop < 140 || loaders.Contains(CompLoaderType.Fabric)) &&
                    (newestDrop < 200 || loaders.Contains(CompLoaderType.NeoForge)) &&
                    (newestDrop < 140 || loaders.Contains(CompLoaderType.Quilt));

        var joined = string.Join(" / ", loaders);
        return isAny ? (Lang.Text("Download.Comp.Type.Any"), "") : (joined, joined);
    }

    /// <summary>
    /// 从工程名拆分出主标题与副标题。
    /// </summary>
    /// <param name="project">工程。</param>
    /// <param name="hasModLoaderDescription">是否显示加载器相关的副标题。</param>
    public static KeyValuePair<string, string> GetControlTitle(CompProject project, bool hasModLoaderDescription)
    {
        var rawName = project.RawName;
        var nameLists = rawName.Split([" | ", " - ", "(", ")", "[", "]", "{", "}"],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim(' ', '/', '\\', '"'))
            .Where(w => !string.IsNullOrEmpty(w))
            .ToList();

        if (nameLists.Count <= 1)
            return new KeyValuePair<string, string>(rawName, "");

        var normalNameList = new List<string>();
        var subtitleList = new List<string>();
        foreach (var name in nameLists)
        {
            var lowerName = name.ToLowerInvariant();
            if (name.ToUpperInvariant() == name && name != "FPS" && name != "HUD")
                subtitleList.Add(name);
            else if (IsModLoaderMarker(lowerName))
                subtitleList.Add(name);
            else
                normalNameList.Add(name);
        }

        if (normalNameList.Count == 0 || subtitleList.Count == 0)
            return new KeyValuePair<string, string>(rawName, "");

        var title = string.Join(" - ", normalNameList);

        var finalSubtitles = new List<string>();
        foreach (var rawEx in subtitleList.Distinct())
        {
            var ex = rawEx;
            var lowerEx = ex.ToLowerInvariant();
            var isModLoader = lowerEx.Contains("forge") || lowerEx.Contains("fabric") || lowerEx.Contains("quilt");

            if (!hasModLoaderDescription && isModLoader) continue;
            if (ex.Length < 16 && lowerEx.Contains("fabric") && lowerEx.Contains("forge")) continue;

            if (isModLoader && !ex.Contains('版') &&
                lowerEx.Replace("forge", "").Replace("fabric", "").Replace("quilt", "").Length <= 3)
                ex = Capitalize(ex.Replace("Edition", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("edition", "", StringComparison.OrdinalIgnoreCase)
                    .Trim()) + Lang.Text("Download.Comp.Detail.CompItem.EditionSuffix");

            ex = ex.Replace("forge", "Forge").Replace("neo", "Neo").Replace("fabric", "Fabric")
                .Replace("quilt", "Quilt");
            finalSubtitles.Add(ex.Trim());
        }

        var subtitleResult = finalSubtitles.Count > 0 ? "  |  " + string.Join("  |  ", finalSubtitles) : "";
        return new KeyValuePair<string, string>(title, subtitleResult);
    }

    private static bool IsModLoaderMarker(string input) =>
        (input.Contains("forge") || input.Contains("fabric") || input.Contains("quilt")) &&
        !Regex.IsMatch(input.Replace("forge", "").Replace("fabric", "").Replace("quilt", ""), "[a-z]+");

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
