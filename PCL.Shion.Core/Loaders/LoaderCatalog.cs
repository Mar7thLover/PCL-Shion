using PCL.Shion.Core.Loaders.Fabric;
using PCL.Shion.Core.Loaders.Forge;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Loaders;

/// <summary>
/// 查询某个原版版本上可用的加载器版本。
/// </summary>
public static class LoaderCatalog
{
    /// <summary>
    /// 列出 <paramref name="minecraftVersion"/> 上可用的 <paramref name="kind"/> 版本，较新的在前；该原版版本没有可用的加载器时返回空列表。
    /// 优先使用 <paramref name="source"/> 指定的下载源，失败时换另一个源。
    /// </summary>
    /// <exception cref="HttpRequestException">全部下载源都无法访问。</exception>
    public static Task<IReadOnlyList<LoaderVersion>> GetVersionsAsync(
        LoaderKind kind,
        string minecraftVersion,
        HttpClient http,
        DownloadSource source,
        CancellationToken cancellationToken)
    {
        return kind switch
        {
            LoaderKind.Fabric or LoaderKind.Quilt =>
                FabricCatalog.GetVersionsAsync(kind, minecraftVersion, http, source, cancellationToken),
            LoaderKind.Forge or LoaderKind.NeoForge =>
                ForgeCatalog.GetVersionsAsync(kind, minecraftVersion, http, source, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}
