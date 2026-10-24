using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using PCL.Shion.Localization;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public partial class MySlider : Border
{
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, RouteEventArgs e);

    // 自定义属性

    private int _Value;
    private bool changeByKey;

    // 拖动

    public Delegate? getHintText;

    private IPointer? dragPointer;
    private Point dragPosition;

    public static MySlider? dragControl;

    // 基础

    private static int uuid;

    public int Uuid = Interlocked.Increment(ref uuid);

    public MySlider()
    {
        InitializeComponent();
        SizeChanged += RefreshWidth;
        PointerPressed += DragStart;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsEnabledProperty)
                RefreshColor();
        };
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        PointerEntered += (_, _) => MySlider_MouseEnter();
        KeyDown += MySlider_KeyDown;
        PointerReleased += (_, _) =>
        {
            if (dragPointer is not null)
                DragStop();
        };
        PointerCaptureLost += (_, _) =>
        {
            if (dragPointer is not null)
                DragStop();
        };
    }

    public int MaxValue
    {
        get => field;
        set
        {
            if (value == field)
                return;
            field = value;
            RefreshWidth(null, null);
        }
    } = 100;

    public int Value
    {
        get => _Value;
        set
        {
            try
            {
                value = (int)Math.Round(MathClamp(value, 0d, MaxValue));
                if (_Value == value)
                    return;

                // 触发 Preview 事件，修改新值
                var oldValue = _Value;
                _Value = value;
                if (ModAnimation.AniControlEnabled == 0)
                {
                    var e = new RouteEventArgs();
                    PreviewChange?.Invoke(this, e);
                    if (e.handled)
                    {
                        _Value = oldValue;
                        DragStop();
                        return;
                    }
                }

                if (IsLoaded && ModAnimation.AniControlEnabled == 0)
                {
                    if (Bounds.Width < ShapeDot.Width)
                        return;
                    var newWidth = _Value / (double)MaxValue * (Bounds.Width - ShapeDot.Width);
                    var deltaProcess =
                        Math.Abs(LineFore.Width / (Bounds.Width - ShapeDot.Width) - _Value / (double)MaxValue);
                    var time = (1d - Math.Pow(1d - deltaProcess, 3d)) * 300d + (changeByKey ? 100 : 0);
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaWidth(LineFore,
                                Math.Max(0d, newWidth + (newWidth < 0.5d ? 0d : 0.5d)) - LineFore.Width,
                                (int)Math.Round(time),
                                ease: time > 50d
                                    ? new ModAnimation.AniEaseOutFluent()
                                    : new ModAnimation.AniEaseLinear()),
                            ModAnimation.AaWidth(LineBack,
                                Math.Max(0d,
                                    Bounds.Width - ShapeDot.Width - newWidth +
                                    (Bounds.Width - ShapeDot.Width - newWidth < 0.5d ? 0d : 0.5d)) - LineBack.Width,
                                (int)Math.Round(time),
                                ease: time > 50d
                                    ? new ModAnimation.AniEaseOutFluent()
                                    : new ModAnimation.AniEaseLinear()),
                            ModAnimation.AaX(ShapeDot, newWidth - ShapeDot.Margin.Left, (int)Math.Round(time),
                                ease: time > 50d
                                    ? new ModAnimation.AniEaseOutFluent()
                                    : new ModAnimation.AniEaseLinear())
                        }, "MySlider Progress " + Uuid);
                }
                else
                {
                    RefreshWidth(null, null);
                }

                if (ModAnimation.AniControlEnabled == 0)
                    Change?.Invoke(this, false);
            }

            catch (Exception ex)
            {
                Trace.TraceError("滑动条进度改变出错：" + ex);
                Hint.Show(Lang.Text("Application.Control.Error.OperationFailed"), HintType.Error);
            }
        }
    }

    // 按键改变

    public uint ValueByKey { get; set; } = 1U;
    public event ChangeEventHandler? Change;
    public event PreviewChangeEventHandler? PreviewChange;

    private void RefreshWidth(object? sender, SizeChangedEventArgs? e)
    {
        if (e is not null)
            PanMain.Width = e.NewSize.Width;
        ModAnimation.AniStop("MySlider Progress " + Uuid);
        var newWidth = _Value / (double)MaxValue * (Bounds.Width - ShapeDot.Width);
        LineFore.Width = Math.Max(0d, newWidth + (newWidth < 0.5d ? 0d : 0.5d));
        LineBack.Width = Math.Max(0d,
            Bounds.Width - ShapeDot.Width - newWidth + (Bounds.Width - ShapeDot.Width - newWidth < 0.5d ? 0d : 0.5d));
        SetLeft(ShapeDot, newWidth);
    }

    private void DragStart(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        e.Pointer.Capture(this);
        dragPointer = e.Pointer;
        PointerMoved += OnDragMouseMove;
        e.Handled = true; // 防止 ScrollViewer 失焦问题
        dragControl = this;
        RefreshColor();
        dragPosition = e.GetPosition(PanMain);
        DragDoing();
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(ShapeDot, 1.3d - ((ScaleTransform)ShapeDot.RenderTransform!).ScaleX, 40,
                ease: new ModAnimation.AniEaseOutFluent()), "MySlider Scale " + Uuid);
        RefreshPopup();
        ModAnimation.AniStop("MySlider KeyPopup " + Uuid);
    }

    public void DragDoing()
    {
        var percent =
            MathClamp((dragPosition.X - ShapeDot.Width / 2d) / (Bounds.Width - ShapeDot.Width), 0d,
                1d);
        var newValue = (int)Math.Round(percent * MaxValue);
        if (newValue != Value) Value = newValue;
        RefreshPopup();
    }

    private void OnDragMouseMove(object? sender, PointerEventArgs e)
    {
        dragPosition = e.GetPosition(PanMain);
        DragDoing();
    }

    public void DragStop()
    {
        if (ReferenceEquals(dragControl, this))
            dragControl = null;
        PointerMoved -= OnDragMouseMove;
        if (dragPointer is not null)
        {
            var pointer = dragPointer;
            dragPointer = null;
            pointer.Capture(null);
        }
        RefreshColor();
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(ShapeDot, 1d - ((ScaleTransform)ShapeDot.RenderTransform!).ScaleX, 200,
                ease: new ModAnimation.AniEaseOutFluent()), "MySlider Scale " + Uuid);
        Popup.IsOpen = false;
    }

    public void RefreshPopup()
    {
        if (getHintText is null)
            return;
        Popup.IsOpen = true;
        TextHint.Text = getHintText.DynamicInvoke(Value)?.ToString() ?? "";
        var typeface = new Typeface(TextHint.FontFamily, TextHint.FontStyle, TextHint.FontWeight);
        var formattedText = new FormattedText(TextHint.Text ?? "", CultureInfo.CurrentCulture,
            TextHint.FlowDirection, typeface, TextHint.FontSize, TextHint.Foreground);
        TextHint.Width = formattedText.Width; // 使用手动测量的宽度修复 #1057
    }

    // 指向动画

    private void RefreshColor()
    {
        try
        {
            // 判断当前颜色
            string foregroundName;
            string dotFillName;
            int animationTime;
            if (IsEnabled)
            {
                if (dragControl is not null && dragControl.Equals(this) || IsPointerOver)
                {
                    foregroundName = "ColorBrush3";
                    dotFillName = "ColorBrush3";
                    animationTime = 40;
                }
                else
                {
                    foregroundName = "ColorBrushBg0";
                    dotFillName = "ColorBrushBg0";
                    animationTime = 100;
                }
            }
            else
            {
                foregroundName = "ColorBrushGray5";
                dotFillName = "ColorBrushGray5";
                animationTime = 200;
            }

            // 触发颜色动画
            if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
            {
                // 有动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaColor(this, BorderBrushProperty, foregroundName, animationTime),
                        ModAnimation.AaColor(ShapeDot, Shape.FillProperty, dotFillName, animationTime)
                    }, "MySlider Color " + Uuid);
            }
            else
            {
                // 无动画
                ModAnimation.AniStop("MySlider Color " + Uuid);
                this.SetResourceReference(BorderBrushProperty, foregroundName);
                ShapeDot.SetResourceReference(Shape.FillProperty, dotFillName);
            }
        }

        catch (Exception ex)
        {
            Trace.TraceError("滑动条颜色改变出错：" + ex);
        }
    }

    private void MySlider_MouseEnter()
    {
        Focus(); // 确保按键能改变值
    }

    private void MySlider_KeyDown(object? sender, KeyEventArgs e)
    {
        // 拒绝一边拖动一边用按键改变
        if (ReferenceEquals(this, dragControl))
            return;
        // 改变值
        if (e.Key == Key.Left)
        {
            changeByKey = true;
            Value = (int)(Value - ValueByKey);
            changeByKey = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            changeByKey = true;
            Value = (int)(Value + ValueByKey);
            changeByKey = false;
            e.Handled = true;
        }
        else
        {
            return;
        }

        // 更新 Popup
        if (getHintText is not null)
        {
            RefreshPopup();
            ModAnimation.AniStop("MySlider KeyPopup " + Uuid);
            ModAnimation.AniStart(
                ModAnimation.AaCode(() => Popup.IsOpen = false, (int)Math.Round(700d * ModAnimation.aniSpeed)),
                "MySlider KeyPopup " + Uuid);
        }
    }

    // 将数值限定在某个范围内。
    private static double MathClamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    // 设置控件的左边距。（仅针对置左控件）
    private static void SetLeft(Control control, double newValue)
    {
        control.Margin = new Thickness(newValue, control.Margin.Top, control.Margin.Right, control.Margin.Bottom);
    }
}
