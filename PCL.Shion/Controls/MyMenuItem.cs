using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PCL.Shion.Services;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public class MyMenuItem : MenuItem
{
    // 指向动画

    private const int AnimationTimeIn = 100;
    private const int AnimationTimeOut = 200;

    public static readonly StyledProperty<string> SvgIconProperty = AvaloniaProperty.Register<MyMenuItem, string>(
        nameof(SvgIcon),
        string.Empty);

    private SvgIcon? _svgIconControl;
    private string? _colorName;
    private Path? _iconControl;

    // 基础

    private static int _uuidSeed;

    public int Uuid = GetUuid();

    public MyMenuItem()
    {
        Loaded += MyMenuItem_Loaded;
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        this.GetObservable(IsEnabledProperty).Subscribe(new Avalonia.Reactive.AnonymousObserver<bool>(_ => RefreshColor()));
    }

    public string SvgIcon
    {
        get => GetValue(SvgIconProperty);
        set => SetValue(SvgIconProperty, value);
    }

    private static int GetUuid()
    {
        return Interlocked.Increment(ref _uuidSeed);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SvgIconProperty)
            OnSvgIconChanged(this, change);
    }

    private static void OnSvgIconChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
    {
        if (d is MyMenuItem { IsLoaded: true } item)
            item.UpdateTemplateIcon();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _iconControl = e.NameScope.Find("Icon") as Path;
    }

    private (string BackName, string ForeName, int Time) GetVisualState()
    {
        if (!IsEnabled)
            return ("ColorBrushTransparent", "ColorBrushGray5", AnimationTimeOut);
        if (IsPointerOver)
            return ("ColorBrush6", "ColorBrush2", AnimationTimeIn);
        return ("ColorBrushTransparent", "ColorBrush1", AnimationTimeOut);
    }

    private void MyMenuItem_Loaded(object? sender, EventArgs e)
    {
        UpdateTemplateIcon();
        ((ContextMenu)Parent!).Opacity = AppSettings.Load().WindowOpacity / 1000.0 + 0.4;
    }

    private void UpdateTemplateIcon()
    {
        var iconControl = _iconControl;
        if (iconControl is null)
            return;

        if (SvgIconControlHelper.HasSvgIcon(SvgIcon))
        {
            iconControl.IsVisible = false;
            EnsureSvgIconControl(iconControl);
            _svgIconControl!.Icon = SvgIcon;
            _svgIconControl.IsVisible = true;
            return;
        }

        if (_svgIconControl is not null)
            _svgIconControl.IsVisible = false;
        if (Icon is null) return;

        iconControl.IsVisible = true;
        iconControl.Data = Geometry.Parse(Icon.ToString()!);
    }

    private void EnsureSvgIconControl(Path iconControl)
    {
        if (_svgIconControl is not null)
            return;

        _svgIconControl = new SvgIcon
        {
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
            Margin = iconControl.Margin,
            Height = iconControl.Height,
            Width = iconControl.Width,
            IsHitTestVisible = false,
            IsVisible = false
        };
        _svgIconControl.Bind(global::PCL.Shion.Controls.SvgIcon.IconBrushProperty,
            this.GetObservable(ForegroundProperty));

        if (iconControl.GetVisualParent() is not Grid grid) return;

        Grid.SetColumn(_svgIconControl, Grid.GetColumn(iconControl));
        Grid.SetRow(_svgIconControl, Grid.GetRow(iconControl));
        grid.Children.Add(_svgIconControl);
    }

    private void RefreshColor()
    {
        var (backName, foreName, time) = GetVisualState();

        // 重复性验证
        if ((_colorName ?? "") == (backName ?? ""))
            return;
        _colorName = backName;
        // 触发颜色动画
        if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
        {
            // 有动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, BackgroundProperty, backName, time),
                    ModAnimation.AaColor(this, ForegroundProperty, foreName, time)
                }, "MyMenuItem Color " + Uuid);
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("MyMenuItem Color " + Uuid);
            this.SetResourceReference(BackgroundProperty, backName);
            this.SetResourceReference(ForegroundProperty, foreName);
        }
    }
}
