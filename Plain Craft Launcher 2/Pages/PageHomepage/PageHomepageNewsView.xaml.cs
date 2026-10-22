using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Model.Tools.News;
using PCL.Core.ViewModel.Homepage;

namespace PCL;

/// <summary>
///     Minecraft 新闻列表。既用作主页预设 15 的内容（嵌在主页的滚动区域里），也用作「工具」里的完整页面（单独显示）。
///     两种用法各自使用独立的实例。
/// </summary>
public partial class PageHomepageNewsView : MyPageRight, IRefreshable
{
    private readonly NewsViewModel _viewModel = new(false);

    public PageHomepageNewsView()
    {
        InitializeComponent();
        DataContext = _viewModel;
        PanScroll = PanBack;
        Loaded += (_, _) => OnLoaded();
        WeakLanguageChanged.Add(this, OnLanguageChanged);
    }

    private void OnLoaded()
    {
        // 单独作为页面显示时，需要自己留出页边距；嵌在主页里时，由主页决定边距
        PanMain.Margin = IsInsideScrollViewer() ? new Thickness(0) : new Thickness(25, 25, 25, 15);
        // 首次显示时加载；已有内容则不会重复请求，上次加载失败则会重试
        _ = _viewModel.LoadAsync();
    }

    private bool IsInsideScrollViewer()
    {
        for (var parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is ScrollViewer)
                return true;
        return false;
    }

    /// <summary>
    ///     重新获取新闻。
    /// </summary>
    public void Refresh()
    {
        _ = _viewModel.LoadAsync(true);
    }

    private static void OnLanguageChanged(PageHomepageNewsView page)
    {
        // 新闻地区跟随界面语言，正在显示时立即换成新语言的内容；未显示时在下次显示时重新加载
        ModBase.RunInUi(() =>
        {
            if (page.IsLoaded)
                _ = page._viewModel.LoadAsync(true);
        });
    }

    /// <summary>
    ///     校验后在浏览器中打开新闻链接。只允许 Minecraft 与微软相关的网址。
    /// </summary>
    public static void OpenNewsLink(string? url)
    {
        if (!NewsViewModel.CanOpenNewsLink(url))
        {
            LogWrapper.Warn("Homepage", $"已拦截不安全的 Minecraft 信息流链接：{url ?? "<null>"}");
            return;
        }

        ModBase.OpenWebsite(url!);
    }

    private void BtnOpenRead_Click(object sender, EventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NewsItem item })
            OpenNewsLink(item.Url);
    }
}
