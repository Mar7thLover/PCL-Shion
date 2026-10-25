using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Themes;

public static class ThemeManager
{
    public static bool IsDarkMode => ThemeService.IsDarkMode;

    public static IResourceDictionary? AppResources => Application.Current?.Resources;

    public static MyColor colorGray1 = new(GetResource("ColorObjectGray1"));
    public static MyColor colorGray4 = new(GetResource("ColorObjectGray4"));
    public static MyColor colorGray5 = new(GetResource("ColorObjectGray5"));
    public static MyColor colorSemiTransparent = new(GetResource("ColorBrushSemiTransparent"));

    public static void ThemeRefresh(int newTheme = -1)
    {
        colorGray1 = new MyColor(GetResource("ColorObjectGray1"));
        colorGray4 = new MyColor(GetResource("ColorObjectGray4"));
        colorGray5 = new MyColor(GetResource("ColorObjectGray5"));
        colorSemiTransparent = new MyColor(GetResource("ColorBrushSemiTransparent"));
        ThemeRefreshMain();
    }

    public static void ThemeRefreshMain()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (MainWindow.Current is not { IsLoaded: true }) return;
            RefreshBackground();
        });
    }

    // 主页面背景
    private static void RefreshBackground()
    {
        var window = MainWindow.Current;
        if (window is null) return;
        window.PanForm.Background = ThemeService.Settings.BackgroundColorful
            ? CreateBackgroundBrush(ThemeService.Settings.BackgroundGradientStyle)
            : GetResource("ColorBrushBackground") as IBrush;
    }

    /// <summary>
    /// 按渐变样式生成主页面的彩色背景，颜色取自当前主题色相与色彩属性。
    /// </summary>
    public static IBrush CreateBackgroundBrush(BackgroundGradientStyle style)
    {
        var hue = ThemeService.GetCurrentThemeArgs().Hue;
        var tone = ThemeService.CurrentTone;
        var darkLight = IsDarkMode ? 0.2d : 1d;
        Color Lch(double l, double c, double h) => LabColor.FromLch(l * darkLight, c, h).ToAvaloniaColor();

        switch (style)
        {
            case BackgroundGradientStyle.Aurora:
            {
                var brush = DiagonalBrush();
                brush.GradientStops.Add(new GradientStop(Lch(0.86d, tone.C5 * 1.2d, hue - 50), 0d));
                brush.GradientStops.Add(new GradientStop(Lch(0.95d, tone.C7 * 1.6d, hue), 0.35d));
                brush.GradientStops.Add(new GradientStop(Lch(0.9d, tone.C5, hue + 45), 0.7d));
                brush.GradientStops.Add(new GradientStop(Lch(0.86d, tone.C5 * 1.1d, hue + 90), 1d));
                return brush;
            }
            case BackgroundGradientStyle.Sunset:
            {
                // 暖色固定为桃色，中段色相沿色环较短的一侧过渡
                const double warmHue = 40;
                var brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative)
                };
                brush.GradientStops.Add(new GradientStop(Lch(0.95d, tone.C7 * 1.4d, hue), 0d));
                brush.GradientStops.Add(new GradientStop(Lch(0.93d, tone.C6 * 1.4d, LerpHue(hue, warmHue, 0.5d)), 0.55d));
                brush.GradientStops.Add(new GradientStop(Lch(0.88d, tone.C5, warmHue), 1d));
                return brush;
            }
            case BackgroundGradientStyle.Halo:
            {
                var origin = new RelativePoint(0.8, 0.2, RelativeUnit.Relative);
                var brush = new RadialGradientBrush
                {
                    Center = origin,
                    GradientOrigin = origin,
                    RadiusX = new RelativeScalar(0.9, RelativeUnit.Relative),
                    RadiusY = new RelativeScalar(0.9, RelativeUnit.Relative)
                };
                brush.GradientStops.Add(new GradientStop(Lch(0.82d, tone.C5 * 1.3d, hue), 0d));
                brush.GradientStops.Add(new GradientStop(Lch(0.93d, tone.C7 * 1.8d, hue + 10), 0.5d));
                brush.GradientStops.Add(new GradientStop(Lch(0.985d, tone.C8, hue + 20), 1d));
                return brush;
            }
            default:
            {
                var brush = DiagonalBrush();
                var stop1 = Lch(0.84d, tone.C5, hue - 15);
                var stop2 = Lch(0.96d, tone.C7, hue);
                var stop3 = Lch(0.84d, tone.C5, hue + 15);
                brush.GradientStops.Add(new GradientStop(LerpColor(stop1, stop2, 0.2d), 0d));
                brush.GradientStops.Add(new GradientStop(stop2, 0.4d));
                brush.GradientStops.Add(new GradientStop(LerpColor(stop2, stop3, 0.6d / 0.7d), 1d));
                return brush;
            }
        }
    }

    private static LinearGradientBrush DiagonalBrush() => new()
    {
        EndPoint = new RelativePoint(0.1, 1, RelativeUnit.Relative),
        StartPoint = new RelativePoint(0.9, 0, RelativeUnit.Relative)
    };

    /// <summary>
    /// 沿色环较短的一侧在两个色相之间插值。
    /// </summary>
    private static double LerpHue(double from, double to, double t)
    {
        var delta = ((to - from) % 360 + 540) % 360 - 180;
        return from + delta * t;
    }

    private static Color LerpColor(Color from, Color to, double t) => Color.FromArgb(
        LerpByte(from.A, to.A, t),
        LerpByte(from.R, to.R, t),
        LerpByte(from.G, to.G, t),
        LerpByte(from.B, to.B, t));

    private static byte LerpByte(byte from, byte to, double t) =>
        (byte)Math.Round(Math.Clamp(from + (to - from) * t, 0d, 255d));

    private static object? GetResource(string key) =>
        Application.Current is { } app && app.TryGetResource(key, null, out var value) ? value : null;
}
