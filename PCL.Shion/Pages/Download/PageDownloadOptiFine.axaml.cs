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

public partial class PageDownloadOptiFine : MyPageRight
{
    /// <summary>OptiFine 版本列表的加载器，供下载页的其他部分共享。</summary>
    public static readonly ModLoader.LoaderTask<int, IReadOnlyList<OptiFineVersion>> Loader =
        new("DlOptiFineList Main", LoadVersions);

    public PageDownloadOptiFine()
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

    private static void LoadVersions(ModLoader.LoaderTask<int, IReadOnlyList<OptiFineVersion>> task)
    {
        task.output = ExtraLoaderCatalog.GetOptiFineVersionsAsync(
                App.Services.Engine.Http, App.Services.Source, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private void Load_OnFinish()
    {
        // 结果数据化
        try
        {
            const string snapshotKey = "Snapshot";
            // 归类
            var dict = new Dictionary<string, List<OptiFineVersion>>();
            dict.Add(snapshotKey, new List<OptiFineVersion>());
            for (var versionCode = 50; versionCode >= 0; versionCode -= 1)
                dict.Add("1." + versionCode, new List<OptiFineVersion>());
            foreach (var version in Loader.output ?? Array.Empty<OptiFineVersion>())
                if (version.MinecraftVersion.StartsWith("1.", StringComparison.Ordinal))
                {
                    var mainVersion = "1." + version.DisplayName.Split(".")[1].Split(" ")[0];
                    if (dict.ContainsKey(mainVersion))
                        dict[mainVersion].Add(version);
                    else
                        dict[snapshotKey].Add(version);
                }
                else
                {
                    dict[snapshotKey].Add(version);
                }

            // 清空当前
            PanMain.Children.Clear();
            // 转化为 UI
            foreach (var pair in dict)
            {
                if (pair.Value.Count == 0)
                    continue;
                // 增加卡片
                var title = pair.Key == snapshotKey
                    ? Lang.Text("Download.Version.Optifine.Snapshot")
                    : pair.Key;
                var newCard = new MyCard
                {
                    Title = title + " (" + pair.Value.Count + ")",
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
                    var sorted = ((List<OptiFineVersion>)stack.Tag!)
                        .OrderByDescending(version => version.DisplayName, Comparer<string>.Create(CompareName));
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
    /// 按数字段比较版本名称，如 <c>1.12.2 C8</c> 排在 <c>1.9 C1</c> 之后。
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

    private MyListItem CreateListItem(OptiFineVersion version)
    {
        var infoParts = new List<string>
        {
            version.IsPreview
                ? Lang.Text("Download.Version.Type.Preview")
                : Lang.Text("Download.Version.Type.Release")
        };

        if (!string.IsNullOrEmpty(version.ReleaseTime))
            infoParts.Add(Lang.Text("Download.Version.ReleaseDate", version.ReleaseTime));

        if (version.RequiredForge is null)
            infoParts.Add(Lang.Text("Download.Version.Optifine.IncompatibleForge"));
        else if (!string.IsNullOrEmpty(version.RequiredForge))
            infoParts.Add(Lang.Text("Download.Version.Optifine.CompatibleForge", version.RequiredForge));

        var item = new MyListItem
        {
            Title = version.DisplayName,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = version,
            Info = string.Join("  |  ", infoParts),
            Logo = PageDownloadSaver.ImagePath("GrassPath.png")
        };
        item.Click += (_, _) => _ = PageDownloadSaver.SaveInstallerAsync(
            this, version.DownloadUrl, version.FileName, version.DownloadUrl);
        item.ContentHandler = (sender, _) =>
        {
            sender.Buttons = [PageDownloadSaver.CreateInfoButton("https://optifine.net/changelog?f=" + version.FileName)];
        };
        return item;
    }

    private void BtnWeb_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl("https://www.optifine.net/");
    }
}
