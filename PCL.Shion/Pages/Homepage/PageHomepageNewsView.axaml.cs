using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PCL.Shion.Pages.Homepage;

/// <summary>
/// Minecraft 新闻列表。既用作主页预设的内容（嵌在主页的滚动区域里），也用作「工具」里的完整页面（单独显示）。
/// 两种用法各自使用独立的实例。
/// </summary>
public partial class PageHomepageNewsView : MyPageRight
{
    private readonly NewsViewModel _viewModel = new();

    public PageHomepageNewsView()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += (_, _) => OnLoaded();
    }

    private void OnLoaded()
    {
        // 单独作为页面显示时，需要自己留出页边距；嵌在主页里时，由主页决定边距
        PanMain.Margin = this.FindAncestorOfType<ScrollViewer>() is null ? new Thickness(25, 25, 25, 15) : new Thickness(0);
        // 首次显示时加载；已有内容则不会重复请求，上次加载失败则会重试
        _ = _viewModel.LoadAsync();
    }

    /// <summary>
    /// 重新获取新闻。
    /// </summary>
    public void Refresh()
    {
        _ = _viewModel.LoadAsync(true);
    }

    /// <summary>
    /// 校验后在浏览器中打开新闻链接。只允许 Minecraft 与微软相关的网址。
    /// </summary>
    public static void OpenNewsLink(string? url)
    {
        if (NewsViewModel.CanOpenNewsLink(url))
            NewsLinks.Open(url);
    }

    /// <summary>
    /// 在启动器内打开新闻详情页，显示新闻的正文。链接不安全时不打开。
    /// </summary>
    public static void OpenNewsDetail(NewsItem? item)
    {
        if (item is not null && NewsViewModel.CanOpenNewsLink(item.Url))
            PageNewsDetailLeft.Open(item);
    }

    private void BtnOpenRead_Click(object? sender, System.EventArgs? e)
    {
        if (sender is Control { DataContext: NewsItem item })
            OpenNewsDetail(item);
    }
}
