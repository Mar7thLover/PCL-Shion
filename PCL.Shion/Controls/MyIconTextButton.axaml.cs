using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Styling;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public partial class MyIconTextButton : Border
{
    public delegate void ChangeEventHandler(object? sender, bool raiseByMouse);

    public delegate void CheckEventHandler(object? sender, bool raiseByMouse);

    public delegate void ClickEventHandler(object? sender, RouteEventArgs e);

    public enum ColorState
    {
        Black,
        Highlight
    }

    // 动画

    private const int animationTimeOfMouseIn = 100; // 鼠标指向动画长度
    private const int animationTimeOfMouseOut = 150; // 鼠标移出动画长度

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyIconTextButton, string?>(nameof(Text));

    public static readonly StyledProperty<ColorState> ColorTypeProperty =
        AvaloniaProperty.Register<MyIconTextButton, ColorState>(nameof(ColorType), ColorState.Black);

    static MyIconTextButton()
    {
        TextProperty.Changed.AddClassHandler<MyIconTextButton>((button, e) =>
            button.LabText.Text = (string?)e.NewValue);
    }

    private bool _hasLegacyLogo;
    private bool isMouseDown;

    // 基础

    private static int _uuidSeed;

    public int Uuid = GetUuid();

    public MyIconTextButton()
    {
        InitializeComponent();
        RefreshLogoHostVisibility();

        PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
                MyIconTextButton_MouseUp();
        };
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                MyIconTextButton_MouseDown();
        };
        PointerExited += (_, _) => MyIconTextButton_MouseLeave();
        PointerEntered += RefreshColor;
        Loaded += RefreshColor;
        this.GetObservable(IsEnabledProperty).Subscribe(new Avalonia.Reactive.AnonymousObserver<bool>(_ => RefreshColor()));
    }

    // 自定义属性

    public string Logo
    {
        get => ShapeLogo.Data?.ToString() ?? string.Empty;
        set
        {
            if (ShapeLogo is null) return;
            _hasLegacyLogo = !string.IsNullOrWhiteSpace(value);
            ShapeLogo.Data = _hasLegacyLogo
                ? Geometry.Parse(value!)
                : null;
            SvgIconControlHelper.ApplyVisibility(ShapeLogo, ShapeSvgIcon, IsUsingSvgIcon);
            RefreshLogoHostVisibility();
        }
    }

    public string SvgIcon
    {
        get;
        set
        {
            value ??= string.Empty;
            if (value == field)
                return;
            field = value;
            if (ShapeLogo is null || ShapeSvgIcon is null)
                return;
            SvgIconControlHelper.ApplyIcon(ShapeLogo, ShapeSvgIcon, field);
            ApplyLogoScale();
            RefreshLogoHostVisibility();
            RefreshColor();
        }
    } = string.Empty;

    private bool IsUsingSvgIcon => SvgIconControlHelper.HasSvgIcon(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1D : LogoScale;

    private bool HasAnyIcon => IsUsingSvgIcon || _hasLegacyLogo;

    private bool IsDarkMode => ActualThemeVariant == ThemeVariant.Dark;

    private void ApplyLogoScale()
    {
        LogoHost?.RenderTransform = new ScaleTransform
        {
            ScaleX = EffectiveLogoScale,
            ScaleY = EffectiveLogoScale
        };
    }

    private void RefreshLogoHostVisibility()
    {
        if (LogoHost is null || LabText is null)
            return;

        if (HasAnyIcon)
        {
            LogoHost.IsVisible = true;
            LogoHost.Width = 16;
            LogoHost.Height = 16;
            LogoHost.Margin = new Thickness(12, 0, 0, 0);
            LabText.Margin = new Thickness(7, 0, 12, 1);
        }
        else
        {
            LogoHost.IsVisible = false;
            LogoHost.Width = 0;
            LogoHost.Height = 16;
            LogoHost.Margin = new Thickness(0);
            LabText.Margin = new Thickness(12, 0, 12, 1);
        }
    }

    public double LogoScale
    {
        get;
        set
        {
            field = value;
            ApplyLogoScale();
        }
    } = 1d;

    [Content]
    public InlineCollection Inlines => LabText.Inlines;

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 内容

    public ColorState ColorType
    {
        get => GetValue(ColorTypeProperty);
        set
        {
            if (ColorType == value)
                return;
            SetValue(ColorTypeProperty, value);
            RefreshColor();
        }
    } // 颜色类别

    public event CheckEventHandler? Check;
    public event ChangeEventHandler? Change;

    private string CheckedAnimationKey => "MyIconTextButton Checked " + Uuid;
    private string ColorAnimationKey => "MyIconTextButton Color " + Uuid;

    // 点击事件

    public event ClickEventHandler? Click;

    private string GetDefaultForegroundResourceKey()
    {
        return ColorType == ColorState.Highlight ? "ColorBrush3" : "ColorBrush1";
    }

    private MyColor GetSemiTransparentColor()
    {
        return new MyColor(this.TryFindResource("ColorBrushSemiTransparent", ActualThemeVariant, out var resource)
            ? resource
            : null);
    }

    private void StartForegroundAnimation(string resourceKey, int duration)
    {
        if (IsUsingSvgIcon)
        {
            SvgIconControlHelper.AnimateSvgIconBrushTo(ShapeSvgIcon, resourceKey, duration, CheckedAnimationKey);
            ModAnimation.AniStart(
                ModAnimation.AaColor(LabText, TextElement.ForegroundProperty, resourceKey, duration),
                CheckedAnimationKey);
        }
        else
        {
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, resourceKey, duration),
                    ModAnimation.AaColor(LabText, TextElement.ForegroundProperty, resourceKey, duration)
                }, CheckedAnimationKey);
        }
    }

    private void StartBackgroundAnimation(string resourceKey, int duration)
    {
        ModAnimation.AniStart(ModAnimation.AaColor(this, BackgroundProperty, resourceKey, duration), ColorAnimationKey);
    }

    private void StartBackgroundAnimation(MyColor delta, int duration)
    {
        ModAnimation.AniStart(ModAnimation.AaColor(this, BackgroundProperty, delta, duration), ColorAnimationKey);
    }

    private void MyIconTextButton_MouseUp()
    {
        if (!isMouseDown)
            return;
        Logger.TryGet(LogEventLevel.Debug, LogArea.Control)?.Log(this, "[Control] 按下带图标按钮：{Text}", Text);
        isMouseDown = false;
        Click?.Invoke(this, new RouteEventArgs(true));
        RefreshColor();
    }

    private void MyIconTextButton_MouseDown()
    {
        isMouseDown = true;
        RefreshColor();
    }

    private void MyIconTextButton_MouseLeave()
    {
        isMouseDown = false;
        RefreshColor();
    }

    private void RefreshColor(object? obj = null, object? e = null)
    {
        try
        {
            if (ControlVisualHelpers.ShouldAnimate(this, e)) // 防止默认属性变更触发动画，若强制不执行动画，则 e 为 False
            {
                if (isMouseDown)
                {
                    StartBackgroundAnimation("ColorBrush6", 70);
                }
                else if (IsPointerOver)
                {
                    StartForegroundAnimation("ColorBrush3", animationTimeOfMouseIn);
                    StartBackgroundAnimation("ColorBrushBg1", animationTimeOfMouseIn);
                }
                else if (IsEnabled)
                {
                    StartForegroundAnimation(GetDefaultForegroundResourceKey(), animationTimeOfMouseOut);
                    StartBackgroundAnimation(GetSemiTransparentColor() - Background, animationTimeOfMouseOut);
                }
                else
                {
                    StartForegroundAnimation("ColorBrushGray5", 100);
                    StartBackgroundAnimation(GetSemiTransparentColor() - Background, animationTimeOfMouseOut);
                }
            }

            else
            {
                // 不使用动画
                ModAnimation.AniStop(CheckedAnimationKey);
                ModAnimation.AniStop(ColorAnimationKey);
                this.SetResourceReference(BackgroundProperty, "ColorBrushSemiTransparent");
                var foregroundKey = IsEnabled ? GetDefaultForegroundResourceKey() : "ColorBrushGray5";
                SvgIconControlHelper.SetIconResource(ShapeLogo, ShapeSvgIcon, IsUsingSvgIcon, foregroundKey);
                LabText.SetResourceReference(TextElement.ForegroundProperty, foregroundKey);
            }
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "刷新带图标按钮颜色出错：{Exception}", ex);
        }
    }

    private static int GetUuid() => Interlocked.Increment(ref _uuidSeed);
}
