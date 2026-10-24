using Avalonia;
using System;
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
/// Forge 版本下载列表：按原版版本分组，展开后显示每个 Forge 构建，可以把安装器保存到本地。
/// </summary>
public partial class PageDownloadForge : MyPageRight
{
    // 有 Forge 构建的原版版本列表，对应 WPF 的 ModDownload.dlForgeListLoader
    private static readonly ModLoader.LoaderTask<string, IReadOnlyList<string>> ListLoader =
        new("DlForgeList Main", LoadMinecraftVersions);

    // 推荐版本表，由版本列表加载器一并获取，获取失败时不标注推荐
    private static IReadOnlyDictionary<string, string> _promotions = new Dictionary<string, string>();

    public PageDownloadForge()
    {
        InitializeComponent();
        PanScroll = PanBack;
        BtnWeb.Click += (_, _) => SystemShell.OpenUrl("https://files.minecraftforge.net");
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

    private static void LoadMinecraftVersions(ModLoader.LoaderTask<string, IReadOnlyList<string>> loader)
    {
        var services = App.Services;
        loader.output = ForgeIndex
            .GetMinecraftVersionsAsync(services.Engine.Http, services.Source, loader.AbortedToken)
            .GetAwaiter()
            .GetResult();
        try
        {
            _promotions = ForgeIndex
                .GetPromotionsAsync(services.Engine.Http, services.Source, loader.AbortedToken)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception) when (!loader.AbortedToken.IsCancellationRequested)
        {
            // 推荐版本只是标注，获取失败时所有版本都不标注推荐
            _promotions = new Dictionary<string, string>();
        }
    }

    // 单个原版版本下的 Forge 构建列表
    private static void LoadForgeVersions(ModLoader.LoaderTask<string, List<LoaderVersion>> loader)
    {
        var services = App.Services;
        var versions = LoaderCatalog
            .GetVersionsAsync(LoaderKind.Forge, loader.input!, services.Engine.Http, services.Source, loader.AbortedToken)
            .GetAwaiter()
            .GetResult();
        loader.output = versions.ToList();
    }

    private void Load_OnFinish()
    {
        // 结果数据化
        try
        {
            // 清空当前
            PanMain.Children.Clear();
            // 转化为 UI
            foreach (var version in ListLoader.output!)
            {
                // 增加卡片
                var newCard = new MyCard
                {
                    Title = version.Replace("_p", " P", StringComparison.Ordinal),
                    Margin = new Thickness(0d, 0d, 0d, 15d)
                };
                var newStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top,
                    RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = version
                };
                newCard.Children.Add(newStack);
                newCard.SwapControl = newStack;
                newCard.InstallMethod = stack => InstallLoadingCard(stack, newCard);
                newCard.IsSwapped = true;
                PanMain.Children.Add(newCard);
            }
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "可视化 Forge 版本列表出错：{Exception}", ex);
            Hint.Show(Lang.Text("Download.Error.OperationFailed"), HintType.Error);
        }
    }

    // 展开卡片时先放一个加载环，加载完成后由 Forge_StateChanged 换成版本列表
    private void InstallLoadingCard(StackPanel stack, MyCard card)
    {
        var loadingPickaxe = new MyLoading { Text = Lang.Text("Download.Version.Forge.LoadingList"), Margin = new Thickness(5d) };
        var loader = new ModLoader.LoaderTask<string, List<LoaderVersion>>("DlForgeVersion Main", LoadForgeVersions);
        loadingPickaxe.State = loader;
        loader.Start(stack.Tag);
        loadingPickaxe.StateChanged += (a, b, c) => Forge_StateChanged((MyLoading)a, b, c, card);
        loadingPickaxe.Click += (a, _) => Forge_Click((MyLoading)a!);
        stack.Children.Add(loadingPickaxe);
    }

    // Forge 版本列表加载
    private static void Forge_Click(MyLoading sender)
    {
        if (sender.State.LoadingState == MyLoading.MyLoadingState.Error)
            ((ModLoader.LoaderTask<string, List<LoaderVersion>>)sender.State).Start(isForceRestart: true);
    }

    private void Forge_StateChanged(MyLoading sender, MyLoading.MyLoadingState newState,
        MyLoading.MyLoadingState oldState, MyCard card)
    {
        if (newState != MyLoading.MyLoadingState.Stop)
            return;

        var loader = (ModLoader.LoaderTask<string, List<LoaderVersion>>)sender.State;
        // 载入列表
        var swap = (StackPanel)card.SwapControl!;
        swap.Children.Clear();
        swap.Tag = loader.output;
        card.InstallMethod = stack =>
        {
            var entries = (List<LoaderVersion>)stack.Tag!;
            PreloadForgeItems(stack, entries);
            foreach (var entry in entries)
                stack.Children.Add(CreateForgeItem(entry));
        };
        card.StackInstall();
    }

    // 推荐版本与最新版本的预先列出，对应 WPF 的 ForgeDownloadListItemPreload
    private void PreloadForgeItems(StackPanel stack, IReadOnlyList<LoaderVersion> entries)
    {
        // 如果只有一个版本，则不特别列出
        if (entries.Count == 1)
            return;

        // 获取推荐版本与最新版本
        LoaderVersion? freshVersion = entries.Count > 0 ? entries[0] : null;
        LoaderVersion? recommendedVersion = null;
        foreach (var entry in entries)
        {
            if (ForgeIndex.IsRecommended(entry, _promotions))
                recommendedVersion = entry;
        }

        // 若推荐版本与最新版本为同一版本，则仅显示推荐版本
        if (freshVersion is not null && ReferenceEquals(freshVersion, recommendedVersion))
            freshVersion = null;

        // 显示各个版本
        if (recommendedVersion is not null)
        {
            var recommended = CreateForgeItem(recommendedVersion);
            recommended.Info = Lang.Text("Download.Version.Type.Recommended") + (string.IsNullOrEmpty(recommended.Info) ? "" : "  |  " + recommended.Info);
            stack.Children.Add(recommended);
        }

        if (freshVersion is not null)
        {
            var fresh = CreateForgeItem(freshVersion);
            fresh.Info = Lang.Text("Download.Version.Latest.Title") + (string.IsNullOrEmpty(fresh.Info) ? "" : "  |  " + fresh.Info);
            stack.Children.Add(fresh);
        }

        // 添加间隔
        stack.Children.Add(new TextBlock
        {
            Text = Lang.Text("Download.Version.AllVersions", entries.Count),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(6d, 13d, 0d, 4d)
        });
    }

    // 单个 Forge 构建的列表项，对应 WPF 的 ModDownloadLib.ForgeDownloadListItem（保存菜单）
    private MyListItem CreateForgeItem(LoaderVersion entry)
    {
        return DownloadListItems.Create(
            entry.Version,
            "",
            DownloadListItems.BlockLogo("Anvil"),
            entry,
            () => SaveForge(entry),
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

    // 保存 Forge 安装器，对应 WPF 的 ForgeSave_Click
    private void SaveForge(LoaderVersion entry)
    {
        var displayName = $"Forge {entry.MinecraftVersion} - {entry.Version}";
        var fileName = $"Forge-{entry.MinecraftVersion}-{entry.Version}.jar";
        var urls = ForgeIndex.GetInstallerUrls(entry, App.Services.Source);
        _ = DownloadSaver.SaveInstallerAsync(TopLevel.GetTopLevel(this), "Forge", displayName, fileName, urls);
    }
}
