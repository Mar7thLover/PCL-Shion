using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Shion.Controls;
using PCL.Shion.Core.Online;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download.Comp;

/// <summary>
/// 资源列表的通用页面，供 Mod、整合包、资源包、光影包、数据包、世界等下载页复用。
/// </summary>
public partial class PageComp : MyScrollViewer
{
    /// <summary>每页展示的结果数量。</summary>
    public const int PageSize = 40;

    /// <summary>
    /// 在切换到页面时，应自动将筛选项设置为与该目标 MC 版本和加载器相同。
    /// </summary>
    public static CompTargetVersion? TargetVersion;

    /// <summary>当前页码，从 0 开始。</summary>
    public int page;

    /// <summary>当前已加载的结果。</summary>
    public CompProjectStorage storage = new();

    // 在点击 MyCompItem 时会获取 Loader 的输入，以使资源详情页面可以应用相同的筛选项
    public ModLoader.LoaderTask<CompProjectRequest, int> loader;

    private bool _isLoaderInited;
    private bool _supportCurseForge = true;
    private bool _supportModrinth = true;
    private CompType _pageType = (CompType)(-1);

    public PageComp()
    {
        loader = new ModLoader.LoaderTask<CompProjectRequest, int>(
            Lang.Text("Download.Comp.List.Source.ResourceFetch", "XXX"), CompProjectsGet, LoaderInput)
        {
            reloadTimeout = 60 * 1000
        };
        InitializeComponent();
        Loaded += PageCompControls_Inited;
        BtnPageFirst.Click += (_, _) => ChangePage(0);
        BtnPageLeft.Click += (_, _) => ChangePage(page - 1);
        BtnPageRight.Click += (_, _) => ChangePage(page + 1);
        PanSearchBox.Search += (_, _) => StartNewSearch();
        PanSearchBox.KeyDown += EnterTrigger;
        TextSearchVersion.KeyDown += EnterTrigger;
        BtnSearchReset.Click += (_, _) => ResetFilter();
        Load.StateChanged += (_, _, _) => Load_State();
    }

    /// <summary>
    /// 用于 XAML 快速设置的 Tag 下拉框列表。
    /// </summary>
    [Content]
    public ItemCollection SearchTags => ComboSearchTag.Items;

    /// <summary>是否显示 CurseForge 来源。</summary>
    public bool SupportCurseForge
    {
        get => _supportCurseForge;
        set
        {
            _supportCurseForge = value;
            ItemSourceCurseForge.IsVisible = value;
        }
    }

    /// <summary>是否显示 Modrinth 来源。</summary>
    public bool SupportModrinth
    {
        get => _supportModrinth;
        set
        {
            _supportModrinth = value;
            ItemSourceModrinth.IsVisible = value;
        }
    }

    /// <summary>
    /// 英文前后不含空格的可读资源类型名，例如 "Mod"、"整合包"。
    /// </summary>
    public string TypeName => CompText.TypeName(PageType);

    /// <summary>
    /// 该页面对应的资源类型。
    /// </summary>
    public CompType PageType
    {
        get => _pageType;
        set
        {
            if (_pageType == value)
                return;
            _pageType = value;
            BtnSearchInstallModPack.IsVisible = value == CompType.ModPack;
            loader.name = Lang.Text("Download.Comp.List.Source.ResourceFetch", TypeName);
            PanSearchBox.HintText = CompText.SearchName(value);
            Load.Text = CompText.LoadingName(value);
        }
    }

    private void PageCompControls_Inited(object? sender, EventArgs e)
    {
        if (TargetVersion is { } target)
        {
            ResetFilter();
            TextSearchVersion.Text = target.VanillaName;

            ComboSearchLoader.SelectedItem = ComboLoaderItemFor(target.Loaders);
            TargetVersion = null;
            if (_isLoaderInited)
                StartNewSearch();
            ScrollToVerticalOffset(0);
        }

        if (_isLoaderInited)
            return;
        _isLoaderInited = true;
        OwnerPage.PageLoaderInit(Load, PanLoad, PanContent, PanAlways, loader,
            _ => Load_OnFinish(), () => LoaderInput());

        var allDrops = CompDisplay.AllDrops;
        if (allDrops is { Count: > 0 } && allDrops[0] > 250)
        {
            var highestVersion = CompVersions.DropToVersion(allDrops[0]);
            if (((MyComboBoxItem)TextSearchVersion.Items[1]!).Content?.ToString() != highestVersion)
                TextSearchVersion.Items.Insert(1, new MyComboBoxItem { Content = highestVersion });
        }

        if (PageType == CompType.Shader)
        {
            LabLoader.IsVisible = true;
            ComboSearchLoader.IsVisible = false;
            ComboSearchShaderLoader.IsVisible = true;
        }
        else if (PageType is CompType.Mod or CompType.ModPack)
        {
            LabLoader.IsVisible = true;
            ComboSearchLoader.IsVisible = true;
            ComboSearchShaderLoader.IsVisible = false;
        }
        else
        {
            LabLoader.IsVisible = false;
            ComboSearchLoader.IsVisible = false;
            ComboSearchShaderLoader.IsVisible = false;
        }
    }

    private MyComboBoxItem ComboLoaderItemFor(IReadOnlyList<CompLoaderType> loaders)
    {
        var name = loaders.Contains(CompLoaderType.Forge) ? "Forge" :
            loaders.Contains(CompLoaderType.Fabric) ? "Fabric" :
            loaders.Contains(CompLoaderType.NeoForge) ? "NeoForge" :
            loaders.Contains(CompLoaderType.Quilt) ? "Quilt" : null;
        if (name is null)
            return (MyComboBoxItem)ComboSearchLoader.Items[0]!;
        foreach (var item in ComboSearchLoader.Items.OfType<MyComboBoxItem>())
            if (string.Equals(item.Content?.ToString(), name, StringComparison.OrdinalIgnoreCase))
                return item;
        return (MyComboBoxItem)ComboSearchLoader.Items[0]!;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && change.GetNewValue<bool>())
            RefreshAllFavoriteStatus();
    }

    private void Load_OnFinish()
    {
        try
        {
            var input = loader.input;
            PanProjects.Children.Clear();
            var index = Math.Min(page * PageSize, storage.Results.Count - 1);
            var count = Math.Min(storage.Results.Count - index, PageSize);
            if (index >= 0 && count > 0)
            {
                foreach (var result in storage.Results.GetRange(index, count))
                {
                    var showQuickDownload = result.Type != CompType.ModPack && result.Type != CompType.DataPack;
                    PanProjects.Children.Add(CompDisplay.CreateItem(result,
                        input?.GameVersion is null,
                        input?.ModLoader == CompLoaderType.Any && (PageType is CompType.Mod or CompType.ModPack),
                        showQuickDownload));
                }
            }

            CardPages.IsVisible =
                storage.Results.Count > 40 || storage.CurseForgeOffset < storage.CurseForgeTotal ||
                storage.ModrinthOffset < storage.ModrinthTotal;
            LabPage.Text = Lang.Number(page + 1, "N0");
            BtnPageFirst.IsEnabled = page > 1;
            BtnPageFirst.Opacity = page > 1 ? 1d : 0.2d;
            BtnPageLeft.IsEnabled = page > 0;
            BtnPageLeft.Opacity = page > 0 ? 1d : 0.2d;
            var isRightEnabled = storage.Results.Count > PageSize * (page + 1) ||
                                 storage.CurseForgeOffset < storage.CurseForgeTotal ||
                                 storage.ModrinthOffset < storage.ModrinthTotal;
            BtnPageRight.IsEnabled = isRightEnabled;
            BtnPageRight.Opacity = isRightEnabled ? 1d : 0.2d;

            if (storage.ErrorMessage is null)
            {
                HintError.IsVisible = false;
            }
            else
            {
                HintError.IsVisible = true;
                HintError.Text = storage.ErrorMessage;
            }

            ScrollToVerticalOffset(0);
        }
        catch (Exception ex)
        {
            Hint.Show(Lang.Text("Download.Comp.Error.OperationFailed") + ": " + ex.Message, HintType.Error);
        }
    }

    // 自动重试
    private void Load_State()
    {
        if (loader.State != LoadState.Failed)
            return;
        var errorMessage = loader.Error?.Message ?? "";
        if (errorMessage.Contains(Lang.Text("Common.Error.InvalidJson")))
            OwnerPage.PageLoaderRestart();
    }

    /// <summary>
    /// 承载本页的 MyPageRight（WPF 中的 LogicalParent）。
    /// </summary>
    private MyPageRight OwnerPage => this.FindAncestorOfType<MyPageRight>(true)!;

    private void ChangePage(int newPage)
    {
        CardPages.IsEnabled = false;
        page = newPage;
        MainWindow.Current?.BackToTop();
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            Dispatcher.UIThread.Post(() => CardPages.IsEnabled = true);
            loader.Start();
        });
    }

    /// <summary>
    /// 刷新所有已显示项目的收藏状态。
    /// </summary>
    public void RefreshAllFavoriteStatus()
    {
        foreach (var item in PanProjects.Children.OfType<MyCompItem>())
            item.RefreshFavoriteStatus();
    }

    private CompProjectRequest LoaderInput()
    {
        var request = new CompProjectRequest(PageType, storage, (page + 1) * PageSize);
        var versionText = TextSearchVersion.Text ?? "";
        var gameVersion = versionText == Lang.Text("Download.Comp.Filter.Version.AllInputAvailable") ? null :
            versionText.Contains('.') || versionText.Contains('w') ? versionText : null;

        var rawLoader = PageType is CompType.Mod or CompType.ModPack
            ? (CompLoaderType)(int)CompVersions.ParseLeadingNumber(CompText.TagOf(ComboSearchLoader.SelectedItem))
            : CompLoaderType.Any;

        request.SearchText = PanSearchBox.Text ?? "";
        request.GameVersion = gameVersion;
        var selectedTag = CompText.TagOf(ComboSearchTag.SelectedItem);
        var loaderTag = CompText.TagOf(ComboSearchShaderLoader.SelectedItem);
        request.Tag = PageType == CompType.Shader
            ? string.IsNullOrEmpty(loaderTag) ? selectedTag : selectedTag + loaderTag
            : selectedTag;
        request.ModLoader = rawLoader;
        request.Source = (CompSourceType)(int)CompVersions.ParseLeadingNumber(CompText.TagOf(ComboSearchSource.SelectedItem));
        request.Sort = (CompSortType)(int)CompVersions.ParseLeadingNumber(CompText.TagOf(ComboSearchSort.SelectedItem));
        return request;
    }

    // 搜索按钮
    private void StartNewSearch()
    {
        page = 0;
        object? argInput = LoaderInput();
        if (loader.ShouldStart(ref argInput))
            storage = new CompProjectStorage(); // 避免连续搜索两次使得 CompProjectStorage 引用丢失
        loader.Start();
    }

    private void EnterTrigger(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            StartNewSearch();
    }

    // 重置按钮
    private void ResetFilter()
    {
        PanSearchBox.Text = "";
        TextSearchVersion.Text = Lang.Text("Download.Comp.Filter.Version.AllInputAvailable");
        TextSearchVersion.SelectedIndex = 0;
        ComboSearchSource.SelectedIndex = 0;
        ComboSearchTag.SelectedIndex = 0;
        ComboSearchLoader.SelectedIndex = 0;
        ComboSearchShaderLoader.SelectedIndex = 0;
        ComboSearchSort.SelectedIndex = 0;
        loader.lastFinishedTime = 0L; // 要求强制重新开始
    }

    private void CompProjectsGet(ModLoader.LoaderTask<CompProjectRequest, int> task)
    {
        var request = task.input!;
        try
        {
            CompServices.CreateSearchEngine()
                .SearchAsync(request, new Progress<double>(p => task.Progress = p), task.AbortedToken)
                .GetAwaiter()
                .GetResult();
        }
        catch (CompException ex)
        {
            throw new Exception(Lang.Text(ex.LangKey, ex.Args));
        }
    }
}
