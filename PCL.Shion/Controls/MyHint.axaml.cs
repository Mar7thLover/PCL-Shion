using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Styling;
using PCL.Shion.Services;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public partial class MyHint : Border
{
    public delegate void ClickEventHandler(object? sender, PointerReleasedEventArgs e);

    // 配色
    public enum Themes
    {
        Blue = 0,
        Red = 1,
        Yellow = 2
    }

    private readonly record struct HintTone(double L2, double L7);

    private static readonly HintTone LightTone = new(0.5d, 0.94d);
    private static readonly HintTone DarkTone = new(0.75d, 0.225d);

    public static readonly StyledProperty<bool> IsWarnProperty =
        AvaloniaProperty.Register<MyHint, bool>(nameof(IsWarn), true);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyHint, string>(nameof(Text), "");

    static MyHint()
    {
        IsWarnProperty.Changed.AddClassHandler<MyHint>((f, e) =>
            f.Theme = e.NewValue is true ? Themes.Red : Themes.Blue);
        TextProperty.Changed.AddClassHandler<MyHint>((f, e) => f.LabText.Text = (string?)e.NewValue);
    }

    // 触发点击事件
    private bool isMouseDown;
    private static int _uuidSeed;
    public int Uuid = GetUuid();

    public MyHint()
    {
        InitializeComponent();
        UpdateUI();
        Loaded += (_, _) => UpdateUI();
        Loaded += MyHint_Loaded;
        PointerReleased += MyHint_MouseUp;
        PointerPressed += MyHint_MouseDown;
        PointerExited += (_, _) => MyHint_MouseLeave();
        Unloaded += (_, _) => Dispose();
        BtnClose.Click += (sender, _) => BtnClose_Click(sender, EventArgs.Empty);
    }

    // 边框
    public bool HasBorder
    {
        get => BorderThickness.Top > 0d;
        set
        {
            if (value)
                BorderThickness = new Thickness(3d, GetWPFSize(1d), GetWPFSize(1d), GetWPFSize(1d));
            else
                BorderThickness = new Thickness(3d, 0d, 0d, 0d);
        }
    }

    public Themes Theme
    {
        get => field;
        set
        {
            field = value;
            UpdateUI();
        }
    } = Themes.Red;

    [Obsolete("IsWarn 已过时。请换用 Theme 属性。")]
    public bool IsWarn
    {
        get => Theme == Themes.Red;
        set => Theme = value ? Themes.Red : Themes.Blue;
    }

    // 文本
    [Content]
    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // 关闭按钮
    public bool CanClose
    {
        get => BtnClose.IsVisible;
        set => BtnClose.IsVisible = value;
    }

    public string RelativeSetup { get; set; } = "";

    // 声明
    public event ClickEventHandler? Click;

    private static int GetUuid()
    {
        return Interlocked.Increment(ref _uuidSeed);
    }

    private double GetWPFSize(double pixelSize)
    {
        return pixelSize / (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d);
    }

    private static SolidColorBrush HslBrush(double hue, double saturation, double lightness)
    {
        return new SolidColorBrush(new MyColor().FromHSL2(hue, saturation, lightness));
    }

    private void UpdateUI()
    {
        var hue = default(double);
        switch (Theme)
        {
            case Themes.Blue:
            {
                hue = 210d;
                break;
            }
            case Themes.Red:
            {
                hue = 355d;
                break;
            }
            case Themes.Yellow:
            {
                hue = 40d;
                break;
            }
        }

        var s = ActualThemeVariant == ThemeVariant.Dark ? DarkTone : LightTone;
        Background = HslBrush(hue, 90, s.L7 * 100);
        BorderBrush = HslBrush(hue, 90, s.L2 * 100);
        LabText.Foreground = HslBrush(hue, 90, s.L2 * 100);
        BtnClose.Foreground = HslBrush(hue, 90, s.L2 * 100);

        // 根据提示气泡对齐方向刷新边框
        // 此处依赖 HasBorder 的副作用进行范围检查
        HasBorder = HasBorder;
    }

    private void MyHint_Loaded(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is { } application)
            application.ActualThemeVariantChanged += _ThemeChanged;
        if (CanClose && IsHintDismissed(RelativeSetup))
            IsVisible = false;
    }

    private void BtnClose_Click(object? sender, EventArgs e)
    {
        SetHintDismissed(RelativeSetup);
        ModAnimation.AniDispose(this, false);
    }

    private void MyHint_MouseUp(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        if (!isMouseDown)
            return;
        isMouseDown = false;
        Trace.TraceInformation("[Control] 按下提示条" + (string.IsNullOrEmpty(Name) ? "" : "：" + Name));
        e.Handled = true;
        Click?.Invoke(sender, e);
    }

    private void MyHint_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            isMouseDown = true;
    }

    private void MyHint_MouseLeave()
    {
        isMouseDown = false;
    }

    private void _ThemeChanged(object? sender, EventArgs e)
    {
        UpdateUI();
    }

    private void Dispose()
    {
        if (Application.Current is { } application)
            application.ActualThemeVariantChanged -= _ThemeChanged;
    }

    // 关闭按钮的“不再显示”记录保存在 AppSettings 中，属性名即 WPF 版的配置键名
    private static PropertyInfo? GetHintSettingProperty(string key)
    {
        return string.IsNullOrEmpty(key)
            ? null
            : typeof(AppSettings).GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
    }

    private static bool IsHintDismissed(string key)
    {
        var property = GetHintSettingProperty(key);
        if (property is null)
            return false;
        return property.GetValue(AppSettings.Load()) switch
        {
            null => false,
            bool value => value,
            _ => true
        };
    }

    private static void SetHintDismissed(string key)
    {
        var property = GetHintSettingProperty(key);
        if (property is null)
            return;
        var settings = AppSettings.Load();
        property.SetValue(settings, true);
        settings.Save();
    }
}
