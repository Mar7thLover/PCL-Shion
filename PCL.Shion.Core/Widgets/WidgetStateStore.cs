using System.Text.Json;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Widgets;

/// <summary>
/// 小工具状态的文件存储，保存在应用数据目录的 <c>PCL Shion/widgets.json</c>。
/// 打卡记录与最近游玩记录共用同一个文件，读写都在同一把锁内完成，避免互相覆盖。
/// </summary>
internal static class WidgetStateStore
{
    private static readonly Lock SyncRoot = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = false
    };

    private static string FilePath { get; } = Path.Combine(
        SpecialFolders.Get(Environment.SpecialFolder.ApplicationData), "PCL Shion", "widgets.json");

    /// <summary>
    /// 已打卡日期，格式见 <see cref="CheckInCalculator.DateFormat" />。
    /// </summary>
    public static string GetCheckInDates()
    {
        lock (SyncRoot)
        {
            return Load().CheckInDates;
        }
    }

    /// <summary>
    /// 最近游玩记录的 JSON。
    /// </summary>
    public static string GetRecentPlay()
    {
        lock (SyncRoot)
        {
            return Load().RecentPlay;
        }
    }

    /// <summary>
    /// 修改状态并保存。<paramref name="changed" /> 表示修改方法是否声明了改动；
    /// 没有改动时不写入文件并直接返回 true。返回是否已成功保存。
    /// </summary>
    public static bool Update(Func<WidgetState, bool> mutate, out bool changed)
    {
        lock (SyncRoot)
        {
            var state = Load();
            changed = mutate(state);
            if (!changed) return true;
            return Save(state);
        }
    }

    private static WidgetState Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new WidgetState();
            return JsonSerializer.Deserialize<WidgetState>(File.ReadAllText(FilePath), JsonOptions) ?? new WidgetState();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new WidgetState();
        }
    }

    private static bool Save(WidgetState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(state, JsonOptions));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// 小工具状态的存储格式。
    /// </summary>
    public sealed class WidgetState
    {
        /// <summary>已打卡日期，逗号分隔的 <c>yyyy-MM-dd</c>。</summary>
        public string CheckInDates { get; set; } = "";

        /// <summary>最近游玩记录 JSON。</summary>
        public string RecentPlay { get; set; } = "";
    }
}
