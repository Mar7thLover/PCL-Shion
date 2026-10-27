using System.Text;

namespace PCL.Shion.Core.IO;

/// <summary>同目录写入后再原子替换，失败时保留原文件；不同写入者不会共用临时文件。</summary>
public static class AtomicFile
{
    // 固定数量的锁避免为每个历史路径永久保留一个锁对象。
    private static readonly Lock[] Gates = Enumerable.Range(0, 64).Select(_ => new Lock()).ToArray();

    internal static Lock GateFor(string path)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        return Gates[(uint)comparer.GetHashCode(Path.GetFullPath(path)) % (uint)Gates.Length];
    }

    public static void WriteAllText(string path, string contents, bool keepBackup = false, bool ownerOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);
        path = Path.GetFullPath(path);
        lock (GateFor(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
                if (ownerOnly && !OperatingSystem.IsWindows())
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using (var stream = new FileStream(temporaryPath, options))
                {
                    var bytes = Encoding.UTF8.GetBytes(contents);
                    stream.Write(bytes);
                    stream.Flush(flushToDisk: true);
                }

                if (keepBackup && File.Exists(path))
                    File.Replace(temporaryPath, path, path + ".bak");
                else
                    File.Move(temporaryPath, path, overwrite: true);

                if (ownerOnly && !OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    if (File.Exists(path + ".bak"))
                        File.SetUnixFileMode(path + ".bak", UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
            finally
            {
                // 清理失败不应掩盖实际写入错误；进程突然退出时只会留下无关的唯一临时文件。
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
