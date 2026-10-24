using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PCL.Shion.Controls;
using PCL.Shion.Core.Loaders.Extra;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download;

public partial class PageDownloadFabric : MyPageRight
{
    /// <summary>Fabric 安装器版本列表的加载器，供下载页的其他部分共享。</summary>
    public static readonly ModLoader.LoaderTask<int, IReadOnlyList<ToolVersion>> Loader =
        new("DlFabricList Main", LoadVersions);

    public PageDownloadFabric()
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
        task.output = ExtraLoaderCatalog.GetFabricInstallersAsync(
                App.Services.Engine.Http, App.Services.Source, CancellationToken.None)
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
            Title = version.Version.Replace("+build", ""),
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = version,
            Info = version.Stable ? Lang.Text("Download.Version.Type.Stable") : Lang.Text("Download.Version.Type.Preview"),
            Logo = PageDownloadSaver.ImagePath("Fabric.png")
        };
        item.Click += (_, _) => _ = PageDownloadSaver.SaveInstallerAsync(
            this, version.Url, PageDownloadSaver.FileNameOf(version.Url), version.Url);
        item.ContentHandler = (sender, _) =>
        {
            sender.Buttons = [PageDownloadSaver.CreateInfoButton("https://fabricmc.net/blog")];
        };
        return item;
    }

    private void BtnWeb_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl("https://www.fabricmc.net");
    }
}

/// <summary>
/// 下载页共用的安装器保存流程：选择保存位置，再把安装器 jar 下载到该位置。
/// </summary>
internal static class PageDownloadSaver
{
    private static readonly HashSet<string> Ongoing = [];

    public static string ImagePath(string name) => $"avares://PCL.Shion/Assets/Images/Blocks/{name}";

    /// <summary>
    /// 只带一个“更多信息”按钮的菜单按钮，点击后打开更新日志。
    /// </summary>
    public static MyIconButton CreateInfoButton(string url)
    {
        var button = new MyIconButton { LogoScale = 1.05d, SvgIcon = "lucide/info" };
        ToolTip.SetTip(button, Lang.Text("Download.Version.Changelog"));
        button.Click += (_, _) => SystemShell.OpenUrl(url);
        return button;
    }

    /// <summary>
    /// 选择保存位置并下载安装器。同一个任务还在进行时不重复开始。
    /// </summary>
    /// <param name="owner">用于弹出保存对话框的控件。</param>
    /// <param name="url">安装器的下载地址。</param>
    /// <param name="fileName">保存对话框中建议的文件名。</param>
    /// <param name="taskKey">任务标识，用于判断是否重复下载。</param>
    public static async Task SaveInstallerAsync(Visual owner, string url, string fileName, string taskKey)
    {
        if (!Begin(taskKey))
        {
            Hint.Show(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), HintType.Error);
            return;
        }

        try
        {
            var target = await SelectSaveFileAsync(owner, fileName);
            if (target is null)
                return;
            await ExtraInstallerDownloader.SaveAsync(App.Services.Engine, url, target, CancellationToken.None);
            Hint.Show(Path.GetFileName(target), HintType.Success);
        }
        catch (Exception)
        {
            Hint.Show(Lang.Text("Download.Error.OperationFailed"), HintType.Error);
        }
        finally
        {
            End(taskKey);
        }
    }

    /// <summary>
    /// 弹出保存对话框，返回用户选择的本地路径；取消时返回 null。
    /// </summary>
    public static async Task<string?> SelectSaveFileAsync(Visual owner, string fileName)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel is null)
            return null;
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Lang.Text("Download.Version.SelectSaveLocation"),
            SuggestedFileName = fileName,
            FileTypeChoices = [new FilePickerFileType("JAR") { Patterns = ["*.jar"] }]
        });
        return file?.TryGetLocalPath();
    }

    public static string FileNameOf(string url)
    {
        var path = new Uri(url).AbsolutePath;
        return Uri.UnescapeDataString(path.Substring(path.LastIndexOf('/') + 1));
    }

    private static bool Begin(string key)
    {
        lock (Ongoing)
        {
            return Ongoing.Add(key);
        }
    }

    private static void End(string key)
    {
        lock (Ongoing)
        {
            Ongoing.Remove(key);
        }
    }
}
