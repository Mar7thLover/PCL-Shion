using System.Text.Json;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Launch;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Install;

/// <summary>
/// 列出一个版本运行所需、并且知道从哪里下载的文件。
/// </summary>
public static class RequirementCollector
{
    private const string LibrariesUrl = "https://libraries.minecraft.net/";
    private const string ResourcesUrl = "https://resources.download.minecraft.net/";

    /// <summary>
    /// 客户端 jar、按当前系统的规则筛出的库（普通库与 natives）、资源索引与日志配置。
    /// 筛选与去重和启动时一致；没有下载地址的文件不在其列，例如 Forge 安装器在本地生成的库。
    /// </summary>
    public static IReadOnlyList<DownloadItem> Collect(string minecraftRoot, ResolvedVersion version, DownloadSource source)
    {
        return Collect(minecraftRoot, version, source, RuleEnvironment.ForCurrentPlatform(new HashSet<string>()));
    }

    internal static IReadOnlyList<DownloadItem> Collect(
        string minecraftRoot,
        ResolvedVersion version,
        DownloadSource source,
        RuleEnvironment environment)
    {
        var items = new List<DownloadItem>();
        if (version.ClientJarDownload is { } clientJar)
            items.Add(CreateItem(clientJar, version.ClientJarPath, source));

        var librariesDirectory = Path.Combine(minecraftRoot, "libraries");
        foreach (var library in LibrarySelector.Select(version.Libraries, environment))
        {
            if (GetLibraryUrl(library) is not { } url)
                continue;

            var path = Path.Combine(librariesDirectory, library.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            items.Add(new DownloadItem(MirrorUrls.Resolve(url, source), path, library.Download?.Size, library.Download?.Sha1));
        }

        if (version.AssetIndexDownload is { } assetIndex)
            items.Add(CreateItem(assetIndex, GetAssetIndexPath(minecraftRoot, version.AssetIndexId), source));
        if (version is { LoggingFileId: { } loggingFileId, LoggingFileDownload: { } loggingFile })
            items.Add(CreateItem(loggingFile, Path.Combine(minecraftRoot, "assets", "log_configs", loggingFileId), source));
        return items;
    }

    /// <summary>
    /// 读取已下载的资源索引，列出其中全部资源对象，同一哈希只列一次。
    /// legacy / virtual 索引的资源同样下载到 objects，启动时再按原名复制。
    /// </summary>
    /// <exception cref="IOException">资源索引不存在或无法读取。</exception>
    /// <exception cref="JsonException">资源索引不是合法的 JSON。</exception>
    public static IReadOnlyList<DownloadItem> CollectAssetObjects(string minecraftRoot, string assetIndexId, DownloadSource source)
    {
        using var stream = File.OpenRead(GetAssetIndexPath(minecraftRoot, assetIndexId));
        using var document = JsonDocument.Parse(stream);
        var objectsDirectory = Path.Combine(minecraftRoot, "assets", "objects");

        var items = new List<DownloadItem>();
        var hashes = new HashSet<string>();
        foreach (var asset in document.RootElement.GetProperty("objects").EnumerateObject())
        {
            var hash = asset.Value.GetProperty("hash").GetString()!;
            if (!hashes.Add(hash))
                continue;

            var prefix = hash[..2];
            items.Add(new DownloadItem(
                MirrorUrls.Resolve($"{ResourcesUrl}{prefix}/{hash}", source),
                Path.Combine(objectsDirectory, prefix, hash),
                asset.Value.GetProperty("size").GetInt64(),
                hash));
        }
        return items;
    }

    internal static string GetAssetIndexPath(string minecraftRoot, string assetIndexId)
    {
        return Path.Combine(minecraftRoot, "assets", "indexes", $"{assetIndexId}.json");
    }

    private static DownloadItem CreateItem(FileDownload download, string targetPath, DownloadSource source)
    {
        return new DownloadItem(MirrorUrls.Resolve(download.Url, source), targetPath, download.Size, download.Sha1);
    }

    /// <summary>
    /// 优先使用声明的地址；未声明时在库所在的 Maven 仓库（没有则为 Mojang 的库服务器）下按路径拼接。
    /// 声明为空字符串表示这个库没有下载地址，返回 null。
    /// </summary>
    private static string? GetLibraryUrl(SelectedLibrary library)
    {
        return library.Download?.Url switch
        {
            null => CombineUrl(library.RepositoryUrl ?? LibrariesUrl, library.RelativePath),
            "" => null,
            var url => url
        };
    }

    private static string CombineUrl(string baseUrl, string relativePath)
    {
        return baseUrl.EndsWith('/') ? baseUrl + relativePath : $"{baseUrl}/{relativePath}";
    }
}
