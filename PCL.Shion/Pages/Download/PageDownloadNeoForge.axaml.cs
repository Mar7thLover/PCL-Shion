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
using PCL.Shion.Core.Loaders;
using PCL.Shion.Core.Loaders.Forge;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download;

/// <summary>
/// NeoForge 版本下载列表：按原版版本分组显示，可以把安装器保存到本地。
/// </summary>
public partial class PageDownloadNeoForge : MyPageRight
{
    // NeoForge 版本列表，对应 WPF 的 ModDownload.dlNeoForgeListLoader
    private static readonly ModLoader.LoaderTask<string, IReadOnlyList<LoaderVersion>> ListLoader =
        new("DlNeoForgeList Main", LoadNeoForgeVersions);

    public PageDownloadNeoForge()
    {
        InitializeComponent();
        PanScroll = PanBack;
        BtnWeb.Click += (_, _) => SystemShell.OpenUrl("https://neoforged.net/");
        LoaderInit();
        Loaded += (_, _) => Init();
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, ListLoader, _ => Load_OnFinish());
    }

    private void Init()
    {
        PanBack.ScrollToHome();
    }

    private static void LoadNeoForgeVersions(ModLoader.LoaderTask<string, IReadOnlyList<LoaderVersion>> loader)
    {
        loader.output = ForgeIndex
            .GetNeoForgeVersionsAsync(App.Services.Engine.Http, loader.AbortedToken)
            .GetAwaiter()
            .GetResult();
    }

    private void Load_OnFinish()
    {
        // 结果数据化
        try
        {
            // 归类
            var dict = ListLoader.output!.GroupBy(version => version.MinecraftVersion)
                .OrderByDescending(group => group.Key)
                .ToDictionary(group => group.Key, group => group.ToList());
            // 清空当前
            PanMain.Children.Clear();
            // 转化为 UI
            foreach (var pair in dict)
            {
                if (!pair.Value.Any())
                    continue;

                // 增加卡片
                var newCard = new MyCard
                {
                    Title = pair.Key + " (" + pair.Value.Count + ")",
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
                newCard.IsSwapped = true;
                newCard.InstallMethod = stack =>
                {
                    foreach (var item in (IEnumerable)stack.Tag!)
                        stack.Children.Add(CreateItem((LoaderVersion)item));
                };
                PanMain.Children.Add(newCard);
            }
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "可视化 NeoForge 版本列表出错：{Exception}", ex);
            Hint.Show(Lang.Text("Download.Error.OperationFailed"), HintType.Error);
        }
    }

    // 单个 NeoForge 版本的列表项，对应 WPF 的 ModDownloadLib.NeoForgeDownloadListItem（保存菜单）
    private MyListItem CreateItem(LoaderVersion entry)
    {
        return DownloadListItems.Create(
            entry.Version,
            entry.Stable ? Lang.Text("Download.Version.Type.Stable") : Lang.Text("Download.Version.Type.Preview"),
            DownloadListItems.BlockLogo("NeoForge"),
            entry,
            () => SaveNeoForge(entry),
            () => [CreateChangelogButton(entry)]);
    }

    private MyIconButton CreateChangelogButton(LoaderVersion entry)
    {
        return DownloadListItems.IconButton(
            "lucide/info",
            Lang.Text("Download.Version.Changelog"),
            () => SystemShell.OpenUrl(ForgeIndex.GetChangelogUrl(entry).AbsoluteUri),
            1.05d);
    }

    // 保存 NeoForge 安装器，对应 WPF 的 NeoForgeSave_Click
    private void SaveNeoForge(LoaderVersion entry)
    {
        var displayName = $"NeoForge {entry.MinecraftVersion} - {entry.Version}";
        var fileName = $"NeoForge-{entry.MinecraftVersion}-{entry.Version}.jar";
        var urls = ForgeIndex.GetInstallerUrls(entry, App.Services.Source);
        _ = DownloadSaver.SaveInstallerAsync(TopLevel.GetTopLevel(this), "NeoForge", displayName, fileName, urls);
    }
}
