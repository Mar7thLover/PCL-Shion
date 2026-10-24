namespace PCL.Shion.Core.Sources;

/// <summary>
/// 把 Mojang 官方地址映射到 BMCLAPI 镜像，并按首选源排出尝试顺序。
/// </summary>
public static class MirrorUrls
{
    private const string BmclapiRoot = "https://bmclapi2.bangbang93.com";

    /// <summary>官方域名到镜像路径前缀的映射，镜像路径为 <c>根 + 前缀 + 原路径</c>。</summary>
    private static readonly Dictionary<string, string> PathPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["piston-meta.mojang.com"] = string.Empty,
        ["launchermeta.mojang.com"] = string.Empty,
        ["launcher.mojang.com"] = string.Empty,
        ["piston-data.mojang.com"] = string.Empty,
        ["resources.download.minecraft.net"] = "/assets",
        ["libraries.minecraft.net"] = "/maven"
    };

    /// <summary>
    /// 返回按优先级排列的候选地址：首选源在前，另一个源在后；没有镜像的地址（非 Mojang 域名）原样返回一个元素。
    /// </summary>
    public static IReadOnlyList<Uri> Resolve(string officialUrl, DownloadSource preferred)
    {
        var official = new Uri(officialUrl);
        if (!PathPrefixes.TryGetValue(official.Host, out var prefix))
            return [official];

        var mirror = new Uri(BmclapiRoot + prefix + official.PathAndQuery);
        return preferred == DownloadSource.Bmclapi ? [mirror, official] : [official, mirror];
    }
}
