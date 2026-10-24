using System.Buffers.Binary;

namespace PCL.Shion.Core.Worlds.Nbt;

/// <summary>
/// 从内存中的未压缩数据解析 NBT（大端）。
/// </summary>
internal ref struct NbtParser
{
    // 与游戏自身的嵌套上限一致，避免恶意文件耗尽调用栈
    private const int MaxDepth = 512;

    private readonly ReadOnlySpan<byte> _data;
    private int _position;

    public NbtParser(ReadOnlySpan<byte> data)
    {
        _data = data;
    }

    /// <summary>
    /// 读取根标签，必须是 Compound；根标签的名称被忽略。
    /// </summary>
    public NbtCompound ReadRoot()
    {
        if (ReadType() != NbtTagType.Compound)
            throw new InvalidDataException("The NBT root tag must be a compound.");

        ReadString();
        return ReadCompound(1);
    }

    private NbtTag ReadPayload(NbtTagType type, int depth) => type switch
    {
        NbtTagType.Byte => new NbtValue<sbyte>(type, (sbyte)Take(1)[0]),
        NbtTagType.Short => new NbtValue<short>(type, BinaryPrimitives.ReadInt16BigEndian(Take(2))),
        NbtTagType.Int => new NbtValue<int>(type, BinaryPrimitives.ReadInt32BigEndian(Take(4))),
        NbtTagType.Long => new NbtValue<long>(type, BinaryPrimitives.ReadInt64BigEndian(Take(8))),
        NbtTagType.Float => new NbtValue<float>(type, BinaryPrimitives.ReadSingleBigEndian(Take(4))),
        NbtTagType.Double => new NbtValue<double>(type, BinaryPrimitives.ReadDoubleBigEndian(Take(8))),
        NbtTagType.ByteArray => new NbtValue<byte[]>(type, Take(ReadLength(sizeof(byte))).ToArray()),
        NbtTagType.String => new NbtValue<string>(type, ReadString()),
        NbtTagType.List => ReadList(depth),
        NbtTagType.Compound => ReadCompound(depth),
        NbtTagType.IntArray => new NbtValue<int[]>(type, ReadIntArray()),
        NbtTagType.LongArray => new NbtValue<long[]>(type, ReadLongArray()),
        _ => throw new InvalidDataException($"Unexpected NBT tag type {type}."),
    };

    private NbtCompound ReadCompound(int depth)
    {
        EnsureDepth(depth);

        var entries = new Dictionary<string, NbtTag>();
        for (var type = ReadType(); type != NbtTagType.End; type = ReadType())
        {
            var name = ReadString();
            entries[name] = ReadPayload(type, depth + 1);
        }
        return new NbtCompound(entries);
    }

    private NbtList ReadList(int depth)
    {
        EnsureDepth(depth);

        var elementType = ReadType();
        // 每个元素至少占一个字节，据此限制长度即可避免按虚假长度预分配
        var length = ReadLength(sizeof(byte));
        var items = new List<NbtTag>(length);
        for (var i = 0; i < length; i++)
            items.Add(ReadPayload(elementType, depth + 1));
        return new NbtList(elementType, items);
    }

    private int[] ReadIntArray()
    {
        var length = ReadLength(sizeof(int));
        var bytes = Take(length * sizeof(int));
        var values = new int[length];
        for (var i = 0; i < length; i++)
            values[i] = BinaryPrimitives.ReadInt32BigEndian(bytes[(i * sizeof(int))..]);
        return values;
    }

    private long[] ReadLongArray()
    {
        var length = ReadLength(sizeof(long));
        var bytes = Take(length * sizeof(long));
        var values = new long[length];
        for (var i = 0; i < length; i++)
            values[i] = BinaryPrimitives.ReadInt64BigEndian(bytes[(i * sizeof(long))..]);
        return values;
    }

    private NbtTagType ReadType()
    {
        var value = Take(1)[0];
        if (value > (byte)NbtTagType.LongArray)
            throw new InvalidDataException($"Unknown NBT tag type {value}.");
        return (NbtTagType)value;
    }

    /// <summary>
    /// 读取数组或列表的元素个数，并确认剩余数据足够容纳这些元素。
    /// </summary>
    private int ReadLength(int elementSize)
    {
        var length = BinaryPrimitives.ReadInt32BigEndian(Take(4));
        if (length < 0 || length > (_data.Length - _position) / elementSize)
            throw new InvalidDataException("Invalid NBT array or list length.");
        return length;
    }

    private string ReadString()
    {
        var length = BinaryPrimitives.ReadUInt16BigEndian(Take(2));
        return DecodeModifiedUtf8(Take(length));
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count > _data.Length - _position)
            throw new InvalidDataException("Unexpected end of NBT data.");

        var slice = _data.Slice(_position, count);
        _position += count;
        return slice;
    }

    private static void EnsureDepth(int depth)
    {
        if (depth > MaxDepth)
            throw new InvalidDataException("The NBT data is nested too deeply.");
    }

    /// <summary>
    /// 解码 Java 的修改版 UTF-8：空字符记为 C0 80，增补字符按代理项各占三字节。
    /// </summary>
    private static string DecodeModifiedUtf8(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[bytes.Length];
        var count = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            int lead = bytes[i];
            if (lead < 0x80)
            {
                chars[count++] = (char)lead;
            }
            else if ((lead & 0xE0) == 0xC0)
            {
                chars[count++] = (char)(((lead & 0x1F) << 6) | ContinuationBits(bytes, i + 1));
                i += 1;
            }
            else if ((lead & 0xF0) == 0xE0)
            {
                var second = ContinuationBits(bytes, i + 1);
                var third = ContinuationBits(bytes, i + 2);
                chars[count++] = (char)(((lead & 0x0F) << 12) | (second << 6) | third);
                i += 2;
            }
            else
            {
                throw new InvalidDataException("Invalid modified UTF-8 in an NBT string.");
            }
        }
        return new string(chars, 0, count);
    }

    private static int ContinuationBits(ReadOnlySpan<byte> bytes, int index)
    {
        if (index >= bytes.Length || (bytes[index] & 0xC0) != 0x80)
            throw new InvalidDataException("Invalid modified UTF-8 in an NBT string.");
        return bytes[index] & 0x3F;
    }
}
