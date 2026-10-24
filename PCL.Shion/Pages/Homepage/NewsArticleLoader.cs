using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Shion.Pages.Homepage;

/// <summary>
/// 获取并解析 Minecraft 新闻文章的正文。解析结果在本次运行期间按链接缓存在内存里。
/// </summary>
public static class NewsArticleLoader
{
    private const int MaxCacheCount = 64;

    private static readonly ConcurrentDictionary<string, IReadOnlyList<NewsArticleBlock>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取文章正文。只会请求 <see cref="NewsViewModel.CanOpenNewsLink"/> 允许的链接，其他链接会抛出 <see cref="ArgumentException"/>。
    /// 请求失败时抛出异常，由调用方决定如何提示；没有解析出任何正文时返回空列表，且不会缓存。
    /// </summary>
    /// <param name="url">文章链接，必须是绝对地址。</param>
    /// <param name="reload">是否忽略缓存，重新请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="HttpRequestException">请求失败或服务器返回了错误状态。</exception>
    public static async Task<IReadOnlyList<NewsArticleBlock>> LoadAsync(
        string? url, bool reload = false, CancellationToken cancellationToken = default)
    {
        if (!NewsViewModel.CanOpenNewsLink(url))
            throw new ArgumentException($"Not an allowed news link: {url ?? "<null>"}", nameof(url));
        if (!reload && Cache.TryGetValue(url, out var cached))
            return cached;

        var html = await NewsHttp.Client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        var blocks = NewsArticleParser.Parse(html, url);
        if (blocks.Count == 0)
            return blocks;

        if (Cache.Count >= MaxCacheCount)
            Cache.Clear();
        Cache[url] = blocks;
        return blocks;
    }
}
