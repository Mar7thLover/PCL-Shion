using System.Buffers;
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
    /// 列出 mods 目录下的 Mod，按显示名称（没有名称时用文件名）排序；单个文件损坏或不可读不清空其余结果。
    /// </summary>
    public static IReadOnlyList<LocalMod> Scan(string gameDirectory, ModScanOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ModScanOptions();
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var directory = new DirectoryInfo(GetModsDirectory(gameDirectory));
        if (!directory.Exists)
            return [];

        var mods = new List<LocalMod>();
        foreach (var file in directory.EnumerateFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsModFileName(file.Name))
                continue;
            try
            {
                mods.Add(Read(file.FullName, options, cancellationToken));
            }
            catch (FileNotFoundException)
            {
                // 扫描时被其他程序删除或改名，下次扫描会读到最新状态。
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                mods.Add(DescribeFile(file.FullName) with { ReadWarning = ex.Message });
            }
        }

        return mods
            .OrderBy(mod => mod.Name ?? mod.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 读取一个 Mod 文件：依次查找 fabric.mod.json、quilt.mod.json、META-INF/neoforge.mods.toml、
    /// META-INF/mods.toml 与 mcmod.info。文件不是有效的 jar 或没有元数据时，只有文件信息与 SHA-1 有值。
    /// </summary>
    public static LocalMod Read(string filePath, ModScanOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ModScanOptions();
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var file = DescribeFile(filePath);
        if (!options.ParseMetadata)
            return file;

        // 游戏运行时 jar 可能正被占用，只要求允许别人同时读写和删除
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var sha1 = ComputeHash(stream, cancellationToken);
        stream.Position = 0;

        var budget = new ModReadBudget(options, cancellationToken);
        var metadata = ReadMetadata(stream, budget);
        return file with
        {
            Id = metadata.Id,
            Name = metadata.Name,
            Version = metadata.Version,
            Loaders = metadata.Loaders,
            Dependencies = metadata.Dependencies,
            Sha1 = sha1,
            Provides = metadata.Provides,
            MetadataComplete = budget.Warning is null,
            ReadWarning = budget.Warning
        };
    }

    private static LocalMod DescribeFile(string filePath)
    {
        var name = Path.GetFileName(filePath);
        return new LocalMod(filePath, name, GetEnabledFileName(name) == name, null, null, null, [], [], "")
        {
            MetadataComplete = false
        };
    }

    private static string ComputeHash(Stream stream, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    break;
                hash.AppendData(buffer, 0, read);
            }
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ModMetadata ReadMetadata(Stream stream, ModReadBudget budget)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            return ModArchiveReader.Read(archive, budget);
        }
        catch (InvalidDataException ex)
        {
            // 损坏的 jar 仍要列出来，用户才能禁用或删除它
            budget.Warn(ex.Message);
            return ModMetadata.Empty;
        }
    }

    /// <summary>只移除末尾的禁用标记，保留文件名中间的 .old / .disabled 文本。</summary>
    public static string GetEnabledFileName(string fileName)
    {
        if (fileName.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
            return fileName[..^DisabledSuffix.Length];
        return fileName.EndsWith(".old", StringComparison.OrdinalIgnoreCase) ? fileName[..^4] : fileName;
    }

    public static bool IsModFileName(string fileName)
    {
        var enabledName = GetEnabledFileName(fileName);
        return enabledName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
               || enabledName.EndsWith(".litemod", StringComparison.OrdinalIgnoreCase);
    }
}
