using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Loaders.Forge;

/// <summary>
/// 列出 Forge 与 NeoForge 的全部版本，供下载页按原版版本分组；同时给出推荐版本、安装器与更新日志的地址。
/// </summary>
public static class ForgeIndex
{
    private const string BmclapiMinecraftVersionsUrl = "https://bmclapi2.bangbang93.com/forge/minecraft";

    private const string ForgePromotionsUrl = "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json";

    private const string BmclapiPromotionsUrl = "https://bmclapi2.bangbang93.com/forge/promos";

    // NeoForge 的版本号中不含 net.neoforged:forge 的 47.1.82，这个版本虽然在版本列表中，但不能下载
    private const string UnavailableNeoForgeVersion = "47.1.82";

    /// <summary>
    /// 列出有 Forge 构建的全部原版版本号，较新的在前。优先使用 <paramref name="source"/>，失败时换另一个源。
    /// </summary>
    /// <exception cref="HttpRequestException">两个源都无法访问。</exception>
    public static async Task<IReadOnlyList<string>> GetMinecraftVersionsAsync(
        HttpClient http,
        DownloadSource source,
        CancellationToken cancellationToken)
    {
        var official = new Source<IReadOnlyList<string>>(ForgeCatalog.ForgeMetadataUrl, ParseForgeMetadataVersions);
        var mirror = new Source<IReadOnlyList<string>>(BmclapiMinecraftVersionsUrl, ParseBmclapiMinecraftVersions);
        var sources = source == DownloadSource.Bmclapi ? new[] { mirror, official } : new[] { official, mirror };
        return await FirstAvailableAsync(http, sources, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 获取推荐版本表，键形如 <c>1.20.1-recommended</c>，值为对应的 Forge 版本号。
    /// </summary>
    /// <exception cref="HttpRequestException">两个源都无法访问。</exception>
    public static async Task<IReadOnlyDictionary<string, string>> GetPromotionsAsync(
        HttpClient http,
        DownloadSource source,
        CancellationToken cancellationToken)
    {
        var official = new Source<IReadOnlyDictionary<string, string>>(ForgePromotionsUrl, ParsePromotions);
        var mirror = new Source<IReadOnlyDictionary<string, string>>(BmclapiPromotionsUrl, ParsePromotions);
        var sources = source == DownloadSource.Bmclapi ? new[] { mirror, official } : new[] { official, mirror };
        return await FirstAvailableAsync(http, sources, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 列出全部 NeoForge 版本，包括 1.20.1 上沿用 Forge 坐标发布的版本，较新的在前。
    /// NeoForge 的 Maven 接口没有镜像格式可用，只请求官方地址。
    /// </summary>
    /// <exception cref="HttpRequestException">无法访问 NeoForge Maven。</exception>
    public static async Task<IReadOnlyList<LoaderVersion>> GetNeoForgeVersionsAsync(
        HttpClient http,
        CancellationToken cancellationToken)
    {
        List<string> latest = JsonSerializer.Deserialize<MavenVersionList>(
            await http.GetStringAsync(ForgeCatalog.NeoForgeVersionsUrl, cancellationToken).ConfigureAwait(false),
            JsonSerializerOptions.Web)?.Versions ?? [];
        List<string> legacy = JsonSerializer.Deserialize<MavenVersionList>(
            await http.GetStringAsync(ForgeCatalog.LegacyNeoForgeVersionsUrl, cancellationToken).ConfigureAwait(false),
            JsonSerializerOptions.Web)?.Versions ?? [];

        var versions = new List<LoaderVersion>();
        var legacyPrefix = ForgeCatalog.LegacyNeoForgeMinecraftVersion + "-";
        foreach (var name in legacy)
        {
            if (name.StartsWith(legacyPrefix, StringComparison.Ordinal))
                versions.Add(CreateNeoForge(name[legacyPrefix.Length..], ForgeCatalog.LegacyNeoForgeMinecraftVersion));
        }

        foreach (var name in latest)
        {
            if (name == UnavailableNeoForgeVersion)
                continue;
            if (ForgeCatalog.GetNeoForgeMinecraftVersion(name) is { } minecraftVersion)
                versions.Add(CreateNeoForge(name, minecraftVersion));
        }

        versions.Sort((left, right) => CompareVersions(right.Version, left.Version));
        return versions;
    }

    /// <summary>
    /// 返回一个 Forge 或 NeoForge 版本的安装器地址，按首选源排列。
    /// </summary>
    public static IReadOnlyList<Uri> GetInstallerUrls(LoaderVersion loader, DownloadSource source)
    {
        return ForgeMirrors.Resolve(ForgeInstaller.GetInstallerUrl(loader), source);
    }

    /// <summary>
    /// 返回一个 Forge 或 NeoForge 版本的更新日志地址。
    /// </summary>
    public static Uri GetChangelogUrl(LoaderVersion loader)
    {
        return new Uri(ForgeInstaller.GetInstallerUrl(loader).Replace("-installer.jar", "-changelog.txt", StringComparison.Ordinal));
    }

    /// <summary>
    /// 由推荐版本表判断 <paramref name="loader"/> 是否为其原版版本的推荐版本。
    /// </summary>
    public static bool IsRecommended(LoaderVersion loader, IReadOnlyDictionary<string, string> promotions)
    {
        if (loader.Kind != LoaderKind.Forge)
            return false;
        if (!promotions.TryGetValue(loader.MinecraftVersion + "-recommended", out var recommended))
            return false;
        return loader.Version == recommended || loader.Version.StartsWith(recommended + "-", StringComparison.Ordinal);
    }

    private sealed record Source<T>(string Url, Func<string, T> Parse);

    // 按顺序请求各个源，全部失败时抛出最后一个错误
    private static async Task<T> FirstAvailableAsync<T>(
        HttpClient http,
        IReadOnlyList<Source<T>> sources,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        foreach (var source in sources)
        {
            try
            {
                return source.Parse(await http.GetStringAsync(source.Url, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new HttpRequestException("No version list source is configured.");
    }

    private static IReadOnlyList<string> ParseForgeMetadataVersions(string json)
    {
        using var document = JsonDocument.Parse(json);
        return SortNewestFirst(document.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .Where(name => !PredatesInstaller(name)));
    }

    private static IReadOnlyList<string> ParseBmclapiMinecraftVersions(string json)
    {
        var names = JsonSerializer.Deserialize<List<string>>(json, JsonSerializerOptions.Web)
            ?? throw new JsonException("The Forge version list must not be null.");
        return SortNewestFirst(names.Where(name => !PredatesInstaller(name)));
    }

    private static IReadOnlyDictionary<string, string> ParsePromotions(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("promos", out var promos))
            root = promos;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
                result[property.Name] = property.Value.GetString()!;
        }

        return result;
    }

    // Forge 从 1.5.2 起才提供安装器，更早的版本只发布需要合并进客户端的压缩包
    private static bool PredatesInstaller(string minecraftVersion)
    {
        var parts = minecraftVersion.Split('.');
        if (parts.Length < 2 || parts[0] != "1" || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
            return false;

        var patch = parts.Length > 2 && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
        return minor < 5 || (minor == 5 && patch < 2);
    }

    private static LoaderVersion CreateNeoForge(string version, string minecraftVersion)
    {
        return new LoaderVersion(LoaderKind.NeoForge, version, minecraftVersion, Stable: !version.Contains('-'));
    }

    private static IReadOnlyList<string> SortNewestFirst(IEnumerable<string> names)
    {
        var list = names.ToList();
        list.Sort((left, right) => CompareVersions(right, left));
        return list;
    }

    /// <summary>
    /// 按版本号中依次出现的数字从大到小比较，数字全部相同时按字符串比较。
    /// </summary>
    private static int CompareVersions(string left, string right)
    {
        var leftNumbers = System.Text.RegularExpressions.Regex.Matches(left, @"\d+");
        var rightNumbers = System.Text.RegularExpressions.Regex.Matches(right, @"\d+");
        for (var i = 0; i < Math.Min(leftNumbers.Count, rightNumbers.Count); i++)
        {
            var result = long.Parse(leftNumbers[i].Value, CultureInfo.InvariantCulture)
                .CompareTo(long.Parse(rightNumbers[i].Value, CultureInfo.InvariantCulture));
            if (result != 0)
                return result;
        }

        var count = leftNumbers.Count.CompareTo(rightNumbers.Count);
        return count != 0 ? count : string.CompareOrdinal(left, right);
    }

    private sealed class MavenVersionList
    {
        public List<string> Versions { get; init; } = [];
    }
}
