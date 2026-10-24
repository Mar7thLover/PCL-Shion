using System.Buffers.Binary;
using System.IO.Compression;
using PCL.Shion.Core.Worlds.Nbt;

namespace PCL.Shion.Core.Test;

/// <summary>
/// 在测试里手工拼出 NBT 字节。*Value 方法生成不带名称的载荷，<see cref="Named"/> 再加上类型与名称。
/// </summary>
internal static class NbtBytes
{
    public static byte[] ByteValue(sbyte value) => [(byte)value];

    public static byte[] ShortValue(short value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] IntValue(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] LongValue(long value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] FloatValue(float value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteSingleBigEndian(bytes, value);
        return bytes;
    }

    public static byte[] DoubleValue(double value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(bytes, value);
        return bytes;
    }

    public static byte[] StringValue(string value)
    {
        var encoded = EncodeModifiedUtf8(value);
        return [.. ShortValue((short)encoded.Length), .. encoded];
    }

    public static byte[] ByteArrayValue(params byte[] values) => [.. IntValue(values.Length), .. values];

    public static byte[] IntArrayValue(params int[] values) => [.. IntValue(values.Length), .. values.SelectMany(value => IntValue(value))];

    public static byte[] LongArrayValue(params long[] values) => [.. IntValue(values.Length), .. values.SelectMany(value => LongValue(value))];

    /// <summary>
    /// 列表载荷：元素类型、个数，随后是各元素的载荷。
    /// </summary>
    public static byte[] ListValue(NbtTagType elementType, params byte[][] items)
    {
        return [(byte)elementType, .. IntValue(items.Length), .. items.SelectMany(item => item)];
    }

    /// <summary>
    /// Compound 载荷：各个带名称的条目，以 End 结尾。
    /// </summary>
    public static byte[] CompoundValue(params byte[][] entries)
    {
        return [.. entries.SelectMany(entry => entry), (byte)NbtTagType.End];
    }

    public static byte[] Named(NbtTagType type, string name, byte[] payload)
    {
        return [(byte)type, .. StringValue(name), .. payload];
    }

    /// <summary>
    /// 根标签：名称为空的 Compound。
    /// </summary>
    public static byte[] Root(params byte[][] entries)
    {
        return Named(NbtTagType.Compound, "", CompoundValue(entries));
    }

    public static byte[] GZip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(data);
        return output.ToArray();
    }

    /// <summary>
    /// Java 的修改版 UTF-8：按 UTF-16 代码单元逐个编码，空字符为 C0 80，代理项各占三字节。
    /// </summary>
    public static byte[] EncodeModifiedUtf8(string value)
    {
        var bytes = new List<byte>();
        foreach (var unit in value)
        {
            if (unit is >= '\u0001' and <= '\u007F')
            {
                bytes.Add((byte)unit);
            }
            else if (unit <= '߿')
            {
                bytes.Add((byte)(0xC0 | (unit >> 6)));
                bytes.Add((byte)(0x80 | (unit & 0x3F)));
            }
            else
            {
                bytes.Add((byte)(0xE0 | (unit >> 12)));
                bytes.Add((byte)(0x80 | ((unit >> 6) & 0x3F)));
                bytes.Add((byte)(0x80 | (unit & 0x3F)));
            }
        }
        return [.. bytes];
    }
}
