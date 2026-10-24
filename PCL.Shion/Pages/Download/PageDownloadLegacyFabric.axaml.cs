using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Input;
using PCL.Shion.Controls;
using PCL.Shion.Core.Loaders.Extra;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download;

public partial class PageDownloadLegacyFabric : MyPageRight
{
    /// <summary>LegacyFabric 安装器版本列表的加载器，供下载页的其他部分共享。</summary>
    public static readonly ModLoader.LoaderTask<int, IReadOnlyList<ToolVersion>> Loader =
        new("DlLegacyFabricList Main", LoadVersions);

    public PageDownloadLegacyFabric()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        BtnWeb.Click += BtnWeb_Click;
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, CardVersions, CardTip, Loader, _ => Load_OnFinish());
    }

    private void Init()
    {
        PanBack.ScrollToHome();
    }

    private static void LoadVersions(ModLoader.LoaderTask<int, IReadOnlyList<ToolVersion>> task)
    {
        task.output = ExtraLoaderCatalog.GetLegacyFabricInstallersAsync(
                App.Services.Engine.Http, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private void Load_OnFinish()
    {
        // 结果数据化
        try
        {
            var versions = Loader.output ?? Array.Empty<ToolVersion>();
            PanVersions.Children.Clear();
            foreach (var version in versions)
                PanVersions.Children.Add(CreateListItem(version));
            CardVersions.Title = Lang.Text("Download.Version.VersionListCount", versions.Count);
        }
        catch (Exception)
        {
            Hint.Show(Lang.Text("Download.Error.OperationFailed"), HintType.Error);
        }
    }

    private MyListItem CreateListItem(ToolVersion version)
    {
        var item = new MyListItem
        {
            Title = version.Version,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = version,
            Info = version.Stable ? Lang.Text("Download.Version.Type.Stable") : Lang.Text("Download.Version.Type.Preview"),
            Logo = PageDownloadSaver.ImagePath("Fabric.png")
        };
        item.Click += (_, _) => _ = PageDownloadSaver.SaveInstallerAsync(
            this, version.Url, PageDownloadSaver.FileNameOf(version.Url), version.Url);
        return item;
    }

    private void BtnWeb_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl("https://legacyfabric.net/");
    }
}
