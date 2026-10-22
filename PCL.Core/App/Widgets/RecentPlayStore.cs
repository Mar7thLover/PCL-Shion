using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PCL.Core.App.Widgets;

/// <summary>
/// 一条最近游玩记录。以规范化后的实例路径为键（见 <see cref="RecentPlayStore.NormalizePath"/>）。
/// </summary>
public sealed class RecentPlayEntry
{
    /// <summary>
    /// 实例文件夹路径。
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// 实例名称（最近一次启动时的名称）。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 最近一次启动的时间（UTC）。
    /// </summary>
    public DateTime LastPlayedUtc { get; set; }

    /// <summary>
    /// 累计游玩时长（秒）。
    /// </summary>
    public long TotalSeconds { get; set; }
}

/// <summary>
/// 最近游玩记录的纯逻辑与序列化，不依赖配置与界面，可直接单测。
/// 列表始终按 <see cref="RecentPlayEntry.LastPlayedUtc"/> 倒序排列，最多保留 <see cref="MaxEntries"/> 条。
/// </summary>
public static class RecentPlayStore
{
    /// <summary>
    /// 最多保留的记录条数，超出时丢弃最旧的。
    /// </summary>
    public const int MaxEntries = 50;

    /// <summary>
    /// 单次游玩时长的上限（秒）。超过的部分按此值计算。
    /// </summary>
    public const long MaxSessionSeconds = 24 * 60 * 60;

    private static readonly JsonSerializerOptions _JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = false
    };

    /// <summary>
    /// 规范化实例路径：去掉首尾空白，统一为反斜杠，去掉末尾分隔符。比较时应配合忽略大小写使用，见 <see cref="IsSamePath"/>。
    /// </summary>
    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        return path.Trim().Replace('/', '\\').TrimEnd('\\');
    }

    /// <summary>
    /// 判断两个实例路径是否指向同一个实例（忽略末尾分隔符与大小写）。任一为空时返回 false。
    /// </summary>
    public static bool IsSamePath(string? a, string? b)
    {
        var left = NormalizePath(a);
        return left.Length > 0 && string.Equals(left, NormalizePath(b), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 解析 JSON。内容为空时返回空列表；JSON 非法或结构不符时同样返回空列表。
    /// 结果已去重（同一路径合并）、按最近游玩时间倒序，并裁剪到 <see cref="MaxEntries"/> 条。
    /// </summary>
    public static List<RecentPlayEntry> Parse(string? json)
    {
        TryParse(json, out var entries);
        return entries;
    }

    /// <summary>
    /// 解析 JSON，并告知是否成功。内容为空视为成功（得到空列表）；失败时 <paramref name="entries"/> 为空列表。
    /// </summary>
    public static bool TryParse(string? json, out List<RecentPlayEntry> entries)
    {
        entries = [];
        if (string.IsNullOrWhiteSpace(json)) return true;

        List<RecentPlayEntry?>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<RecentPlayEntry?>>(json, _JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return false;
        }
        if (raw is null) return true;

        // 清洗并按路径去重：同一路径保留较新的一条，累计时长相加
        var merged = new Dictionary<string, RecentPlayEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in raw)
        {
            if (item is null) continue;
            var key = NormalizePath(item.Path);
            if (key.Length == 0) continue;
            var entry = new RecentPlayEntry
            {
                Path = item.Path.Trim(),
                Name = string.IsNullOrWhiteSpace(item.Name) ? _NameFromPath(key) : item.Name,
                LastPlayedUtc = _ToUtc(item.LastPlayedUtc),
                TotalSeconds = Math.Max(0, item.TotalSeconds)
            };
            if (merged.TryGetValue(key, out var existing))
            {
                var total = existing.TotalSeconds + entry.TotalSeconds;
                if (entry.LastPlayedUtc > existing.LastPlayedUtc) existing = entry;
                existing.TotalSeconds = total;
                merged[key] = existing;
            }
            else
            {
                merged[key] = entry;
            }
        }

        entries = merged.Values.ToList();
        _SortAndTrim(entries);
        return true;
    }

    /// <summary>
    /// 序列化为 JSON。
    /// </summary>
    public static string Serialize(IEnumerable<RecentPlayEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return JsonSerializer.Serialize(entries.ToList(), _JsonOptions);
    }

    /// <summary>
    /// 记录一次游戏启动：更新（或新建）该实例的最近游玩时间与名称，并把它排到最前。
    /// 超过 <see cref="MaxEntries"/> 条时丢弃最旧的。路径为空时忽略。
    /// </summary>
    /// <param name="list">记录列表，会被原地修改。</param>
    /// <param name="path">实例路径。</param>
    /// <param name="name">实例名称；为空时取路径的最后一级文件夹名。</param>
    /// <param name="utcNow">启动时间（UTC）。</param>
    public static void RecordStart(List<RecentPlayEntry> list, string path, string? name, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(list);
        var key = NormalizePath(path);
        if (key.Length == 0) return;

        var entry = _Find(list, key);
        if (entry is null)
            entry = new RecentPlayEntry();
        else
            list.Remove(entry);

        entry.Path = path.Trim();
        entry.Name = string.IsNullOrWhiteSpace(name) ? _NameFromPath(key) : name;
        entry.LastPlayedUtc = _ToUtc(utcNow);
        // 插到最前：时间相同时，最新操作的记录排在前面，也就不会被裁掉
        list.Insert(0, entry);
        _SortAndTrim(list);
    }

    /// <summary>
    /// 记录一次游玩时长：累加到该实例上。负值与零被忽略，单次超过 24 小时按 24 小时算。
    /// 找不到对应记录（例如记录已被清空）时忽略。
    /// </summary>
    /// <param name="list">记录列表，会被原地修改。</param>
    /// <param name="path">实例路径。</param>
    /// <param name="duration">本次游玩时长。</param>
    public static void RecordSession(List<RecentPlayEntry> list, string path, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (duration <= TimeSpan.Zero) return;
        var key = NormalizePath(path);
        if (key.Length == 0) return;

        var entry = _Find(list, key);
        if (entry is null) return;
        var seconds = (long)Math.Min(Math.Floor(duration.TotalSeconds), MaxSessionSeconds);
        entry.TotalSeconds += seconds;
    }

    /// <summary>
    /// 按最近游玩时间倒序取前 <paramref name="n"/> 条。
    /// </summary>
    public static List<RecentPlayEntry> GetRecent(IEnumerable<RecentPlayEntry> list, int n)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (n <= 0) return [];
        return list.OrderByDescending(e => e.LastPlayedUtc).Take(n).ToList();
    }

    private static RecentPlayEntry? _Find(List<RecentPlayEntry> list, string normalizedPath)
    {
        return list.FirstOrDefault(e =>
            string.Equals(NormalizePath(e.Path), normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 按时间倒序排列（稳定排序），并裁剪到上限。
    /// </summary>
    private static void _SortAndTrim(List<RecentPlayEntry> list)
    {
        var sorted = list.OrderByDescending(e => e.LastPlayedUtc).ToList();
        list.Clear();
        list.AddRange(sorted);
        if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
    }

    /// <summary>
    /// 取路径的最后一级文件夹名。
    /// </summary>
    private static string _NameFromPath(string normalizedPath)
    {
        var index = normalizedPath.LastIndexOf('\\');
        return index >= 0 ? normalizedPath[(index + 1)..] : normalizedPath;
    }

    private static DateTime _ToUtc(DateTime time)
    {
        return time.Kind switch
        {
            DateTimeKind.Utc => time,
            DateTimeKind.Local => time.ToUniversalTime(),
            _ => DateTime.SpecifyKind(time, DateTimeKind.Utc)
        };
    }
}
