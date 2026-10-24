using System.Text.Json;

namespace PCL.Shion.Core.Loaders.Cleanroom;

/// <summary>
/// 获取 Cleanroom 的全部发布版本。数据来自 GitHub 的发布接口，没有镜像源。
/// </summary>
public static class CleanroomCatalog
{
    /// <summary>发布列表接口的地址。</summary>
    public const string ReleasesUrl = "https://api.github.com/repos/CleanroomMC/Cleanroom/releases";

    /// <summary>
    /// 列出全部 Cleanroom 发布，较新的在前。
    /// </summary>
    /// <exception cref="HttpRequestException">无法访问 GitHub，或返回了错误状态码。</exception>
    /// <exception cref="InvalidDataException">返回内容无法解析，或没有任何发布。</exception>
    public static async Task<IReadOnlyList<CleanroomVersion>> GetVersionsAsync(
        HttpClient http,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesUrl);
        request.Headers.UserAgent.ParseAdd("PCL-Shion");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Parse(json);
    }

    /// <summary>
    /// 解析发布列表，按版本号从新到旧排序。
    /// </summary>
    internal static IReadOnlyList<CleanroomVersion> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var versions = new List<CleanroomVersion>();
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("tag_name", out var tag) && tag.ValueKind == JsonValueKind.String)
                versions.Add(new CleanroomVersion(tag.GetString()!));
        }

        if (versions.Count == 0)
            throw new InvalidDataException("No Cleanroom release is available.");

        return versions.OrderByDescending(version => ToVersion(version.Number)).ToList();
    }

    private static Version ToVersion(string text) => Version.TryParse(text, out var version) ? version : new Version(0, 0);
}
