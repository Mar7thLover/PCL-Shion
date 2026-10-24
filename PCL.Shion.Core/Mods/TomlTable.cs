namespace PCL.Shion.Core.Mods;

/// <summary>
/// TOML 表。值为 string、long、double、bool、<see cref="List{T}"/>（数组，表数组的元素是 <see cref="TomlTable"/>）或 <see cref="TomlTable"/>。
/// </summary>
internal sealed class TomlTable : Dictionary<string, object>
{
    public string? GetString(string key) => TryGetValue(key, out var value) ? value as string : null;

    public bool? GetBool(string key) => TryGetValue(key, out var value) && value is bool flag ? flag : null;

    public TomlTable? GetTable(string key) => TryGetValue(key, out var value) ? value as TomlTable : null;

    /// <summary>
    /// 取表数组 <c>[[key]]</c> 的全部元素；键不存在或不是表数组时返回空列表。
    /// </summary>
    public IReadOnlyList<TomlTable> GetTables(string key)
    {
        if (TryGetValue(key, out var value) && value is List<object> list)
            return list.OfType<TomlTable>().ToList();
        return [];
    }
}
