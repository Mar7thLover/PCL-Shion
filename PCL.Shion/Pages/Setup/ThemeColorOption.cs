using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Shion.Themes;

namespace PCL.Shion.Pages.Setup;

/// <summary>
///     设置页配色下拉框中的一个选项，带有该配色的色块预览。
/// </summary>
public sealed partial class ThemeColorOption : ObservableObject
{
    public ThemeColorOption(ColorTheme theme, string name, bool isDarkMode)
    {
        Theme = theme;
        Name = name;
        IsDarkMode = isDarkMode;
        RefreshSwatch();
    }

    public ColorTheme Theme { get; }

    public string Name { get; }

    /// <summary>
    ///     色块按深色还是浅色模式下的配色计算。
    /// </summary>
    public bool IsDarkMode { get; }

    [ObservableProperty]
    public partial IBrush Swatch { get; private set; } = Brushes.Transparent;

    /// <summary>
    ///     重新计算色块。自定义配色的色相或饱和度改变后需要调用。
    /// </summary>
    public void RefreshSwatch()
    {
        var (deep, light) = ThemeService.GetThemePreviewColors(Theme, IsDarkMode);
        Swatch = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(light, 0), new GradientStop(deep, 1) }
        };
    }

    public override string ToString() => Name;
}
