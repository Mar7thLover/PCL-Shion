namespace PCL.Shion.Core.Worlds.Nbt;

/// <summary>
/// NBT 标签类型，数值与格式中的类型 ID 一致。
/// </summary>
public enum NbtTagType : byte
{
    End = 0,
    Byte = 1,
    Short = 2,
    Int = 3,
    Long = 4,
    Float = 5,
    Double = 6,
    ByteArray = 7,
    String = 8,
    List = 9,
    Compound = 10,
    IntArray = 11,
    LongArray = 12,
}
