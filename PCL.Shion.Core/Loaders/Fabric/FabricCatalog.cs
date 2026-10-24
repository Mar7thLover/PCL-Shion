using System.Text.Json;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Loaders.Fabric;

/// <summary>
/// 从 Fabric 或 Quilt 的 meta 服务获取某个原版版本可用的加载器版本。
/// </summary>
internal static class FabricCatalog
{
    /// <summary>
    /// 返回接口给出的顺序，较新的在前。
    /// </summary>
    public static Task<IReadOnlyList<LoaderVersion>> GetVersionsAsync(
        LoaderKind kind,
        string minecraftVersion,
        HttpClient http,
        DownloadSource source,
        CancellationToken cancellationToken)
    {
        return FabricMeta.FetchAsync(
            http,
            FabricMeta.GetLoaderListUrls(kind, minecraftVersion, source),
            json => Parse(kind, minecraftVersion, json),
            cancellationToken);
    }

    /// <summary>
    /// 解析加载器列表。Fabric 的条目带有 stable 标记；Quilt 没有，按版本号是否带预发布后缀（如 -beta.1）判断。
    /// </summary>
    /// <exception cref="JsonException">内容不是合法的 JSON，或缺少必需字段。</exception>
    internal static IReadOnlyList<LoaderVersion> Parse(LoaderKind kind, string minecraftVersion, string json)
    {
        var entries = JsonSerializer.Deserialize<List<EntryDto>>(json, JsonSerializerOptions.Web)
            ?? throw new JsonException("The loader list must be a JSON array.");

        return entries.ConvertAll(entry =>
            new LoaderVersion(kind, entry.Loader.Version, minecraftVersion, entry.Loader.Stable ?? !entry.Loader.Version.Contains('-')));
    }

    private sealed class EntryDto
    {
        public required LoaderDto Loader { get; init; }
    }

    private sealed class LoaderDto
    {
        public required string Version { get; init; }

        public bool? Stable { get; init; }
    }
}
