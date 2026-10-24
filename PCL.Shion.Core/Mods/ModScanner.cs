using System.IO.Compression;
using System.Security.Cryptography;

namespace PCL.Shion.Core.Mods;

/// <summary>
/// 扫描并读取游戏目录下 mods 文件夹里的 Mod。
/// </summary>
public static class ModScanner
{
    /// <summary>已禁用的 Mod 在 .jar 之后追加的后缀。</summary>
    internal const string DisabledSuffix = ".disabled";

    /// <summary>
    /// 游戏目录下存放 Mod 的目录。
    /// </summary>
    public static string GetModsDirectory(string gameDirectory) => Path.Combine(gameDirectory, "mods");

    /// <summary>
    /// 列出 mods 目录下的 *.jar 与 *.jar.disabled，按显示名称（没有名称时用文件名）排序；目录不存在时返回空列表。
    /// </summary>
    public static IReadOnlyList<LocalMod> Scan(string gameDirectory)
    {
        var directory = new DirectoryInfo(GetModsDirectory(gameDirectory));
        if (!directory.Exists)
            return [];

        return directory.EnumerateFiles()
            .Where(file => IsModFile(file.Name))
            .Select(file => Read(file.FullName))
            .OrderBy(mod => mod.Name ?? mod.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 读取一个 Mod 文件：依次查找 fabric.mod.json、quilt.mod.json、META-INF/neoforge.mods.toml、
    /// META-INF/mods.toml 与 mcmod.info。文件不是有效的 jar 或没有元数据时，只有文件信息与 SHA-1 有值。
    /// </summary>
    public static LocalMod Read(string filePath)
    {
        // 游戏运行时 jar 可能正被占用，只要求允许别人同时读写和删除
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var sha1 = Convert.ToHexStringLower(SHA1.HashData(stream));
        stream.Position = 0;

        var metadata = ReadMetadata(stream);
        var fileName = Path.GetFileName(filePath);
        return new LocalMod(
            filePath,
            fileName,
            !fileName.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase),
            metadata.Id,
            metadata.Name,
            metadata.Version,
            metadata.Loaders,
            metadata.Dependencies,
            sha1)
        {
            Provides = metadata.Provides
        };
    }

    private static ModMetadata ReadMetadata(Stream stream)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            return ModArchiveReader.Read(archive);
        }
        catch (InvalidDataException)
        {
            // 损坏的 jar 仍要列出来，用户才能禁用或删除它
            return ModMetadata.Empty;
        }
    }

    private static bool IsModFile(string fileName) =>
        fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".jar" + DisabledSuffix, StringComparison.OrdinalIgnoreCase);
}
