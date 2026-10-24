using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Loaders.Extra;

/// <summary>
/// 不经过 <see cref="LoaderKind"/> 的加载器的版本清单：Fabric 与 LegacyFabric 安装器、OptiFine、LiteLoader 与 LabyMod。
/// 每个清单都按 <see cref="DownloadSource"/> 排列候选地址，首选源失败时换另一个源。
/// </summary>
public static class ExtraLoaderCatalog
{
    private const string BmclapiRoot = "https://bmclapi2.bangbang93.com";

    /// <summary>
    /// 列出 Fabric 安装器，较新的在前。安装器只从官方地址下载，BMCLAPI 不提供安装器文件。
    /// </summary>
    /// <exception cref="HttpRequestException">全部候选地址都无法访问。</exception>
    public static Task<IReadOnlyList<ToolVersion>> GetFabricInstallersAsync(
        HttpClient http,
        DownloadSource source,
        CancellationToken cancellationToken)
    {
        var urls = source == DownloadSource.Bmclapi
            ? new[] { new Uri($"{BmclapiRoot}/fabric-meta/v2/versions"), new Uri("https://meta.fabricmc.net/v2/versions") }
            : new[] { new Uri("https://meta.fabricmc.net/v2/versions"), new Uri($"{BmclapiRoot}/fabric-meta/v2/versions") };
        return FetchAsync(http, urls, (_, text) => ParseInstallers(text), cancellationToken);
    }

    /// <summary>
    /// 列出 LegacyFabric 安装器，较新的在前。LegacyFabric 没有 BMCLAPI 镜像。
    /// </summary>
    /// <exception cref="HttpRequestException">官方地址无法访问。</exception>
    public static Task<IReadOnlyList<ToolVersion>> GetLegacyFabricInstallersAsync(
        HttpClient http,
        CancellationToken cancellationToken)
    {
        return FetchAsync(http, [new Uri("https://meta.legacyfabric.net/v2/versions")], (_, text) => ParseInstallers(text), cancellationToken);
    }

    /// <summary>
    /// 列出 OptiFine 版本，较新的在前。
    /// </summary>
    /// <exception cref="HttpRequestException">全部候选地址都无法访问。</exception>
    /// <exception cref="FormatException">清单内容无法解析。</exception>
    public static Task<IReadOnlyList<OptiFineVersion>> GetOptiFineVersionsAsync(
        HttpClient http,
        DownloadSource source,
        CancellationToken cancellationToken)
    {
        var bmclapi = new Uri($"{BmclapiRoot}/optifine/versionList");
        var official = new Uri("https://optifine.net/downloads");
        var urls = source == DownloadSource.Bmclapi ? new[] { bmclapi, official } : new[] { official, bmclapi };
        return FetchAsync(http, urls, (url, text) => ParseOptiFine(text, url == official), cancellationToken);
    }

    /// <summary>
    /// 列出 LiteLoader 版本，较新的在前。
    /// </summary>
    /// <exception cref="HttpRequestException">全部候选地址都无法访问。</exception>
    /// <exception cref="JsonException">清单内容无法解析。</exception>
    public static Task<IReadOnlyList<LiteLoaderVersion>> GetLiteLoaderVersionsAsync(
        HttpClient http,
        DownloadSource source,
        CancellationToken cancellationToken)
    {
        var bmclapi = new Uri($"{BmclapiRoot}/maven/com/mumfrey/liteloader/versions.json");
        var official = new Uri("https://dl.liteloader.com/versions/versions.json");
        var urls = source == DownloadSource.Bmclapi ? new[] { bmclapi, official } : new[] { official, bmclapi };
        return FetchAsync(http, urls, (_, text) => ParseLiteLoader(text), cancellationToken);
    }

    /// <summary>
    /// 获取 LabyMod 的正式版与快照版最新版本。
    /// </summary>
    /// <exception cref="HttpRequestException">清单无法访问。</exception>
    /// <exception cref="JsonException">清单内容无法解析。</exception>
    public static async Task<IReadOnlyList<LabyModChannel>> GetLabyModChannelsAsync(
        HttpClient http,
        CancellationToken cancellationToken)
    {
        var channels = new List<LabyModChannel>();
        foreach (var channel in new[] { "production", "snapshot" })
        {
            var json = await http.GetStringAsync(
                new Uri($"https://releases.r2.labymod.net/api/v1/manifest/{channel}/latest.json"), cancellationToken)
                .ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var version = document.RootElement.GetProperty("labyModVersion").GetString()
                          ?? throw new JsonException("The LabyMod manifest has no labyModVersion.");
            channels.Add(new LabyModChannel(channel, version));
        }

        return channels;
    }

    /// <summary>
    /// LabyMod 安装器的下载地址。
    /// </summary>
    public static string GetLabyModInstallerUrl(string channel) =>
        $"https://releases.labymod.net/api/v1/installer/{channel}/java";

    /// <summary>
    /// LiteLoader 安装器的下载地址。1.7.10 及更早的版本使用固定的 redist 文件，其余版本使用 Jenkins 构建产物。
    /// </summary>
    /// <exception cref="NotSupportedException">远古版本没有已知的安装器文件。</exception>
    public static string GetLiteLoaderInstallerUrl(LiteLoaderVersion version)
    {
        if (!version.IsLegacy)
        {
            var folder = version.MinecraftVersion == "1.8" ? "ant/dist/" : "build/libs/";
            return "http://jenkins.liteloader.com/job/LiteLoaderInstaller%20" + version.MinecraftVersion +
                   "/lastSuccessfulBuild/artifact/" + folder + version.FileName;
        }

        return version.MinecraftVersion switch
        {
            "1.7.10" => "https://dl.liteloader.com/redist/1.7.10/liteloader-installer-1.7.10-04.jar",
            "1.7.2" => "https://dl.liteloader.com/redist/1.7.2/liteloader-installer-1.7.2-04.jar",
            "1.6.4" => "https://dl.liteloader.com/redist/1.6.4/liteloader-installer-1.6.4-01.jar",
            "1.6.2" => "https://dl.liteloader.com/redist/1.6.2/liteloader-installer-1.6.2-04.jar",
            "1.5.2" => "https://dl.liteloader.com/redist/1.5.2/liteloader-installer-1.5.2-01.jar",
            _ => throw new NotSupportedException($"Unknown Minecraft version for LiteLoader: {version.MinecraftVersion}")
        };
    }

    /// <summary>
    /// 按顺序请求候选地址，返回第一个成功获取并解析的结果；全部失败时抛出最后一个地址的异常。
    /// 取消时立即抛出，不再尝试后面的地址。
    /// </summary>
    private static async Task<IReadOnlyList<T>> FetchAsync<T>(
        HttpClient http,
        IReadOnlyList<Uri> urls,
        Func<Uri, string, IReadOnlyList<T>> parse,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < urls.Count - 1; i++)
        {
            try
            {
                return parse(urls[i], await http.GetStringAsync(urls[i], cancellationToken).ConfigureAwait(false));
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // 这个地址不可用，换下一个
            }
        }

        return parse(urls[^1], await http.GetStringAsync(urls[^1], cancellationToken).ConfigureAwait(false));
    }

    private static IReadOnlyList<ToolVersion> ParseInstallers(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new List<ToolVersion>();
        foreach (var entry in document.RootElement.GetProperty("installer").EnumerateArray())
        {
            result.Add(new ToolVersion(
                entry.GetProperty("version").GetString() ?? "",
                entry.GetProperty("url").GetString() ?? "",
                entry.TryGetProperty("stable", out var stable) && stable.GetBoolean()));
        }

        return result;
    }

    /// <summary>
    /// 解析 OptiFine 清单。BMCLAPI 返回 JSON 数组；官方页面是 HTML，按版本表格的列解析。
    /// </summary>
    private static IReadOnlyList<OptiFineVersion> ParseOptiFine(string text, bool isOfficial)
    {
        return isOfficial ? ParseOptiFineOfficial(text) : ParseOptiFineBmclapi(text);
    }

    private static IReadOnlyList<OptiFineVersion> ParseOptiFineBmclapi(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new List<OptiFineVersion>();
        foreach (var token in document.RootElement.EnumerateArray())
        {
            var mcVersion = Text(token, "mcversion");
            var type = Text(token, "type");
            var patch = Text(token, "patch");
            var inherit = TrimPatchZero(mcVersion);
            var displayName = (mcVersion + type.Replace("HD_U", "").Replace("_", " ") + " " + patch).Replace(".0 ", " ");
            var forge = (token.TryGetProperty("forge", out var forgeElement) ? forgeElement.ToString() : "")
                .Replace("Forge ", "").Replace("#", "");
            result.Add(new OptiFineVersion(
                DisplayName: displayName,
                MinecraftVersion: inherit,
                FileName: Text(token, "filename"),
                VersionName: inherit + "-OptiFine_" +
                             (type + " " + patch).Replace(".0 ", " ").Replace(" ", "_").Replace(inherit + "_", ""),
                IsPreview: patch.Contains("pre", StringComparison.OrdinalIgnoreCase),
                ReleaseTime: "",
                RequiredForge: forge.Contains("N/A") ? null : forge,
                DownloadUrl: $"{BmclapiRoot}/optifine/{Uri.EscapeDataString(mcVersion)}/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(patch)}"));
        }

        return result;
    }

    private static IReadOnlyList<OptiFineVersion> ParseOptiFineOfficial(string html)
    {
        if (html.Length < 200)
            throw new FormatException("The OptiFine download page is too short.");
        var forge = Regex.Matches(html, "(?<=colForge'>)[^<]*").Select(m => m.Value).ToList();
        var releaseTime = Regex.Matches(html, "(?<=colDate'>)[^<]+").Select(m => m.Value).ToList();
        var names = Regex.Matches(html, "(?<=OptiFine_)[0-9A-Za-z_.]+(?=.jar\")").Select(m => m.Value).ToList();
        if (releaseTime.Count != names.Count || forge.Count != names.Count || releaseTime.Count < 10)
            throw new FormatException("The OptiFine download page layout is not recognized.");

        var formats = new[] { "d.M.yyyy", "dd.M.yyyy", "d.MM.yyyy", "dd.MM.yyyy" };
        var result = new List<OptiFineVersion>();
        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i].Replace("_", " ");
            var date = DateTime.ParseExact(releaseTime[i], formats, CultureInfo.InvariantCulture, DateTimeStyles.None);
            var inherit = TrimPatchZero(name.Split(" ")[0]);
            var isPreview = name.Contains("pre", StringComparison.OrdinalIgnoreCase);
            var fileName = (isPreview ? "preview_" : "") + "OptiFine_" + name.Replace(" ", "_") + ".jar";
            var requiredForge = forge[i].Replace("Forge ", "").Replace("#", "");
            result.Add(new OptiFineVersion(
                DisplayName: name.Replace("HD U ", "").Replace(".0 ", " "),
                MinecraftVersion: inherit,
                FileName: fileName,
                VersionName: inherit + "-OptiFine_" + name.Replace(" ", "_").Replace(inherit + "_", ""),
                IsPreview: isPreview,
                ReleaseTime: date.ToString("d", CultureInfo.CurrentCulture),
                RequiredForge: requiredForge.Contains("N/A") ? null : requiredForge,
                DownloadUrl: "https://optifine.net/adloadx?f=" + Uri.EscapeDataString(fileName)));
        }

        return result;
    }

    private static IReadOnlyList<LiteLoaderVersion> ParseLiteLoader(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new List<LiteLoaderVersion>();
        foreach (var pair in document.RootElement.GetProperty("versions").EnumerateObject())
        {
            var key = pair.Name;
            if (key.StartsWith("1.6", StringComparison.Ordinal) || key.StartsWith("1.5", StringComparison.Ordinal))
                continue;
            var value = pair.Value;
            var container = value.TryGetProperty("artefacts", out var artefacts) ? artefacts : value.GetProperty("snapshots");
            var latest = container.GetProperty("com.mumfrey:liteloader").GetProperty("latest");
            result.Add(new LiteLoaderVersion(
                MinecraftVersion: key,
                FileName: "liteloader-installer-" + key + (key is "1.8" or "1.9" ? ".0" : "") + "-00-SNAPSHOT.jar",
                IsLegacy: double.Parse(key.Split(".")[1], CultureInfo.InvariantCulture) < 8d,
                IsPreview: latest.GetProperty("stream").GetString()?.ToLowerInvariant() == "snapshot",
                ReleaseTime: FormatUnixTime(latest.GetProperty("timestamp"))));
        }

        return result;
    }

    private static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }

    /// <summary>
    /// 去掉版本号末尾的 <c>.0</c>，如 <c>1.8.0</c> 变为 <c>1.8</c>，与 WPF 版 <c>Inherit</c> 的处理一致。
    /// </summary>
    private static string TrimPatchZero(string version) =>
        version.EndsWith(".0", StringComparison.Ordinal) ? version[..^2] : version;

    private static string FormatUnixTime(JsonElement timestamp)
    {
        var seconds = timestamp.ValueKind == JsonValueKind.Number
            ? timestamp.GetInt64()
            : long.Parse(timestamp.GetString()!, CultureInfo.InvariantCulture);
        return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
    }
}
