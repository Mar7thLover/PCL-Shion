using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;

namespace PCL.Core.ViewModel.Homepage;

/// <summary>
/// 获取并解析 Minecraft 新闻文章的正文。解析结果在本次运行期间按链接缓存在内存里。
/// </summary>
public static class NewsArticleLoader
{
    private const int _MaxCacheCount = 64;

    private static readonly ConcurrentDictionary<string, IReadOnlyList<NewsArticleBlock>> _Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 尝试从缓存读取文章正文。
    /// </summary>
    public static bool TryGetCached(string? url, out IReadOnlyList<NewsArticleBlock> blocks)
    {
        if (url is not null && _Cache.TryGetValue(url, out var cached))
        {
            blocks = cached;
            return true;
        }

        blocks = [];
        return false;
    }

    /// <summary>
    /// 获取文章正文。只会请求 <see cref="NewsViewModel.CanOpenNewsLink"/> 允许的链接，其他链接会抛出 <see cref="ArgumentException"/>。
    /// 请求失败时抛出异常，由调用方决定如何提示；没有解析出任何正文时返回空列表，且不会缓存。
    /// </summary>
    /// <param name="url">文章链接，必须是绝对地址。</param>
    /// <param name="reload">是否忽略缓存，重新请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static async Task<IReadOnlyList<NewsArticleBlock>> LoadAsync(
        string? url, bool reload = false, CancellationToken cancellationToken = default)
    {
        if (url is null || !NewsViewModel.CanOpenNewsLink(url))
            throw new ArgumentException($"不是允许打开的新闻链接：{url ?? "<null>"}", nameof(url));
        if (!reload && TryGetCached(url, out var cached)) return cached;

        using var resp = await HttpRequest
            .Create(url)
            .SendAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var html = await resp.AsStringAsync(cancellationToken).ConfigureAwait(false);

        var blocks = NewsArticleParser.Parse(html, url);
        LogWrapper.Info("Homepage", $"已解析 Minecraft 新闻正文：{url}（{blocks.Count} 个块）");
        if (blocks.Count == 0) return blocks;

        if (_Cache.Count >= _MaxCacheCount) _Cache.Clear();
        _Cache[url] = blocks;
        return blocks;
    }
}
