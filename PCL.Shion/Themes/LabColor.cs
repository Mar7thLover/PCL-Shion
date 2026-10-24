using System;
using Avalonia.Media;

namespace PCL.Shion.Themes;

/// <summary>
/// 基于 OKLab 色彩空间的颜色定义，移植自 WPF 版 PCL.Core.UI.Theme.LabColor。
/// </summary>
public sealed class LabColor
{
    /// <summary>
    /// Light channel.
    /// </summary>
    public double L { get; }

    /// <summary>
    /// A channel.
    /// </summary>
    public double A { get; }

    /// <summary>
    /// B channel.
    /// </summary>
    public double B { get; }

    /// <summary>
    /// Alpha channel.
    /// </summary>
    public double Alpha { get; }

    private LabColor(double l, double a, double b, double alpha)
    {
        L = l;
        A = a;
        B = b;
        Alpha = alpha;
    }

    /// <summary>
    /// Create a <see cref="LabColor"/> instance.
    /// </summary>
    public static LabColor Create(double l, double a, double b, double alpha = 1.0) => new(l, a, b, alpha);

    /// <summary>
    /// Create a <see cref="LabColor"/> instance from OKLCH values.
    /// </summary>
    public static LabColor FromLch(double l, double c = 0, double h = 0, double alpha = 1.0)
    {
        var radian = h * Math.PI / 180d;
        return new LabColor(l, c * Math.Cos(radian), c * Math.Sin(radian), alpha);
    }

    /// <summary>
    /// Convert to ScRGB values, the chroma is reduced to fit the sRGB gamut when needed.
    /// </summary>
    public (float Alpha, float Red, float Green, float Blue) ToScRgb()
    {
        var (red, green, blue) = ToLinearRgb(L, A, B);
        if (!IsInGamut(red, green, blue))
            (red, green, blue) = ReduceChromaToGamut();
        return ((float)Alpha, (float)Math.Clamp(red, 0d, 1d), (float)Math.Clamp(green, 0d, 1d),
            (float)Math.Clamp(blue, 0d, 1d));
    }

    /// <summary>
    /// Convert to <see cref="CatColorResource"/>.
    /// </summary>
    /// <param name="suffix">See <see cref="CatColorResource.Suffix"/></param>
    /// <param name="applyToColor">See <see cref="CatColorResource.ApplyToColor"/></param>
    /// <param name="applyToBrush">See <see cref="CatColorResource.ApplyToBrush"/></param>
    public CatColorResource ToCatColor(string suffix, bool applyToColor = true, bool applyToBrush = true)
    {
        var (a, r, g, b) = ToScRgb();
        return new CatColorResource
        {
            Red = r,
            Green = g,
            Blue = b,
            Alpha = a,
            ApplyToBrush = applyToBrush,
            ApplyToColor = applyToColor,
            Suffix = suffix
        };
    }

    /// <summary>
    /// 将 ScRGB 线性值转换为 Avalonia 颜色（sRGB 编码）。
    /// </summary>
    public static Color ScRgbToColor(float alpha, float red, float green, float blue) => Color.FromArgb(
        ToByte(Math.Clamp(alpha, 0f, 1f) * 255d),
        ToSrgbByte(red),
        ToSrgbByte(green),
        ToSrgbByte(blue));

    /// <summary>
    /// 转换为 Avalonia 颜色。
    /// </summary>
    public Color ToAvaloniaColor()
    {
        var (a, r, g, b) = ToScRgb();
        return ScRgbToColor(a, r, g, b);
    }

    private (double Red, double Green, double Blue) ReduceChromaToGamut()
    {
        var chroma = Math.Sqrt(A * A + B * B);
        var hue = Math.Atan2(B, A);
        var low = 0d;
        var high = chroma;
        for (var i = 0; i < 24; i++)
        {
            var middle = (low + high) / 2d;
            var (r, g, b) = ToLinearRgb(L, middle * Math.Cos(hue), middle * Math.Sin(hue));
            if (IsInGamut(r, g, b)) low = middle;
            else high = middle;
        }

        return ToLinearRgb(L, low * Math.Cos(hue), low * Math.Sin(hue));
    }

    private static (double Red, double Green, double Blue) ToLinearRgb(double l, double a, double b)
    {
        var lp = l + 0.3963377774d * a + 0.2158037573d * b;
        var mp = l - 0.1055613458d * a - 0.0638541728d * b;
        var sp = l - 0.0894841775d * a - 1.2914855480d * b;
        var l3 = lp * lp * lp;
        var m3 = mp * mp * mp;
        var s3 = sp * sp * sp;
        return (
            4.0767416621d * l3 - 3.3077115913d * m3 + 0.2309699292d * s3,
            -1.2684380046d * l3 + 2.6097574011d * m3 - 0.3413193965d * s3,
            -0.0041960863d * l3 - 0.7034186147d * m3 + 1.7076147010d * s3);
    }

    private static bool IsInGamut(double red, double green, double blue)
    {
        const double tolerance = 1e-6d;
        return IsInRange(red, tolerance) && IsInRange(green, tolerance) && IsInRange(blue, tolerance);
    }

    private static bool IsInRange(double value, double tolerance) =>
        value >= -tolerance && value <= 1d + tolerance;

    private static byte ToSrgbByte(double linear)
    {
        var x = Math.Clamp(linear, 0d, 1d);
        var encoded = x <= 0.0031308d ? 12.92d * x : 1.055d * Math.Pow(x, 1d / 2.4d) - 0.055d;
        return ToByte(encoded * 255d);
    }

    private static byte ToByte(double value) => (byte)Math.Round(Math.Clamp(value, 0d, 255d));
}
