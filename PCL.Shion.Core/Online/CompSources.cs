namespace PCL.Shion.Core.Online;

/// <summary>
/// Modrinth 与 CurseForge 接口及下载地址的镜像源映射与尝试顺序。
/// </summary>
public static class CompSources
{
    private const string MirrorRoot = "https://mod.mcimirror.top";

    /// <summary>
    /// 把接口地址映射为镜像地址。无法映射时原样返回。
    /// </summary>
    public static string ToApiMirror(string original) =>
        original.Replace("https://api.modrinth.com", MirrorRoot + "/modrinth")
            .Replace("https://api.curseforge.com", MirrorRoot + "/curseforge");

    /// <summary>
    /// 按镜像源策略返回接口请求的候选地址及超时秒数，依次尝试。
    /// </summary>
    public static List<(string Url, int TimeoutSeconds)> ApiAttempts(string url, CompSourceStrategy strategy)
    {
        var mirror = ToApiMirror(url);
        var result = new List<(string Url, int TimeoutSeconds)>();
        if (mirror == url)
        {
            result.Add((url, 5));
            result.Add((url, 15));
            return result;
        }

        switch (strategy)
        {
            case CompSourceStrategy.MirrorFirst:
                result.Add((mirror, 5));
                result.Add((mirror, 10));
                result.Add((url, 15));
                break;
            case CompSourceStrategy.Balanced:
                result.Add((url, 5));
                result.Add((mirror, 5));
                result.Add((url, 15));
                result.Add((mirror, 10));
                break;
            default:
                result.Add((url, 5));
                result.Add((url, 15));
                result.Add((mirror, 10));
                break;
        }

        return result;
    }

    /// <summary>
    /// 按镜像源策略排列文件下载地址。
    /// </summary>
    public static List<string> OrderDownloadUrls(string original, CompSourceStrategy strategy)
    {
        var mirror = original.Replace("https://cdn.modrinth.com", MirrorRoot)
            .Replace("https://edge.forgecdn.net", MirrorRoot);
        var result = new List<string>();
        switch (strategy)
        {
            case CompSourceStrategy.MirrorFirst:
                result.Add(mirror);
                break;
            case CompSourceStrategy.Balanced:
                result.Add(original);
                result.Add(mirror);
                break;
            default:
                result.Add(original);
                break;
        }

        result.Add(original);
        return result.Distinct().ToList();
    }

    /// <summary>
    /// 镜像源的下载链接映射后地址是否与原地址不同。
    /// </summary>
    public static bool HasMirror(string original) => ToApiMirror(original) != original;
}
