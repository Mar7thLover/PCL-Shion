namespace PCL.Shion.Core.Platform;

/// <summary>
/// 把文件或目录移到系统回收站，而不是永久删除。
/// </summary>
public static class Trash
{
    /// <summary>
    /// 把文件或目录（连同其中的内容）移到回收站。
    /// Windows 使用回收站；macOS 移到 ~/.Trash；Linux 按 freedesktop.org 回收站规范放入 ~/.local/share/Trash。
    /// </summary>
    /// <exception cref="FileNotFoundException">路径不存在。</exception>
    /// <exception cref="IOException">无法移到回收站。</exception>
    public static void MoveToTrash(string path)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Path.Exists(fullPath))
            throw new FileNotFoundException("The file or directory to move to the trash does not exist.", fullPath);

        switch (PlatformInfo.Os)
        {
            case OsKind.Windows:
                WindowsRecycleBin.Move(fullPath);
                break;
            case OsKind.MacOs:
                UnixTrash.MoveToMacTrash(fullPath, Path.Combine(SpecialFolders.Get(Environment.SpecialFolder.UserProfile), ".Trash"));
                break;
            default:
                // 在 Linux 上 LocalApplicationData 即 $XDG_DATA_HOME，未设置时为 ~/.local/share
                UnixTrash.MoveToFreedesktopTrash(fullPath, Path.Combine(SpecialFolders.Get(Environment.SpecialFolder.LocalApplicationData), "Trash"), DateTime.Now);
                break;
        }
    }
}
