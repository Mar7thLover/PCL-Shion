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
using PCL.Shion.Core.Loaders.Cleanroom;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download;

/// <summary>
/// Cleanroom 版本下载列表（适用于 1.12.2），可以把安装器保存到本地。
/// </summary>
public partial class PageDownloadCleanroom : MyPageRight
{
    // Cleanroom 版本列表，对应 WPF 的 ModDownload.dlCleanroomListLoader
    private static readonly ModLoader.LoaderTask<string, IReadOnlyList<CleanroomVersion>> ListLoader =
        new("DlCleanroomList Main", LoadCleanroomVersions);

    public PageDownloadCleanroom()
    {
        InitializeComponent();
        PanScroll = PanBack;
        BtnWeb.Click += (_, _) => SystemShell.OpenUrl("https://cleanroommc.com/zh/");
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

    private static void LoadCleanroomVersions(ModLoader.LoaderTask<string, IReadOnlyList<CleanroomVersion>> loader)
    {
        loader.output = CleanroomCatalog
            .GetVersionsAsync(App.Services.Engine.Http, loader.AbortedToken)
            .GetAwaiter()
            .GetResult();
    }

    private void Load_OnFinish()
    {
        // 结果数据化
        try
        {
            // 归类，Cleanroom 只适用于 1.12.2，所有版本位于同一张卡片下
            var dict = ListLoader.output!.GroupBy(version => CleanroomVersion.MinecraftVersion)
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
                        stack.Children.Add(CreateItem((CleanroomVersion)item));
                };
                PanMain.Children.Add(newCard);
            }
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "可视化 Cleanroom 版本列表出错：{Exception}", ex);
            Hint.Show(Lang.Text("Download.Error.OperationFailed"), HintType.Error);
        }
    }

    // 单个 Cleanroom 版本的列表项，对应 WPF 的 ModDownloadLib.CleanroomDownloadListItem（保存菜单）
    private MyListItem CreateItem(CleanroomVersion entry)
    {
        return DownloadListItems.Create(
            entry.Tag,
            entry.IsBeta ? Lang.Text("Download.Version.Type.Preview") : Lang.Text("Download.Version.Type.Stable"),
            DownloadListItems.BlockLogo("Cleanroom"),
            entry,
            () => SaveCleanroom(entry),
            () => [CreateChangelogButton(entry)]);
    }

    private MyIconButton CreateChangelogButton(CleanroomVersion entry)
    {
        return DownloadListItems.IconButton(
            "lucide/info",
            Lang.Text("Download.Version.Changelog"),
            () => SystemShell.OpenUrl(entry.ChangelogUrl.AbsoluteUri),
            1.05d);
    }

    // 保存 Cleanroom 安装器，对应 WPF 的 CleanroomSave_Click
    private void SaveCleanroom(CleanroomVersion entry)
    {
        var displayName = $"Cleanroom {CleanroomVersion.MinecraftVersion} - {entry.Tag}";
        var fileName = $"Cleanroom-{CleanroomVersion.MinecraftVersion}-{entry.Tag}.jar";
        _ = DownloadSaver.SaveInstallerAsync(TopLevel.GetTopLevel(this), "Cleanroom", displayName, fileName, [entry.InstallerUrl]);
    }
}
