using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCL.Shion.Core.Updates;

/// <summary>
/// 通过 GitHub 的发布列表检查启动器是否有新版本。
/// </summary>
public static class UpdateChecker
{
    /// <summary>
    /// 发布列表的 API 地址。
    /// </summary>
    public const string LatestReleaseApi = "https://api.github.com/repos/Mar7thLover/PCL-Shion/releases";

    /// <summary>
    /// 获取版本号最高的已发布版本；没有可用的版本时返回 null。
    /// 标签不是版本号的发布、草稿都会被忽略；版本号相同时正式版优先于预发布版。
    /// </summary>
    /// <param name="http">GitHub 要求请求带有 User-Agent，<c>DownloadEngine.Http</c> 已经带上。</param>
    /// <param name="includePrerelease">是否把预发布版本也计入。</param>
    /// <exception cref="HttpRequestException">网络请求失败，或 GitHub 返回了错误状态（如超出访问频率限制）。</exception>
    /// <exception cref="JsonException">返回的内容不是发布列表。</exception>
    public static async Task<ReleaseInfo?> GetLatestAsync(HttpClient http, bool includePrerelease, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var releases = await JsonSerializer.DeserializeAsync<List<ReleaseDto>>(content, JsonSerializerOptions.Web, cancellationToken)
            ?? throw new JsonException("The release list must be a JSON array.");

        return releases
            .Select(ToReleaseInfo)
            .OfType<ReleaseInfo>()
            .Where(release => includePrerelease || !release.Prerelease)
            .OrderByDescending(release => Normalize(release.Version))
            .ThenBy(release => release.Prerelease)
            .ThenByDescending(release => release.PublishedAt)
            .FirstOrDefault();
    }

    /// <summary>
    /// 发布的版本号是否高于当前版本。缺少的版本号部分按 0 比较，所以 0.2.0 与 0.2.0.0 视为相同。
    /// </summary>
    public static bool IsNewer(ReleaseInfo release, Version current)
    {
        return Normalize(release.Version) > Normalize(current);
    }

    /// <summary>
    /// 从发布标签解析版本号。容忍 v 前缀，并忽略 - 或 + 之后的预发布与构建信息，
    /// 如 "v0.2.0"、"0.2.0-beta.1" 都得到 0.2.0。
    /// </summary>
    /// <returns>标签不是版本号时返回 false。</returns>
    public static bool TryParseTag(string tag, [MaybeNullWhen(false)] out Version version)
    {
        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
            text = text[1..];

        var suffix = text.IndexOfAny(['-', '+']);
        if (suffix >= 0)
            text = text[..suffix];

        return Version.TryParse(text, out version);
    }

    private static Version Normalize(Version version)
    {
        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
    }

    private static ReleaseInfo? ToReleaseInfo(ReleaseDto release)
    {
        if (release is not { Draft: false, PublishedAt: { } publishedAt } || !TryParseTag(release.TagName, out var version))
            return null;

        var name = string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name;
        return new ReleaseInfo(release.TagName, version, name, release.HtmlUrl, publishedAt, release.Prerelease);
    }

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public required string TagName { get; init; }

        public string? Name { get; init; }

        [JsonPropertyName("html_url")]
        public required Uri HtmlUrl { get; init; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; init; }

        public bool Prerelease { get; init; }

        public bool Draft { get; init; }
    }
}
