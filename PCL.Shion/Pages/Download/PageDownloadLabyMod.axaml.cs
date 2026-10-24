using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using PCL.Shion.Controls;
using PCL.Shion.Core.Loaders.Extra;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download;

public partial class PageDownloadLabyMod : MyPageRight
{
    /// <summary>LabyMod 正式版与快照版的加载器，供下载页的其他部分共享。</summary>
    public static readonly ModLoader.LoaderTask<int, IReadOnlyList<LabyModChannel>> Loader =
        new("DlLabyModList Main", LoadChannels);

    public PageDownloadLabyMod()
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

    private static void LoadChannels(ModLoader.LoaderTask<int, IReadOnlyList<LabyModChannel>> task)
    {
        task.output = ExtraLoaderCatalog.GetLabyModChannelsAsync(App.Services.Engine.Http, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private void Load_OnFinish()
    {
        // 结果数据化
        try
        {
            var channels = Loader.output ?? Array.Empty<LabyModChannel>();
            PanVersions.Children.Clear();
            foreach (var channel in channels)
                PanVersions.Children.Add(CreateListItem(channel));
            CardVersions.Title = Lang.Text("Download.Version.VersionListCount", channels.Count);
        }
        catch (Exception)
        {
            Hint.Show(Lang.Text("Download.Error.OperationFailed"), HintType.Error);
        }
    }

    private MyListItem CreateListItem(LabyModChannel channel)
    {
        var isSnapshot = channel.Channel == "snapshot";
        var channelName = isSnapshot ? Lang.Text("Download.Version.Type.Snapshot") : Lang.Text("Download.Version.Type.Stable");
        var item = new MyListItem
        {
            Title = channel.LabyModVersion + " " + channelName,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = channel,
            Info = channelName,
            Logo = PageDownloadSaver.ImagePath("LabyMod.png")
        };
        item.Click += (_, _) => SaveInstaller(channel);
        item.ContentHandler = (sender, _) =>
        {
            var btnSave = new MyIconButton { SvgIcon = "lucide/save" };
            ToolTip.SetTip(btnSave, Lang.Text("Download.Version.SaveAs"));
            btnSave.Click += (_, _) => SaveInstaller(channel);
            sender.Buttons = [btnSave, PageDownloadSaver.CreateInfoButton("https://www.labymod.net/zh_Hans/download")];
        };
        return item;
    }

    /// <summary>
    /// 保存 LabyMod 安装器。
    /// </summary>
    private void SaveInstaller(LabyModChannel channel)
    {
        var isSnapshot = channel.Channel == "snapshot";
        var url = ExtraLoaderCatalog.GetLabyModInstallerUrl(channel.Channel);
        var fileName = isSnapshot ? "LabyMod4SnapshotInstaller.jar" : "LabyMod4ProductionInstaller.jar";
        _ = PageDownloadSaver.SaveInstallerAsync(this, url, fileName, url);
    }

    private void BtnWeb_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl("https://labymod.net");
    }
}
