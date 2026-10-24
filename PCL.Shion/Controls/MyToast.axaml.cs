using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public partial class MyToast : Border
{
    public int Uuid = Interlocked.Increment(ref _uuid);

    private static int _uuid;

    /// <summary>判定为拖动而非点击的最小水平位移（像素）。</summary>
    private const double DragDeadzone = 4d;

    /// <summary>拖动时透明度下限，确保控件始终可见。</summary>
    private const double DragOpacityFloor = 0.35d;

    /// <summary>触发关闭的位移占控件宽度的比例。</summary>
    private const double DismissThresholdRatio = 0.12d;

    /// <summary>触发关闭的最小绝对位移（像素）。</summary>
    private const double DismissThresholdMin = 24d;

    /// <summary>拖动释放后，若剩余显示时间不足此值则直接关闭。</summary>
    private const double MinRemainingMs = 300d;

    /// <summary>拖动释放后回到原位的动画时长（毫秒）。</summary>
    private const int ReturnAnimationMs = 150;

    // 拖动状态
    private bool _dragPending;
    private bool _isDragging;
    private Point _dragStartPoint;
    private double _dragStartTranslateX;
    private Visual? _dragReference;
    private IPointer? _dragPointer;

    // 进度条状态
    private double _progressStartWidth;
    private double _progressTotalMs;
    private double _progressBaseWidth = double.NaN;
    private double _progressFromWidth;
    private double _progressTimerDurationMs;
    private Stopwatch? _progressWatch;
    private DispatcherTimer? _progressTimer;

    public MyToast()
    {
        InitializeComponent();
        BtnClose.Click += (_, _) => Dismiss();
        AddHandler(InputElement.PointerPressedEvent, Toast_PreviewMouseLeftButtonDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerMovedEvent, Toast_PreviewMouseMove, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerReleasedEvent, Toast_PreviewMouseLeftButtonUp, RoutingStrategies.Tunnel);
        PointerCaptureLost += Toast_LostMouseCapture;
        Loaded += (_, _) =>
        {
            UpdateColors();
            if (Application.Current is { } application)
            {
                application.ActualThemeVariantChanged += OnThemeChanged;
                application.ResourcesChanged += OnColorThemeChanged;
            }
        };
        Unloaded += (_, _) =>
        {
            if (Application.Current is { } application)
            {
                application.ActualThemeVariantChanged -= OnThemeChanged;
                application.ResourcesChanged -= OnColorThemeChanged;
            }
            ModAnimation.AniStop($"Toast Show {Uuid}");
            ModAnimation.AniStop($"Toast Hide {Uuid}");
            ModAnimation.AniStop($"Toast Dismiss {Uuid}");
            ModAnimation.AniStop($"Toast Emphasize {Uuid}");
            ModAnimation.AniStop($"Toast Drag Return {Uuid}");
            ClearProgressAnimation();
        };
    }

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateColors();
    private void OnColorThemeChanged(object? sender, ResourcesChangedEventArgs e) => UpdateColors();

    public string Context
    {
        get => TitleText.Text ?? string.Empty;
        set => TitleText.Text = value;
    }

    public string Icon { get; set; } = "lucide/info";

    public HintType ToastType { get; set; } = HintType.Info;

    public double DisplayDuration { get; set; } = 5000;

    public bool IsDismissing { get; private set; }

    private double _targetHeight;

    public void Show()
    {
        if (Parent is not Panel)
            return;
        if (MainWindow.Current is { } mainWindow)
            MaxWidth = mainWindow.Bounds.Width * 0.9;
        Margin = new Thickness(0, 0, 16, 4);
        Opacity = 0;

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(0, 0, DesiredSize.Width, DesiredSize.Height));
        _targetHeight = Math.Max(Bounds.Height, 45d);
        Height = 0;

        RenderTransform = new TranslateTransform(60, 0);

        ModAnimation.AniStop($"Toast Drag Return {Uuid}");
        var enterAnimations = new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, -60, 400, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaHeight(this, _targetHeight, 150, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaOpacity(this, 1, 100)
        };
        ModAnimation.AniStart(enterAnimations, $"Toast Show {Uuid}");

        RestartHideAnimation();
    }

    public void Emphasize()
    {
        ModAnimation.AniStop($"Toast Show {Uuid}");
        ModAnimation.AniStop($"Toast Hide {Uuid}");
        ModAnimation.AniStop($"Toast Emphasize {Uuid}");
        ModAnimation.AniStop($"Toast Drag Return {Uuid}");
        ClearProgressAnimation();
        if (RenderTransform is TranslateTransform tt) tt.X = 0;
        Opacity = 1;
        Height = _targetHeight;
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, -8, 70, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaTranslateX(this, 16, 70, after: true),
            ModAnimation.AaTranslateX(this, -8, 60, after: true, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(RestartHideAnimation, after: true)
        }, $"Toast Emphasize {Uuid}");
    }

    private void RestartHideAnimation()
    {
        StartHideAnimation(DisplayDuration);
        StartProgressAnimation(DisplayDuration);
    }

    private void StartHideAnimation(double delayMs)
    {
        var delay = (int)Math.Round(delayMs);
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, 60, 200, delay, new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaOpacity(this, -1, 150, delay),
            ModAnimation.AaHeight(this, -_targetHeight, 100, ease: new ModAnimation.AniEaseOutFluent(), after: true),
            ModAnimation.AaCode(() =>
            {
                if (Parent is Panel p)
                    p.Children.Remove(this);
            }, after: true)
        }, $"Toast Hide {Uuid}");
    }

    public void Dismiss()
    {
        if (IsDismissing) return;
        IsDismissing = true;
        _isDragging = false;
        _dragPending = false;
        if (IsPointerCaptured) ReleasePointerCapture();
        ModAnimation.AniStop($"Toast Show {Uuid}");
        ModAnimation.AniStop($"Toast Hide {Uuid}");
        ModAnimation.AniStop($"Toast Emphasize {Uuid}");
        ModAnimation.AniStop($"Toast Drag Return {Uuid}");
        ClearProgressAnimation();
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, 60, 150, ease: new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaOpacity(this, -1, 100),
            ModAnimation.AaCode(() =>
            {
                if (Parent is Panel p)
                    p.Children.Remove(this);
            }, after: true)
        }, $"Toast Dismiss {Uuid}");
    }

    private void StartProgressAnimation(double duration)
    {
        var totalMs = (int)Math.Round(duration);
        if (totalMs <= 0)
            return;
        var w = ProgressBar.Bounds.Width;
        if (w <= 0) w = 300;
        ProgressBar.HorizontalAlignment = HorizontalAlignment.Left;
        ProgressBar.Width = w;
        _progressStartWidth = w;
        _progressTotalMs = totalMs;
        _progressBaseWidth = w;
        RunProgressAnimation(w, totalMs);
    }

    private void RunProgressAnimation(double fromWidth, int durationMs)
    {
        StopProgressAnimation();
        _progressFromWidth = fromWidth;
        _progressTimerDurationMs = durationMs;
        _progressWatch = Stopwatch.StartNew();
        ProgressBar.Width = fromWidth;
        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _progressTimer.Tick += ProgressTimer_Tick;
        _progressTimer.Start();
    }

    private void ProgressTimer_Tick(object? sender, EventArgs e)
    {
        if (_progressWatch is null)
            return;
        var percent = Math.Clamp(_progressWatch.Elapsed.TotalMilliseconds / _progressTimerDurationMs, 0d, 1d);
        ProgressBar.Width = _progressFromWidth * (1d - percent);
        if (percent >= 1d)
            StopProgressAnimation();
    }

    private void StopProgressAnimation()
    {
        if (_progressTimer is not null)
        {
            _progressTimer.Stop();
            _progressTimer.Tick -= ProgressTimer_Tick;
            _progressTimer = null;
        }
        _progressWatch = null;
    }

    private void ClearProgressAnimation()
    {
        StopProgressAnimation();
        if (!double.IsNaN(_progressBaseWidth))
            ProgressBar.Width = _progressBaseWidth;
    }

    private void RootGrid_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        RootGrid.Clip = new RectangleGeometry(new Rect(0, 0, RootGrid.Bounds.Width, RootGrid.Bounds.Height), 8, 8);
    }

    private void UpdateColors()
    {
        var baseHue = ToastType switch
        {
            HintType.Success => 145d,
            HintType.Error => 355d,
            HintType.Warning => 40d,
            _ => 210d
        };
        Color accent = new MyColor().FromHSL2(baseHue, 75, 60);
        var bg = ActualThemeVariant == ThemeVariant.Dark
            ? (IBrush)new SolidColorBrush(OklabGray(0.35))
            : this.TryFindResource("ColorBrushBackground", ActualThemeVariant, out var background) &&
              background is IBrush backgroundBrush
                ? backgroundBrush
                : Brushes.Transparent;
        var text = this.TryFindResource("ColorBrushGray1", ActualThemeVariant, out var textResource) &&
                   textResource is SolidColorBrush textBrush
            ? textBrush
            : new SolidColorBrush(Colors.Black);
        var accentBrush = new SolidColorBrush(accent);

        Root.Background = bg;
        Root.BorderBrush = bg;
        TitleText.Foreground = text;
        ProgressBar.Fill = accentBrush;
        BtnClose.Foreground = text;
        ToastIcon.Icon = Icon;
        ToastIcon.IconBrush = accentBrush;
        ToastIcon.StrokeThickness = 0;
    }

    /// <summary>
    /// 把 OKLCH 灰阶（色度为 0）转换为颜色，对应 WPF 版 LabColor.FromLch(l)。
    /// </summary>
    private static Color OklabGray(double l)
    {
        // OKLab 在 a = b = 0 时三个线性通道相同，均为 l³，再做 sRGB 伽马编码
        var gray = ToSrgbByte(l * l * l);
        return Color.FromRgb(gray, gray, gray);
    }

    private static byte ToSrgbByte(double linear)
    {
        var value = linear <= 0.0031308d ? 12.92d * linear : 1.055d * Math.Pow(linear, 1d / 2.4d) - 0.055d;
        return MathByte(value * 255d);
    }

    private static byte MathByte(double d)
    {
        if (d < 0d)
            d = 0d;
        if (d > 255d)
            d = 255d;
        return (byte)Math.Round(Math.Round(d));
    }

    #region 拖动关闭

    private void Toast_PreviewMouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        _dragPending = false;
        if (IsDismissing)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (IsDescendantOf(e.Source as Visual, BtnClose))
            return;
        _dragReference = Parent as Visual;
        if (_dragReference is null)
            return;
        _dragPending = true;
        _isDragging = false;
        _dragStartPoint = e.GetPosition(_dragReference);
        _dragPointer = e.Pointer;
    }

    private void Toast_PreviewMouseMove(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
        {
            if (_dragReference is null || !e.GetCurrentPoint(_dragReference).Properties.IsLeftButtonPressed)
            {
                _isDragging = false;
                _dragPending = false;
                if (IsPointerCaptured) ReleasePointerCapture();
                ReturnFromDrag();
                return;
            }
            var dragCurrent = e.GetPosition(_dragReference);
            UpdateDragPosition(dragCurrent.X - _dragStartPoint.X);
            e.Handled = true;
            return;
        }

        if (!_dragPending)
            return;
        if (_dragReference is null || !e.GetCurrentPoint(_dragReference).Properties.IsLeftButtonPressed)
        {
            _dragPending = false;
            return;
        }

        var current = e.GetPosition(_dragReference);
        var delta = current.X - _dragStartPoint.X;

        if (delta < DragDeadzone)
            return;

        BeginDrag(delta);
    }

    private void Toast_PreviewMouseLeftButtonUp(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;

        if (_dragPending && !_isDragging)
        {
            _dragPending = false;
            return;
        }

        if (!_isDragging)
            return;

        _isDragging = false;
        _dragPending = false;
        e.Handled = true;

        if (IsPointerCaptured)
            ReleasePointerCapture();

        var currentX = (RenderTransform as TranslateTransform)?.X ?? 0d;
        if (currentX - _dragStartTranslateX >= GetDismissThreshold())
        {
            Dismiss();
            return;
        }

        ReturnFromDrag();
    }

    private void Toast_LostMouseCapture(object? sender, PointerCaptureLostEventArgs e)
    {
        _dragPointer = null;
        if (!_isDragging)
            return;
        _isDragging = false;
        _dragPending = false;
        ReturnFromDrag();
    }

    private bool IsPointerCaptured => _dragPointer is not null && ReferenceEquals(_dragPointer.Captured, this);

    private void ReleasePointerCapture()
    {
        var pointer = _dragPointer;
        _dragPointer = null;
        pointer?.Capture(null);
    }

    private void BeginDrag(double initialDelta)
    {
        _isDragging = true;
        _dragPending = false;

        ModAnimation.AniStop($"Toast Show {Uuid}");
        ModAnimation.AniStop($"Toast Hide {Uuid}");
        ModAnimation.AniStop($"Toast Emphasize {Uuid}");
        ModAnimation.AniStop($"Toast Drag Return {Uuid}");

        PauseProgress();

        Height = _targetHeight;
        _dragStartTranslateX = (RenderTransform as TranslateTransform)?.X ?? 0d;

        _dragPointer?.Capture(this);

        UpdateDragPosition(initialDelta);
    }

    private void UpdateDragPosition(double delta)
    {
        var newX = _dragStartTranslateX + ApplyDragResistance(delta);
        if (RenderTransform is TranslateTransform tt)
            tt.X = newX;
        Opacity = GetDragOpacity(newX);
    }

    private void ReturnFromDrag()
    {
        if (Parent is null || IsDismissing)
            return;
        var currentX = (RenderTransform as TranslateTransform)?.X ?? 0d;
        var currentOpacity = Opacity;

        var remaining = GetProgressRemainingMs();
        if (remaining < MinRemainingMs)
        {
            Dismiss();
            return;
        }

        ResumeProgress(remaining);

        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, -currentX, ReturnAnimationMs, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaOpacity(this, 1d - currentOpacity, ReturnAnimationMs),
            ModAnimation.AaCode(() => StartHideAnimation(GetProgressRemainingMs()), after: true)
        }, $"Toast Drag Return {Uuid}");
    }

    private static double ApplyDragResistance(double delta)
    {
        return Math.Max(0d, delta);
    }

    private double GetDragOpacity(double translateX)
    {
        if (translateX <= 0d)
            return 1d;
        var width = Bounds.Width > 0 ? Bounds.Width : 1d;
        return Math.Max(DragOpacityFloor, 1d - (translateX / width) * (1d - DragOpacityFloor));
    }

    private double GetDismissThreshold()
    {
        return Math.Max(DismissThresholdMin, Bounds.Width * DismissThresholdRatio);
    }

    private static bool IsDescendantOf(Visual? descendant, Visual ancestor)
    {
        while (descendant is not null)
        {
            if (ReferenceEquals(descendant, ancestor))
                return true;
            descendant = descendant.GetVisualParent();
        }
        return false;
    }

    #endregion

    #region 进度条暂停与恢复

    private void PauseProgress()
    {
        var currentWidth = ProgressBar.Width;
        StopProgressAnimation();
        _progressBaseWidth = currentWidth;
        ProgressBar.Width = currentWidth;
    }

    private void ResumeProgress(double remainingMs)
    {
        if (remainingMs <= 0)
            return;
        var currentWidth = ProgressBar.Width;
        if (currentWidth <= 0)
            return;
        RunProgressAnimation(currentWidth, (int)Math.Round(remainingMs));
    }

    private double GetProgressRemainingMs()
    {
        if (_progressStartWidth <= 0)
            return 0d;
        var currentWidth = ProgressBar.Width;
        return _progressTotalMs * (currentWidth / _progressStartWidth);
    }

    #endregion
}
