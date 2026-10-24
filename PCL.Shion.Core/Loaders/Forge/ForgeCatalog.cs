using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Loaders.Forge;

/// <summary>
/// 获取某个原版版本上可用的 Forge 或 NeoForge 版本。官方源为 Forge 的 maven-metadata.json 与 NeoForge Maven 的版本接口，
/// 镜像源为 BMCLAPI 的列表接口；两边的格式不同，各自解析。
/// </summary>
/// <remarks>
/// <see cref="LoaderVersion.Version"/> 不含原版版本号前缀：Forge 为 47.4.0、10.13.4.1614-1.7.10 这样的形式，
/// NeoForge 为 21.1.77，1.20.1 上的 NeoForge 为 47.1.106。安装时由 <see cref="ForgeInstaller"/> 还原出完整的 Maven 坐标。
/// </remarks>
internal static partial class ForgeCatalog
{
    public const string ForgeMetadataUrl = "https://files.minecraftforge.net/net/minecraftforge/forge/maven-metadata.json";

    public const string NeoForgeVersionsUrl = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge";

    public const string LegacyNeoForgeVersionsUrl = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge";

    /// <summary>NeoForge 在这个原版版本上沿用 Forge 的坐标 net.neoforged:forge 发布。</summary>
    public const string LegacyNeoForgeMinecraftVersion = "1.20.1";

    private const string BmclapiRoot = "https://bmclapi2.bangbang93.com";

    [GeneratedRegex(@"^1\.(\d+)(?:\.(\d+))?$")]
    private static partial Regex ReleaseVersionPattern();

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberPattern();

    /// <summary>
    /// 列出 <paramref name="minecraftVersion"/> 上的版本，较新的在前。优先使用 <paramref name="source"/>，失败时换另一个源。
    /// </summary>
    /// <exception cref="HttpRequestException">两个源都无法访问。</exception>
    public static Task<IReadOnlyList<LoaderVersion>> GetVersionsAsync(
        LoaderKind kind,
        string minecraftVersion,
        HttpClient http,
        DownloadSource source,
        CancellationToken cancellationToken)
    {
        var escaped = Uri.EscapeDataString(minecraftVersion);
        VersionList official;
        VersionList mirror;
        if (kind == LoaderKind.Forge)
        {
            official = new VersionList(ForgeMetadataUrl, json => ParseForgeMetadata(json, minecraftVersion));
            mirror = new VersionList($"{BmclapiRoot}/forge/minecraft/{escaped}", json => ParseBmclapiForge(json, minecraftVersion));
        }
        else
        {
            official = minecraftVersion == LegacyNeoForgeMinecraftVersion
                ? new VersionList(LegacyNeoForgeVersionsUrl, ParseLegacyNeoForge)
                : new VersionList(NeoForgeVersionsUrl, json => ParseNeoForge(json, minecraftVersion));
            mirror = new VersionList($"{BmclapiRoot}/neoforge/list/{escaped}", json => ParseBmclapiNeoForge(json, minecraftVersion));
        }
        return FetchAsync(http, source == DownloadSource.Bmclapi ? [mirror, official] : [official, mirror], cancellationToken);
    }

    /// <summary>
    /// 解析 Forge 的 maven-metadata.json：原版版本号到完整版本号（&lt;原版&gt;-&lt;Forge&gt;[-&lt;分支&gt;]）列表的映射。
    /// Forge 的构建没有测试版标记，一律视为稳定版。
    /// </summary>
    internal static IReadOnlyList<LoaderVersion> ParseForgeMetadata(string json, string minecraftVersion)
    {
        using var document = JsonDocument.Parse(json);
        if (PredatesInstaller(minecraftVersion) || !document.RootElement.TryGetProperty(minecraftVersion, out var versions))
            return [];

        var prefix = minecraftVersion + "-";
        return SortNewestFirst(versions.EnumerateArray()
            .Select(version => version.GetString()!)
            .Where(version => version.StartsWith(prefix, StringComparison.Ordinal))
            .Select(version => new LoaderVersion(LoaderKind.Forge, version[prefix.Length..], minecraftVersion, Stable: true)));
    }

    /// <summary>
    /// 解析 BMCLAPI 的 /forge/minecraft/&lt;原版&gt;，只保留提供了安装器的构建；有分支名的构建，版本号带上分支后缀。
    /// </summary>
    internal static IReadOnlyList<LoaderVersion> ParseBmclapiForge(string json, string minecraftVersion)
    {
        return SortNewestFirst(Deserialize<List<BmclapiForgeBuild>>(json)
            .Where(build => build.Files.Any(file => file.Category == "installer"))
            .Select(build => new LoaderVersion(
                LoaderKind.Forge,
                build.Branch is null ? build.Version : $"{build.Version}-{build.Branch}",
                minecraftVersion,
                Stable: true)));
    }

    /// <summary>
    /// 解析 NeoForge Maven 的版本接口，按版本号推出所属的原版版本后筛选。
    /// </summary>
    internal static IReadOnlyList<LoaderVersion> ParseNeoForge(string json, string minecraftVersion)
    {
        return SortNewestFirst(Deserialize<MavenVersions>(json).Versions
            .Where(version => GetNeoForgeMinecraftVersion(version) == minecraftVersion)
            .Select(version => CreateNeoForge(version, minecraftVersion)));
    }

    /// <summary>
    /// 解析 net.neoforged:forge 的版本接口，版本号形如 1.20.1-47.1.106，去掉原版前缀。
    /// </summary>
    internal static IReadOnlyList<LoaderVersion> ParseLegacyNeoForge(string json)
    {
        const string prefix = LegacyNeoForgeMinecraftVersion + "-";
        return SortNewestFirst(Deserialize<MavenVersions>(json).Versions
            .Where(version => version.StartsWith(prefix, StringComparison.Ordinal))
            .Select(version => CreateNeoForge(version[prefix.Length..], LegacyNeoForgeMinecraftVersion)));
    }

    /// <summary>
    /// 解析 BMCLAPI 的 /neoforge/list/&lt;原版&gt;，其中的版本号与 <see cref="LoaderVersion.Version"/> 的约定一致。
    /// </summary>
    internal static IReadOnlyList<LoaderVersion> ParseBmclapiNeoForge(string json, string minecraftVersion)
    {
        return SortNewestFirst(Deserialize<List<BmclapiNeoForgeBuild>>(json)
            .Select(build => CreateNeoForge(build.Version, minecraftVersion)));
    }

    /// <summary>
    /// 由 NeoForge 版本号推出所属的原版版本。1.20.2 至 1.21.x 时期为 &lt;次版本&gt;.&lt;修订&gt;.&lt;构建&gt;：20.2.3-beta 属于 1.20.2，21.0.1 属于 1.21；
    /// 26.1 起以原版版本开头：26.1.0.5 属于 26.1，26.1.2.122 属于 26.1.2，26.1.0.0-alpha.1+snapshot-1 属于 26.1-snapshot-1；
    /// 愚人节版本以 0. 开头：0.25w14craftmine.3-beta 属于 25w14craftmine。无法识别时返回 null。
    /// </summary>
    internal static string? GetNeoForgeMinecraftVersion(string version)
    {
        var core = version.Split('-', '+')[0];
        if (core.StartsWith("0.", StringComparison.Ordinal))
        {
            var build = core.LastIndexOf('.');
            return build > 2 ? core[2..build] : null;
        }

        var parts = core.Split('.');
        var plus = version.IndexOf('+');
        var snapshot = plus >= 0 ? "-" + version[(plus + 1)..] : "";
        return parts.Length switch
        {
            3 => "1." + parts[0] + (parts[1] == "0" ? "" : "." + parts[1]),
            4 => parts[0] + "." + parts[1] + (parts[2] == "0" ? "" : "." + parts[2]) + snapshot,
            _ => null
        };
    }

    // Forge 从 1.5.2 起才提供安装器，更早的版本只发布需要合并进客户端的压缩包
    private static bool PredatesInstaller(string minecraftVersion)
    {
        var match = ReleaseVersionPattern().Match(minecraftVersion);
        if (!match.Success)
            return false;

        var minor = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var patch = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        return minor < 5 || (minor == 5 && patch < 2);
    }

    // NeoForge 的测试版与预览版带有 -beta、-alpha 等后缀
    private static LoaderVersion CreateNeoForge(string version, string minecraftVersion)
    {
        return new LoaderVersion(LoaderKind.NeoForge, version, minecraftVersion, Stable: !version.Contains('-'));
    }

    /// <summary>
    /// 按版本号中依次出现的数字从大到小排序，数字全部相同时按字符串排序。
    /// </summary>
    private static IReadOnlyList<LoaderVersion> SortNewestFirst(IEnumerable<LoaderVersion> versions)
    {
        var list = versions.ToList();
        list.Sort((left, right) => CompareVersions(right.Version, left.Version));
        return list;
    }

    private static int CompareVersions(string left, string right)
    {
        var leftNumbers = NumberPattern().Matches(left);
        var rightNumbers = NumberPattern().Matches(right);
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

    // 按顺序请求各个源，最后一个源的异常直接抛出；取消时立即抛出
    private static async Task<IReadOnlyList<LoaderVersion>> FetchAsync(
        HttpClient http,
        IReadOnlyList<VersionList> lists,
        CancellationToken cancellationToken)
    {
        foreach (var list in lists.SkipLast(1))
        {
            try
            {
                return list.Parse(await http.GetStringAsync(list.Url, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // 这个源不可用，换下一个
            }
        }
        var last = lists[^1];
        return last.Parse(await http.GetStringAsync(last.Url, cancellationToken).ConfigureAwait(false));
    }

    private static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Web)
            ?? throw new JsonException("The version list must not be null.");
    }

    /// <summary>一个源的版本列表地址，以及对应的解析方法。</summary>
    private sealed record VersionList(string Url, Func<string, IReadOnlyList<LoaderVersion>> Parse);

    private sealed class MavenVersions
    {
        public required List<string> Versions { get; init; }
    }

    private sealed class BmclapiForgeBuild
    {
        public required string Version { get; init; }

        public string? Branch { get; init; }

        public List<BmclapiForgeFile> Files { get; init; } = [];
    }

    private sealed class BmclapiForgeFile
    {
        public string? Category { get; init; }
    }

    private sealed class BmclapiNeoForgeBuild
    {
        public required string Version { get; init; }
    }
}
