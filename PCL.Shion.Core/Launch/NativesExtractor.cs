using System.IO.Compression;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 把旧式 natives jar 解压到本地库目录。
/// </summary>
internal static class NativesExtractor
{
    /// <summary>
    /// 解压 jar 中的文件，跳过 META-INF/ 与 <paramref name="exclude"/> 列出的路径前缀。
    /// 目标已存在且大小相同的文件不再覆盖，这样游戏仍在运行（文件被占用）时也能再次启动。
    /// </summary>
    /// <param name="jarPath">natives jar 的路径。</param>
    /// <param name="targetDirectory">目标目录，须为不带结尾分隔符的完整路径。</param>
    /// <param name="exclude">extract.exclude 中的路径前缀。</param>
    /// <exception cref="InvalidDataException">jar 中含有指向目标目录之外的路径。</exception>
    public static void Extract(string jarPath, string targetDirectory, IReadOnlyList<string> exclude)
    {
        using var archive = ZipFile.OpenRead(jarPath);
        foreach (var entry in archive.Entries)
        {
            if (entry.Name.Length == 0 || IsExcluded(entry.FullName, exclude))
                continue;

            var destination = Path.GetFullPath(Path.Combine(targetDirectory, entry.FullName));
            if (!destination.StartsWith(targetDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException($"Unsafe path '{entry.FullName}' in {jarPath}");

            if (File.Exists(destination) && new FileInfo(destination).Length == entry.Length)
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    private static bool IsExcluded(string entryName, IReadOnlyList<string> exclude)
    {
        return entryName.StartsWith("META-INF/", StringComparison.Ordinal)
            || exclude.Any(prefix => entryName.StartsWith(prefix, StringComparison.Ordinal));
    }
}
