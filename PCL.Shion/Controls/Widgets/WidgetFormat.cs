using System;
using PCL.Shion.Localization;

namespace PCL.Shion.Controls;

/// <summary>
///     小工具里相对时间与时长的格式化。
///     取整规则放在 <see cref="GetRelativeTimeParts" /> 与 <see cref="GetDurationParts" />（不依赖语言资源，可单测），
///     <see cref="RelativeTime(DateTime)" /> 与 <see cref="Duration" /> 再套用 <c>Widget.Time.*</c> / <c>Widget.Duration.*</c> 文本。
/// </summary>
internal static class WidgetFormat
{
    /// <summary>
    ///     相对时间的显示档位。
    /// </summary>
    internal enum RelativeTimeUnit
    {
        JustNow,
        Minutes,
        Hours,
        Days
    }

    /// <summary>
    ///     相对时间的档位与数值。<see cref="RelativeTimeUnit.JustNow" /> 时数值为 0。
    /// </summary>
    internal readonly record struct RelativeTimeParts(RelativeTimeUnit Unit, long Value);

    /// <summary>
    ///     时长的显示档位。
    /// </summary>
    internal enum DurationUnit
    {
        LessThanMinute,
        Minutes,
        HoursMinutes
    }

    /// <summary>
    ///     时长的档位与数值。仅 <see cref="DurationUnit.HoursMinutes" /> 使用 <see cref="DurationParts.Hours" />，
    ///     <see cref="DurationUnit.Minutes" /> 与 <see cref="DurationUnit.HoursMinutes" /> 使用 <see cref="DurationParts.Minutes" />。
    /// </summary>
    internal readonly record struct DurationParts(DurationUnit Unit, long Hours, long Minutes);

    /// <summary>
    ///     计算相对时间的档位：不足 1 分钟（含时间在未来的情况）为刚刚，不足 1 小时按分钟，不足 1 天按小时，其余按天；均向下取整。
    /// </summary>
    /// <param name="utc">要描述的时间（UTC）。</param>
    /// <param name="utcNow">当前时间（UTC）。</param>
    internal static RelativeTimeParts GetRelativeTimeParts(DateTime utc, DateTime utcNow)
    {
        var elapsed = ToUtc(utcNow) - ToUtc(utc);
        if (elapsed < TimeSpan.FromMinutes(1)) return new RelativeTimeParts(RelativeTimeUnit.JustNow, 0);
        if (elapsed < TimeSpan.FromHours(1)) return new RelativeTimeParts(RelativeTimeUnit.Minutes, (long)elapsed.TotalMinutes);
        if (elapsed < TimeSpan.FromDays(1)) return new RelativeTimeParts(RelativeTimeUnit.Hours, (long)elapsed.TotalHours);
        return new RelativeTimeParts(RelativeTimeUnit.Days, (long)elapsed.TotalDays);
    }

    /// <summary>
    ///     计算时长的档位：不足 1 分钟（含负值）为不到 1 分钟，不足 1 小时只显示分钟，其余显示小时与分钟；均向下取整。
    /// </summary>
    internal static DurationParts GetDurationParts(TimeSpan duration)
    {
        if (duration < TimeSpan.FromMinutes(1)) return new DurationParts(DurationUnit.LessThanMinute, 0, 0);
        var totalMinutes = (long)duration.TotalMinutes;
        if (totalMinutes < 60) return new DurationParts(DurationUnit.Minutes, 0, totalMinutes);
        return new DurationParts(DurationUnit.HoursMinutes, totalMinutes / 60, totalMinutes % 60);
    }

    /// <summary>
    ///     相对于当前时间的描述，例如「刚刚」「5 分钟前」「3 小时前」「2 天前」。
    /// </summary>
    /// <param name="utc">要描述的时间（UTC）。</param>
    internal static string RelativeTime(DateTime utc)
    {
        return RelativeTime(utc, DateTime.UtcNow);
    }

    /// <summary>
    ///     相对于指定时间的描述。
    /// </summary>
    /// <param name="utc">要描述的时间（UTC）。</param>
    /// <param name="utcNow">用作参照的当前时间（UTC）。</param>
    internal static string RelativeTime(DateTime utc, DateTime utcNow)
    {
        var parts = GetRelativeTimeParts(utc, utcNow);
        return parts.Unit switch
        {
            RelativeTimeUnit.Minutes => Lang.Text("Widget.Time.MinutesAgo", parts.Value),
            RelativeTimeUnit.Hours => Lang.Text("Widget.Time.HoursAgo", parts.Value),
            RelativeTimeUnit.Days => Lang.Text("Widget.Time.DaysAgo", parts.Value),
            _ => Lang.Text("Widget.Time.JustNow")
        };
    }

    /// <summary>
    ///     时长描述，例如「不到 1 分钟」「35 分钟」「2 小时 10 分钟」。
    /// </summary>
    internal static string Duration(TimeSpan duration)
    {
        var parts = GetDurationParts(duration);
        return parts.Unit switch
        {
            DurationUnit.Minutes => Lang.Text("Widget.Duration.Minutes", parts.Minutes),
            DurationUnit.HoursMinutes => Lang.Text("Widget.Duration.HoursMinutes", parts.Hours, parts.Minutes),
            _ => Lang.Text("Widget.Duration.LessThanMinute")
        };
    }

    private static DateTime ToUtc(DateTime time)
    {
        return time.Kind switch
        {
            DateTimeKind.Utc => time,
            DateTimeKind.Local => time.ToUniversalTime(),
            _ => DateTime.SpecifyKind(time, DateTimeKind.Utc)
        };
    }
}
