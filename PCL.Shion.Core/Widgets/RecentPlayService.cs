using System.Text.Json;
using PCL.Shion.Core.Instances;

namespace PCL.Shion.Core.Widgets;

/// <summary>
/// 最近游玩记录服务：读写小工具状态里的最近游玩记录，并与实例设置里的游玩时间保持同步。
/// 所有方法都是线程安全的，且不会向调用方抛出异常，可以放心在启动与退出流程里调用。
/// </summary>
public static class RecentPlayService
{
    private static readonly Lock SyncRoot = new();

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
        Update(list => RecentPlayStore.RecordStart(list, path, name, DateTime.UtcNow));
    }

    /// <summary>
    /// 游戏退出后调用：累加该实例的游玩时长。
    /// </summary>
    /// <param name="path">实例文件夹路径。</param>
    /// <param name="duration">本次游玩时长。</param>
    public static void OnGameExited(string path, TimeSpan duration)
    {
        Update(list => RecentPlayStore.RecordSession(list, path, duration));
    }

    /// <summary>
    /// 按最近游玩时间倒序取前 <paramref name="n" /> 条记录。读取失败时返回空列表。
    /// </summary>
    public static IReadOnlyList<RecentPlayEntry> GetRecent(int n)
    {
        try
        {
            lock (SyncRoot)
            {
                return RecentPlayStore.GetRecent(Load(), n);
            }
        }
        catch (Exception)
        {
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
            bool changed;
            bool saved;
            lock (SyncRoot)
            {
                saved = WidgetStateStore.Update(state =>
                {
                    if (state.RecentPlay.Length == 0) return false;
                    state.RecentPlay = "";
                    return true;
                }, out changed);
            }
            if (changed && saved) RaiseChanged();
        }
        catch (Exception)
        {
            // 清空失败不应影响界面，列表会保持原样
        }
    }

    /// <summary>
    /// 从实例设置里同步最近游玩时间与累计时长：已存在同名路径的记录就地更新，没有的补一条。
    /// 已经不存在于磁盘上的实例保留原有记录，以便列表继续显示「实例已不存在」。
    /// </summary>
    /// <param name="minecraftRoot">当前使用的 .minecraft 目录。</param>
    public static void SyncFromInstances(string minecraftRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(minecraftRoot)) return;
            var stats = ReadInstanceStats(minecraftRoot);
            if (stats.Count == 0) return;

            Update(list =>
            {
                foreach (var item in stats)
                {
                    var entry = list.FirstOrDefault(entry => RecentPlayStore.IsSamePath(entry.Path, item.Directory));
                    if (entry is null)
                    {
                        list.Add(new RecentPlayEntry
                        {
                            Path = item.Directory,
                            Name = item.Name,
                            LastPlayedUtc = item.LastPlayedUtc,
                            TotalSeconds = item.TotalSeconds
                        });
                    }
                    else
                    {
                        entry.Name = item.Name;
                        entry.LastPlayedUtc = item.LastPlayedUtc;
                        entry.TotalSeconds = item.TotalSeconds;
                    }
                }
            });
        }
        catch (Exception)
        {
            // 同步失败时继续使用已有的记录
        }
    }

    private static void Update(Action<List<RecentPlayEntry>> mutate)
    {
        try
        {
            bool changed;
            bool saved;
            lock (SyncRoot)
            {
                var list = Load();
                mutate(list);
                // 写入前按时间排序，重复同步时未变化的结果不会触发 Changed
                var ordered = RecentPlayStore.GetRecent(list, RecentPlayStore.MaxEntries);
                var json = RecentPlayStore.Serialize(ordered);
                saved = WidgetStateStore.Update(state =>
                {
                    if (state.RecentPlay == json) return false;
                    state.RecentPlay = json;
                    return true;
                }, out changed);
            }
            if (changed && saved) RaiseChanged();
        }
        catch (Exception)
        {
            // 写入失败只影响记录，不应影响调用方的启动或退出流程
        }
    }

    private static List<RecentPlayEntry> Load()
    {
        RecentPlayStore.TryParse(WidgetStateStore.GetRecentPlay(), out var list);
        return list;
    }

    /// <summary>
    /// 读取实例设置里的最近游玩时间与累计时长。不解析版本 JSON，避免刷新卡片时产生多余开销。
    /// </summary>
    private static List<InstancePlayStats> ReadInstanceStats(string minecraftRoot)
    {
        var result = new List<InstancePlayStats>();
        var versionsDirectory = Path.Combine(minecraftRoot, "versions");
        if (!Directory.Exists(versionsDirectory)) return result;

        foreach (var directory in Directory.EnumerateDirectories(versionsDirectory))
        {
            var name = Path.GetFileName(directory);
            if (!File.Exists(Path.Combine(directory, $"{name}.json"))) continue;

            InstanceSettings settings;
            try
            {
                settings = InstanceStore.Load(minecraftRoot, name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                continue;
            }
            if (settings.LastPlayed is not { } lastPlayed) continue;
            result.Add(new InstancePlayStats(directory, name, lastPlayed.UtcDateTime, settings.TotalPlaySeconds));
        }

        return result;
    }

    private static void RaiseChanged()
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
            catch (Exception)
            {
                // 订阅者自己的异常不应影响其他订阅者
            }
        }
    }

    /// <summary>
    /// 实例设置里与最近游玩相关的数据。
    /// </summary>
    private sealed record InstancePlayStats(string Directory, string Name, DateTime LastPlayedUtc, long TotalSeconds);
}
