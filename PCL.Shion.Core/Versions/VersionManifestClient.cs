using System.Text.Json;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Versions;

/// <summary>
/// 获取并解析 Mojang 版本清单（version_manifest_v2.json）。
/// </summary>
public static class VersionManifestClient
{
    /// <summary>
    /// 官方版本清单地址，镜像地址由 <see cref="MirrorUrls"/> 推导。
    /// </summary>
    public const string ManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

    /// <summary>
    /// 解析版本清单 JSON。内容不是合法的 JSON，或缺少必需字段时抛出 <see cref="JsonException"/>。
    /// </summary>
    public static VersionManifest Parse(string json)
    {
        var manifest = JsonSerializer.Deserialize<ManifestDto>(json, JsonSerializerOptions.Web)
            ?? throw new JsonException("The version manifest must be a JSON object.");

        return new VersionManifest(
            manifest.Latest.Release,
            manifest.Latest.Snapshot,
            manifest.Versions.ConvertAll(version =>
                new RemoteVersion(version.Id, version.Type, version.ReleaseTime, version.Url, version.Sha1)));
    }

    /// <summary>
    /// 按 <see cref="MirrorUrls"/> 给出的顺序依次尝试，返回第一个成功获取并解析的清单。
    /// 全部地址都失败时抛出最后一个地址的异常；取消时立即抛出，不再尝试后面的地址。
    /// </summary>
    public static async Task<VersionManifest> FetchAsync(HttpClient http, DownloadSource source, CancellationToken cancellationToken)
    {
        var urls = MirrorUrls.Resolve(ManifestUrl, source);
        foreach (var url in urls.SkipLast(1))
        {
            try
            {
                return await FetchFromAsync(http, url, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // 这个地址不可用，换下一个
            }
        }
        return await FetchFromAsync(http, urls.Last(), cancellationToken);
    }

    private static async Task<VersionManifest> FetchFromAsync(HttpClient http, Uri url, CancellationToken cancellationToken)
    {
        return Parse(await http.GetStringAsync(url, cancellationToken));
    }

    private sealed class ManifestDto
    {
        public required LatestDto Latest { get; init; }

        public required List<VersionDto> Versions { get; init; }
    }

    private sealed class LatestDto
    {
        public required string Release { get; init; }

        public required string Snapshot { get; init; }
    }

    private sealed class VersionDto
    {
        public required string Id { get; init; }

        public required string Type { get; init; }

        public required Uri Url { get; init; }

        public required DateTimeOffset ReleaseTime { get; init; }

        public string? Sha1 { get; init; }
    }
}
