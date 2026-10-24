using System.Buffers.Binary;
using System.IO;
using System.Text;
using PCL.Shion.Core.Worlds.Nbt;

namespace PCL.Shion.Core.Worlds;

/// <summary>
/// 多人游戏服务器列表（<c>servers.dat</c>）中的一项。
/// </summary>
/// <param name="Name">服务器名称。</param>
/// <param name="Address">服务器地址（ip 字段）。</param>
/// <param name="Icon">服务器图标的 Base64 字符串，没有时为 null。</param>
public sealed record ServersDatEntry(string Name, string Address, string? Icon);

/// <summary>
/// 读写 <c>servers.dat</c>。该文件是未压缩的 NBT，根为空名复合标签，内含名为 <c>servers</c> 的列表。
/// 写入时只保留 name、ip 与 icon 字段。
/// </summary>
public static class ServersDat
{
    private const byte TagEnd = 0;
    private const byte TagString = 8;
    private const byte TagList = 9;
    private const byte TagCompound = 10;

    /// <summary>读取 <paramref name="path"/> 中的服务器列表；文件不存在时返回空列表。</summary>
    public static List<ServersDatEntry> Read(string path)
    {
        var result = new List<ServersDatEntry>();
        if (!File.Exists(path))
            return result;

        using var stream = File.OpenRead(path);
        var root = NbtReader.Read(stream);
        var servers = root.GetList("servers");
        if (servers is null)
            return result;

        foreach (var tag in servers)
        {
            if (tag is not NbtCompound server)
                continue;
            result.Add(new ServersDatEntry(
                server.GetString("name") ?? "Unknown",
                server.GetString("ip") ?? "Unknown",
                server.GetString("icon")));
        }

        return result;
    }

    /// <summary>把 <paramref name="entries"/> 写入 <paramref name="path"/>，覆盖原文件。</summary>
    public static void Write(string path, IReadOnlyList<ServersDatEntry> entries)
    {
        using var buffer = new MemoryStream();

        // 根复合标签（空名）
        WriteByte(buffer, TagCompound);
        WriteString(buffer, "");

        // servers 列表，元素类型为复合标签
        WriteByte(buffer, TagList);
        WriteString(buffer, "servers");
        WriteByte(buffer, TagCompound);
        WriteInt(buffer, entries.Count);
        foreach (var entry in entries)
        {
            WriteByte(buffer, TagString);
            WriteString(buffer, "name");
            WriteString(buffer, entry.Name);
            WriteByte(buffer, TagString);
            WriteString(buffer, "ip");
            WriteString(buffer, entry.Address);
            if (entry.Icon is not null)
            {
                WriteByte(buffer, TagString);
                WriteString(buffer, "icon");
                WriteString(buffer, entry.Icon);
            }

            WriteByte(buffer, TagEnd);
        }

        WriteByte(buffer, TagEnd);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, buffer.ToArray());
    }

    private static void WriteByte(Stream stream, byte value) => stream.WriteByte(value);

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var length = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)bytes.Length);
        stream.Write(length);
        stream.Write(bytes);
    }

    private static void WriteInt(Stream stream, int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }
}
