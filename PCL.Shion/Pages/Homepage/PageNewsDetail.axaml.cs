using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Shion.Controls;
using PCL.Shion.Localization;

namespace PCL.Shion.Pages.Homepage;

/// <summary>
/// Minecraft 新闻详情页：在启动器内显示一篇新闻的正文。
/// 标题、作者、日期和头图直接取自新闻列表项，正文在页面首次显示时从新闻页面获取并解析。
/// 所有失败都只在页面上给出提示，不会弹出错误窗口。
/// </summary>
public partial class PageNewsDetail : MyPageRight
{
    private enum DetailState
    {
        Loading,
        Content,
        Failed,
        NoContent
    }

    private static Cursor? _handCursor;

    private static Cursor HandCursor => _handCursor ??= new Cursor(StandardCursorType.Hand);

    private DetailState _state = DetailState.Loading;
    private string? _errorDetail;
    private int _loadVersion;
    private bool _started;

    public PageNewsDetail()
    {
        InitializeComponent();
        Loaded += (_, _) => OnLoaded();
    }

    /// <summary>要显示的新闻。</summary>
    public required NewsItem Item
    {
        get => field;
        init
        {
            field = value;
            ApplyHeader();
            ApplyState();
        }
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
    /// 重新获取并显示正文。
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
            var blocks = await NewsArticleLoader.LoadAsync(Item.Url, reload);
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
                _state = DetailState.NoContent;
                ApplyState();
            }
        }
        catch (Exception ex)
        {
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
    /// 刷新标题、作者与日期、头图。这些内容来自新闻列表项，无需等待正文。
    /// </summary>
    private void ApplyHeader()
    {
        LabTitle.Text = WebUtility.HtmlDecode(Item.Title);

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(Item.Author))
            meta.Add(Item.Author.Trim());
        if (Item.Time > 0)
            meta.Add(Item.FormatPublishDate("d"));
        LabMeta.Text = string.Join(" · ", meta);
        LabMeta.IsVisible = meta.Count > 0;

        if (string.IsNullOrWhiteSpace(Item.Image))
        {
            ImgHero.IsVisible = false;
        }
        else
        {
            ImgHero.IsVisible = true;
            ImgHero.Source = Item.Image;
            if (!string.IsNullOrWhiteSpace(Item.ImageAltText))
                ToolTip.SetTip(ImgHero, WebUtility.HtmlDecode(Item.ImageAltText));
        }
    }

    private void ApplyState()
    {
        var isContent = _state == DetailState.Content;
        PanBody.IsVisible = isContent;
        PanState.IsVisible = !isContent;
        PanLoading.IsVisible = _state == DetailState.Loading;
        PanFailed.IsVisible = _state is DetailState.Failed or DetailState.NoContent;
        BtnRetry.IsVisible = _state == DetailState.Failed;

        if (_state == DetailState.Failed)
        {
            LabFailed.Text = Lang.Text("Widget.News.Detail.Failed");
            LabFailed.Classes.Set("error", true);
            LabFailedDetail.Text = _errorDetail ?? "";
            LabFailedDetail.IsVisible = !string.IsNullOrEmpty(_errorDetail);
        }
        else if (_state == DetailState.NoContent)
        {
            LabFailed.Text = Lang.Text("Widget.News.Detail.NoContent");
            LabFailed.Classes.Set("error", false);
            LabFailedDetail.IsVisible = false;
        }
    }

    #endregion

    #region 正文

    /// <summary>
    /// 用解析出的正文块替换正文区域的内容，返回成功生成的块数。
    /// </summary>
    private int RenderBlocks(IReadOnlyList<NewsArticleBlock> blocks)
    {
        var elements = new List<Control>();
        NewsArticleBlock? previous = null;
        foreach (var block in blocks)
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

        if (elements.Count == 0)
            return 0;
        PanBody.Children.Clear();
        PanBody.Children.AddRange(elements);
        return elements.Count;
    }

    private Control? CreateBlockElement(NewsArticleBlock block, bool isFirst)
    {
        switch (block.Kind)
        {
            case NewsArticleBlockKind.Heading:
            {
                var fontSize = block.Level switch { 1 => 20d, 2 => 18d, 3 => 16d, _ => 14.5d };
                var text = CreateText(block, fontSize, FontWeight.Bold);
                text.Margin = new Thickness(0, isFirst ? 0 : 16, 0, 6);
                return text;
            }
            case NewsArticleBlockKind.ListItem:
                return CreateListItem(block);
            case NewsArticleBlockKind.Image:
                return CreateImage(block);
            case NewsArticleBlockKind.Caption:
            {
                var text = CreateText(block, 12, FontWeight.Normal);
                text.Classes.Add("caption");
                text.TextAlignment = TextAlignment.Center;
                text.Margin = new Thickness(0, -6, 0, 12);
                return text;
            }
            default:
            {
                var text = CreateText(block, 14, FontWeight.Normal);
                text.Margin = new Thickness(0, 0, 0, 10);
                return text;
            }
        }
    }

    private TextBlock CreateText(NewsArticleBlock block, double fontSize, FontWeight weight)
    {
        var text = new TextBlock
        {
            FontSize = fontSize,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            LineHeight = Math.Round(fontSize * 1.55)
        };
        FillText(text, block);
        return text;
    }

    /// <summary>
    /// 把块文本放进 TextBlock：链接做成可点击的下划线文字。
    /// 文字里的换行符（\n）由文本排版直接换行，因此各段文字的位置与块文本中的位置一一对应。
    /// </summary>
    private void FillText(TextBlock target, NewsArticleBlock block)
    {
        var text = block.Text;
        var links = block.Links
            .Where(l => l.Length > 0 && l.Start + l.Length <= text.Length)
            .OrderBy(l => l.Start)
            .ToList();
        if (links.Count == 0)
        {
            target.Text = text;
            return;
        }

        var linkBrush = this.TryFindResource("ColorBrush3", ActualThemeVariant, out var brush) ? brush as IBrush : null;
        var inlines = new InlineCollection();
        var position = 0;
        var clickable = new List<NewsArticleLink>();
        foreach (var link in links)
        {
            if (link.Start < position)
                continue;
            if (link.Start > position)
                inlines.Add(new Run(text[position..link.Start]));
            inlines.Add(new Run(text.Substring(link.Start, link.Length))
            {
                Foreground = linkBrush,
                TextDecorations = TextDecorations.Underline
            });
            clickable.Add(link);
            position = link.Start + link.Length;
        }

        if (position < text.Length)
            inlines.Add(new Run(text[position..]));
        target.Inlines = inlines;
        AttachLinks(target, clickable);
    }

    /// <summary>
    /// 让 TextBlock 里的链接文字可以点击：鼠标移到链接上显示手形光标和网址，点击后在浏览器中打开。
    /// </summary>
    private static void AttachLinks(TextBlock target, IReadOnlyList<NewsArticleLink> links)
    {
        NewsArticleLink? FindLink(PointerEventArgs e)
        {
            var hit = target.TextLayout.HitTestPoint(e.GetPosition(target));
            if (!hit.IsInside)
                return null;
            var index = hit.CharacterHit.FirstCharacterIndex;
            return links.FirstOrDefault(l => index >= l.Start && index < l.Start + l.Length);
        }

        target.PointerMoved += (_, e) =>
        {
            var link = FindLink(e);
            target.Cursor = link is null ? null : HandCursor;
            ToolTip.SetTip(target, link?.Url);
        };
        target.PointerExited += (_, _) =>
        {
            target.Cursor = null;
            ToolTip.SetTip(target, null);
        };
        target.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left && FindLink(e) is { } link)
                NewsLinks.Open(link.Url);
        };
    }

    private Control CreateListItem(NewsArticleBlock block)
    {
        const double fontSize = 14;
        var grid = new Grid
        {
            Margin = new Thickness(4 + Math.Max(block.Level - 1, 0) * 22, 0, 0, 4),
            ColumnDefinitions = new ColumnDefinitions("Auto,*")
        };

        var isNumbered = block.Index > 0;
        grid.Children.Add(new TextBlock
        {
            Text = isNumbered ? block.Index + "." : "•",
            FontSize = fontSize,
            MinWidth = 18,
            Margin = new Thickness(0, 0, 8, 0),
            TextAlignment = isNumbered ? TextAlignment.Right : TextAlignment.Center,
            TextTrimming = TextTrimming.None,
            VerticalAlignment = VerticalAlignment.Top,
            LineHeight = Math.Round(fontSize * 1.55)
        });

        var text = CreateText(block, fontSize, FontWeight.Normal);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private static Control? CreateImage(NewsArticleBlock block)
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
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
        if (!string.IsNullOrWhiteSpace(block.Text))
            ToolTip.SetTip(image, block.Text);
        return image;
    }

    #endregion

    #region 按钮

    private void BtnRetry_Click(object? sender, PointerReleasedEventArgs e)
    {
        _ = LoadAsync(true);
    }

    private void BtnBrowser_Click(object? sender, PointerReleasedEventArgs e)
    {
        PageHomepageNewsView.OpenNewsLink(Item.Url);
    }

    #endregion
}
