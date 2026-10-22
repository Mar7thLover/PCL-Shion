using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Model.Tools.News;
using PCL.Core.ViewModel.Homepage;

namespace PCL;

/// <summary>
///     Minecraft 新闻详情页：在启动器内显示一篇新闻的正文。
///     标题、作者、日期和头图直接取自新闻列表项，正文在页面首次显示时从新闻页面获取并解析。
///     所有失败都只记录日志，并在页面上给出提示，不会弹出错误窗口。
/// </summary>
public partial class PageNewsDetail : IRefreshable
{
    private enum DetailState
    {
        Loading,
        Content,
        Failed,
        NoContent
    }

    private readonly NewsItem _item;
    private DetailState _state = DetailState.Loading;
    private string? _errorDetail;
    private int _loadVersion;
    private bool _started;

    public PageNewsDetail(NewsItem item)
    {
        _item = item;
        InitializeComponent();
        ApplyHeader();
        ApplyState();
        Loaded += (_, _) => OnLoaded();
        WeakLanguageChanged.Add(this, OnLanguageChanged);
    }

    private static void OnLanguageChanged(PageNewsDetail page) => ModBase.RunInUi(page.OnLanguageChangedUi);

    private void OnLanguageChangedUi()
    {
        ApplyHeader();
        ApplyState();
    }

    private void OnLoaded()
    {
        if (_started)
            return;
        _started = true;
        PanBack.ScrollToHome();
        _ = LoadAsync(false);
    }

    /// <summary>
    ///     重新获取并显示正文。
    /// </summary>
    public void Refresh()
    {
        _started = true;
        _ = LoadAsync(true);
    }

    #region 加载

    private async Task LoadAsync(bool reload)
    {
        var version = ++_loadVersion;
        // 重新加载时保留已经显示的正文，加载成功后再替换；失败则继续显示旧内容
        var hasContent = _state == DetailState.Content;
        if (!hasContent)
        {
            _state = DetailState.Loading;
            ApplyState();
        }

        try
        {
            var blocks = await NewsArticleLoader.LoadAsync(_item.Url, reload);
            if (version != _loadVersion)
                return;
            var rendered = RenderBlocks(blocks);
            if (rendered > 0)
            {
                _state = DetailState.Content;
                ApplyState();
            }
            else if (!hasContent)
            {
                // 页面能打开，但没有认出正文的结构
                LogWrapper.Warn("Homepage", $"没有从 Minecraft 新闻页面中解析出正文：{_item.Url}");
                _state = DetailState.NoContent;
                ApplyState();
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Homepage", $"加载 Minecraft 新闻正文失败：{_item.Url}");
            if (version != _loadVersion || hasContent)
                return;
            _errorDetail = ex.Message.Split('\n')[0].Trim();
            _state = DetailState.Failed;
            ApplyState();
        }
    }

    #endregion

    #region 界面状态

    /// <summary>
    ///     刷新标题、作者与日期、头图。这些内容来自新闻列表项，无需等待正文。
    /// </summary>
    private void ApplyHeader()
    {
        LabTitle.Text = System.Net.WebUtility.HtmlDecode(_item.Title ?? "");

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(_item.Author))
            meta.Add(_item.Author.Trim());
        if (_item.Time > 0)
            meta.Add(Lang.Date(_item.PublishDate, "d"));
        LabMeta.Text = string.Join(" · ", meta);
        LabMeta.Visibility = meta.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        if (string.IsNullOrWhiteSpace(_item.Image))
        {
            ImgHero.Visibility = Visibility.Collapsed;
        }
        else
        {
            ImgHero.Visibility = Visibility.Visible;
            ImgHero.Source = _item.Image;
            if (!string.IsNullOrWhiteSpace(_item.ImageAltText))
                ImgHero.ToolTip = System.Net.WebUtility.HtmlDecode(_item.ImageAltText);
        }
    }

    private void ApplyState()
    {
        var isContent = _state == DetailState.Content;
        PanBody.Visibility = isContent ? Visibility.Visible : Visibility.Collapsed;
        PanState.Visibility = isContent ? Visibility.Collapsed : Visibility.Visible;
        PanLoading.Visibility = _state == DetailState.Loading ? Visibility.Visible : Visibility.Collapsed;
        PanFailed.Visibility = (_state is DetailState.Failed or DetailState.NoContent)
            ? Visibility.Visible
            : Visibility.Collapsed;
        BtnRetry.Visibility = _state == DetailState.Failed ? Visibility.Visible : Visibility.Collapsed;

        if (_state == DetailState.Failed)
        {
            LabFailed.Text = Lang.Text("Widget.News.Detail.Failed");
            LabFailed.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedLight");
            LabFailedDetail.Text = _errorDetail ?? "";
            LabFailedDetail.Visibility = string.IsNullOrEmpty(_errorDetail) ? Visibility.Collapsed : Visibility.Visible;
        }
        else if (_state == DetailState.NoContent)
        {
            LabFailed.Text = Lang.Text("Widget.News.Detail.NoContent");
            LabFailed.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
            LabFailedDetail.Visibility = Visibility.Collapsed;
        }
    }

    #endregion

    #region 正文

    /// <summary>
    ///     用解析出的正文块替换正文区域的内容，返回成功生成的块数。
    /// </summary>
    private int RenderBlocks(IReadOnlyList<NewsArticleBlock> blocks)
    {
        var elements = new List<FrameworkElement>();
        NewsArticleBlock? previous = null;
        foreach (var block in blocks)
        {
            try
            {
                var element = CreateBlockElement(block, elements.Count == 0);
                if (element is null)
                    continue;
                // 列表结束后，下一个块与列表之间多留一点空隙
                if (previous is { Kind: NewsArticleBlockKind.ListItem } && block.Kind != NewsArticleBlockKind.ListItem)
                {
                    var margin = element.Margin;
                    element.Margin = new Thickness(margin.Left, margin.Top + 8, margin.Right, margin.Bottom);
                }

                elements.Add(element);
                previous = block;
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Homepage", $"生成新闻正文块失败（{block.Kind}）");
            }
        }

        if (elements.Count == 0)
            return 0;
        PanBody.Children.Clear();
        foreach (var element in elements)
            PanBody.Children.Add(element);
        return elements.Count;
    }

    private static FrameworkElement? CreateBlockElement(NewsArticleBlock block, bool isFirst)
    {
        switch (block.Kind)
        {
            case NewsArticleBlockKind.Heading:
            {
                var fontSize = block.Level switch { 1 => 20d, 2 => 18d, 3 => 16d, _ => 14.5d };
                var text = CreateText(block, fontSize, FontWeights.Bold);
                text.Margin = new Thickness(0, isFirst ? 0 : 16, 0, 6);
                return text;
            }
            case NewsArticleBlockKind.ListItem:
                return CreateListItem(block);
            case NewsArticleBlockKind.Image:
                return CreateImage(block);
            case NewsArticleBlockKind.Caption:
            {
                var text = CreateText(block, 12, FontWeights.Normal);
                text.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray2");
                text.TextAlignment = TextAlignment.Center;
                text.Margin = new Thickness(0, -6, 0, 12);
                return text;
            }
            default:
            {
                var text = CreateText(block, 14, FontWeights.Normal);
                text.Margin = new Thickness(0, 0, 0, 10);
                return text;
            }
        }
    }

    private static TextBlock CreateText(NewsArticleBlock block, double fontSize, FontWeight weight)
    {
        var text = new TextBlock
        {
            FontSize = fontSize,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            LineHeight = Math.Round(fontSize * 1.55),
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };
        FillInlines(text, block);
        return text;
    }

    /// <summary>
    ///     把块文本放进 TextBlock：链接做成可点击的超链接，换行符转为换行。
    /// </summary>
    private static void FillInlines(TextBlock target, NewsArticleBlock block)
    {
        var text = block.Text;
        var position = 0;
        foreach (var link in block.Links.OrderBy(l => l.Start))
        {
            if (link.Start < position || link.Length <= 0 || link.Start + link.Length > text.Length)
                continue;
            AddPlainText(target, text.Substring(position, link.Start - position));

            var url = link.Url;
            var hyperlink = new Hyperlink(new Run(text.Substring(link.Start, link.Length))) { ToolTip = url };
            hyperlink.SetResourceReference(TextElement.ForegroundProperty, "ColorBrush3");
            hyperlink.Click += (_, _) => OpenLink(url);
            target.Inlines.Add(hyperlink);
            position = link.Start + link.Length;
        }

        AddPlainText(target, text.Substring(position));
    }

    private static void AddPlainText(TextBlock target, string text)
    {
        if (text.Length == 0)
            return;
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                target.Inlines.Add(new LineBreak());
            if (lines[i].Length > 0)
                target.Inlines.Add(new Run(lines[i]));
        }
    }

    private static FrameworkElement CreateListItem(NewsArticleBlock block)
    {
        const double fontSize = 14;
        var grid = new Grid { Margin = new Thickness(4 + Math.Max(block.Level - 1, 0) * 22, 0, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var isNumbered = block.Index > 0;
        var marker = new TextBlock
        {
            Text = isNumbered ? block.Index.ToString(CultureInfo.InvariantCulture) + "." : "•",
            FontSize = fontSize,
            MinWidth = 18,
            Margin = new Thickness(0, 0, 8, 0),
            TextAlignment = isNumbered ? TextAlignment.Right : TextAlignment.Center,
            TextTrimming = TextTrimming.None,
            VerticalAlignment = VerticalAlignment.Top,
            LineHeight = Math.Round(fontSize * 1.55),
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };
        grid.Children.Add(marker);

        var text = CreateText(block, fontSize, FontWeights.Normal);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private static FrameworkElement? CreateImage(NewsArticleBlock block)
    {
        if (string.IsNullOrEmpty(block.Url))
            return null;
        // 不显示默认的占位图，避免图片加载完成时版面跳动两次
        var image = new MyImage
        {
            LoadingSource = "",
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            MaxHeight = 420,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 14),
            CornerRadius = new CornerRadius(6),
            Source = block.Url
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        if (!string.IsNullOrWhiteSpace(block.Text))
            image.ToolTip = block.Text;
        return image;
    }

    private static void OpenLink(string url)
    {
        try
        {
            ModBase.OpenWebsite(url);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Homepage", $"打开新闻正文中的链接失败：{url}");
        }
    }

    #endregion

    #region 按钮

    private void BtnRetry_Click(object sender, MouseButtonEventArgs e)
    {
        _ = LoadAsync(true);
    }

    private void BtnBrowser_Click(object sender, MouseButtonEventArgs e)
    {
        PageHomepageNewsView.OpenNewsLink(_item.Url);
    }

    #endregion
}
