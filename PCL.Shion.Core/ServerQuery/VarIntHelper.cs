using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Shion.Core.ServerQuery;

/// <summary>
/// Minecraft 协议使用的小端 VarInt 与无符号短整数的读写。
/// </summary>
internal static class VarIntHelper
{
    private const int MaxLength = 5;

    /// <summary>把整数编码成协议使用的 VarInt。</summary>
    public static byte[] Encode(int value)
    {
        var buffer = new byte[MaxLength];
        var remaining = (uint)value;
        var count = 0;
        do
        {
            var current = (byte)(remaining & 0x7F);
            remaining >>= 7;
            if (remaining != 0) current |= 0x80;
            buffer[count++] = current;
        } while (remaining != 0);

        return buffer[..count];
    }

    /// <summary>从流中读取一个 VarInt，读到不完整的数据时抛出 <see cref="EndOfStreamException"/>。</summary>
    public static async Task<int> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var result = 0;
        var shift = 0;
        var buffer = new byte[1];
        while (true)
        {
            if (await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken) == 0)
                throw new EndOfStreamException();

            var current = buffer[0];
            result |= (current & 0x7F) << shift;
            if ((current & 0x80) == 0) return result;

            shift += 7;
            if (shift >= MaxLength * 7) throw new FormatException("VarInt 超过 5 字节。");
        }
    }

    /// <summary>把无符号短整数按大端写入缓冲区。</summary>
    public static void WriteUInt16BigEndian(Span<byte> destination, ushort value)
    {
        destination[0] = (byte)(value >> 8);
        destination[1] = (byte)value;
    }

    /// <summary>把 64 位整数按大端写入缓冲区。</summary>
    public static void WriteInt64BigEndian(Span<byte> destination, long value)
    {
        for (var i = 7; i >= 0; i--)
        {
            destination[i] = (byte)value;
            value >>= 8;
        }
    }

    /// <summary>从缓冲区按大端读取 64 位整数。</summary>
    public static long ReadInt64BigEndian(ReadOnlySpan<byte> source)
    {
        long result = 0;
        for (var i = 0; i < 8; i++) result = (result << 8) | source[i];
        return result;
    }

    /// <summary>从缓冲区按大端读取无符号短整数。</summary>
    public static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> source) =>
        (ushort)((source[0] << 8) | source[1]);
}
