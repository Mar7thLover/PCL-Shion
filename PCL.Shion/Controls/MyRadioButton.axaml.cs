using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using PCL.Shion.Localization;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public partial class MyRadioButton : Border
{
    public delegate void ChangeEventHandler(MyRadioButton sender, bool raiseByMouse);

    public delegate void CheckEventHandler(MyRadioButton sender, bool raiseByMouse);

    public delegate void PreviewClickEventHandler(object sender, RouteEventArgs e);

    public enum ColorState
    {
        White,
        Highlight
    }

    // 动画

    private const int animationTimeOfMouseIn = 90; // 鼠标指向动画长度
    private const int animationTimeOfMouseOut = 150; // 鼠标移出动画长度
    private const int animationTimeOfCheck = 120; // 勾选状态变更动画长度

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyRadioButton, string?>(nameof(Text));

    private bool _Checked; // 是否选中
    private bool _hasLegacyLogo;
    private bool isMouseDown;
    private static int _uuidSeed;

    // 基础

    public int Uuid = GetUuid();

    public MyRadioButton()
    {
        InitializeComponent();
        RefreshLogoHostVisibility();

        PropertyChanged += (_, e) =>
        {
            if (e.Property == TextProperty)
                LabText.Text = e.GetNewValue<string?>();
        };

        Loaded += (_, _) =>
        {
            LabText?.Text = GetValue(TextProperty);

            if (Application.Current is { } application)
            {
                application.ActualThemeVariantChanged += OnColorModeChanged;
                application.ResourcesChanged += OnColorThemeChanged;
            }
        };

        Unloaded += (_, _) =>
        {
            if (Application.Current is { } application)
            {
                application.ActualThemeVariantChanged -= OnColorModeChanged;
                application.ResourcesChanged -= OnColorThemeChanged;
            }
        };

        PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
                Radiobox_MouseUp();
        };
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                Radiobox_MouseDown();
        };
        PointerExited += (_, _) => Radiobox_MouseLeave();
        PointerEntered += RefreshColor;
        PointerExited += RefreshColor;
        Loaded += RefreshColor;
    }

    private void OnColorModeChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(RefreshMyRadioButtonColor);
    }

    private void OnColorThemeChanged(object? sender, ResourcesChangedEventArgs e)
    {
        Dispatcher.UIThread.Invoke(RefreshMyRadioButtonColor);
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
                ? Geometry.Parse(value)
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
        }
        else
        {
            LogoHost.IsVisible = true;
            LogoHost.Width = 0;
        }

        LogoHost.Height = 16;
        LogoHost.Margin = new Thickness(12, 0, 0, 0);
        LabText.Margin = new Thickness(8, 0, 12, 0);
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

    public bool Checked
    {
        get => _Checked;
        set => SetChecked(value, false, true);
    }

    [Content]
    public InlineCollection Inlines => LabText.Inlines;

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 内容

    public ColorState ColorType
    {
        get;
        set
        {
            field = value;
            RefreshColor();
        }
    } = ColorState.White; // 颜色类别

    public event CheckEventHandler? Check;

    /// <summary>
    ///     手动设置 Checked 属性。
    /// </summary>
    /// <param name="value">新的 Checked 属性。</param>
    /// <param name="raiseByMouse">是否由用户引发。</param>
    /// <param name="anime">是否执行动画。</param>
    public void SetChecked(bool value, bool raiseByMouse, bool anime)
    {
        try
        {
            // 自定义属性基础

            var isChanged = false;
            if (_Checked != value)
            {
                _Checked = value;
                isChanged = true;
            }

            // 保证只有一个单选框选中

            if (Parent is null) return;
            var radioboxList = new List<MyRadioButton>();
            var checkedCount = 0;
            // 收集控件列表与选中个数
            foreach (var Control in ((Panel)Parent).Children)
                if (Control is MyRadioButton radioButton)
                {
                    radioboxList.Add(radioButton);
                    if (radioButton.Checked)
                        checkedCount += 1;
                }

            // 判断选中情况
            switch (checkedCount)
            {
                case 0:
                {
                    // 没有任何单选框被选中，选择第一个
                    radioboxList[0].Checked = true;
                    break;
                }
                case var @case when @case > 1:
                {
                    // 选中项目多于 1 个
                    if (Checked)
                    {
                        // 如果本控件选中，则取消其他所有控件的选中
                        foreach (var Control in radioboxList)
                            if (Control.Checked && !Control.Equals(this))
                                Control.Checked = false;
                    }
                    else
                    {
                        // 如果本控件未选中，则只保留第一个选中的控件
                        var firstChecked = false;
                        foreach (var Control in radioboxList)
                            if (Control.Checked)
                            {
                                if (firstChecked)
                                    Control.Checked = false; // 修改 Checked 会自动触发 Change 事件，所以不用额外触发
                                else
                                    firstChecked = true;
                            }
                    }

                    break;
                }
            }

            // 更改动画

            if (!isChanged)
                return;
            RefreshColor(null, anime);

            // 触发事件
            if (Checked)
                Check?.Invoke(this, raiseByMouse);
        }

        catch (Exception ex)
        {
            Trace.TraceError("单选按钮勾选改变错误：" + ex);
            Hint.Show(Lang.Text("Application.Control.Error.OperationFailed"), HintType.Error);
        }
    }

    // 点击事件

    public event PreviewClickEventHandler? PreviewClick;

    private void Radiobox_MouseUp()
    {
        if (Checked)
            return;
        if (!isMouseDown)
            return;
        Logger.TryGet(LogEventLevel.Debug, LogArea.Control)?.Log(this, "[Control] 按下单选按钮：{Text}", Text);
        isMouseDown = false;
        var e = new RouteEventArgs(true);
        PreviewClick?.Invoke(this, e);
        if (e.handled)
            return;
        SetChecked(true, true, true);
    }

    private void Radiobox_MouseDown()
    {
        if (Checked)
            return;
        isMouseDown = true;
        RefreshColor();
    }

    private void Radiobox_MouseLeave()
    {
        isMouseDown = false;
    }

    private static int GetUuid() => Interlocked.Increment(ref _uuidSeed);

    private object? ResolveResource(string key)
    {
        return this.TryFindResource(key, ActualThemeVariant, out var resource) ? resource : null;
    }

    private void RefreshColor(object? obj = null, object? e = null)
    {
        try
        {
            if (IsLoaded && ModAnimation.AniControlEnabled == 0 &&
                !false.Equals(e)) // 防止默认属性变更触发动画，若强制不执行动画，则 e 为 False
            {
                switch (ColorType)
                {
                    case ColorState.White:
                    {
                        if (Checked)
                        {
                            // 勾选
                            var color3 = new MyColor(ResolveResource("ColorObject3"));
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, color3 - ShapeLogo.Fill,
                                        animationTimeOfCheck),
                                    ModAnimation.AaColor(LabText, TextElement.ForegroundProperty,
                                        color3 - LabText.Foreground, animationTimeOfCheck)
                                }, "MyRadioButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    new MyColor(255d, 255d, 255d) - Background, animationTimeOfCheck),
                                "MyRadioButton Color " + Uuid);
                        }
                        else if (isMouseDown)
                        {
                            // 按下
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    new MyColor(120d,
                                        new MyColor(ResolveResource("ColorObject8"))) - Background, 60),
                                "MyRadioButton Color " + Uuid);
                        }
                        else if (IsPointerOver)
                        {
                            // 指向
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty,
                                        new MyColor(255d, 255d, 255d) - ShapeLogo.Fill, animationTimeOfMouseIn),
                                    ModAnimation.AaColor(LabText, TextElement.ForegroundProperty,
                                        new MyColor(255d, 255d, 255d) - LabText.Foreground,
                                        animationTimeOfMouseIn)
                                }, "MyRadioButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    new MyColor(50d,
                                        new MyColor(ResolveResource("ColorObject8"))) - Background,
                                    animationTimeOfMouseIn), "MyRadioButton Color " + Uuid);
                        }
                        else
                        {
                            // 正常
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty,
                                        new MyColor(255d, 255d, 255d) - ShapeLogo.Fill,
                                        animationTimeOfMouseOut),
                                    ModAnimation.AaColor(LabText, TextElement.ForegroundProperty,
                                        new MyColor(255d, 255d, 255d) - LabText.Foreground,
                                        animationTimeOfMouseOut)
                                }, "MyRadioButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    new MyColor(ResolveResource("ColorBrushSemiTransparent")) -
                                    Background, animationTimeOfMouseOut), "MyRadioButton Color " + Uuid);
                        }

                        break;
                    }
                    case ColorState.Highlight:
                    {
                        if (Checked)
                        {
                            // 勾选
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty,
                                        new MyColor(255d, 255d, 255d) - ShapeLogo.Fill, animationTimeOfCheck),
                                    ModAnimation.AaColor(LabText, TextElement.ForegroundProperty,
                                        new MyColor(255d, 255d, 255d) - LabText.Foreground,
                                        animationTimeOfCheck)
                                }, "MyRadioButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty, "ColorBrush3", animationTimeOfCheck),
                                "MyRadioButton Color " + Uuid);
                        }
                        else if (isMouseDown)
                        {
                            // 按下
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty, "ColorBrush6", animationTimeOfMouseIn),
                                "MyRadioButton Color " + Uuid);
                        }
                        else if (IsPointerOver)
                        {
                            // 指向
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, "ColorBrush3",
                                        animationTimeOfMouseIn),
                                    ModAnimation.AaColor(LabText, TextElement.ForegroundProperty, "ColorBrush3",
                                        animationTimeOfMouseIn)
                                }, "MyRadioButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty, "ColorBrush7", animationTimeOfMouseIn),
                                "MyRadioButton Color " + Uuid);
                        }
                        else
                        {
                            // 正常
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, "ColorBrush3",
                                        animationTimeOfMouseOut),
                                    ModAnimation.AaColor(LabText, TextElement.ForegroundProperty, "ColorBrush3",
                                        animationTimeOfMouseOut)
                                }, "MyRadioButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    new MyColor(ResolveResource("ColorBrushSemiTransparent")) -
                                    Background, animationTimeOfMouseOut), "MyRadioButton Color " + Uuid);
                        }

                        break;
                    }
                }
            }

            else
            {
                // 不使用动画
                ModAnimation.AniStop("MyRadioButton Checked " + Uuid);
                ModAnimation.AniStop("MyRadioButton Color " + Uuid);
                switch (ColorType)
                {
                    case ColorState.White:
                    {
                        if (Checked)
                        {
                            ResourceReferenceExtensions.SetValueReplacingBinding(this, BackgroundProperty,
                                (SolidColorBrush)new MyColor(255d, 255d, 255d));
                            ShapeLogo.SetResourceReference(Shape.FillProperty, "ColorBrush3");
                            LabText.SetResourceReference(TextElement.ForegroundProperty, "ColorBrush3");
                        }
                        else
                        {
                            ResourceReferenceExtensions.SetValueReplacingBinding(this, BackgroundProperty,
                                ResolveResource("ColorBrushSemiTransparent"));
                            ResourceReferenceExtensions.SetValueReplacingBinding(ShapeLogo, Shape.FillProperty,
                                (SolidColorBrush)new MyColor(255d, 255d, 255d));
                            ResourceReferenceExtensions.SetValueReplacingBinding(LabText,
                                TextElement.ForegroundProperty, (SolidColorBrush)new MyColor(255d, 255d, 255d));
                        }

                        break;
                    }
                    case ColorState.Highlight:
                    {
                        if (Checked)
                        {
                            this.SetResourceReference(BackgroundProperty, "ColorBrush3");
                            ResourceReferenceExtensions.SetValueReplacingBinding(ShapeLogo, Shape.FillProperty,
                                (SolidColorBrush)new MyColor(255d, 255d, 255d));
                            ResourceReferenceExtensions.SetValueReplacingBinding(LabText,
                                TextElement.ForegroundProperty, (SolidColorBrush)new MyColor(255d, 255d, 255d));
                        }
                        else
                        {
                            ResourceReferenceExtensions.SetValueReplacingBinding(this, BackgroundProperty,
                                ResolveResource("ColorBrushSemiTransparent"));
                            ShapeLogo.SetResourceReference(Shape.FillProperty, "ColorBrush3");
                            LabText.SetResourceReference(TextElement.ForegroundProperty, "ColorBrush3");
                        }

                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("刷新单选按钮颜色出错：" + ex);
        }
    }

    public void RefreshMyRadioButtonColor()
    {
        RefreshColor();
    }
}
