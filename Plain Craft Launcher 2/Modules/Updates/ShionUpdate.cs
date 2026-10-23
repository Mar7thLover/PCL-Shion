using System.Net.Http;
using System.Text.Json.Nodes;
using PCL.Network;

namespace PCL;

/// <summary>
/// 从 GitHub Releases 读取 PCL Shion 的版本信息。<br/>
/// 仅用于展示最新版本与更新日志，不会下载或替换程序。
/// </summary>
public static class ShionUpdate
{
    private const string ReleasesApi =
        "https://api.github.com/repos/Mar7thLover/PCL-Shion/releases?per_page=30";

    /// <summary>
    /// 一条 GitHub Release。
    /// </summary>
    public sealed class ReleaseInfo(string tag, string version, string body, string url, bool isPrerelease)
    {
        /// <summary>原始标签，如 <c>v1.0.0</c>。</summary>
        public string Tag { get; } = tag;

        /// <summary>去掉前缀与后缀的版本号，如 <c>1.0.0</c>。</summary>
        public string Version { get; } = version;

        /// <summary>发布说明（Markdown）。</summary>
        public string Body { get; } = body;

        /// <summary>发布页地址。</summary>
        public string Url { get; } = url;

        /// <summary>是否为预发布版本。</summary>
        public bool IsPrerelease { get; } = isPrerelease;
    }

    private static readonly SemaphoreSlim FetchLock = new(1, 1);
    private static List<ReleaseInfo>? _cache;
    private static DateTime _cacheTime;

    /// <summary>
    /// 获取发布列表。同一进程内缓存 5 分钟；网络失败会抛出异常。
    /// </summary>
    public static async Task<List<ReleaseInfo>> GetReleasesAsync(bool force = false)
    {
        if (!force && _cache is not null && DateTime.Now - _cacheTime < TimeSpan.FromMinutes(5))
            return _cache;

        await FetchLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!force && _cache is not null && DateTime.Now - _cacheTime < TimeSpan.FromMinutes(5))
                return _cache;

            var array = await Requester.FetchJsonAsync<JsonArray>(ReleasesApi, new RequestParam
            {
                Accept = "application/vnd.github+json",
                Timeout = 15000,
                Retries = 1
            }).ConfigureAwait(false);

            var releases = new List<ReleaseInfo>();
            foreach (var node in array)
            {
                if (node is not JsonObject obj) continue;
                if (obj["draft"]?.GetValue<bool>() is true) continue;
                var tag = obj["tag_name"]?.ToString() ?? string.Empty;
                var version = ParseTag(tag);
                if (version is null) continue;
                releases.Add(new ReleaseInfo(
                    tag,
                    version,
                    obj["body"]?.ToString() ?? string.Empty,
                    obj["html_url"]?.ToString() ?? ModBase.shionReleasesUrl,
                    obj["prerelease"]?.GetValue<bool>() is true));
            }

            _cache = releases;
            _cacheTime = DateTime.Now;
            return releases;
        }
        finally
        {
            FetchLock.Release();
        }
    }

    /// <summary>
    /// 版本号最高的正式版；没有正式版时退回预发布版。
    /// </summary>
    public static ReleaseInfo? FindLatest(IEnumerable<ReleaseInfo> releases)
    {
        var list = releases.ToList();
        return list.Where(r => !r.IsPrerelease).OrderByDescending(r => ParseVersion(r.Version)).FirstOrDefault()
               ?? list.OrderByDescending(r => ParseVersion(r.Version)).FirstOrDefault();
    }

    /// <summary>
    /// 找出指定版本对应的发布，用于读取更新日志。
    /// </summary>
    public static ReleaseInfo? FindForVersion(IEnumerable<ReleaseInfo> releases, string version)
    {
        var target = ParseVersion(version);
        return releases.FirstOrDefault(r => ParseVersion(r.Version) == target);
    }

    /// <summary>
    /// <paramref name="version"/> 是否比 <paramref name="current"/> 新。
    /// </summary>
    public static bool IsNewer(string version, string current) => ParseVersion(version) > ParseVersion(current);

    /// <summary>
    /// 把 <c>v1.0.0</c>、<c>1.0.0-beta.1</c> 之类的标签解析为 <c>1.0.0</c>；不是版本号时返回 null。
    /// </summary>
    public static string? ParseTag(string tag)
    {
        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
            text = text[1..];
        var suffix = text.IndexOfAny(['-', '+']);
        if (suffix >= 0) text = text[..suffix];
        if (!Version.TryParse(text, out var version)) return null;
        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0)).ToString();
    }

    private static Version ParseVersion(string text)
    {
        var parsed = ParseTag(text);
        return parsed is null ? new Version(0, 0, 0) : Version.Parse(parsed);
    }
}
