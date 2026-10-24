using System.IO.Compression;
using System.Text;

namespace PCL.Shion.Core.Test.Mods;

/// <summary>
/// 在测试里拼出 jar（zip）文件，条目是文件名与内容的字节。
/// </summary>
internal static class ModJar
{
    public static (string Name, byte[] Data) Text(string name, string content) =>
        (name, new UTF8Encoding(false).GetBytes(content));

    public static byte[] Build(params (string Name, byte[] Data)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(data);
            }
        }
        return buffer.ToArray();
    }

    public static string Write(string path, params (string Name, byte[] Data)[] entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Build(entries));
        return path;
    }
}
