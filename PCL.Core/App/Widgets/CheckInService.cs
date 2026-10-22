using System;
using System.Collections.Generic;
using PCL.Core.Logging;

namespace PCL.Core.App.Widgets;

/// <summary>
/// 日期打卡服务。负责读写 <see cref="States.Widget" /> 中的打卡记录，今天取本机本地日期。
/// 读写失败只记录日志，不向外抛出。
/// </summary>
public static class CheckInService
{
    private const string LogModule = "CheckIn";

    private static readonly object _Lock = new();

    /// <summary>
    /// 本机当前的本地日期。
    /// </summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>
    /// 读取当前的打卡统计。
    /// </summary>
    public static CheckInStats Get()
    {
        lock (_Lock)
        {
            return CheckInCalculator.Compute(Load(), Today);
        }
    }

    /// <summary>
    /// 读取已打卡的日期集合。
    /// </summary>
    public static IReadOnlySet<DateOnly> GetDates()
    {
        lock (_Lock)
        {
            return Load();
        }
    }

    /// <summary>
    /// 为今天打卡并返回打卡后的统计。今天已打卡时不会重复写入。
    /// </summary>
    public static CheckInStats CheckIn()
    {
        lock (_Lock)
        {
            var today = Today;
            var dates = Load();
            if (!dates.Contains(today))
            {
                var updated = new HashSet<DateOnly>(dates) { today };
                if (Save(updated)) dates = updated;
            }
            return CheckInCalculator.Compute(dates, today);
        }
    }

    private static IReadOnlySet<DateOnly> Load()
    {
        try
        {
            return CheckInCalculator.Parse(States.Widget.CheckInDates);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, LogModule, "读取打卡记录失败");
            return new HashSet<DateOnly>();
        }
    }

    private static bool Save(IEnumerable<DateOnly> dates)
    {
        try
        {
            States.Widget.CheckInDates = CheckInCalculator.Serialize(dates);
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, LogModule, "保存打卡记录失败");
            return false;
        }
    }
}
