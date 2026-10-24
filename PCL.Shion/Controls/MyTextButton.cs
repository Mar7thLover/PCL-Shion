using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Logging;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public class MyTextButton : Label
{
    public delegate void ClickEventHandler(object? sender, EventArgs? e);

    // 指向动画

    private const int animationTimeIn = 100;
    private const int animationTimeOut = 200;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyTextButton, string>(nameof(Text), "");

    static MyTextButton()
    {
        TextProperty.Changed.AddClassHandler<MyTextButton>((button, e) =>
        {
            if (Equals(e.OldValue, e.NewValue)) return;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaOpacity(button, -button.Opacity, 50),
                    ModAnimation.AaCode(() => button.Content = e.NewValue, after: true),
                    ModAnimation.AaOpacity(button, 1d, 170)
                }, "MyTextButton Text " + button.Uuid);
        });
    }

    private string? colorName;

    // 鼠标事件

    public bool isMouseDown;

    // 基础

    private static int _uuidSeed;

    public int Uuid = GetUuid();

    public MyTextButton()
    {
        this.SetResourceReference(ForegroundProperty, "ColorBrush1");
        this.SetResourceReference(BackgroundProperty, "ColorBrushSemiTransparent");
        AddHandler(PointerPressedEvent, MyTextButton_MouseLeftButtonDown, RoutingStrategies.Tunnel);
        PointerExited += (_, _) => MyTextButton_MouseLeave();
        AddHandler(PointerReleasedEvent, MyTextButton_MouseLeftButtonUp, RoutingStrategies.Tunnel);
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        this.GetObservable(IsEnabledProperty).Subscribe(new Avalonia.Reactive.AnonymousObserver<bool>(_ => RefreshColor()));
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                RefreshColor();
        };
        PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
                RefreshColor();
        };
    }

    // 文本

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public event ClickEventHandler? Click;

    private static int GetUuid() => Interlocked.Increment(ref _uuidSeed);

    private (string ForeName, int Time) GetVisualState()
    {
        if (isMouseDown)
            return ("ColorBrush4", 30);
        if (IsPointerOver)
            return ("ColorBrush3", animationTimeIn);
        return ("ColorBrush1", animationTimeOut);
    }

    private void MyTextButton_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        isMouseDown = true;
        e.Handled = true;
    }

    private void MyTextButton_MouseLeave()
    {
        isMouseDown = false;
    }

    private void MyTextButton_MouseLeftButtonUp(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        if (!isMouseDown) return;
        isMouseDown = false;
        Logger.TryGet(LogEventLevel.Debug, LogArea.Control)?.Log(this, "[Control] 按下文本按钮：{Text}", Text);
        Click?.Invoke(this, null);
        e.Handled = true;
    }

    private void RefreshColor()
    {
        var (ForeName, Time) = GetVisualState();

        // 重复性验证
        if ((colorName ?? "") == (ForeName ?? ""))
            return;
        colorName = ForeName;
        // 触发颜色动画
        ControlVisualHelpers.AnimateColorOrSetResource(this, ForegroundProperty, ForeName, Time,
            "MyTextButton Color " + Uuid, ControlVisualHelpers.ShouldAnimate(this));
    }
}
