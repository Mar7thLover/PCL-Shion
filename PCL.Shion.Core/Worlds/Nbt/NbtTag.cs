namespace PCL.Shion.Core.Worlds.Nbt;

/// <summary>
/// 解析得到的 NBT 标签。
/// </summary>
public abstract class NbtTag
{
    private protected NbtTag(NbtTagType type)
    {
        Type = type;
    }

    public NbtTagType Type { get; }
}
