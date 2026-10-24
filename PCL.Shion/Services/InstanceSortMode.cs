namespace PCL.Shion.Services;

/// <summary>
/// 实例列表的排序方式。
/// </summary>
public enum InstanceSortMode
{
    /// <summary>按名称。</summary>
    Name,

    /// <summary>按 Minecraft 版本。</summary>
    Version,

    /// <summary>按最近游玩时间，最近的在前。</summary>
    LastPlayed,
}
