namespace PCL.Shion.Themes;

/// <summary>
/// 主题模式（亮/暗/系统）
/// </summary>
public enum ColorMode
{
    Light = 0,
    Dark = 1,
    System = 2
}

/// <summary>
/// 配色主题。数值会写入设置文件，新增项只能追加在末尾。
/// </summary>
public enum ColorTheme
{
    SkyBlue = 0,
    CatBlue = 1,
    DeathBlue = 2,
    HmclBlue = 3,
    SakuraPink = 4,
    LavenderPurple = 5,
    MintGreen = 6,
    ForestGreen = 7,
    AmberOrange = 8,
    CrimsonRed = 9,
    Custom = 10
}

/// <summary>
/// 彩色背景的渐变样式。
/// </summary>
public enum BackgroundGradientStyle
{
    /// <summary>沿对角线的同色系渐变，色相偏移较小。</summary>
    Classic = 0,

    /// <summary>色相跨度较大的多段渐变。</summary>
    Aurora = 1,

    /// <summary>自上而下由主题色过渡到暖色。</summary>
    Sunset = 2,

    /// <summary>从右上方向外扩散的径向光晕。</summary>
    Halo = 3
}
