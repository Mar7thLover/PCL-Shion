using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Loaders.Fabric;

/// <summary>
/// Fabric 与 Quilt 的 meta 服务。两者的接口同构，只有根地址不同，各自在 BMCLAPI 上有对应的镜像路径。
/// </summary>
internal static class FabricMeta
{
    private const string BmclapiRoot = "https://bmclapi2.bangbang93.com";

    /// <summary>
    /// 某个原版版本上可用的加载器列表的候选地址，首选源在前。
    /// </summary>
    public static IReadOnlyList<Uri> GetLoaderListUrls(LoaderKind kind, string minecraftVersion, DownloadSource source)
    {
        return Resolve(kind, $"versions/loader/{Uri.EscapeDataString(minecraftVersion)}", source);
    }

    /// <summary>
    /// 某个加载器版本的启动配置（profile JSON）的候选地址，首选源在前。
    /// </summary>
    public static IReadOnlyList<Uri> GetProfileUrls(LoaderVersion loader, DownloadSource source)
    {
        var minecraftVersion = Uri.EscapeDataString(loader.MinecraftVersion);
        var loaderVersion = Uri.EscapeDataString(loader.Version);
        return Resolve(loader.Kind, $"versions/loader/{minecraftVersion}/{loaderVersion}/profile/json", source);
    }

    /// <summary>
    /// 按顺序请求候选地址，返回第一个成功获取并解析的结果。
    /// 全部地址都失败时抛出最后一个地址的异常；取消时立即抛出，不再尝试后面的地址。
    /// </summary>
    public static async Task<T> FetchAsync<T>(
        HttpClient http,
        IReadOnlyList<Uri> urls,
        Func<string, T> parse,
        CancellationToken cancellationToken)
    {
        foreach (var url in urls.SkipLast(1))
        {
            try
            {
                return parse(await http.GetStringAsync(url, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // 这个地址不可用，换下一个
            }
        }
        return parse(await http.GetStringAsync(urls[^1], cancellationToken).ConfigureAwait(false));
    }

    private static IReadOnlyList<Uri> Resolve(LoaderKind kind, string path, DownloadSource source)
    {
        var (officialRoot, mirrorRoot) = kind == LoaderKind.Quilt
            ? ("https://meta.quiltmc.org/v3", $"{BmclapiRoot}/quilt-meta/v3")
            : ("https://meta.fabricmc.net/v2", $"{BmclapiRoot}/fabric-meta/v2");

        var official = new Uri($"{officialRoot}/{path}");
        var mirror = new Uri($"{mirrorRoot}/{path}");
        return source == DownloadSource.Bmclapi ? [mirror, official] : [official, mirror];
    }
}
