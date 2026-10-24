using System.Globalization;
using System.Text;

namespace PCL.Shion.Core.Platform;

/// <summary>
/// macOS 与 Linux 的回收站：把文件或目录移动到回收站目录里。
/// </summary>
internal static class UnixTrash
{
    /// <summary>
    /// 按 freedesktop.org 回收站规范放入 <paramref name="trashDirectory"/>：内容放在 files 下，
    /// 原路径与删除时间写入 info 下同名的 .trashinfo 文件。重名时在名称后追加序号。
    /// </summary>
    /// <param name="path">要移入回收站的文件或目录的完整路径。</param>
    /// <param name="trashDirectory">回收站目录，如 ~/.local/share/Trash。</param>
    /// <param name="deletedAt">记录的删除时间（本地时间）。</param>
    public static void MoveToFreedesktopTrash(string path, string trashDirectory, DateTime deletedAt)
    {
        var filesDirectory = Path.Combine(trashDirectory, "files");
        var infoDirectory = Path.Combine(trashDirectory, "info");
        Directory.CreateDirectory(filesDirectory);
        Directory.CreateDirectory(infoDirectory);

        var isDirectory = Directory.Exists(path);
        var infoContent = Encoding.UTF8.GetBytes(CreateTrashInfo(path, deletedAt));
        for (var attempt = 1; ; attempt++)
        {
            var name = GetCandidateName(Path.GetFileName(path), isDirectory, attempt);
            var target = Path.Combine(filesDirectory, name);
            var infoPath = Path.Combine(infoDirectory, name + ".trashinfo");
            if (Path.Exists(target)) continue;

            // 以 CreateNew 创建 .trashinfo，既写入信息，也原子地占用这个名称
            FileStream info;
            try
            {
                info = new FileStream(infoPath, FileMode.CreateNew, FileAccess.Write);
            }
            catch (IOException) when (File.Exists(infoPath))
            {
                continue;
            }

            try
            {
                using (info)
                {
                    info.Write(infoContent);
                }
                MoveEntry(path, target);
            }
            catch
            {
                File.Delete(infoPath);
                throw;
            }
            return;
        }
    }

    /// <summary>
    /// 移入 macOS 的回收站目录（~/.Trash）。重名时在名称后追加序号。
    /// </summary>
    /// <param name="path">要移入回收站的文件或目录的完整路径。</param>
    /// <param name="trashDirectory">回收站目录。</param>
    public static void MoveToMacTrash(string path, string trashDirectory)
    {
        Directory.CreateDirectory(trashDirectory);

        var isDirectory = Directory.Exists(path);
        for (var attempt = 1; ; attempt++)
        {
            var target = Path.Combine(trashDirectory, GetCandidateName(Path.GetFileName(path), isDirectory, attempt));
            if (Path.Exists(target)) continue;

            MoveEntry(path, target);
            return;
        }
    }

    /// <summary>
    /// 生成 .trashinfo 的内容。
    /// </summary>
    internal static string CreateTrashInfo(string originalPath, DateTime deletedAt)
    {
        var deletionDate = deletedAt.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        return $"[Trash Info]\nPath={EncodePath(originalPath)}\nDeletionDate={deletionDate}\n";
    }

    /// <summary>
    /// 按 URL 的方式对路径转义，保留 / 分隔符。
    /// </summary>
    internal static string EncodePath(string path)
    {
        return string.Join('/', path.Split('/').Select(segment => Uri.EscapeDataString(segment)));
    }

    // 第一次使用原名；之后文件在扩展名前、目录在名称后追加序号，如 "mod 2.jar"、"saves 2"
    private static string GetCandidateName(string name, bool isDirectory, int attempt)
    {
        if (attempt == 1) return name;

        var extension = isDirectory ? string.Empty : Path.GetExtension(name);
        var stem = name[..^extension.Length];
        return stem.Length == 0 ? $"{name} {attempt}" : $"{stem} {attempt}{extension}";
    }

    private static void MoveEntry(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            File.Move(source, destination);
            return;
        }

        try
        {
            Directory.Move(source, destination);
        }
        catch (IOException)
        {
            // 回收站与目录不在同一个文件系统上时无法直接移动，改为复制后删除
            CopyDirectory(source, destination);
            Directory.Delete(source, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            var target = Path.Combine(destination, entry.Name);
            if (entry is DirectoryInfo)
                CopyDirectory(entry.FullName, target);
            else
                File.Copy(entry.FullName, target);
        }
    }
}
