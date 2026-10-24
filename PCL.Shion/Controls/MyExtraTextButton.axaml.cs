using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public partial class MyExtraTextButton : Grid
{
    public delegate void ClickEventHandler(object? sender, PointerReleasedEventArgs e); // 自定义事件

    // 自定义事件
    // 务必放在 IsMouseDown 更新之后
    private const int animationColorIn = 120;
    private const int animationColorOut = 150;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyExtraTextButton, string?>(nameof(Text));

    // 鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    private bool isLeftMouseHeld;

    // 自定义属性
    private static int _uuidSeed;

    public int Uuid = GetUuid();

    static MyExtraTextButton()
    {
        TextProperty.Changed.AddClassHandler<MyExtraTextButton>((sender, e) =>
            sender.LabText.Text = e.NewValue as string);
    }

    public MyExtraTextButton()
    {
        InitializeComponent();
        RefreshIconHostVisibility();

        Loaded += (_, _) => RefreshColor();
        this.GetObservable(IsEnabledProperty).Subscribe(new Avalonia.Reactive.AnonymousObserver<bool>(_ => RefreshColor()));
        PanClick.PointerPressed += (sender, e) =>
        {
            if (e.GetCurrentPoint(PanClick).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
                Button_LeftMouseDown(sender, e);
        };
        PanClick.PointerReleased += (sender, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
                Button_LeftMouseUp(sender, e);
        };
        PanClick.PointerExited += Button_MouseLeave;
        PanClick.PointerReleased += (sender, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Right)
                Button_RightMouseUp(sender, e);
        };
        PanClick.PointerEntered += (_, _) => RefreshColor();
    }

    public string Logo
    {
        get;
        set
        {
            if ((value ?? "") == (field ?? ""))
                return;
            field = value ?? string.Empty;
            Path.Data = string.IsNullOrWhiteSpace(value)
                ? null
                : Geometry.Parse(value);
            SvgIconControlHelper.ApplyVisibility(Path, ShapeSvgIcon, IsUsingSvgIcon);
            RefreshIconHostVisibility();
        }
    } = string.Empty;

    public string SvgIcon
    {
        get;
        set
        {
            value ??= string.Empty;
            if (value == field)
                return;
            field = value;
            if (Path is null || ShapeSvgIcon is null)
                return;
            SvgIconControlHelper.ApplyIcon(Path, ShapeSvgIcon, field);
            ApplyLogoScale();
            RefreshIconHostVisibility();
        }
    } = string.Empty;

    private bool IsUsingSvgIcon => SvgIconControlHelper.HasSvgIcon(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1D : LogoScale;

    private bool HasAnyIcon => IsUsingSvgIcon || !string.IsNullOrWhiteSpace(Logo);

    private void ApplyLogoScale()
    {
        IconHost?.RenderTransform = new ScaleTransform
        {
            ScaleX = EffectiveLogoScale,
            ScaleY = EffectiveLogoScale
        };
    }

    private void RefreshIconHostVisibility()
    {
        if (IconHost is null || LabText is null)
            return;

        if (HasAnyIcon)
        {
            IconHost.IsVisible = true;
            IconHost.Width = 16;
            IconHost.Margin = new Thickness(2, 12, 0, 12);
            LabText.Margin = new Thickness(12, 0, 0, 0.8);
        }
        else
        {
            IconHost.IsVisible = false;
            IconHost.Width = 0;
            IconHost.Margin = new Thickness(0, 12, 0, 12);
            LabText.Margin = new Thickness(0, 0, 0, 0.8);
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

    // 显示文本
    [Content]
    public InlineCollection Inlines => LabText.Inlines;

    public string? Text
    {
        get => GetValue(TextProperty);
        set
        {
            if (value is null) return;
            SetValue(TextProperty, value);
        }
    }

    public bool Show
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            RunInUi(() =>
            {
                if (value)
                {
                    // 有了
                    Opacity = 0d;
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(this, 1d - Opacity, 80, 50),
                            ModAnimation.AaScaleTransform(this, 0.15d - ((ScaleTransform)RenderTransform!).ScaleX, 400,
                                50, new ModAnimation.AniEaseOutBack()),
                            ModAnimation.AaScaleTransform(this, 0.85d, 160, 50, new ModAnimation.AniEaseOutFluent())
                        }, "MyExtraTextButton MainScale " + Uuid);
                }
                else
                {
                    // 没了
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(this, -Opacity, 50, 50),
                            ModAnimation.AaScaleTransform(this, -((ScaleTransform)RenderTransform!).ScaleX, 100,
                                ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak))
                        }, "MyExtraTextButton MainScale " + Uuid);
                }

                IsHitTestVisible = value; // 防止缩放动画中依然可以点进去
            });
        }
    }

    // 声明
    public event ClickEventHandler? Click;

    private static int GetUuid()
    {
        return Interlocked.Increment(ref _uuidSeed);
    }

    private static void RunInUi(Action action)
    {
        if (Application.Current is null)
            return;
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    private void StartScaleAnimation(double targetScale, double reboundScale, int reboundDuration = 60)
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanScale, targetScale - ((ScaleTransform)PanScale.RenderTransform!).ScaleX,
                    800, ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                ModAnimation.AaScaleTransform(PanScale, reboundScale, reboundDuration,
                    ease: new ModAnimation.AniEaseOutFluent())
            }, "MyExtraTextButton Scale " + Uuid);
    }

    private void RefreshScaleAfterRelease()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanScale, 1d - ((ScaleTransform)PanScale.RenderTransform!).ScaleX, 300,
                    ease: new ModAnimation.AniEaseOutBack())
            }, "MyExtraTextButton Scale " + Uuid);
    }

    // 触发点击事件
    private void Button_LeftMouseUp(object? sender, PointerReleasedEventArgs e)
    {
        if (!isLeftMouseHeld) return;
        Logger.TryGet(LogEventLevel.Debug, LogArea.Control)?.Log(this, "[Control] 按下附加图标按钮：" + Text);
        Click?.Invoke(sender, e);
        e.Handled = true;
        Button_LeftMouseUp();
    }

    private void Button_LeftMouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (!isLeftMouseHeld)
            StartScaleAnimation(0.85d, -0.05d);
        isLeftMouseHeld = true;
        Focus();
    }

    private void Button_LeftMouseUp()
    {
        RefreshScaleAfterRelease();
        isLeftMouseHeld = false;
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_RightMouseUp(object? sender, PointerReleasedEventArgs e)
    {
        if (!isLeftMouseHeld)
            RefreshScaleAfterRelease();
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_MouseLeave(object? sender, PointerEventArgs e)
    {
        isLeftMouseHeld = false;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanScale, 1d - ((ScaleTransform)PanScale.RenderTransform!).ScaleX, 500,
                    ease: new ModAnimation.AniEaseOutFluent())
            }, "MyExtraTextButton Scale " + Uuid);
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    public void RefreshColor()
    {
        try
        {
            if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
            {
                if (!IsEnabled)
                    // 禁用
                    ModAnimation.AniStart(
                        ModAnimation.AaColor(PanColor, Border.BackgroundProperty, "ColorBrushGray4", animationColorIn),
                        "MyExtraTextButton Color " + Uuid);
                else if (IsPointerOver)
                    // 指向
                    ModAnimation.AniStart(
                        ModAnimation.AaColor(PanColor, Border.BackgroundProperty, "ColorBrush4", animationColorIn),
                        "MyExtraTextButton Color " + Uuid);
                else
                    // 普通
                    ModAnimation.AniStart(
                        ModAnimation.AaColor(PanColor, Border.BackgroundProperty, "ColorBrush3", animationColorOut),
                        "MyExtraTextButton Color " + Uuid);
            }

            else
            {
                ControlVisualHelpers.AnimateColorOrSetResource(PanColor, Border.BackgroundProperty,
                    !IsEnabled ? "ColorBrushGray4" : IsPointerOver ? "ColorBrush4" : "ColorBrush3",
                    !IsEnabled || IsPointerOver ? animationColorIn : animationColorOut,
                    "MyExtraTextButton Color " + Uuid, false);
            }
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "刷新附加图标按钮颜色出错：{Exception}", ex);
        }
    }
}
