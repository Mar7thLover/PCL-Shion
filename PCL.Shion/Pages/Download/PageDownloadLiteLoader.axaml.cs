using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Shion.Controls;
using PCL.Shion.Core.Loaders.Extra;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download;

public partial class PageDownloadLiteLoader : MyPageRight
{
    /// <summary>LiteLoader 版本列表的加载器，供下载页的其他部分共享。</summary>
    public static readonly ModLoader.LoaderTask<int, IReadOnlyList<LiteLoaderVersion>> Loader =
        new("DlLiteLoaderList Main", LoadVersions);

    public PageDownloadLiteLoader()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        BtnWeb.Click += BtnWeb_Click;
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, Loader, _ => Load_OnFinish());
    }

    private void Init()
    {
        PanBack.ScrollToHome();
    }

    private static void LoadVersions(ModLoader.LoaderTask<int, IReadOnlyList<LiteLoaderVersion>> task)
    {
        task.output = ExtraLoaderCatalog.GetLiteLoaderVersionsAsync(
                App.Services.Engine.Http, App.Services.Source, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private void Load_OnFinish()
    {
        // 结果数据化
        try
        {
            const string unknownKey = "Unknown";
            // 归类
            var dict = new Dictionary<string, List<LiteLoaderVersion>>();
            for (var versionCode = 30; versionCode >= 0; versionCode -= 1)
                dict.Add("1." + versionCode, new List<LiteLoaderVersion>());
            dict.Add(unknownKey, []);
            foreach (var version in Loader.output ?? Array.Empty<LiteLoaderVersion>())
            {
                var mainVersion = "1." + version.MinecraftVersion.Split(".")[1];
                if (dict.ContainsKey(mainVersion))
                    dict[mainVersion].Add(version);
                else
                    dict[unknownKey].Add(version);
            }

            // 清空当前
            PanMain.Children.Clear();
            // 转化为 UI
            foreach (var pair in dict)
            {
                if (pair.Value.Count == 0)
                    continue;
                // 增加卡片
                var newCard = new MyCard
                {
                    Title = (pair.Key == unknownKey
                        ? Lang.Text("Minecraft.Version.Unknown")
                        : pair.Key) + " (" + pair.Value.Count + ")",
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
                    var sorted = ((List<LiteLoaderVersion>)stack.Tag!)
                        .OrderByDescending(version => version.MinecraftVersion, Comparer<string>.Create(CompareName));
                    foreach (var version in sorted)
                        stack.Children.Add(CreateListItem(version));
                };
                PanMain.Children.Add(newCard);
            }
        }
        catch (Exception)
        {
            Hint.Show(Lang.Text("Download.Error.OperationFailed"), HintType.Error);
        }
    }

    /// <summary>
    /// 按数字段比较 Minecraft 版本号，如 <c>1.12.2</c> 排在 <c>1.9</c> 之后。
    /// </summary>
    private static int CompareName(string a, string b)
    {
        var partsA = Regex.Matches(a, @"\d+|\D+").Select(match => match.Value).ToArray();
        var partsB = Regex.Matches(b, @"\d+|\D+").Select(match => match.Value).ToArray();
        for (var i = 0; i < Math.Min(partsA.Length, partsB.Length); i++)
        {
            var result = char.IsDigit(partsA[i][0]) && char.IsDigit(partsB[i][0])
                ? decimal.Parse(partsA[i]).CompareTo(decimal.Parse(partsB[i]))
                : string.CompareOrdinal(partsA[i], partsB[i]);
            if (result != 0)
                return result;
        }

        return partsA.Length.CompareTo(partsB.Length);
    }

    private MyListItem CreateListItem(LiteLoaderVersion version)
    {
        var infoParts = new List<string>
        {
            version.IsPreview
                ? Lang.Text("Download.Version.Type.Preview")
                : Lang.Text("Download.Version.Type.Stable")
        };

        if (!string.IsNullOrEmpty(version.ReleaseTime))
            infoParts.Add(Lang.Text("Download.Version.ReleaseDate", version.ReleaseTime));

        // 建立控件
        var item = new MyListItem
        {
            Title = version.MinecraftVersion,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = version,
            Info = string.Join("  |  ", infoParts),
            Logo = PageDownloadSaver.ImagePath("Egg.png")
        };

        item.Click += (_, _) => SaveInstaller(version);
        // 建立菜单
        item.ContentHandler = (sender, _) =>
        {
            sender.Buttons = version.IsLegacy
                ? Array.Empty<MyIconButton>()
                : new[] { CreateViewAllButton(version) };
        };
        return item;
    }

    /// <summary>
    /// 保存 LiteLoader 安装器。远古版本没有已知的安装器地址时只给出提示。
    /// </summary>
    private void SaveInstaller(LiteLoaderVersion version)
    {
        string url;
        try
        {
            url = ExtraLoaderCatalog.GetLiteLoaderInstallerUrl(version);
        }
        catch (NotSupportedException)
        {
            Hint.Show(Lang.Text("Minecraft.Download.Error.UnknownMinecraftVersion", version.MinecraftVersion), HintType.Error);
            return;
        }

        _ = PageDownloadSaver.SaveInstallerAsync(this, url, version.FileName.Replace("-SNAPSHOT", ""), url);
    }

    private static MyIconButton CreateViewAllButton(LiteLoaderVersion version)
    {
        var button = new MyIconButton { SvgIcon = "lucide/list" };
        ToolTip.SetTip(button, Lang.Text("Download.Version.ViewAllVersions"));
        button.Click += (_, _) => SystemShell.OpenUrl("https://jenkins.liteloader.com/view/" + version.MinecraftVersion);
        return button;
    }

    private void BtnWeb_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl("https://www.liteloader.com");
    }
}
