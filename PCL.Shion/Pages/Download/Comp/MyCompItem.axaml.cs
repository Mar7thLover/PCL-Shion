using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Shion.Core.Online;
using PCL.Shion.Pages.Download.Comp;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

/// <summary>
/// 资源列表中的一个工程卡片。
/// </summary>
public partial class MyCompItem : Grid
{
    public delegate void ClickEventHandler(object? sender, PointerReleasedEventArgs e);

    private static int _uuidSeed;

    private string? _stateLast;
    private bool _isMouseDown;
    private bool _isLogoDown;
    private Border? _rectBack;

    public MyCompItem()
    {
        InitializeComponent();
        AddHandler(PointerReleasedEvent, Button_MouseUp, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, Button_MouseDown, RoutingStrategies.Tunnel);
        PointerExited += Button_MouseLeave;
        PointerEntered += RefreshColor;
        PointerExited += RefreshColor;
        PointerPressed += (sender, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                RefreshColor(sender, e);
        };
        PointerReleased += (sender, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
                RefreshColor(sender, e);
        };
        LabInfo.PointerEntered += LabInfo_PointerEntered;
        BtnDelete.Click += BtnDelete_Click;
        BtnDownload.Click += BtnDownload_Click;
        Uuid = Interlocked.Increment(ref _uuidSeed);
        ShowFavoriteBtn = true;
    }

    /// <summary>
    /// 是否允许交互。目前仅用于详情页顶部栏展示：关闭后不会响应鼠标。
    /// </summary>
    public bool CanInteraction { get; set; } = true;

    /// <summary>用于动画分组的唯一编号。</summary>
    public int Uuid { get; }

    /// <summary>触发点击事件。</summary>
    public event ClickEventHandler? Click;

    /// <summary>Logo 地址。</summary>
    public string? Logo
    {
        get => PathLogo.Source;
        set => PathLogo.Source = value;
    }

    /// <summary>标题。</summary>
    public string Title
    {
        get => LabTitle.Text ?? "";
        set
        {
            if ((LabTitle.Text ?? "") == (value ?? ""))
                return;
            LabTitle.Text = value;
        }
    }

    /// <summary>副标题，为空时隐藏。</summary>
    public string SubTitle
    {
        get => LabTitleRaw.Text ?? "";
        set
        {
            if ((LabTitleRaw.Text ?? "") == (value ?? ""))
                return;
            LabTitleRaw.Text = value;
            LabTitleRaw.IsVisible = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>描述。</summary>
    public string Description
    {
        get => LabInfo.Text ?? "";
        set
        {
            if ((LabInfo.Text ?? "") == (value ?? ""))
                return;
            LabInfo.Text = value;
        }
    }

    /// <summary>标签文字列表。</summary>
    public List<string> Tags
    {
        set
        {
            PanTags.Children.Clear();
            PanTags.IsVisible = value.Count > 0;
            foreach (var tagText in value)
            {
                var newTag = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(17, 0, 0, 0)),
                    Padding = new Thickness(3d, 1d, 3d, 1d),
                    CornerRadius = new CornerRadius(3d),
                    Margin = new Thickness(0d, 0d, 3d, 0d),
                    Child = new TextBlock
                    {
                        Text = tagText,
                        Foreground = new SolidColorBrush(Color.FromRgb(134, 134, 134)),
                        FontSize = 11d
                    }
                };
                PanTags.Children.Add(newTag);
            }
        }
    }

    /// <summary>版本段文字。</summary>
    public string VersionText
    {
        get => LabVersion.Text ?? "";
        set => LabVersion.Text = value;
    }

    /// <summary>是否显示版本段（图标与文字）。</summary>
    public bool ShowVersionArea
    {
        set
        {
            SvgIconVersion.IsVisible = value;
            LabVersion.IsVisible = value;
        }
    }

    /// <summary>来源文字。</summary>
    public string SourceText
    {
        get => LabSource.Text ?? "";
        set => LabSource.Text = value;
    }

    /// <summary>更新时间文字。</summary>
    public string TimeText
    {
        get => LabTime.Text ?? "";
        set => LabTime.Text = value;
    }

    /// <summary>是否显示更新时间段。</summary>
    public bool ShowTimeArea
    {
        set
        {
            SvgIconTime.IsVisible = value;
            LabTime.IsVisible = value;
        }
    }

    /// <summary>下载量文字。</summary>
    public string DownloadText
    {
        get => LabDownload.Text ?? "";
        set => LabDownload.Text = value;
    }

    /// <summary>是否显示收藏按钮。</summary>
    public bool ShowFavoriteBtn
    {
        get => BtnDelete.IsVisible;
        set
        {
            BtnDelete.IsVisible = value;
            UpdatePanButtons();
        }
    }

    /// <summary>是否显示快速下载按钮。</summary>
    public bool ShowDownloadBtn
    {
        get => BtnDownload.IsVisible;
        set
        {
            BtnDownload.IsVisible = value;
            UpdatePanButtons();
        }
    }

    private bool HasActionButtons => ShowFavoriteBtn || ShowDownloadBtn;

    private void UpdatePanButtons()
    {
        PanButtons.IsVisible = HasActionButtons;
    }

    /// <summary>
    /// 刷新收藏状态图标，不影响悬停展示。
    /// </summary>
    public void RefreshFavoriteStatus()
    {
        if (Tag is not CompProject project) return;
        var isFavourite = CompFavoriteStore.IsFavourite(project.Id);
        BtnDelete.SvgIcon = isFavourite ? "lucide/heart-filled" : "lucide/heart";
    }

    /// <summary>
    /// 刷新悬停颜色与按钮淡入淡出。
    /// </summary>
    public void RefreshColor(object? sender, EventArgs e)
    {
        if (!CanInteraction)
            return;

        string stateNew;
        int time;
        if (IsPointerOver)
        {
            stateNew = _isMouseDown ? "MouseDown" : "MouseOver";
            time = 120;
        }
        else
        {
            stateNew = "Idle";
            time = 180;
        }

        if ((_stateLast ?? "") == (stateNew ?? ""))
            return;
        _stateLast = stateNew;

        if (IsLoaded && ModAnimation.AniControlEnabled == 0)
        {
            var ani = new List<ModAnimation.AniData>();
            if (IsPointerOver)
            {
                if (HasActionButtons)
                    ani.Add(ModAnimation.AaOpacity(PanButtons, 1d - PanButtons.Opacity, (int)Math.Round(time * 0.35d),
                        (int)Math.Round(time * 0.15d)));
                ani.AddRange(new[]
                {
                    ModAnimation.AaColor(RectBack, Border.BackgroundProperty,
                        _isMouseDown ? "ColorBrush6" : "ColorBrushBg1", time),
                    ModAnimation.AaOpacity(RectBack, 1d - RectBack.Opacity, time,
                        ease: new ModAnimation.AniEaseOutFluent())
                });
                var scale = (ScaleTransform)RectBack.RenderTransform!;
                ani.Add(ModAnimation.AaScaleTransform(RectBack,
                    (_isMouseDown ? 0.996d : 1d) - scale.ScaleX, (int)Math.Round(time * 1.2d),
                    ease: new ModAnimation.AniEaseOutFluent()));
            }
            else
            {
                if (HasActionButtons)
                    ani.Add(ModAnimation.AaOpacity(PanButtons, -PanButtons.Opacity, (int)Math.Round(time * 0.4d)));
                var scale = (ScaleTransform)RectBack.RenderTransform!;
                ani.AddRange(new[]
                {
                    ModAnimation.AaOpacity(RectBack, -RectBack.Opacity, time),
                    ModAnimation.AaColor(RectBack, Border.BackgroundProperty,
                        _isMouseDown ? "ColorBrush6" : "ColorBrush7", time),
                    ModAnimation.AaScaleTransform(RectBack, 0.996d - scale.ScaleX,
                        time, ease: new ModAnimation.AniEaseOutFluent()),
                    ModAnimation.AaScaleTransform(RectBack, -0.196d, 1, after: true)
                });
            }

            ModAnimation.AniStart(ani, "CompItem Color " + Uuid);
        }
        else
        {
            ModAnimation.AniStop("CompItem Color " + Uuid);
            if (_rectBack is not null)
                RectBack.Opacity = 0d;
            PanButtons.Opacity = 0d;
        }
    }

    // 指向背景
    private Border RectBack
    {
        get
        {
            if (_rectBack is null)
            {
                var rect = new Border
                {
                    Name = "RectBack",
                    CornerRadius = new CornerRadius(3d),
                    RenderTransform = new ScaleTransform(0.8d, 0.8d),
                    RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative),
                    BorderThickness = new Thickness(1d),
                    IsHitTestVisible = false,
                    Opacity = 0d
                };
                rect.SetResourceReference(Border.BackgroundProperty, "ColorBrush7");
                rect.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6");
                SetColumnSpan(rect, 999);
                SetRowSpan(rect, 999);
                Children.Insert(0, rect);
                _rectBack = rect;
            }

            return _rectBack;
        }
    }

    private void LabInfo_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (LabInfo.Text is { } text && IsTextTrimmed(LabInfo))
            ToolTip.SetTip(LabInfo, text);
        else
            ToolTip.SetTip(LabInfo, null);
    }

    private static bool IsTextTrimmed(TextBlock textBlock)
    {
        var text = textBlock.Text ?? "";
        return text.Length > 0 && textBlock.Bounds.Width > 0 &&
               textBlock.TextLayout.WidthIncludingTrailingWhitespace > textBlock.Bounds.Width;
    }

    private void BtnDelete_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (PanButtons.Opacity > 0d && Tag is CompProject project)
            CompFavoriteMenu.Show(project, BtnDelete, RefreshFavoriteStatus);
    }

    private void BtnDownload_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (PanButtons.Opacity > 0d && Tag is CompProject project)
            CompQuickDownload.Run(this, project);
    }

    private void Button_MouseUp(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || !_isMouseDown)
            return;
        Click?.Invoke(sender, e);
        if (e.Handled)
            return;
        if (Tag is CompProject project)
            PageDownloadCompDetail.OpenFromItem(project);
    }

    private void Button_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (!CanInteraction)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var clickPosition = e.GetPosition(this);
        var isClickOnButton = PanButtons.IsVisible &&
                              (IsClickOnActionButton(BtnDelete, clickPosition) ||
                               IsClickOnActionButton(BtnDownload, clickPosition));
        if (isClickOnButton)
            return;

        var isClickOnLabInfo = false;
        if (LabInfo.IsVisible)
        {
            var bounds = new Rect(LabInfo.TranslatePoint(new Point(0d, 0d), this) ?? default, LabInfo.Bounds.Size);
            isClickOnLabInfo = bounds.Contains(clickPosition);
        }

        if (IsPointerOver || isClickOnLabInfo)
            _isMouseDown = true;
    }

    private void Button_MouseLeave(object? sender, PointerEventArgs e)
    {
        _isMouseDown = false;
    }

    private bool IsClickOnActionButton(Control button, Point clickPosition)
    {
        if (!button.IsVisible) return false;
        var origin = button.TranslatePoint(new Point(0d, 0d), this);
        if (origin is null) return false;
        return new Rect(origin.Value, button.Bounds.Size).Contains(clickPosition);
    }
}
