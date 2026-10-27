using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace PCL.Shion.Core.Mods;

/// <summary>
/// 读取 jar 内的全部元数据文件并合并。一个 jar 可以同时支持多个加载器。
/// </summary>
internal static class ModArchiveReader
{
    private const string JarVersionPlaceholder = "${file.jarVersion}";

    // Fabric、Quilt 与 Forge、NeoForge 存放内嵌 jar 的目录
    private static readonly string[] NestedJarDirectories = ["META-INF/jars/", "META-INF/jarjar/"];

    /// <summary>
    /// 合并 jar 内各格式的元数据：名称、ID、版本取第一个读到的值，依赖按 ID 去重，
    /// 内嵌 jar 的 Mod ID 计入提供的 ID。损坏的元数据文件视同不存在。
    /// </summary>
    /// <exception cref="InvalidDataException">jar 不是有效的压缩包。</exception>
    public static ModMetadata Read(ZipArchive archive, ModReadBudget budget, int depth = 0)
    {
        budget.CancellationToken.ThrowIfCancellationRequested();
        var parsed = new List<ModMetadata>();
        AddFormat(parsed, archive, "fabric.mod.json", ModMetadataParser.ParseFabric, budget);
        AddFormat(parsed, archive, "quilt.mod.json", ModMetadataParser.ParseQuilt, budget);
        AddFormat(parsed, archive, "META-INF/neoforge.mods.toml", toml => ModMetadataParser.ParseForge(toml, true), budget);
        AddFormat(parsed, archive, "META-INF/mods.toml", toml => ModMetadataParser.ParseForge(toml, false), budget);
        AddFormat(parsed, archive, "mcmod.info", ModMetadataParser.ParseLegacy, budget);

        return new ModMetadata(
            parsed.Select(metadata => metadata.Id).FirstOrDefault(id => id is not null),
            parsed.Select(metadata => metadata.Name).FirstOrDefault(name => name is not null),
            parsed.Select(metadata => metadata.Version).FirstOrDefault(version => version is not null),
            parsed.SelectMany(metadata => metadata.Loaders).Distinct().ToList(),
            parsed.SelectMany(metadata => metadata.Dependencies).DistinctBy(dependency => dependency.Id).ToList(),
            parsed.SelectMany(metadata => metadata.Provides).Concat(ReadNestedIds(archive, budget, depth)).Distinct().ToList());
    }

    private static void AddFormat(List<ModMetadata> parsed, ZipArchive archive, string entryName, Func<string, ModMetadata> parse,
        ModReadBudget budget)
    {
        if (archive.GetEntry(entryName) is not { } entry)
            return;

        try
        {
            if (ReadText(entry, budget) is not { } text)
                return;
            var metadata = parse(text);
            parsed.Add(metadata with { Version = ResolveVersion(metadata.Version, archive, budget) });
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidDataException)
        {
            // 元数据文件损坏时视同没有，不影响其余格式的读取
            budget.Warn(ex.Message);
        }
    }

    // ${file.jarVersion} 表示取 MANIFEST.MF 的 Implementation-Version；其他没被替换掉的占位符没有意义
    private static string? ResolveVersion(string? version, ZipArchive archive, ModReadBudget budget)
    {
        if (version == JarVersionPlaceholder)
            version = ReadManifestVersion(archive, budget);
        return version is null || version.StartsWith("${", StringComparison.Ordinal) ? null : version;
    }

    private static string? ReadManifestVersion(ZipArchive archive, ModReadBudget budget)
    {
        if (archive.GetEntry("META-INF/MANIFEST.MF") is not { } entry)
            return null;

        const string key = "Implementation-Version:";
        foreach (var line in (ReadText(entry, budget) ?? "").Split('\n'))
        {
            if (line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                return line[key.Length..].Trim();
        }
        return null;
    }

    // 内嵌的 jar 也是 Mod（例如 Fabric API 的各个模块），其他 Mod 可以依赖它们
    private static List<string> ReadNestedIds(ZipArchive archive, ModReadBudget budget, int depth)
    {
        var ids = new List<string>();
        foreach (var entry in archive.Entries.Where(IsNestedJar))
        {
            budget.CancellationToken.ThrowIfCancellationRequested();
            if (!budget.TryEnterNested(depth))
                break;
            try
            {
                using var buffer = budget.ReadEntry(entry, budget.Options.MaxNestedJarBytes);
                if (buffer is null)
                    continue;

                using var nestedArchive = new ZipArchive(buffer, ZipArchiveMode.Read);
                var nested = Read(nestedArchive, budget, depth + 1);
                if (nested.Id is not null)
                    ids.Add(nested.Id);
                ids.AddRange(nested.Provides);
            }
            catch (InvalidDataException ex)
            {
                // 内嵌文件不是有效的压缩包时忽略，它本来就不会被加载器当作 Mod
                budget.Warn(ex.Message);
            }
        }
        return ids;
    }

    private static bool IsNestedJar(ZipArchiveEntry entry) =>
        entry.FullName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
        && NestedJarDirectories.Any(directory => entry.FullName.StartsWith(directory, StringComparison.Ordinal));

    private static string? ReadText(ZipArchiveEntry entry, ModReadBudget budget)
    {
        using var buffer = budget.ReadEntry(entry, budget.Options.MaxMetadataBytes);
        if (buffer is null)
            return null;
        using var reader = new StreamReader(buffer, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
