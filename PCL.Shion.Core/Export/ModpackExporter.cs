using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.Shion.Core.Export;

/// <summary>
/// 需要联网查询下载地址的本地文件（压缩包形式的模组或资源包）。
/// </summary>
public sealed class ExportFile
{
    private ExportFile(string path, string modrinthHash, uint curseForgeHash)
    {
        Path = path;
        ModrinthHash = modrinthHash;
        CurseForgeHash = curseForgeHash;
    }

    /// <summary>文件在导出目录中的完整路径。</summary>
    public string Path { get; }

    /// <summary>Modrinth 使用的 SHA-1（小写十六进制）。</summary>
    public string ModrinthHash { get; }

    /// <summary>CurseForge 使用的 Murmur2 指纹（忽略空白字节后计算）。</summary>
    public uint CurseForgeHash { get; }

    public static ExportFile Create(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return new ExportFile(path, Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant(), CurseForgeFingerprint(bytes));
    }

    /// <summary>
    /// CurseForge 指纹：去掉制表符、换行、回车与空格后计算 MurmurHash2（种子为 1）。
    /// </summary>
    private static uint CurseForgeFingerprint(byte[] data)
    {
        var filtered = data.Where(b => b is not (0x09 or 0x0A or 0x0D or 0x20)).ToArray();
        const uint m = 0x5bd1e995;
        const int r = 24;
        uint h = 1u ^ (uint)filtered.Length;
        var index = 0;
        for (; index + 4 <= filtered.Length; index += 4)
        {
            uint k = BitConverter.ToUInt32(filtered, index);
            k *= m;
            k ^= k >> r;
            k *= m;
            h *= m;
            h ^= k;
        }

        var remaining = filtered.Length - index;
        if (remaining >= 3) h ^= (uint)filtered[index + 2] << 16;
        if (remaining >= 2) h ^= (uint)filtered[index + 1] << 8;
        if (remaining >= 1)
        {
            h ^= filtered[index];
            h *= m;
        }

        h ^= h >> 13;
        h *= m;
        h ^= h >> 15;
        return h;
    }
}

/// <summary>
/// 在线查询得到的结果：每个文件对应的下载地址，以及失败的查询。
/// </summary>
public sealed record ExportLookupResult(
    IReadOnlyDictionary<ExportFile, List<string>> Urls,
    IReadOnlyList<Exception> Failures);

/// <summary>
/// 整合包导出的文件操作：按规则复制实例文件、联网查询下载地址、生成 modrinth.index.json 并打包。
/// </summary>
public static class ModpackExporter
{
    /// <summary>根目录下跳过的文件夹（文件多且无用）。</summary>
    private static readonly string[] RootSkippedFolders = ["assets", "versions", "libraries"];

    /// <summary>任何层级都跳过的文件夹。</summary>
    private static readonly string[] AlwaysSkippedFolders =
        ["structureCacheV1", ".fabric", ".git", "avatar-cache", "cosmetic-cache"];

    /// <summary>需要联网查询下载地址的文件扩展名。</summary>
    private static readonly string[] HostedExtensions = [".zip", ".rar", ".jar", ".disabled", ".old"];

    /// <summary>需要联网查询下载地址的文件所在文件夹关键字。</summary>
    private static readonly string[] HostedFolderKeywords = ["mods", "packs", "openloader", "resource"];

    /// <summary>
    /// 把 <paramref name="indieRoot"/> 下符合规则的文件复制到 <paramref name="overridesFolder"/>，并返回需要联网查询的压缩包。
    /// 当 <paramref name="checkHostedAssets"/> 为假时不返回任何需要查询的文件。
    /// </summary>
    public static List<ExportFile> CopyOverrides(string indieRoot, string overridesFolder, IReadOnlyList<string> rules,
        bool checkHostedAssets, IProgress<double>? progress = null)
    {
        var hosted = new List<ExportFile>();
        var processed = 0;
        var root = new DirectoryInfo(indieRoot);
        if (!root.Exists)
            return hosted;

        void SearchFolder(DirectoryInfo folder)
        {
            foreach (var subFolder in folder.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                if (folder.FullName == root.FullName && RootSkippedFolders.Contains(subFolder.Name, StringComparer.OrdinalIgnoreCase))
                    continue;
                if (AlwaysSkippedFolders.Contains(subFolder.Name, StringComparer.OrdinalIgnoreCase))
                    continue;
                SearchFolder(subFolder);
            }

            foreach (var entry in folder.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                var relativePath = Path.GetRelativePath(root.FullName, entry.FullName);
                var shouldKeep = false;
                foreach (var rule in rules)
                {
                    var revert = rule.StartsWith('!');
                    if (ExportRules.Matches(relativePath, rule.TrimStart('!')))
                        shouldKeep = !revert;
                }

                if (!shouldKeep)
                    continue;

                var targetPath = Path.Combine(overridesFolder, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(entry.FullName, targetPath, true);
                if (checkHostedAssets &&
                    HostedExtensions.Contains(entry.Extension.ToLowerInvariant()) &&
                    HostedFolderKeywords.Any(keyword => relativePath.Contains(keyword, StringComparison.Ordinal)))
                    hosted.Add(ExportFile.Create(targetPath));

                processed += 1;
                if (processed % 25 == 0)
                    progress?.Report(processed);
            }
        }

        SearchFolder(root);
        return hosted;
    }

    /// <summary>
    /// 把 <paramref name="sourceFolder"/> 整个复制到 <paramref name="targetFolder"/>，不存在则不做任何事。
    /// </summary>
    public static void CopyDirectory(string sourceFolder, string targetFolder)
    {
        if (!Directory.Exists(sourceFolder))
            return;
        foreach (var directory in Directory.EnumerateDirectories(sourceFolder, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(targetFolder, Path.GetRelativePath(sourceFolder, directory)));
        foreach (var file in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(targetFolder, Path.GetRelativePath(sourceFolder, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    /// <summary>
    /// 通过 Modrinth 与 CurseForge 查询文件的下载地址。两个平台并行查询，单个平台失败不影响另一个，失败原因放在结果的 <see cref="ExportLookupResult.Failures"/> 中。
    /// CurseForge 需要 API Key（构建时写入，或通过环境变量 <c>PCL_CURSEFORGE_API_KEY</c> 提供），未提供时跳过 CurseForge。
    /// </summary>
    public static async Task<ExportLookupResult> LookupDownloadUrlsAsync(HttpClient http, IReadOnlyList<ExportFile> files,
        bool modrinthUploadMode, CancellationToken cancellationToken)
    {
        var urls = new Dictionary<ExportFile, List<string>>();
        var failures = new List<Exception>();
        var gate = new object();

        void Add(ExportFile file, string url)
        {
            lock (gate)
            {
                if (!urls.TryGetValue(file, out var list))
                    urls[file] = list = [];
                list.Add(url);
            }
        }

        var modrinthTask = LookupModrinthAsync(http, files, Add, cancellationToken);
        var curseForgeKey = BuildSecrets.CurseForgeApiKey;
        var curseForgeTask = modrinthUploadMode || string.IsNullOrWhiteSpace(curseForgeKey)
            ? Task.CompletedTask
            : LookupCurseForgeAsync(http, files, curseForgeKey, Add, cancellationToken);

        var modrinthResult = await SafeAwait(modrinthTask).ConfigureAwait(false);
        var curseForgeResult = await SafeAwait(curseForgeTask).ConfigureAwait(false);
        if (modrinthResult is not null) failures.Add(modrinthResult);
        if (curseForgeResult is not null) failures.Add(curseForgeResult);
        return new ExportLookupResult(urls, failures);
    }

    private static async Task<Exception?> SafeAwait(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static async Task LookupModrinthAsync(HttpClient http, IReadOnlyList<ExportFile> files,
        Action<ExportFile, string> add, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
            return;
        var body = new JsonObject
        {
            ["hashes"] = new JsonArray(files.Select(f => (JsonNode)f.ModrinthHash).ToArray()),
            ["algorithm"] = "sha1"
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.modrinth.com/v2/version_files")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var raw = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))!.AsObject();
        foreach (var file in files)
        {
            if (raw[file.ModrinthHash] is not JsonObject version)
                continue;
            var firstFile = version["files"]?.AsArray().FirstOrDefault() as JsonObject;
            if (firstFile?["hashes"]?["sha1"]?.GetValue<string>() != file.ModrinthHash)
                continue;
            var url = firstFile["url"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(url))
                add(file, url);
        }
    }

    private static async Task LookupCurseForgeAsync(HttpClient http, IReadOnlyList<ExportFile> files, string apiKey,
        Action<ExportFile, string> add, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
            return;
        var body = new JsonObject
        {
            ["fingerprints"] = new JsonArray(files.Select(f => (JsonNode)f.CurseForgeHash).ToArray())
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.curseforge.com/v1/fingerprints/432")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var raw = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))!;
        var matches = raw["data"]?["exactMatches"]?.AsArray() ?? [];
        foreach (var match in matches)
        {
            if (match?["file"] is not JsonObject file)
                continue;
            var downloadUrl = file["downloadUrl"]?.GetValue<string>();
            if (string.IsNullOrEmpty(downloadUrl))
                continue;
            var fingerprint = file["fileFingerprint"]?.GetValue<uint>();
            var target = files.FirstOrDefault(f => f.CurseForgeHash == fingerprint);
            if (target is not null)
                add(target, downloadUrl);
        }
    }

    /// <summary>
    /// 生成 <c>modrinth.index.json</c> 的内容。
    /// </summary>
    public static string BuildIndex(string overridesFolder, IReadOnlyDictionary<ExportFile, List<string>> urls,
        string minecraftVersion, string? forgeVersion, string? fabricVersion, string? neoForgeVersion,
        string packVersion, string packName, string summary)
    {
        var files = new JsonArray();
        foreach (var pair in urls)
        {
            files.Add(new JsonObject
            {
                ["path"] = Path.GetRelativePath(overridesFolder, pair.Key.Path).Replace('\\', '/'),
                ["hashes"] = new JsonObject
                {
                    ["sha1"] = pair.Key.ModrinthHash,
                    ["sha512"] = Convert.ToHexString(SHA512.HashData(File.ReadAllBytes(pair.Key.Path))).ToLowerInvariant()
                },
                ["downloads"] = new JsonArray(pair.Value
                    .OrderByDescending(u => u.Contains("modrinth.com", StringComparison.Ordinal))
                    .Select(s => (JsonNode)s).ToArray()),
                ["fileSize"] = new FileInfo(pair.Key.Path).Length
            });
        }

        var dependencies = new JsonObject { ["minecraft"] = minecraftVersion };
        if (forgeVersion is not null) dependencies["forge"] = forgeVersion;
        if (fabricVersion is not null) dependencies["fabric-loader"] = fabricVersion;
        if (neoForgeVersion is not null) dependencies["neoforge"] = neoForgeVersion;
        var result = new JsonObject
        {
            ["game"] = "minecraft",
            ["formatVersion"] = 1,
            ["versionId"] = packVersion,
            ["name"] = packName,
            ["summary"] = summary,
            ["files"] = files,
            ["dependencies"] = dependencies
        };
        return result.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
