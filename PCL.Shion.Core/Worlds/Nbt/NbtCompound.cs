namespace PCL.Shion.Core.Worlds.Nbt;

/// <summary>
/// Compound 标签：名称到标签的映射。同名条目以后出现的为准。
/// </summary>
public sealed class NbtCompound : NbtTag
{
    internal NbtCompound(Dictionary<string, NbtTag> entries) : base(NbtTagType.Compound)
    {
        Entries = entries;
    }

    public IReadOnlyDictionary<string, NbtTag> Entries { get; }

    /// <summary>
    /// 取指定名称的标签，不存在时为 null。
    /// </summary>
    public NbtTag? Get(string name)
    {
        return Entries.GetValueOrDefault(name);
    }

    /// <summary>
    /// 取 String 标签的值；不存在或类型不符时为 null。
    /// </summary>
    public string? GetString(string name)
    {
        return Get(name) is NbtValue<string> tag ? tag.Value : null;
    }

    /// <summary>
    /// 取整数标签（Byte、Short、Int、Long）的值；不存在或类型不符时为 null。
    /// </summary>
    public long? GetLong(string name)
    {
        return Get(name) switch
        {
            NbtValue<sbyte> tag => tag.Value,
            NbtValue<short> tag => tag.Value,
            NbtValue<int> tag => tag.Value,
            NbtValue<long> tag => tag.Value,
            _ => null,
        };
    }

    public NbtCompound? GetCompound(string name)
    {
        return Get(name) as NbtCompound;
    }

    public NbtList? GetList(string name)
    {
        return Get(name) as NbtList;
    }
}
