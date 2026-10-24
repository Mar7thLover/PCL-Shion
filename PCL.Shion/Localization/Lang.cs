using System;
using System.Collections.Generic;
using System.Globalization;
using Humanizer;

namespace PCL.Shion.Localization;

/// <summary>
/// 本地化文本与展示格式的代码侧入口。XAML 里的静态文字用 <c>{loc:L 键名}</c>。
/// 格式化方法使用展示区域性 <see cref="Culture"/>，只用于生成给用户看的文字；
/// 配置、日志、协议、文件名等需要稳定格式的场合应显式使用 <see cref="CultureInfo.InvariantCulture"/>。
/// </summary>
public static class Lang
{
    private static readonly HashSet<string> _UnrestrictedTimeZones = new(StringComparer.OrdinalIgnoreCase)
    {
        "China Standard Time"
    };

    private static readonly HashSet<string> _UnrestrictedCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "zh-CN"
    };

    private static readonly string[] FileSizeUnits = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB", "ZiB", "YiB"];

    /// <summary>当前展示区域性。</summary>
    public static CultureInfo Culture => LocalizationService.CurrentFormatCulture;

    /// <summary>当前功能是否不受限制。</summary>
    public static bool IsFeaturesUnrestricted =>
        _UnrestrictedTimeZones.Contains(TimeZoneInfo.Local.Id) &&
        (_UnrestrictedCultures.Contains(CultureInfo.CurrentCulture.Name) ||
         _UnrestrictedCultures.Contains(CultureInfo.CurrentUICulture.Name));

    /// <summary>当前展示区域性是否为 <c>zh-CN</c>。</summary>
    public static bool IsChineseMainland => Culture.Name == "zh-CN";

    /// <summary>
    /// 获取指定键的文本。键不存在时，调试版返回 <c>!键!</c>，发布版返回键本身。
    /// </summary>
    public static string Text(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (LocalizationService.FindText(key) is { } text) return text;
#if DEBUG
        return $"!{key}!";
#else
        return key;
#endif
    }

    /// <summary>
    /// 获取指定键的格式文本，并用展示区域性填入参数（标准复合格式，如 <c>{0}</c>、<c>{1:N2}</c>）。
    /// </summary>
    public static string Text(string key, params object?[] args) => string.Format(Culture, Text(key), args);

    /// <summary>按展示区域性格式化日期时间，默认格式为 <c>G</c>。</summary>
    public static string Date(DateTime value, string format = "G") => value.ToString(format, Culture);

    /// <summary>按展示区域性格式化日期时间，默认格式为 <c>G</c>。</summary>
    public static string Date(DateTimeOffset value, string format = "G") => value.ToString(format, Culture);

    /// <summary>
    /// 按展示区域性格式化时间间隔，如“3 天前”。
    /// </summary>
    /// <param name="span">负值表示过去，正值表示未来。</param>
    /// <param name="precision">显示的时间单位个数。</param>
    /// <param name="addAffixes">是否按正负加上“前”“后”之类的前后缀。</param>
    /// <param name="maxUnit">允许显示的最大单位。</param>
    /// <param name="minUnit">允许显示的最小单位。</param>
    public static string TimeSpan(
        TimeSpan span, int precision = 2, bool addAffixes = true,
        TimeUnit maxUnit = TimeUnit.Year, TimeUnit minUnit = TimeUnit.Hour)
    {
        var isPast = span.TotalMilliseconds < 0;
        if (isPast) span = span.Negate();

        var text = span.Humanize(precision, maxUnit: maxUnit, minUnit: minUnit, culture: Culture);
        return addAffixes
            ? Text(isPast ? "Common.Format.TimeSpan.Past" : "Common.Format.TimeSpan.Future", text)
            : text;
    }

    /// <summary>按展示区域性格式化数值。<paramref name="format"/> 为空时用类型的默认格式。</summary>
    public static string Number<T>(T value, string? format = null) where T : IFormattable => value.ToString(format, Culture);

    /// <summary>
    /// 按展示区域性格式化紧凑数值（如下载量），东亚语言以万、亿进位，其余以千、百万进位。
    /// </summary>
    public static string CompactNumber(long value)
    {
        var absValue = Math.Abs((double)value);
        var sign = value < 0 ? -1 : 1;

        if (IsEastAsianCulture(Culture.Name))
            return absValue switch
            {
                > 1_000_000_000_000d => Text("Common.Format.Number.Digit3", Number(sign * absValue / 1_000_000_000_000d, "N2")),
                > 100_000_000d => Text("Common.Format.Number.Digit2", Number(sign * absValue / 100_000_000d, "N2")),
                > 100_000d => Text("Common.Format.Number.Digit1", Number(sign * Math.Round(absValue / 10_000d), "N0")),
                _ => Number(value, "N0")
            };

        return absValue switch
        {
            > 1_000_000_000d => Text("Common.Format.Number.Digit3", Number(sign * absValue / 1_000_000_000d, "N2")),
            > 1_000_000d => Text("Common.Format.Number.Digit2", Number(sign * absValue / 1_000_000d, "N2")),
            > 10_000d => Text("Common.Format.Number.Digit1", Number(sign * Math.Round(absValue / 1_000d), "N0")),
            _ => Number(value, "N0")
        };
    }

    /// <summary>
    /// 按展示区域性格式化文件大小，1024 进制，如 <c>1.28 MiB</c>。
    /// </summary>
    /// <param name="length">字节数。</param>
    /// <param name="startUnit">起始单位的序号，0 为 B。</param>
    public static string FileSize(long length, int startUnit = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startUnit);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startUnit, FileSizeUnits.Length);

        if (length == 0) return "0 B";

        decimal value = Math.Abs((decimal)length);
        var unit = startUnit;
        while (value >= 1024 && unit < FileSizeUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var sign = length < 0 ? "-" : "";
        return $"{sign}{value.ToString("0.##", Culture)} {FileSizeUnits[unit]}";
    }

    private static bool IsEastAsianCulture(string cultureName) =>
        cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase) || cultureName is "ja-JP" or "ko-KR" or "lzh";
}
