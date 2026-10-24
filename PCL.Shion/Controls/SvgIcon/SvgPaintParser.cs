using System;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace PCL.Shion.Controls;

internal static class SvgPaintParser
{
    /// <summary>
    /// 把 fill / stroke 的颜色字面量解析为纯色画刷。
    /// none、currentColor 与无法识别的值返回 null，调用方应改用图标画刷。
    /// </summary>
    public static IBrush? ParseColorBrush(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        if (text.Equals("none", StringComparison.OrdinalIgnoreCase)
            || text.Equals("currentColor", StringComparison.OrdinalIgnoreCase))
            return null;

        return TryParseColor(text, out var color) ? new ImmutableSolidColorBrush(color) : null;
    }

    private static bool TryParseColor(string text, out Color color)
    {
        if (text[0] == '#')
            return TryParseHex(text.AsSpan(1), out color);

        if (text.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            return TryParseRgbFunction(text, out color);

        return Color.TryParse(text, out color);
    }

    // CSS 十六进制写法：#RGB、#RGBA、#RRGGBB、#RRGGBBAA（透明度在末尾，与 Avalonia 自带的 #AARRGGBB 不同）
    private static bool TryParseHex(ReadOnlySpan<char> hex, out Color color)
    {
        color = default;
        if (hex.Length is not (3 or 4 or 6 or 8)
            || !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            return false;

        switch (hex.Length)
        {
            case 3:
                color = Color.FromRgb(Expand(value >> 8), Expand(value >> 4), Expand(value));
                break;
            case 4:
                color = Color.FromArgb(Expand(value), Expand(value >> 12), Expand(value >> 8), Expand(value >> 4));
                break;
            case 6:
                color = Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
                break;
            default:
                color = Color.FromArgb((byte)value, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8));
                break;
        }

        return true;
    }

    private static byte Expand(uint nibble)
    {
        return (byte)((nibble & 0xF) * 17);
    }

    private static bool TryParseRgbFunction(string value, out Color color)
    {
        color = default;

        var start = value.IndexOf('(');
        var end = value.LastIndexOf(')');
        if (start < 0 || end <= start)
            return false;

        var parts = value[(start + 1)..end]
            .Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 3)
            return false;

        var r = ParseColorComponent(parts[0]);
        var g = ParseColorComponent(parts[1]);
        var b = ParseColorComponent(parts[2]);
        if (r is null || g is null || b is null)
            return false;

        var a = parts.Length >= 4 ? ParseAlpha(parts[3]) : (byte)255;
        color = Color.FromArgb(a, r.Value, g.Value, b.Value);
        return true;
    }

    private static byte? ParseColorComponent(string value)
    {
        if (value.EndsWith('%'))
        {
            if (!double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                return null;

            return (byte)Math.Clamp(Math.Round(percent / 100D * 255D), 0D, 255D);
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw)
            ? (byte)Math.Clamp(Math.Round(raw), 0D, 255D)
            : null;
    }

    private static byte ParseAlpha(string value)
    {
        if (value.EndsWith('%'))
        {
            if (double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                return (byte)Math.Clamp(Math.Round(percent / 100D * 255D), 0D, 255D);

            return 255;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw)
            ? (byte)Math.Clamp(Math.Round(raw <= 1D ? raw * 255D : raw), 0D, 255D)
            : (byte)255;
    }
}
