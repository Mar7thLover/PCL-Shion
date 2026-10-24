using System.Globalization;

namespace PCL.Shion.Core.Widgets;

/// <summary>
/// 日期打卡的统计结果。
/// </summary>
/// <param name="CheckedToday">今天是否已打卡。</param>
/// <param name="CurrentStreak">当前连续打卡天数。今天已打卡则从今天往前数，否则从昨天往前数。</param>
/// <param name="LongestStreak">历史最长连续打卡天数。</param>
/// <param name="TotalDays">累计打卡天数。</param>
public readonly record struct CheckInStats(bool CheckedToday, int CurrentStreak, int LongestStreak, int TotalDays);

/// <summary>
/// 日期打卡的纯函数计算与序列化。不依赖配置、时钟与界面，便于单元测试。
/// </summary>
public static class CheckInCalculator
{
    /// <summary>
    /// 打卡日期在存储中使用的格式。
    /// </summary>
    public const string DateFormat = "yyyy-MM-dd";

    private const char Separator = ',';

    /// <summary>
    /// 解析以逗号分隔的 <c>yyyy-MM-dd</c> 日期串。非法项会被忽略，重复项会被合并。
    /// </summary>
    /// <param name="raw">存储的原始字符串，可为 <see langword="null" /> 或空串。</param>
    /// <returns>已去重、按日期升序排列的日期集合。</returns>
    public static IReadOnlySet<DateOnly> Parse(string? raw)
    {
        var result = new SortedSet<DateOnly>();
        if (string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var item in raw.Split(Separator))
        {
            if (DateOnly.TryParseExact(item.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var date))
                result.Add(date);
        }
        return result;
    }

    /// <summary>
    /// 把日期序列化为以逗号分隔的 <c>yyyy-MM-dd</c> 字符串。结果已去重并按日期升序排列。
    /// </summary>
    public static string Serialize(IEnumerable<DateOnly> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        return string.Join(Separator,
            new SortedSet<DateOnly>(dates).Select(date => date.ToString(DateFormat, CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// 计算打卡统计。
    /// </summary>
    /// <param name="dates">已打卡的日期集合。</param>
    /// <param name="today">今天的本地日期。</param>
    public static CheckInStats Compute(IReadOnlySet<DateOnly> dates, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(dates);
        var checkedToday = dates.Contains(today);

        // 当前连续：今天已打卡从今天往前数，否则从昨天往前数（昨天也没打卡则为 0）
        var current = 0;
        var day = checkedToday ? today.DayNumber : today.DayNumber - 1;
        while (day >= DateOnly.MinValue.DayNumber && dates.Contains(DateOnly.FromDayNumber(day)))
        {
            current++;
            day--;
        }

        // 最长连续：按日期升序扫描，相邻一天则累加，断档则重新计数
        var longest = 0;
        var run = 0;
        var previous = int.MinValue;
        foreach (var number in dates.Select(date => date.DayNumber).Order())
        {
            run = previous != int.MinValue && number == previous + 1 ? run + 1 : 1;
            if (run > longest) longest = run;
            previous = number;
        }

        return new CheckInStats(checkedToday, current, longest, dates.Count);
    }
}
