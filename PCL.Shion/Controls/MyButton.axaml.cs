using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Metadata;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public partial class MyButton : Border
{
    public delegate void ClickEventHandler(object? sender, PointerReleasedEventArgs e); // 自定义事件

    public enum ColorState
    {
        Normal = 0,
        Highlight = 1,
        Red = 2
    }

    // 自定义事件
    private const int animationColorIn = 100;
    private const int animationColorOut = 200;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyButton, string?>(nameof(Text));

    // 属性穿透
    public new static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<MyButton, Thickness>(nameof(Padding));

    private ColorState _ColorType = ColorState.Normal; // 配色方案

    // 鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）

    // 自定义属性
    private static int _uuidSeed;

    public int Uuid = GetUuid();

    public MyButton()
    {
        InitializeComponent();

        PropertyChanged += (_, e) =>
        {
            if (e.Property == TextProperty)
                LabText.Text = (string?)e.NewValue;
            else if (e.Property == PaddingProperty)
                PanFore.Padding = e.GetNewValue<Thickness>();
        };

        PointerEntered += RefreshColor;
        PointerExited += RefreshColor;
        Loaded += RefreshColor;
        this.GetObservable(IsEnabledProperty).Subscribe(new Avalonia.Reactive.AnonymousObserver<bool>(_ => RefreshColor()));
        PointerReleased += (sender, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
                Button_MouseUp(sender, e);
        };
        PointerPressed += (sender, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                Button_MouseDown(sender, e);
        };
        PointerEntered += (_, _) => Button_MouseEnter();
        PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
                Button_MouseUp();
        };
        PointerExited += (_, _) => Button_MouseLeave();
    }

    [Content]
    public InlineCollection Inlines => LabText.Inlines;

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 显示文本

    public Thickness TextPadding
    {
        get => LabText.Padding;
        set => LabText.Padding = value;
    }

    public ColorState ColorType
    {
        get => _ColorType;
        set
        {
            _ColorType = value;
            RefreshColor();
        }
    }

    public new Thickness Padding
    {
        get => PanFore.Padding;
        set => PanFore.Padding = value;
    }

    public ITransform? RealRenderTransform
    {
        get => PanFore.RenderTransform;
        set => PanFore.RenderTransform = value;
    }

    // 声明
    public event ClickEventHandler? Click;

    private static int GetUuid()
    {
        return Interlocked.Increment(ref _uuidSeed);
    }

    private string GetBorderBrushResourceKey()
    {
        return ColorType switch
        {
            ColorState.Normal => IsPointerOver ? "ColorBrush3" : "ColorBrush1",
            ColorState.Highlight => IsPointerOver ? "ColorBrush3" : "ColorBrush2",
            ColorState.Red => IsPointerOver ? "ColorBrushRedLight" : "ColorBrushRedDark",
            _ => "ColorBrush1"
        };
    }

    private void StartBorderBrushAnimation(string resourceKey, int duration)
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaColor(PanFore, BorderBrushProperty, resourceKey, duration)
            }, "MyButton Color " + Uuid);
    }

    private void RefreshColor(object? obj = null, object? e = null)
    {
        try
        {
            if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
            {
                if (IsEnabled)
                    StartBorderBrushAnimation(GetBorderBrushResourceKey(),
                        IsPointerOver ? animationColorIn : animationColorOut);
                else
                    // 不可用（Gray 4）
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaColor(PanFore, BorderBrushProperty, "ColorBrushGray4", animationColorOut)
                        }, "MyButton Color " + Uuid);
            }
            else
            {
                ModAnimation.AniStop("MyButton Color " + Uuid);
                if (IsEnabled)
                    PanFore.SetResourceReference(BorderBrushProperty, GetBorderBrushResourceKey());
                else
                    PanFore.SetResourceReference(BorderBrushProperty, "ColorBrushGray4");
            }
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "刷新按钮颜色出错：{Exception}", ex);
        }
    }

    // 实现自定义事件
    private bool isMouseDown = false;

    private void Button_MouseUp(object? sender, PointerReleasedEventArgs e)
    {
        if (!isMouseDown)
            return;
        Logger.TryGet(LogEventLevel.Debug, LogArea.Control)?.Log(this, "[Control] 按下按钮：" + Text);
        Click?.Invoke(sender, e);
    }

    private void Button_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        isMouseDown = true;
        Focus();
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanFore, 0.955d - ((ScaleTransform)PanFore.RenderTransform!).ScaleX, 80,
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                ModAnimation.AaScaleTransform(PanFore, -0.01d, 700, ease: new ModAnimation.AniEaseOutFluent())
            }, "MyButton Scale " + Uuid);
    }

    private void Button_MouseEnter()
    {
        ModAnimation.AniStart(
            ModAnimation.AaColor(PanFore, BackgroundProperty,
                _ColorType == ColorState.Red ? "ColorBrushRedBack" : "ColorBrush7", animationColorIn),
            "MyButton Background " + Uuid);
    }

    private void Button_MouseUp()
    {
        if (!isMouseDown)
            return;
        isMouseDown = false;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanFore, 1d - ((ScaleTransform)PanFore.RenderTransform!).ScaleX, 300, 10,
                    new ModAnimation.AniEaseOutFluent())
            }, "MyButton Scale " + Uuid);
    }

    private void Button_MouseLeave()
    {
        ModAnimation.AniStart(
            ModAnimation.AaColor(PanFore, BackgroundProperty, "ColorBrushHalfWhite", animationColorOut),
            "MyButton Background " + Uuid);
        if (!isMouseDown)
            return;
        isMouseDown = false;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanFore, 1d - ((ScaleTransform)PanFore.RenderTransform!).ScaleX, 800,
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong))
            }, "MyButton Scale " + Uuid);
    }
}
