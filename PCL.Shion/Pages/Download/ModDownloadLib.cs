using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Loaders;
using PCL.Shion.Core.Loaders.Cleanroom;
using PCL.Shion.Core.Loaders.Extra;
using PCL.Shion.Core.Loaders.Forge;
using PCL.Shion.Core.Online;
using PCL.Shion.Core.Platform;
using PCL.Shion.Core.Versions;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Download;
using PCL.Shion.UI;

namespace PCL.Shion;

/// <summary>
/// 版本下载列表的条目构建与实例安装入口，对应 WPF 的 ModDownloadLib。
/// 列表项的样式与“保存”“更新日志”等按钮沿用下载页的 <see cref="DownloadListItems"/> 约定；
/// 保存安装器、下载原版文件交给 <see cref="DownloadSaver"/>，安装交给 PCL.Shion.Core 的安装器。
/// </summary>
public static class ModDownloadLib
{
    private const string mcInstallDefaultType = "安装";
    private const string UnsupportedMessage = "该功能暂未在 Shion 中提供";

    /// <summary>
    /// Forge 推荐版本表，键形如 <c>1.20.1-recommended</c>，值为对应的 Forge 版本号。由 <see cref="McDownloadForgeRecommendedRefresh"/> 刷新。
    /// </summary>
    internal static volatile IReadOnlyDictionary<string, string> ForgePromotions = new Dictionary<string, string>();

    private static bool isForgeRecommendedRefreshed;

    #region 推荐版本

    /// <summary>
    /// 在后台刷新 Forge 推荐版本表。同一次运行中只刷新一次；失败时下次调用会重新尝试。
    /// </summary>
    public static void McDownloadForgeRecommendedRefresh()
    {
        if (isForgeRecommendedRefreshed)
            return;
        isForgeRecommendedRefreshed = true;

        var services = App.Services;
        _ = Task.Run(async () =>
        {
            try
            {
                ForgePromotions = await ForgeIndex
                    .GetPromotionsAsync(services.Engine.Http, services.Source, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                isForgeRecommendedRefreshed = false;
                LogWrapper.Warn(ex, "刷新 Forge 推荐版本缓存失败");
            }
        });
    }

    /// <summary>
    /// 获取某个 Minecraft 版本的推荐 Forge 版本号，没有时返回 null。
    /// </summary>
    public static string? McDownloadForgeRecommendedGet(string mcInstance) =>
        ForgePromotions.TryGetValue(mcInstance + "-recommended", out var version) ? version : null;

    #endregion

    #region 通用辅助

    /// <summary>
    /// 创建一个可点击的列表项，点击时执行 <paramref name="onClick"/>。
    /// </summary>
    private static MyListItem CreateItem(string title, string? info, string logo, object tag, MyListItem.ClickEventHandler? onClick)
    {
        var item = new MyListItem
        {
            Title = title,
            Info = info,
            Logo = logo,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = tag
        };
        if (onClick is not null)
            item.Click += onClick;
        return item;
    }

    private static TopLevel? TopOf(MyListItem item) => TopLevel.GetTopLevel(item);

    private static MyIconButton SaveButton(Action action) =>
        DownloadListItems.IconButton("lucide/save", Lang.Text("Download.Version.SaveAs"), action);

    private static MyIconButton ChangelogButton(Action action) =>
        DownloadListItems.IconButton("lucide/info", Lang.Text("Download.Version.Changelog"), action, 1.05d);

    private static MyIconButton ServerButton(Action action) =>
        DownloadListItems.IconButton("lucide/server", Lang.Text("Download.Version.DownloadServer"), action);

    private static IReadOnlyList<Uri> UrlsOf(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return Array.Empty<Uri>();
        return new[] { new Uri(url) };
    }

    private static DateTimeOffset ReadReleaseTime(JsonObject entry) =>
        DateTimeOffset.TryParse(entry["releaseTime"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : DateTimeOffset.UnixEpoch;

    private static RemoteVersion ToRemoteVersion(JsonObject entry) => new(
        entry["id"]?.ToString() ?? "",
        entry["type"]?.ToString() ?? "",
        ReadReleaseTime(entry),
        new Uri(entry["url"]?.ToString() ?? ""),
        entry["sha1"]?.ToString());

    private static bool IsStable(JsonObject entry) => entry["stable"]?.GetValue<bool>() ?? false;

    private static TextBlock CreateAllVersionsText(int count) => new()
    {
        Text = Lang.Text("Download.Version.AllVersions", count),
        HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(6d, 13d, 0d, 4d)
    };

    /// <summary>
    /// 把 Forge 条目转换为 PCL.Shion.Core 的加载器版本。安装器版本号优先使用列表原始值。
    /// </summary>
    private static LoaderVersion ToCoreForge(ModDownload.DlForgeVersionEntry entry) =>
        new(LoaderKind.Forge, entry.InstallerVersion ?? entry.FileVersion, entry.Inherit, true);

    /// <summary>
    /// 把 NeoForge 条目转换为 PCL.Shion.Core 的加载器版本。1.20.1 上的旧版本号去掉了 <c>1.20.1-</c> 前缀。
    /// </summary>
    private static LoaderVersion ToCoreNeoForge(ModDownload.DlNeoForgeListEntry entry)
    {
        const string legacyPrefix = "1.20.1-";
        var version = entry.ApiName.StartsWith(legacyPrefix, StringComparison.Ordinal)
            ? entry.ApiName[legacyPrefix.Length..]
            : entry.ApiName;
        return new LoaderVersion(LoaderKind.NeoForge, version, entry.Inherit, !entry.IsBeta);
    }

    private static string StatusText(CompFileStatus status) => status switch
    {
        CompFileStatus.Release => Lang.Text("Download.Comp.Detail.FileList.ReleaseType.Release"),
        CompFileStatus.Beta => Lang.Text("Download.Comp.Detail.FileList.ReleaseType.Beta"),
        _ => Lang.Text("Download.Comp.Detail.FileList.ReleaseType.Alpha")
    };

    private static string CompFileInfo(CompFile file) =>
        StatusText(file.Status) + "  |  " + Lang.Text("Download.Version.ReleaseDate", Lang.Date(file.ReleaseDate, "g"));

    /// <summary>
    /// 把加载器安装器交给下载页的保存流程，由用户选择保存位置。
    /// </summary>
    private static void SaveInstaller(MyListItem item, string loaderName, string displayName, string fileName, IReadOnlyList<Uri> urls)
    {
        _ = DownloadSaver.SaveInstallerAsync(TopOf(item), loaderName, displayName, fileName, urls);
    }

    #endregion

    #region 原版版本列表项

    /// <summary>
    /// 原版版本列表项。<paramref name="entry"/> 为版本清单中的一个 JSON 对象（含 id、type、url、releaseTime）。
    /// 普通模式下菜单为“服务端”与“更新日志”；保存模式下额外有“保存”。
    /// </summary>
    public static MyListItem McDownloadListItem(JsonObject entry, MyListItem.ClickEventHandler onClick, bool isSaveOnly)
    {
        var id = entry["id"]?.ToString() ?? "";
        var type = entry["type"]?.ToString();
        var logo = DownloadListItems.BlockLogo(type switch
        {
            "release" => "Grass",
            "snapshot" or "pending" => "CommandBlock",
            "special" => "GoldBlock",
            _ => "CobbleStone"
        });

        var formattedVersion = VanillaVersionText.FormatVersion(id).Replace("_", " ");
        var releaseDate = ReadReleaseTime(entry).ToLocalTime();
        string info;
        var lore = entry["lore"]?.ToString();
        if (lore is null)
        {
            var date = Lang.Date(releaseDate, "g");
            info = formattedVersion != id ? date + " | " + id : date;
        }
        else
        {
            info = formattedVersion != id ? lore + " | " + id : lore;
        }

        var item = CreateItem(formattedVersion, info, logo, entry, onClick);
        if (entry["url"]?.ToString().Contains("unlisted-versions-of-minecraft", StringComparison.Ordinal) == true)
            item.Tags = Lang.Text("Download.Source.Tag.Uvmc");

        item.ContentHandler = (sender, _) =>
        {
            var server = ServerButton(() => DownloadSaver.SaveServerAsync(TopOf(item), ToRemoteVersion(entry)));
            var changelog = ChangelogButton(() => SystemShell.OpenUrl(VanillaVersionText.GetWikiUrl(id)));
            sender.Buttons = isSaveOnly
                ? new MyIconButton[]
                {
                    SaveButton(() => DownloadSaver.SaveClientAsync(TopOf(item), ToRemoteVersion(entry))),
                    server,
                    changelog
                }
                : new[] { server, changelog };
        };
        return item;
    }

    #endregion

    #region OptiFine

    /// <summary>
    /// OptiFine 版本列表项。点击执行 <paramref name="onClick"/>；普通模式下菜单为“保存”与“更新日志”，保存模式下只有“更新日志”。
    /// </summary>
    public static MyListItem OptiFineDownloadListItem(ModDownload.DlOptiFineListEntry entry, MyListItem.ClickEventHandler onClick, bool isSaveOnly)
    {
        var infoParts = new List<string>
        {
            entry.IsPreview ? Lang.Text("Download.Version.Type.Preview") : Lang.Text("Download.Version.Type.Release")
        };
        if (!string.IsNullOrEmpty(entry.ReleaseTime))
            infoParts.Add(Lang.Text("Download.Version.ReleaseDate", entry.ReleaseTime));
        if (entry.RequiredForgeVersion is null)
            infoParts.Add(Lang.Text("Download.Version.Optifine.IncompatibleForge"));
        else if (!string.IsNullOrEmpty(entry.RequiredForgeVersion))
            infoParts.Add(Lang.Text("Download.Version.Optifine.CompatibleForge", entry.RequiredForgeVersion));

        var item = CreateItem(entry.DisplayName, string.Join("  |  ", infoParts), DownloadListItems.BlockLogo("GrassPath"), entry, onClick);
        item.ContentHandler = (sender, _) =>
        {
            var changelog = ChangelogButton(() => SystemShell.OpenUrl("https://optifine.net/changelog?f=" + entry.NameFile));
            sender.Buttons = isSaveOnly
                ? new[] { changelog }
                : new[]
                {
                    SaveButton(() => SaveInstaller(item, "OptiFine", entry.DisplayName, entry.NameFile, UrlsOf(entry.DownloadUrl))),
                    changelog
                };
        };
        return item;
    }

    #endregion

    #region LiteLoader

    /// <summary>
    /// LiteLoader 版本列表项。菜单为“保存安装器”。
    /// TODO(shion): WPF 中的“查看全部版本”按钮尚未移植。
    /// </summary>
    public static MyListItem LiteLoaderDownloadListItem(ModDownload.DlLiteLoaderListEntry entry, MyListItem.ClickEventHandler onClick, bool isSaveOnly)
    {
        var infoParts = new List<string>
        {
            entry.IsPreview ? Lang.Text("Download.Version.Type.Preview") : Lang.Text("Download.Version.Type.Release")
        };
        if (!string.IsNullOrEmpty(entry.ReleaseTime))
            infoParts.Add(Lang.Text("Download.Version.ReleaseDate", entry.ReleaseTime));

        var item = CreateItem(entry.Inherit, string.Join("  |  ", infoParts), DownloadListItems.BlockLogo("Egg"), entry, onClick);
        item.ContentHandler = (sender, _) =>
        {
            sender.Buttons = new[]
            {
                SaveButton(() => SaveInstaller(item, "LiteLoader", "LiteLoader " + entry.Inherit, entry.FileName, UrlsOf(entry.DownloadUrl)))
            };
        };
        return item;
    }

    #endregion

    #region Forge

    /// <summary>
    /// 把 Forge 版本列表预先列出：推荐版本与最新版本放在最前，最后是版本总数。版本只有一个时不做特别列出。
    /// </summary>
    public static void ForgeDownloadListItemPreload(StackPanel stack, List<ModDownload.DlForgeVersionEntry> entries,
        MyListItem.ClickEventHandler onClick, bool isSaveOnly)
    {
        if (entries.Count == 1)
            return;

        ModDownload.DlForgeVersionEntry? freshVersion = entries.Count > 0 ? entries[0] : null;
        ModDownload.DlForgeVersionEntry? recommendedVersion = null;
        foreach (var entry in entries)
        {
            if (entry.IsRecommended)
                recommendedVersion = entry;
        }

        // 若推荐版本与最新版本为同一版本，则仅显示推荐版本
        if (freshVersion is not null && ReferenceEquals(freshVersion, recommendedVersion))
            freshVersion = null;

        if (recommendedVersion is not null)
        {
            var recommended = ForgeDownloadListItem(recommendedVersion, onClick, isSaveOnly);
            recommended.Info = Lang.Text("Download.Version.Type.Recommended") +
                               (string.IsNullOrEmpty(recommended.Info) ? "" : "  |  " + recommended.Info);
            stack.Children.Add(recommended);
        }

        if (freshVersion is not null)
        {
            var fresh = ForgeDownloadListItem(freshVersion, onClick, isSaveOnly);
            fresh.Info = Lang.Text("Download.Version.Latest.Title") +
                         (string.IsNullOrEmpty(fresh.Info) ? "" : "  |  " + fresh.Info);
            stack.Children.Add(fresh);
        }

        stack.Children.Add(CreateAllVersionsText(entries.Count));
    }

    /// <summary>
    /// 单个 Forge 构建的列表项。普通模式下菜单为“保存”与“更新日志”，保存模式下只有“更新日志”。
    /// </summary>
    public static MyListItem ForgeDownloadListItem(ModDownload.DlForgeVersionEntry entry, MyListItem.ClickEventHandler onClick, bool isSaveOnly)
    {
        var infoParts = new List<string>();
        if (!string.IsNullOrEmpty(entry.ReleaseTime))
            infoParts.Add(Lang.Text("Download.Version.ReleaseDate", entry.ReleaseTime));

        var core = ToCoreForge(entry);
        var item = CreateItem(entry.VersionName, string.Join("  |  ", infoParts), DownloadListItems.BlockLogo("Anvil"), entry, onClick);
        item.ContentHandler = (sender, _) =>
        {
            var changelog = ChangelogButton(() => SystemShell.OpenUrl(ForgeIndex.GetChangelogUrl(core).AbsoluteUri));
            sender.Buttons = isSaveOnly
                ? new[] { changelog }
                : new[]
                {
                    SaveButton(() => SaveInstaller(item, "Forge", $"Forge {core.MinecraftVersion} - {core.Version}",
                        $"Forge-{core.MinecraftVersion}-{core.Version}.jar", ForgeIndex.GetInstallerUrls(core, App.Services.Source))),
                    changelog
                };
        };
        return item;
    }

    #endregion

    #region NeoForge

    /// <summary>
    /// NeoForge 版本列表预先列出：最新稳定版与最新测试版放在最前，最后是版本总数。
    /// </summary>
    public static void NeoForgeDownloadListItemPreload(StackPanel stack, List<ModDownload.DlNeoForgeListEntry> entries,
        MyListItem.ClickEventHandler onClick, bool isSaveOnly)
    {
        if (entries.Count == 1)
            return;

        ModDownload.DlNeoForgeListEntry? freshStableVersion = null;
        ModDownload.DlNeoForgeListEntry? freshBetaVersion = null;
        foreach (var entry in entries)
        {
            if (entry.IsBeta)
            {
                freshBetaVersion ??= entry;
            }
            else
            {
                freshStableVersion = entry;
                break;
            }
        }

        if (freshStableVersion is not null)
        {
            var fresh = NeoForgeDownloadListItem(freshStableVersion, onClick, isSaveOnly);
            fresh.Info = string.IsNullOrEmpty(fresh.Info)
                ? Lang.Text("Download.Version.Fresh.Stable")
                : Lang.Text("Download.Version.Fresh.Latest") + "  |  " + fresh.Info;
            stack.Children.Add(fresh);
        }

        if (freshBetaVersion is not null)
        {
            var fresh = NeoForgeDownloadListItem(freshBetaVersion, onClick, isSaveOnly);
            fresh.Info = string.IsNullOrEmpty(fresh.Info)
                ? Lang.Text("Download.Version.Fresh.Development")
                : Lang.Text("Download.Version.Fresh.Latest") + "  |  " + fresh.Info;
            stack.Children.Add(fresh);
        }

        stack.Children.Add(CreateAllVersionsText(entries.Count));
    }

    /// <summary>
    /// 单个 NeoForge 版本的列表项。
    /// </summary>
    public static MyListItem NeoForgeDownloadListItem(ModDownload.DlNeoForgeListEntry info, MyListItem.ClickEventHandler onClick, bool isSaveOnly)
    {
        var core = ToCoreNeoForge(info);
        var item = CreateItem(info.VersionName,
            info.IsBeta ? Lang.Text("Download.Version.Type.Preview") : Lang.Text("Download.Version.Type.Stable"),
            DownloadListItems.BlockLogo("NeoForge"), info, onClick);
        item.ContentHandler = (sender, _) =>
        {
            var changelog = ChangelogButton(() => SystemShell.OpenUrl(ForgeIndex.GetChangelogUrl(core).AbsoluteUri));
            sender.Buttons = isSaveOnly
                ? new[] { changelog }
                : new[]
                {
                    SaveButton(() => SaveInstaller(item, "NeoForge", $"NeoForge {core.MinecraftVersion} - {core.Version}",
                        $"NeoForge-{core.Version}.jar", ForgeIndex.GetInstallerUrls(core, App.Services.Source))),
                    changelog
                };
        };
        return item;
    }

    #endregion

    #region Cleanroom

    /// <summary>
    /// Cleanroom 版本列表预先列出：最新版本放在最前，最后是版本总数。
    /// </summary>
    public static void CleanroomDownloadListItemPreload(StackPanel stack, List<ModDownload.DlCleanroomListEntry> entries,
        MyListItem.ClickEventHandler onClick, bool isSaveOnly)
    {
        if (entries.Count == 0)
            return;

        var freshVersion = entries[0];
        var fresh = CleanroomDownloadListItem(freshVersion, onClick, isSaveOnly);
        fresh.Info = string.IsNullOrEmpty(fresh.Info)
            ? Lang.Text("Download.Version.Fresh.Development")
            : Lang.Text("Download.Version.Fresh.Latest") + "  |  " + fresh.Info;
        stack.Children.Add(fresh);

        stack.Children.Add(CreateAllVersionsText(entries.Count));
    }

    /// <summary>
    /// 单个 Cleanroom 版本的列表项。
    /// </summary>
    public static MyListItem CleanroomDownloadListItem(ModDownload.DlCleanroomListEntry info, MyListItem.ClickEventHandler onClick, bool isSaveOnly)
    {
        var version = new CleanroomVersion(info.ApiName);
        var item = CreateItem(info.VersionName,
            info.IsBeta ? Lang.Text("Download.Version.Type.Preview") : Lang.Text("Download.Version.Type.Stable"),
            DownloadListItems.BlockLogo("Cleanroom"), info, onClick);
        item.ContentHandler = (sender, _) =>
        {
            var changelog = ChangelogButton(() => SystemShell.OpenUrl(version.ChangelogUrl.AbsoluteUri));
            sender.Buttons = isSaveOnly
                ? new[] { changelog }
                : new[]
                {
                    SaveButton(() => SaveInstaller(item, "Cleanroom", $"Cleanroom {info.ApiName}",
                        $"cleanroom-{info.ApiName}-installer.jar", new[] { version.InstallerUrl })),
                    changelog
                };
        };
        return item;
    }

    #endregion

    #region Fabric 与 LegacyFabric

    /// <summary>
    /// Fabric 加载器版本列表项。
    /// </summary>
    public static MyListItem FabricDownloadListItem(JsonObject entry, MyListItem.ClickEventHandler onClick)
    {
        var version = entry["version"]?.ToString() ?? "";
        var item = CreateItem(version.Replace("+build", ""),
            IsStable(entry) ? Lang.Text("Download.Version.Type.Stable") : Lang.Text("Download.Version.Type.Preview"),
            DownloadListItems.BlockLogo("Fabric"), entry, onClick);
        item.ContentHandler = (sender, _) =>
        {
            sender.Buttons = new[]
            {
                ChangelogButton(() => SystemShell.OpenUrl("https://fabricmc.net/blog"))
            };
        };
        return item;
    }

    /// <summary>
    /// LegacyFabric 加载器版本列表项。
    /// </summary>
    public static MyListItem LegacyFabricDownloadListItem(JsonObject entry, MyListItem.ClickEventHandler onClick)
    {
        var version = entry["version"]?.ToString() ?? "";
        return CreateItem(version,
            IsStable(entry) ? Lang.Text("Download.Version.Type.Stable") : Lang.Text("Download.Version.Type.Preview"),
            DownloadListItems.BlockLogo("Fabric"), entry, onClick);
    }

    /// <summary>
    /// Fabric API 文件列表项。标题取自文件显示名中 <c>]</c> 之后的部分。
    /// </summary>
    public static MyListItem FabricApiDownloadListItem(CompFile entry, MyListItem.ClickEventHandler onClick)
    {
        var parts = entry.DisplayName.Split(']');
        var title = (parts.Length > 1 ? parts[1] : entry.DisplayName)
            .Replace("Fabric API ", "")
            .Replace(" build ", ".")
            .Trim();
        return CreateItem(title, CompFileInfo(entry), DownloadListItems.BlockLogo("Fabric"), entry, onClick);
    }

    /// <summary>
    /// OptiFabric 文件列表项。
    /// </summary>
    public static MyListItem OptiFabricDownloadListItem(CompFile entry, MyListItem.ClickEventHandler onClick)
    {
        var title = entry.DisplayName.ToLowerInvariant()
            .Replace("optifabric-", "")
            .Replace(".jar", "")
            .Trim()
            .TrimStart('v');
        return CreateItem(title, CompFileInfo(entry), DownloadListItems.BlockLogo("OptiFabric"), entry, onClick);
    }

    /// <summary>
    /// Legacy Fabric API 文件列表项。
    /// </summary>
    public static MyListItem LegacyFabricApiDownloadListItem(CompFile entry, MyListItem.ClickEventHandler onClick)
    {
        var title = entry.DisplayName.Replace("Legacy Fabric API ", "");
        return CreateItem(title, CompFileInfo(entry), DownloadListItems.BlockLogo("Fabric"), entry, onClick);
    }

    #endregion

    #region LabyMod

    /// <summary>
    /// LabyMod 版本列表项。条目来自 LabyMod 清单，含 <c>version</c> 与 <c>channel</c>（<c>production</c> 或 <c>snapshot</c>）。
    /// </summary>
    public static MyListItem LabyModDownloadListItem(JsonObject entry, MyListItem.ClickEventHandler onClick)
    {
        var channel = entry["channel"]?.ToString() ?? "production";
        var version = entry["version"]?.ToString() ?? "";
        var label = channel.Contains("snapshot", StringComparison.Ordinal)
            ? Lang.Text("Download.Version.Type.Snapshot")
            : Lang.Text("Download.Version.Type.Stable");

        var item = CreateItem(version + " " + label, label, DownloadListItems.BlockLogo("LabyMod"), entry, onClick);
        item.ContentHandler = (sender, _) =>
        {
            sender.Buttons = new[]
            {
                SaveButton(() => SaveInstaller(item, "LabyMod", version + " " + label, $"LabyMod-{channel}-{version}.jar",
                    UrlsOf(ExtraLoaderCatalog.GetLabyModInstallerUrl(channel))))
            };
        };
        return item;
    }

    #endregion

    #region 安装

    /// <summary>
    /// 安装请求，对应 WPF 的 ModDownloadLib.McInstallRequest。
    /// 各个加载器字段为 null 或空时表示不安装该加载器。
    /// </summary>
    public class McInstallRequest
    {
        // 若要下载 Cleanroom，则需要在下面两项中完成至少一项
        public ModDownload.DlCleanroomListEntry? cleanroomEntry = null;
        public string? cleanroomVersion;

        public CompFile? fabricApi = null;
        public string? fabricVersion = null;

        // 若要下载 Forge，则需要在下面两项中完成至少一项
        public ModDownload.DlForgeVersionEntry? forgeEntry = null;
        public string? forgeVersion;

        public string? labyModChannel = null;
        public string? labyModCommitRef = null;

        public CompFile? legacyFabricApi = null;
        public string? legacyFabricVersion = null;

        public ModDownload.DlLiteLoaderListEntry? liteLoaderEntry = null;

        public string? minecraftJson = null;
        public string? minecraftName = null;

        // 若要下载 NeoForge，则需要在下面两项中完成至少一项
        public ModDownload.DlNeoForgeListEntry? neoForgeEntry = null;
        public string? neoForgeVersion;

        public CompFile? optiFabric = null;

        // 若要下载 OptiFine，则需要在下面两项中完成至少一项
        public ModDownload.DlOptiFineListEntry? optiFineEntry;
        public string? optiFineVersion;

        public string? targetInstanceFolder;
        public string? targetInstanceName;
    }

    /// <summary>
    /// 加载器状态变化时的提示，只在失败时弹出错误提示。
    /// </summary>
    public static void LoaderStateChangedHintOnly(object loaderObj)
    {
        if (loaderObj is not ModLoader.LoaderBase loader || loader.State != LoadState.Failed)
            return;

        var message = loader.Error?.Message ?? Lang.Text("Download.Install.Error.OperationFailed");
        Dispatcher.UIThread.Post(() => Hint.Show(message, HintType.Error));
    }

    /// <summary>
    /// 按请求安装实例，对应 WPF 的 McInstall。安装在后台进行，本方法在校验通过并开始安装后返回 true。
    /// </summary>
    /// <param name="request">安装请求。</param>
    /// <param name="type">操作类型的显示名称，如“安装”或“重置”。</param>
    public static bool McInstall(McInstallRequest request, string type = mcInstallDefaultType)
    {
        try
        {
            if (string.IsNullOrEmpty(request.targetInstanceName) ||
                string.IsNullOrEmpty(request.targetInstanceFolder) ||
                string.IsNullOrEmpty(request.minecraftName) ||
                string.IsNullOrEmpty(request.minecraftJson))
            {
                throw new ArgumentException("安装请求缺少实例名、实例目录、Minecraft 版本或版本清单地址。");
            }

            // TODO(shion): OptiFine、LiteLoader、Cleanroom、LabyMod 与 LegacyFabric 的安装器还没有对应的 PCL.Shion.Core 实现，暂时拒绝这些组合
            if (request.optiFineEntry is not null || !string.IsNullOrEmpty(request.optiFineVersion) ||
                request.liteLoaderEntry is not null ||
                request.cleanroomEntry is not null || !string.IsNullOrEmpty(request.cleanroomVersion) ||
                !string.IsNullOrEmpty(request.labyModChannel) ||
                request.legacyFabricApi is not null || !string.IsNullOrEmpty(request.legacyFabricVersion))
            {
                Hint.Show(UnsupportedMessage, HintType.Error);
                return false;
            }

            var loader = ResolveLoader(request);
            var mods = new List<CompFile>();
            if (request.fabricApi is not null)
                mods.Add(request.fabricApi);
            if (request.optiFabric is not null)
                mods.Add(request.optiFabric);

            var minecraft = new RemoteVersion(request.minecraftName, "release", DateTimeOffset.UtcNow,
                new Uri(request.minecraftJson), null);

            // TODO(shion): Forge 与 NeoForge 的处理器需要 Java，目前直接使用设置中的默认 Java；后续接入 Java 选择
            var javaExecutable = App.Services.Settings.DefaultJavaExecutable ?? "java";

            // TODO(shion): 任务管理器中的安装进度与 ModLoader.LoaderTaskbarAdd 尚未接入，这里只在后台执行
            _ = InstallInBackgroundAsync(request.targetInstanceName, request.targetInstanceFolder, minecraft, loader, mods, javaExecutable);
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "开始安装失败");
            Hint.Show(Lang.Text("Download.Install.Error.OperationFailed"), HintType.Error);
            return false;
        }
    }

    /// <summary>
    /// 从请求中选出要安装的加载器。Forge、NeoForge、Fabric 不能同时安装，同时指定时抛出异常。
    /// </summary>
    private static LoaderVersion? ResolveLoader(McInstallRequest request)
    {
        var candidates = new List<LoaderVersion>();
        if (request.forgeEntry is not null)
            candidates.Add(ToCoreForge(request.forgeEntry));
        else if (!string.IsNullOrEmpty(request.forgeVersion))
            candidates.Add(new LoaderVersion(LoaderKind.Forge, request.forgeVersion, request.minecraftName!, true));

        if (request.neoForgeEntry is not null)
            candidates.Add(ToCoreNeoForge(request.neoForgeEntry));
        else if (!string.IsNullOrEmpty(request.neoForgeVersion))
            candidates.Add(new LoaderVersion(LoaderKind.NeoForge, request.neoForgeVersion, request.minecraftName!, !request.neoForgeVersion.Contains('-')));

        if (!string.IsNullOrEmpty(request.fabricVersion))
            candidates.Add(new LoaderVersion(LoaderKind.Fabric, request.fabricVersion, request.minecraftName!, true));

        if (candidates.Count > 1)
            throw new NotSupportedException("同一个实例不能同时安装多个模组加载器。");
        return candidates.Count == 0 ? null : candidates[0];
    }

    /// <summary>
    /// 安装原版（若有加载器则安装加载器并自动补全原版），再把 Fabric API 等模组下载到实例的 mods 文件夹。
    /// 失败时提示并清理未完成的实例文件夹。
    /// </summary>
    private static async Task InstallInBackgroundAsync(string instanceName, string instanceFolder, RemoteVersion minecraft,
        LoaderVersion? loader, IReadOnlyList<CompFile> mods, string javaExecutable)
    {
        var services = App.Services;
        var root = services.Settings.MinecraftRoot;
        try
        {
            if (loader is null)
            {
                await services.CreateInstaller()
                    .InstallVanillaAsync(root, minecraft, instanceName, null, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            else
            {
                await new LoaderInstaller(services.Engine, services.Source)
                    .InstallAsync(root, loader, instanceName, javaExecutable, null, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            if (mods.Count > 0)
            {
                var modsFolder = Path.Combine(instanceFolder, "mods");
                Directory.CreateDirectory(modsFolder);
                var items = new List<DownloadItem>();
                foreach (var mod in mods)
                {
                    if (!mod.Available)
                        throw new InvalidOperationException($"{mod.DisplayName} 没有可用的下载地址。");
                    items.Add(mod.ToDownloadItem(Path.Combine(modsFolder, CompNaming.SanitizeFileName(mod.FileName))));
                }

                var result = await services.Engine.DownloadAsync(items, null, CancellationToken.None).ConfigureAwait(false);
                if (!result.Succeeded)
                    throw new InvalidOperationException("模组文件下载失败：" + string.Join(", ", result.Failed.Select(item => item.TargetPath)));
            }

            services.NotifyInstancesChanged();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "安装实例失败");
            Dispatcher.UIThread.Post(() => Hint.Show(Lang.Text("Download.Install.Error.OperationFailed"), HintType.Error));
            CleanupFailedFolder(instanceFolder);
        }
    }

    /// <summary>
    /// 删除安装失败后残留的实例文件夹：只在里面没有子文件夹且至多一个文件时删除，与 WPF 的行为一致。
    /// </summary>
    private static void CleanupFailedFolder(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
                return;
            if (Directory.GetFiles(folder).Length <= 1 && Directory.GetDirectories(folder).Length == 0)
                Directory.Delete(folder, true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "清理未完成的实例文件夹失败");
        }
    }

    #endregion
}
