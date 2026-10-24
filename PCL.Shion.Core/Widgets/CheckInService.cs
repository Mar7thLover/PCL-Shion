namespace PCL.Shion.Core.Widgets;

/// <summary>
/// 日期打卡服务。负责读写小工具状态里的打卡记录，今天取本机本地日期。
/// 读写失败只当作没有记录处理，不向外抛出。
/// </summary>
public static class CheckInService
{
    private static readonly Lock SyncRoot = new();

    /// <summary>
    /// 本机当前的本地日期。
    /// </summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>
    /// 读取当前的打卡统计。
    /// </summary>
    public static CheckInStats Get()
    {
        lock (SyncRoot)
        {
            return CheckInCalculator.Compute(Load(), Today);
        }
    }

    /// <summary>
    /// 读取已打卡的日期集合。
    /// </summary>
    public static IReadOnlySet<DateOnly> GetDates()
    {
        lock (SyncRoot)
        {
            return Load();
        }
    }

    /// <summary>
    /// 为今天打卡并返回打卡后的统计。今天已打卡时不会重复写入。
    /// </summary>
    public static CheckInStats CheckIn()
    {
        lock (SyncRoot)
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
        return CheckInCalculator.Parse(WidgetStateStore.GetCheckInDates());
    }

    private static bool Save(IEnumerable<DateOnly> dates)
    {
        var value = CheckInCalculator.Serialize(dates);
        return WidgetStateStore.Update(state =>
        {
            if (state.CheckInDates == value) return false;
            state.CheckInDates = value;
            return true;
        }, out _);
    }
}
