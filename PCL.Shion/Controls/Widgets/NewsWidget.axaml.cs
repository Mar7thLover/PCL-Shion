using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Threading;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Homepage;

namespace PCL.Shion.Controls;

/// <summary>
///     首页侧栏的 Minecraft 新闻卡片，显示最新的几条新闻标题。
/// </summary>
public partial class NewsWidget : MyCard
{
    private const int ItemCount = 3;

    /// <summary>
    ///     距离上次成功加载不到这段时间时，<see cref="Refresh" /> 不会重新请求。
    ///     首页每次显示都会刷新侧栏，不加限制的话会在页面来回切换时反复请求新闻接口。
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly NewsViewModel _viewModel = new();
    private DateTime _lastSuccessUtc = DateTime.MinValue;

    public NewsWidget()
    {
        InitializeComponent();
        Render();
        // 首次显示时加载；已有内容则不会重复请求，上次加载失败则会重试
        Loaded += (_, _) => _ = LoadAsync(false);
        WeakLanguageChanged.Add(this, OnLanguageChanged);
    }

    private static void OnLanguageChanged(NewsWidget widget) => Dispatcher.UIThread.Post(() => widget.OnLanguageChangedUi());

    private void OnLanguageChangedUi()
    {
        Render();
        // 新闻地区跟随界面语言；未显示时会在下次显示时按新语言重新加载
        if (IsLoaded)
            _ = LoadAsync(true);
    }

    /// <summary>
    ///     重新获取新闻。距离上次成功加载不足 5 分钟，或正在加载时不会重复请求。
    /// </summary>
    public void Refresh()
    {
        if (_viewModel.IsLoading)
            return;
        var isFresh = _viewModel.ErrorMessage is null
                      && _viewModel.NewsItems.Count > 0
                      && DateTime.UtcNow - _lastSuccessUtc < RefreshInterval;
        if (isFresh)
        {
            Render(); // 日期显示可能受语言影响，至少重绘一次
            return;
        }

        _ = LoadAsync(true);
    }

    private async Task LoadAsync(bool reload)
    {
        try
        {
            var task = _viewModel.LoadAsync(reload);
            Render(); // 已经进入加载状态（没有旧内容时显示“加载中”）
            await task;
            if (_viewModel.ErrorMessage is null)
                _lastSuccessUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "加载 Minecraft 新闻卡片失败：{Exception}", ex);
        }

        Render();
    }

    /// <summary>
    ///     按当前状态重绘：有新闻就显示最新几条，没有就显示加载中 / 失败 / 暂无新闻。
    /// </summary>
    private void Render()
    {
        try
        {
            PanItems.Children.Clear();
            var items = _viewModel.NewsItems.Take(ItemCount).ToList();
            foreach (var news in items)
            {
                var title = news.Title;
                var row = new MyListItem
                {
                    Title = title,
                    Info = Lang.Date(news.PublishDate, "d"),
                    Type = MyListItem.CheckType.Clickable
                };
                ToolTip.SetTip(row, title);
                row.Click += (_, _) => PageHomepageNewsView.OpenNewsDetail(news);
                PanItems.Children.Add(row);
            }

            if (items.Count > 0)
            {
                LabState.IsVisible = false;
                return;
            }

            // 没有新闻可显示：还没开始或正在加载时是“加载中”，加载失败是“失败”，否则是“暂无新闻”
            var key = _viewModel.ErrorMessage is not null
                ? "Widget.News.Failed"
                : _viewModel.IsLoading || _lastSuccessUtc == DateTime.MinValue
                    ? "Widget.News.Loading"
                    : "Widget.News.Empty";
            LabState.Text = Lang.Text(key);
            LabState.IsVisible = true;
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "刷新 Minecraft 新闻卡片失败：{Exception}", ex);
        }
    }

    private void BtnMore_Click(object? sender, EventArgs? e)
    {
        try
        {
            Navigator.Go(PageType.Tools, PageSubType.ToolsNews);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "打开 Minecraft 新闻页面失败：{Exception}", ex);
        }
    }
}
