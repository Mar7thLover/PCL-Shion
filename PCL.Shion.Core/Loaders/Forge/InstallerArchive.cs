using System.IO.Compression;

namespace PCL.Shion.Core.Loaders.Forge;

/// <summary>
/// 以只读方式打开的安装器 jar。条目名可以带开头的 /，这是 install_profile.json 中的写法。
/// </summary>
internal sealed class InstallerArchive(string filePath) : IDisposable
{
    private readonly ZipArchive _archive = ZipFile.OpenRead(filePath);

    /// <summary>安装器 jar 的完整路径。</summary>
    public string FilePath { get; } = filePath;

    public bool Contains(string entryName)
    {
        return Find(entryName) is not null;
    }

    /// <exception cref="InvalidDataException">安装器中没有这个条目。</exception>
    public string ReadText(string entryName)
    {
        using var reader = new StreamReader(Get(entryName).Open());
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 把条目解压到 <paramref name="targetPath"/>，覆盖已有的文件。
    /// </summary>
    /// <exception cref="InvalidDataException">安装器中没有这个条目。</exception>
    public void Extract(string entryName, string targetPath)
    {
        var entry = Get(entryName);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        entry.ExtractToFile(targetPath, overwrite: true);
    }

    public void Dispose()
    {
        _archive.Dispose();
    }

    private ZipArchiveEntry? Find(string entryName)
    {
        return _archive.GetEntry(entryName.TrimStart('/'));
    }

    private ZipArchiveEntry Get(string entryName)
    {
        return Find(entryName) ?? throw new InvalidDataException($"The installer has no entry '{entryName}'.");
    }
}
