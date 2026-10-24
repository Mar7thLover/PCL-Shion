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
        if (ThemeService.Settings.BackgroundColorful)
        {
            var brush = new LinearGradientBrush
            {
                EndPoint = new RelativePoint(0.1, 1, RelativeUnit.Relative),
                StartPoint = new RelativePoint(0.9, 0, RelativeUnit.Relative)
            };

            var hue = ThemeService.GetCurrentThemeArgs().Hue;
            var hue1 = hue - 15;
            var hue2 = hue + 15;
            var tone = ThemeService.CurrentTone;
            var darkLight = IsDarkMode ? 0.2d : 1d;
            var stop1 = LabColor.FromLch(0.84d * darkLight, tone.C5, hue1).ToAvaloniaColor();
            var stop2 = LabColor.FromLch(0.96d * darkLight, tone.C7, hue).ToAvaloniaColor();
            var stop3 = LabColor.FromLch(0.84d * darkLight, tone.C5, hue2).ToAvaloniaColor();
            brush.GradientStops.Add(new GradientStop(LerpColor(stop1, stop2, 0.2d), 0d));
            brush.GradientStops.Add(new GradientStop(stop2, 0.4d));
            brush.GradientStops.Add(new GradientStop(LerpColor(stop2, stop3, 0.6d / 0.7d), 1d));
            window.PanForm.Background = brush;
        }
        else
        {
            window.PanForm.Background = GetResource("ColorBrushBackground") as IBrush;
        }
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
