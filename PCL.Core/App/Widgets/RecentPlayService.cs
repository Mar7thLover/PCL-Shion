using System;
using System.Collections.Generic;
using PCL.Core.Logging;

namespace PCL.Core.App.Widgets;

/// <summary>
/// 最近游玩记录服务：读写 <c>States.Widget.RecentPlay</c>。
/// 所有方法都是线程安全的，且不会向调用方抛出异常（失败只记录日志），可以放心在启动与退出流程里调用。
/// </summary>
public static class RecentPlayService
{
    private static readonly object _Lock = new();

    /// <summary>
    /// 记录发生变化后触发。可能在任意线程触发，需要操作界面的订阅者应自行切回 UI 线程。
    /// </summary>
    public static event Action? Changed;

    /// <summary>
    /// 游戏进程启动成功后调用：更新该实例的最近游玩时间。
    /// </summary>
    /// <param name="path">实例文件夹路径。</param>
    /// <param name="name">实例名称。</param>
    public static void OnGameStarted(string path, string name)
    {
        _Update("记录游戏启动", list => RecentPlayStore.RecordStart(list, path, name, DateTime.UtcNow));
    }

    /// <summary>
    /// 游戏退出后调用：累加该实例的游玩时长。
    /// </summary>
    /// <param name="path">实例文件夹路径。</param>
    /// <param name="duration">本次游玩时长。</param>
    public static void OnGameExited(string path, TimeSpan duration)
    {
        _Update("记录游戏退出", list => RecentPlayStore.RecordSession(list, path, duration));
    }

    /// <summary>
    /// 游戏日志中识别到进入世界或服务器时调用：记录下来，供之后一键重新进入。
    /// </summary>
    /// <param name="path">实例文件夹路径。</param>
    /// <param name="target">进入的世界或服务器。</param>
    public static void OnTargetJoined(string path, RecentPlayTarget target)
    {
        _Update("记录进入的世界或服务器", list => RecentPlayStore.RecordTarget(list, path, target));
    }

    /// <summary>
    /// 按最近游玩时间倒序取前 <paramref name="n"/> 条记录。读取失败时返回空列表。
    /// </summary>
    public static IReadOnlyList<RecentPlayEntry> GetRecent(int n)
    {
        try
        {
            lock (_Lock)
            {
                return RecentPlayStore.GetRecent(_Load(), n);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Widget", "最近游玩：读取记录失败");
            return [];
        }
    }

    /// <summary>
    /// 清空所有记录。
    /// </summary>
    public static void Clear()
    {
        try
        {
            lock (_Lock)
            {
                States.Widget.RecentPlay = "";
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Widget", "最近游玩：清空记录失败");
            return;
        }
        _RaiseChanged();
    }

    private static void _Update(string action, Action<List<RecentPlayEntry>> mutate)
    {
        try
        {
            lock (_Lock)
            {
                var list = _Load();
                mutate(list);
                States.Widget.RecentPlay = RecentPlayStore.Serialize(list);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Widget", $"最近游玩：{action}失败");
            return;
        }
        _RaiseChanged();
    }

    private static List<RecentPlayEntry> _Load()
    {
        if (!RecentPlayStore.TryParse(States.Widget.RecentPlay, out var list))
            LogWrapper.Warn("Widget", "最近游玩：已保存的记录无法解析，将按空记录处理");
        return list;
    }

    private static void _RaiseChanged()
    {
        var handlers = Changed;
        if (handlers is null) return;
        // 逐个调用，避免某个订阅者出错影响其他订阅者
        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler)();
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Widget", "最近游玩：变化通知的订阅者执行失败");
            }
        }
    }
}
