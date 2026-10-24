using System.IO.Compression;

namespace PCL.Shion.Core.Worlds.Nbt;

/// <summary>
/// 只读的 NBT 读取器，解析大端格式的全部标签类型。
/// 数据格式有误时抛出 <see cref="InvalidDataException"/>。
/// </summary>
public static class NbtReader
{
    // level.dat 与 servers.dat 远小于此值；解压后的总量设上限以防压缩炸弹
    private const int MaxDataSize = 16 * 1024 * 1024;

    /// <summary>
    /// 解析内存中未压缩的 NBT。
    /// </summary>
    public static NbtCompound Read(ReadOnlySpan<byte> data)
    {
        return new NbtParser(data).ReadRoot();
    }

    /// <summary>
    /// 读取流中未压缩的 NBT，不会关闭该流。
    /// </summary>
    public static NbtCompound Read(Stream stream)
    {
        using var buffer = new MemoryStream();
        Span<byte> chunk = stackalloc byte[8192];
        int read;
        while ((read = stream.Read(chunk)) > 0)
        {
            if (buffer.Length + read > MaxDataSize)
                throw new InvalidDataException("The NBT data is too large.");
            buffer.Write(chunk[..read]);
        }
        return Read(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    /// <summary>
    /// 读取流中经 GZip 压缩的 NBT，不会关闭该流。
    /// </summary>
    public static NbtCompound ReadGZip(Stream stream)
    {
        using var gzip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
        return Read(gzip);
    }
}
