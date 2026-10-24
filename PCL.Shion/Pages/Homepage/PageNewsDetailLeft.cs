using PCL.Shion.Localization;
using PCL.Shion.Navigation;

namespace PCL.Shion.Pages.Homepage;

/// <summary>
/// 新闻详情的左侧页。详情页没有左侧内容，只负责在导航时把右侧换成要看的新闻。
/// </summary>
public sealed class PageNewsDetailLeft : MyPageLeft
{
    /// <summary>下一次导航到详情页时要显示的新闻。</summary>
    private static NewsItem? _pending;

    private PageNewsDetail? _page;

    public override string? Title => Lang.Text("Main.Title.NewsDetail");

    /// <summary>
    /// 跳转到新闻详情页，显示指定的新闻。
    /// </summary>
    internal static void Open(NewsItem item)
    {
        _pending = item;
        Navigator.Go(PageType.NewsDetail);
    }

    public override void OnNavigated(PageSubType sub)
    {
        if (_pending is { } item)
        {
            _pending = null;
            _page = new PageNewsDetail { Item = item };
        }

        if (_page is not null)
            Navigator.ShowRight(_page);
    }
}
