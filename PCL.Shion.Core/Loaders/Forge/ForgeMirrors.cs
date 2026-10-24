using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Loaders.Forge;

/// <summary>
/// 把 Forge 与 NeoForge 的 Maven 仓库地址映射到 BMCLAPI 的 /maven 镜像；其他地址交给 <see cref="MirrorUrls"/>。
/// </summary>
internal static class ForgeMirrors
{
    private const string BmclapiMaven = "https://bmclapi2.bangbang93.com/maven";

    /// <summary>仓库所在的域名，以及仓库根目录在该域名下的路径前缀。</summary>
    private static readonly (string Host, string Prefix)[] Repositories =
    [
        ("maven.minecraftforge.net", ""),
        ("files.minecraftforge.net", "/maven"),
        ("maven.neoforged.net", "/releases")
    ];

    /// <summary>
    /// 返回按优先级排列的候选地址：首选源在前，另一个源在后。
    /// </summary>
    public static IReadOnlyList<Uri> Resolve(string url, DownloadSource preferred)
    {
        var official = new Uri(url);
        foreach (var (host, prefix) in Repositories)
        {
            if (!official.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
                || !official.AbsolutePath.StartsWith(prefix + "/", StringComparison.Ordinal))
            {
                continue;
            }

            var mirror = new Uri(BmclapiMaven + official.PathAndQuery[prefix.Length..]);
            return preferred == DownloadSource.Bmclapi ? [mirror, official] : [official, mirror];
        }
        return MirrorUrls.Resolve(url, preferred);
    }
}
