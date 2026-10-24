using Avalonia;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Media;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Core.Versions;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download;

/// <summary>
/// 原版版本下载列表：按发布、快照、旧版本、愚人节分组显示，可以把客户端或服务端保存到本地。
/// </summary>
public partial class PageDownloadClient : MyPageRight
{
    // 版本清单的加载器，对应 WPF 的 ModDownload.dlClientListLoader
    private static readonly ModLoader.LoaderTask<string, VersionManifest> ClientListLoader =
        new("DlClientList Main", LoadClientList);

    private static readonly VanillaVersionCategory[] CategoryOrder =
    [
        VanillaVersionCategory.Release,
        VanillaVersionCategory.Snapshot,
        VanillaVersionCategory.BeforeRelease,
        VanillaVersionCategory.AprilFools
    ];

    public PageDownloadClient()
    {
        InitializeComponent();
        PanScroll = PanBack;
        LoaderInit();
        Loaded += (_, _) => Init();
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanBack, null, ClientListLoader, _ => Load_OnFinish());
    }

    private void Init()
    {
        PanBack.ScrollToHome();
    }

    private static void LoadClientList(ModLoader.LoaderTask<string, VersionManifest> loader)
    {
        var services = App.Services;
        loader.output = VersionManifestClient
            .FetchAsync(services.Engine.Http, services.Source, loader.AbortedToken)
            .GetAwaiter()
            .GetResult();
    }

    private void Load_OnFinish()
    {
        // 结果数据化
        try
        {
            var manifest = ClientListLoader.output!;
            var dict = CategoryOrder.ToDictionary(
                category => category,
                _ => new List<ClassifiedVanillaVersion>()
            );
            foreach (var version in manifest.Versions)
            {
                var classified = VanillaVersionText.Classify(version);
                dict[classified.Category].Add(classified);
            }

            foreach (var category in CategoryOrder)
                dict[category] = dict[category]
                    .OrderByDescending(entry => entry.Version.ReleaseTime)
                    .ToList();

            PanMain.Children.Clear();

            var cardInfo = new MyCard { Title = Lang.Text("Download.Version.Latest.Title"), Margin = new Thickness(0d, 0d, 0d, 15d) };
            var topestVersions = new List<ClassifiedVanillaVersion>();
            var release = dict[VanillaVersionCategory.Release][0];
            topestVersions.Add(release with
            {
                Lore = Lang.Text("Download.Version.Latest.Release", Lang.Date(release.Version.ReleaseTime.ToLocalTime(), "g"))
            });
            var snapshot = dict[VanillaVersionCategory.Snapshot][0];
            if (release.Version.ReleaseTime < snapshot.Version.ReleaseTime)
            {
                topestVersions.Add(snapshot with
                {
                    Lore = Lang.Text("Download.Version.Latest.Development", Lang.Date(snapshot.Version.ReleaseTime.ToLocalTime(), "g"))
                });
            }

            var panInfo = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = new TranslateTransform(0d, 0d),
                Tag = topestVersions
            };

            void PutMethod(StackPanel stack)
            {
                foreach (var item in (IEnumerable)stack.Tag!)
                    stack.Children.Add(CreateItem((ClassifiedVanillaVersion)item));
            }

            MyCard.StackInstall(ref panInfo, PutMethod);
            cardInfo.Children.Add(panInfo);
            PanMain.Children.Add(cardInfo);

            foreach (var pair in dict)
            {
                if (!pair.Value.Any())
                    continue;

                var newCard = new MyCard
                {
                    Title = VanillaVersionText.GetCategoryDisplayName(pair.Key) + " (" + pair.Value.Count + ")",
                    Margin = new Thickness(0d, 0d, 0d, 15d)
                };
                var newStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top,
                    RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = pair.Value
                };
                newCard.Children.Add(newStack);
                newCard.SwapControl = newStack;
                newCard.InstallMethod = PutMethod;
                newCard.IsSwapped = true;
                PanMain.Children.Add(newCard);
            }
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "可视化 MC 版本列表出错：{Exception}", ex);
            Hint.Show(Lang.Text("Download.Error.OperationFailed"), HintType.Error);
        }
    }

    // 版本列表项，对应 WPF 的 ModDownloadLib.McDownloadListItem（保存菜单）
    private MyListItem CreateItem(ClassifiedVanillaVersion entry)
    {
        var version = entry.Version;
        var logo = version.Type switch
        {
            "release" => DownloadListItems.BlockLogo("Grass"),
            "snapshot" or "pending" => DownloadListItems.BlockLogo("CommandBlock"),
            "special" => DownloadListItems.BlockLogo("GoldBlock"),
            _ => DownloadListItems.BlockLogo("CobbleStone")
        };

        var formattedVersion = VanillaVersionText.FormatVersion(version.Id).Replace("_", " ");
        var releaseDate = Lang.Date(version.ReleaseTime.ToLocalTime(), "g");
        string info;
        if (entry.Lore is null)
            info = formattedVersion != version.Id ? releaseDate + " | " + version.Id : releaseDate;
        else
            info = formattedVersion != version.Id ? entry.Lore + " | " + version.Id : entry.Lore;

        var item = DownloadListItems.Create(
            formattedVersion,
            info,
            logo,
            entry,
            () => _ = DownloadSaver.SaveClientAsync(TopLevel.GetTopLevel(this), version),
            () => [CreateServerButton(version), CreateInfoButton(version)]);

        if (version.JsonUrl.AbsoluteUri.Contains("unlisted-versions-of-minecraft", StringComparison.Ordinal))
            item.Tags = Lang.Text("Download.Source.Tag.Uvmc");
        return item;
    }

    // 服务端按钮，对应 WPF 的 btnServer
    private MyIconButton CreateServerButton(RemoteVersion version)
    {
        return DownloadListItems.IconButton(
            "lucide/server",
            Lang.Text("Download.Version.DownloadServer"),
            () => _ = DownloadSaver.SaveServerAsync(TopLevel.GetTopLevel(this), version),
            1d);
    }

    // 更新日志按钮，对应 WPF 的 btnInfo
    private MyIconButton CreateInfoButton(RemoteVersion version)
    {
        return DownloadListItems.IconButton(
            "lucide/info",
            Lang.Text("Download.Version.Changelog"),
            () => SystemShell.OpenUrl(VanillaVersionText.GetWikiUrl(version.Id)),
            1.05d);
    }
}
