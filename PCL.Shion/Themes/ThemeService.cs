using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using PCL.Shion.Services;

namespace PCL.Shion.Themes;

/// <summary>
/// 配色模式更改事件。
/// </summary>
/// <param name="isDarkMode">当前是否为深色模式</param>
/// <param name="theme">当前配色主题</param>
public delegate void ColorModeChangedEvent(bool isDarkMode, ColorTheme theme);

/// <summary>
/// 配色主题更改事件。
/// </summary>
/// <param name="theme">当前配色主题</param>
public delegate void ColorThemeChangedEvent(ColorTheme theme);

public static class ThemeService
{
    private static AppSettings? _settings;
    private static CatColorResource[]? _lightGrayCache;
    private static CatColorResource[]? _darkGrayCache;
    private static bool _started;

    /// <summary>
    /// 配色属性配置。
    /// </summary>
    public static ToneProfileConfig ToneProfiles { get; set; } = new();

    /// <summary>
    /// 启动器设置，配色模式与配色主题保存在其中。
    /// </summary>
    public static AppSettings Settings
    {
        get => _settings ??= AppSettings.Load();
        set => _settings = value;
    }

    /// <summary>
    /// 当前是否为深色主题。
    /// </summary>
    public static bool IsDarkMode { get; private set; }

    /// <summary>
    /// 配色模式更改事件。
    /// </summary>
    public static event ColorModeChangedEvent? ColorModeChanged;

    /// <summary>
    /// 配色主题更改事件。
    /// </summary>
    public static event ColorThemeChangedEvent? ColorThemeChanged;

    /// <summary>
    /// 愚人节时需要刷新窗口标题时触发。
    /// </summary>
    public static event Action? WindowTitleRefreshRequested;

    /// <summary>
    /// 当前使用的色彩属性。
    /// </summary>
    public static ToneProfile CurrentTone => IsDarkMode ? ToneProfiles.Dark : ToneProfiles.Light;

    /// <summary>
    /// 当前使用的主题色。
    /// </summary>
    public static ColorTheme CurrentTheme
    {
        get => IsDarkMode ? Settings.DarkColor : Settings.LightColor;
        set
        {
            var oldValue = CurrentTheme;
            if (oldValue == value) return;
            if (IsDarkMode) Settings.DarkColor = value;
            else Settings.LightColor = value;
            Settings.Save();
            if (!_started) return;
            ApplyColorResources();
            ColorThemeChanged?.Invoke(CurrentTheme);
            _AprilFoolLogic();
        }
    }

    /// <summary>
    /// 启动配色服务，在应用程序创建窗口前调用。
    /// </summary>
    public static void Initialize()
    {
        IsDarkMode = _IsDarkMode();
        _RefreshAll();
        _started = true;
    }

    private static bool _IsDarkMode() => Settings.ColorMode switch
    {
        ColorMode.Light => false,
        ColorMode.Dark => true,
        ColorMode.System => _IsSystemInDarkMode(),
        _ => false
    };

    private static bool _IsSystemInDarkMode() =>
        Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;

    private static void _RefreshAll()
    {
        ApplyGrayResources();
        ApplyColorResources();
        _ApplyThemeVariant();
        ColorModeChanged?.Invoke(IsDarkMode, CurrentTheme);
        _AprilFoolLogic();
    }

    private static void _ApplyThemeVariant()
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private static void _AprilFoolLogic()
    {
        // for HMCL theme on April Fools' Day
        if (DateTime.Now is { Month: 4, Day: 1 }) WindowTitleRefreshRequested?.Invoke();
        else if (CurrentTheme == ColorTheme.HmclBlue) CurrentTheme = ColorTheme.CatBlue;
    }

    /// <summary>
    /// 刷新配色模式，若检测到当前配色模式有实际更改，则会触发主题刷新。
    /// </summary>
    public static void RefreshColorMode()
    {
        var isDarkMode = _IsDarkMode();
        if (IsDarkMode == isDarkMode) return;
        IsDarkMode = isDarkMode;
        if (_started)
            Dispatcher.UIThread.Post(_RefreshAll);
    }

    /// <summary>
    /// 设置页中可供选择的配色主题，按显示顺序排列。HMCL 配色只在愚人节出现。
    /// </summary>
    public static ColorTheme[] GetSelectableThemes(bool includeAprilFool) =>
    [
        ColorTheme.SkyBlue,
        ColorTheme.CatBlue,
        ColorTheme.DeathBlue,
        .. includeAprilFool ? [ColorTheme.HmclBlue] : (ColorTheme[])[],
        ColorTheme.SakuraPink,
        ColorTheme.LavenderPurple,
        ColorTheme.MintGreen,
        ColorTheme.ForestGreen,
        ColorTheme.AmberOrange,
        ColorTheme.CrimsonRed,
        ColorTheme.Custom
    ];

    /// <summary>
    /// 自定义配色的饱和度设置值（0 到 100）对应的彩度调整量，50 为不调整。
    /// </summary>
    public static double CustomChromaAdjust(int saturation) => Math.Clamp(saturation, 0, 100) / 100d - 0.5;

    /// <summary>
    /// 获取当前色彩主题对应的各种参数。
    /// </summary>
    public static (int Hue, double LightAdjust, double ChromaAdjust) GetCurrentThemeArgs() => GetThemeArgs(CurrentTheme);

    /// <summary>
    /// 获取指定色彩主题对应的各种参数。色相为 OKLCH 色相角，调整量的含义见 <see cref="_AdjustLinear"/>。
    /// </summary>
    public static (int Hue, double LightAdjust, double ChromaAdjust) GetThemeArgs(ColorTheme theme)
    {
        return theme switch
        {
            ColorTheme.SkyBlue => (235, 0.36, 0.2),
            ColorTheme.CatBlue => (255, 0, -0.2),
            ColorTheme.DeathBlue => (268, -0.05, -0.1),
            ColorTheme.HmclBlue => (275, -0.03, -0.35),
            ColorTheme.SakuraPink => (355, 0.12, -0.15),
            ColorTheme.LavenderPurple => (300, 0.05, -0.2),
            ColorTheme.MintGreen => (170, 0.2, -0.15),
            ColorTheme.ForestGreen => (150, -0.08, -0.25),
            ColorTheme.AmberOrange => (62, 0.22, -0.05),
            ColorTheme.CrimsonRed => (22, -0.04, -0.1),
            ColorTheme.Custom => (((Settings.ThemeCustomHue % 360) + 360) % 360, 0,
                CustomChromaAdjust(Settings.ThemeCustomSaturation)),
#if DEBUG
            _ => ((int)theme, 0, 0)
#else
            _ => throw new IndexOutOfRangeException($"Invalid theme index: {(int)theme}")
#endif
        };
    }

    private static double _AdjustLinear(double value, double adjustment)
    {
        if (adjustment == 0) return value;
        // 确保输入在合理范围内
        value = Math.Clamp(value, 0.0, 1.0);
        adjustment = Math.Clamp(adjustment, -1.0, 1.0);
        // 非对称线性插值
        return adjustment switch
        {
            > 0 => value + (1.0 - value) * adjustment,
            _ => value + value * adjustment
        };
    }

    private static CatColorResource[] _CalculateGrays(ToneProfile tone) =>
    [
        LabColor.FromLch(tone.L1).ToCatColor("Gray1"),
        LabColor.FromLch(tone.L2).ToCatColor("Gray2"),
        LabColor.FromLch(tone.L3).ToCatColor("Gray3"),
        LabColor.FromLch(tone.L4).ToCatColor("Gray4"),
        LabColor.FromLch(tone.L5).ToCatColor("Gray5"),
        LabColor.FromLch(tone.L6).ToCatColor("Gray6"),
        LabColor.FromLch(tone.L7).ToCatColor("Gray7"),
        LabColor.FromLch(tone.L8).ToCatColor("Gray8"),
        LabColor.FromLch(tone.LWhite, alpha: tone.AHalfWhite).ToCatColor("HalfWhite", false),
        LabColor.FromLch(tone.LWhite, alpha: tone.ASemiWhite).ToCatColor("SemiWhite", false),
        LabColor.FromLch(tone.LWhite).ToCatColor("White", false),
        LabColor.FromLch(tone.LWhite, alpha: tone.ATransparent).ToCatColor("Transparent", false),
        LabColor.FromLch(tone.LBackground, alpha: tone.ABackground).ToCatColor("TransparentBackground", false),
        LabColor.FromLch(tone.LBackground).ToCatColor("Background", false),
        LabColor.FromLch(tone.LBackground, alpha: tone.AToolTip).ToCatColor("ToolTip", false),
        LabColor.FromLch(tone.L7, 0.25, 30, tone.AHalfTransparent).ToCatColor("RedBack", false),
        LabColor.FromLch(tone.LForeground).ToCatColor("Memory", false),
    ];

    private static CatColorResource[] _CalculateColors(ToneProfile tone, (int hue, double lightAdj, double chromaAdj) args) =>
    [
        LabColor.FromLch(_AdjustLinear(tone.L1, args.lightAdj * 0.1), _AdjustLinear(tone.C1, args.chromaAdj * 0.25), args.hue).ToCatColor("1"),
        LabColor.FromLch(_AdjustLinear(tone.L2, args.lightAdj), _AdjustLinear(tone.C2, args.chromaAdj), args.hue).ToCatColor("2"),
        LabColor.FromLch(_AdjustLinear(tone.L3, args.lightAdj), _AdjustLinear(tone.C3, args.chromaAdj), args.hue).ToCatColor("3"),
        LabColor.FromLch(_AdjustLinear(tone.L4, args.lightAdj), _AdjustLinear(tone.C4, args.chromaAdj), args.hue).ToCatColor("4"),
        LabColor.FromLch(_AdjustLinear(tone.L5, args.lightAdj), _AdjustLinear(tone.C5, args.chromaAdj), args.hue).ToCatColor("5"),
        LabColor.FromLch(_AdjustLinear(tone.L6, args.lightAdj), _AdjustLinear(tone.C6, args.chromaAdj), args.hue).ToCatColor("6"),
        LabColor.FromLch(_AdjustLinear(tone.L7, args.lightAdj), _AdjustLinear(tone.C7, args.chromaAdj), args.hue).ToCatColor("7"),
        LabColor.FromLch(_AdjustLinear(tone.L8, args.lightAdj), _AdjustLinear(tone.C8, args.chromaAdj), args.hue).ToCatColor("8"),
        LabColor.FromLch(_AdjustLinear(tone.L8, args.lightAdj), _AdjustLinear(tone.C8, args.chromaAdj), args.hue, tone.ASemiTransparent).ToCatColor("SemiTransparent", false),
        LabColor.FromLch(_AdjustLinear(tone.L5, args.lightAdj), _AdjustLinear(tone.C5, args.chromaAdj), args.hue).ToCatColor("Bg0"),
        LabColor.FromLch(_AdjustLinear(tone.L7, args.lightAdj), _AdjustLinear(tone.C7, args.chromaAdj), args.hue, tone.ASemiWhite).ToCatColor("Bg1"),
    ];

    private static CatColorResource[] LightGrayCache => _lightGrayCache ??= _CalculateGrays(ToneProfiles.Light);

    private static CatColorResource[] DarkGrayCache => _darkGrayCache ??= _CalculateGrays(ToneProfiles.Dark);

    /// <summary>
    /// 清除灰度配色的计算缓存。
    /// </summary>
    public static void InvalidateGrayCache()
    {
        _lightGrayCache = null;
        _darkGrayCache = null;
    }

    /// <summary>
    /// 应用灰度配色到资源字典。
    /// </summary>
    public static void ApplyGrayResources()
    {
        var cache = IsDarkMode ? DarkGrayCache : LightGrayCache;
        foreach (var c in cache) c.Apply();
    }

    /// <summary>
    /// 获取配色在指定模式下的预览色，分别与该配色的 Color2 与 Color4 相同，用于设置页的色块预览。
    /// </summary>
    public static (Color Deep, Color Light) GetThemePreviewColors(ColorTheme theme, bool isDarkMode)
    {
        var tone = isDarkMode ? ToneProfiles.Dark : ToneProfiles.Light;
        var (hue, lightAdj, chromaAdj) = GetThemeArgs(theme);
        return (
            LabColor.FromLch(_AdjustLinear(tone.L2, lightAdj), _AdjustLinear(tone.C2, chromaAdj), hue).ToAvaloniaColor(),
            LabColor.FromLch(_AdjustLinear(tone.L4, lightAdj), _AdjustLinear(tone.C4, chromaAdj), hue).ToAvaloniaColor());
    }

    /// <summary>
    /// 应用彩色配色到资源字典。
    /// </summary>
    public static void ApplyColorResources()
    {
        var colors = _CalculateColors(CurrentTone, GetCurrentThemeArgs());
        foreach (var c in colors) c.Apply();
    }
}
