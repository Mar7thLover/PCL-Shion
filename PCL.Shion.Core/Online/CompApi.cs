using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace PCL.Shion.Core.Online;

/// <summary>
/// 资源功能的用户可见错误。<see cref="LangKey"/> 为界面语言键，由界面层用 <c>Lang.Text</c> 翻译。
/// </summary>
public sealed class CompException(string langKey, params object?[] args) : Exception(langKey)
{
    /// <summary>界面语言键。</summary>
    public string LangKey { get; } = langKey;

    /// <summary>语言键的格式化参数。</summary>
    public object?[] Args { get; } = args;
}

/// <summary>
/// 访问 Modrinth 与 CurseForge 接口。所有方法都会在后台线程执行网络请求。
/// </summary>
public sealed class CompApi
{
    /// <summary>
    /// 已获取过文件列表的工程缓存，键为工程 ID。
    /// </summary>
    public static readonly ConcurrentDictionary<string, List<CompFile>> FilesCache = new();

    private readonly HttpClient _http;

    /// <summary>
    /// 创建接口访问器。
    /// </summary>
    /// <param name="http">HTTP 客户端，应带有应用的 User-Agent。</param>
    /// <param name="strategy">镜像源策略。</param>
    /// <param name="curseForgeApiKey">CurseForge API Key，为 null 时读取环境变量 PCL_CURSEFORGE_API_KEY。</param>
    public CompApi(HttpClient http, CompSourceStrategy strategy = CompSourceStrategy.Balanced, string? curseForgeApiKey = null)
    {
        _http = http;
        Strategy = strategy;
        CurseForgeApiKey = curseForgeApiKey ?? Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY");
    }

    /// <summary>镜像源策略。</summary>
    public CompSourceStrategy Strategy { get; }

    /// <summary>CurseForge API Key，为空时不请求 CurseForge。</summary>
    public string? CurseForgeApiKey { get; }

    /// <summary>是否配置了 CurseForge API Key。</summary>
    public bool HasCurseForgeApiKey => !string.IsNullOrWhiteSpace(CurseForgeApiKey);

    /// <summary>
    /// 请求 JSON 接口。依次尝试镜像与官方地址，全部失败时抛出异常。
    /// </summary>
    /// <param name="url">官方接口地址。</param>
    /// <param name="method">请求方法，为 null 时使用 GET。</param>
    /// <param name="body">请求体。</param>
    /// <param name="contentType">请求体的内容类型。</param>
    /// <param name="allowMirror">是否允许按策略使用镜像源。为 false 时只使用官方地址与最后的镜像备选。</param>
    public async Task<JsonNode> RequestJsonAsync(string url, HttpMethod? method = null, string? body = null,
        string? contentType = null, bool allowMirror = true, CancellationToken cancellationToken = default)
    {
        var errors = new StringBuilder();
        var strategy = allowMirror ? Strategy : CompSourceStrategy.OfficialOnly;
        foreach (var (attemptUrl, timeoutSeconds) in CompSources.ApiAttempts(url, strategy))
        {
            try
            {
                using var request = new HttpRequestMessage(method ?? HttpMethod.Get, attemptUrl);
                if (attemptUrl.Contains("api.curseforge.com", StringComparison.OrdinalIgnoreCase) ||
                    attemptUrl.Contains("/curseforge/", StringComparison.OrdinalIgnoreCase))
                {
                    if (HasCurseForgeApiKey)
                        request.Headers.TryAddWithoutValidation("x-api-key", CurseForgeApiKey);
                }

                if (body is not null)
                    request.Content = new StringContent(body, Encoding.UTF8, contentType ?? "application/json");

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                using var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                return JsonNode.Parse(text) ?? throw new InvalidDataException("empty response");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (!attemptUrl.Contains("mcimirror", StringComparison.OrdinalIgnoreCase))
                    errors.AppendLine(ex.Message);
            }
        }

        throw new HttpRequestException(errors.ToString());
    }

    /// <summary>
    /// 通过一批 ID 获取工程信息，CurseForge 与 Modrinth 分别请求。
    /// </summary>
    public async Task<List<CompProject>> GetProjectsByIdsAsync(IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        var curseForgeIds = ids.Where(CompLinks.IsFromCurseForge).ToList();
        var modrinthIds = ids.Where(id => !CompLinks.IsFromCurseForge(id)).ToList();

        var tasks = new List<Task<List<CompProject>>>();
        if (curseForgeIds.Count > 0 && HasCurseForgeApiKey)
            tasks.Add(GetProjectsFromCurseForgeAsync(curseForgeIds, cancellationToken));
        if (modrinthIds.Count > 0)
            tasks.Add(GetProjectsFromModrinthAsync(modrinthIds, cancellationToken));

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.SelectMany(r => r).ToList();
    }

    private async Task<List<CompProject>> GetProjectsFromCurseForgeAsync(List<string> ids, CancellationToken cancellationToken)
    {
        try
        {
            var body = "{\"modIds\": [" + string.Join(",", ids) + "]}";
            var response = await RequestJsonAsync("https://api.curseforge.com/v1/mods", HttpMethod.Post, body,
                "application/json", allowMirror: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            var data = (response["data"] as JsonArray) ?? new JsonArray();
            return data.Select(d => new CompProject((JsonObject)d!)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    private async Task<List<CompProject>> GetProjectsFromModrinthAsync(List<string> ids, CancellationToken cancellationToken)
    {
        try
        {
            var response = await RequestJsonAsync(
                $"https://api.modrinth.com/v2/projects?ids=[\"{string.Join("\",\"", ids)}\"]",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return ((JsonArray)response).Select(d => new CompProject((JsonObject)d!)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    /// <summary>
    /// 从资源链接解析出工程 ID。无法识别或获取失败时返回 null。
    /// </summary>
    public async Task<string?> ResolveLinkToProjectIdAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!CompLinks.TryParse(url, out var site, out var category, out var slug))
        {
            var token = CompLinks.FindFirstToken(url);
            if (token is null || !CompLinks.TryParse(token, out site, out category, out slug))
                return null;
        }

        if (site == CompLinks.Site.CurseForge)
        {
            if (!HasCurseForgeApiKey)
                return null;
            var encodedSlug = WebUtility.UrlEncode(slug);
            var json = await RequestJsonAsync(
                $"https://api.curseforge.com/v1/mods/search?gameId=432&slug={encodedSlug}",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var dataArray = (JsonArray)json["data"]!;
            if (dataArray.Count == 0) return null;

            var receivedClassId = dataArray[0]!["classId"]?.ToString();
            if (!CompLinks.CurseForgeCategoryClassIds.TryGetValue(category, out var targetClassId) ||
                receivedClassId == targetClassId)
                return dataArray[0]!["id"]?.ToString();

            var filtered = await RequestJsonAsync(
                $"https://api.curseforge.com/v1/mods/search?gameId=432&slug={encodedSlug}&classId={targetClassId}",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var filteredDatas = (JsonArray)filtered["data"]!;
            return filteredDatas.Count > 0 ? filteredDatas[0]!["id"]?.ToString() : null;
        }

        var mr = await RequestJsonAsync($"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(slug)}",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return mr["id"]?.ToString();
    }

    /// <summary>
    /// 获取某个工程下的全部文件列表，并补全各文件的前置信息。
    /// </summary>
    public async Task<List<CompFile>> GetFilesAsync(string projectId, bool fromCurseForge,
        CancellationToken cancellationToken = default)
    {
        if (!CompProject.Cache.TryGetValue(projectId, out var targetProject))
        {
            if (fromCurseForge)
            {
                var json = await RequestJsonAsync($"https://api.curseforge.com/v1/mods/{projectId}",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                targetProject = new CompProject((JsonObject)json["data"]!);
            }
            else
            {
                var json = await RequestJsonAsync($"https://api.modrinth.com/v2/project/{projectId}",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                targetProject = new CompProject((JsonObject)json);
            }
        }

        if (!FilesCache.ContainsKey(projectId))
        {
            JsonArray resultJsonArray;
            if (fromCurseForge)
            {
                var response = await RequestJsonAsync(
                    $"https://api.curseforge.com/v1/mods/{projectId}/files?pageSize=10000",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                resultJsonArray = (JsonArray)response["data"]!;
            }
            else
            {
                var response = await RequestJsonAsync(
                    $"https://api.modrinth.com/v2/project/{projectId}/version?include_changelog=false",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                resultJsonArray = (JsonArray)response;
            }

            FilesCache[projectId] = resultJsonArray
                .Select(a => new CompFile((JsonObject)a!, targetProject.Type, Strategy))
                .Where(a => a.Available)
                .GroupBy(a => a.Id)
                .Select(g => g.First())
                .ToList();
        }

        var currentFiles = FilesCache[projectId];

        var allRawDeps = currentFiles.SelectMany(f => f.RawDependencies.Concat(f.RawOptionalDependencies))
            .Distinct().ToList();
        var undoneDeps = allRawDeps.Where(id => !CompProject.Cache.ContainsKey(id)).ToList();

        if (undoneDeps.Count > 0)
        {
            JsonArray projects;
            if (fromCurseForge)
            {
                if (!HasCurseForgeApiKey)
                    projects = [];
                else
                {
                    var response = await RequestJsonAsync("https://api.curseforge.com/v1/mods", HttpMethod.Post,
                        "{\"modIds\": [" + string.Join(",", undoneDeps) + "]}", "application/json",
                        allowMirror: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                    projects = (JsonArray)response["data"]!;
                }
            }
            else
            {
                var response = await RequestJsonAsync(
                    $"https://api.modrinth.com/v2/projects?ids=[\"{string.Join("\",\"", undoneDeps)}\"]",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                projects = (JsonArray)response;
            }

            foreach (var project in projects)
                _ = new CompProject((JsonObject)project!);
        }

        var availableDeps = allRawDeps
            .Where(id => CompProject.Cache.ContainsKey(id) && (id ?? "") != (projectId ?? ""))
            .Select(id => CompProject.Cache[id])
            .ToList();

        foreach (var file in currentFiles)
        foreach (var dep in availableDeps)
        {
            if (file.RawDependencies.Contains(dep.Id) && !file.Dependencies.Contains(dep.Id))
                file.Dependencies.Add(dep.Id);
            if (file.RawOptionalDependencies.Contains(dep.Id) && !file.OptionalDependencies.Contains(dep.Id))
                file.OptionalDependencies.Add(dep.Id);
        }

        return FilesCache[projectId];
    }
}

/// <summary>
/// 资源链接的识别。
/// </summary>
public static class CompLinks
{
    /// <summary>链接所属网站。</summary>
    public enum Site
    {
        None,
        CurseForge,
        Modrinth
    }

    /// <summary>CurseForge 分类 URL 段到 classId 的映射。</summary>
    public static readonly Dictionary<string, string> CurseForgeCategoryClassIds = new()
    {
        { "mc-mods", "6" },
        { "modpacks", "4471" },
        { "texture-packs", "12" },
        { "shaders", "6552" }
    };

    /// <summary>通过项目 ID 判断是否来自 CurseForge（CurseForge 为数字 ID，Modrinth 为乱码 ID）。</summary>
    public static bool IsFromCurseForge(string id) => int.TryParse(id, out _);

    /// <summary>
    /// 解析单个 token 是否为 CurseForge 或 Modrinth 的资源链接。
    /// </summary>
    public static bool TryParse(string token, out Site site, out string category, out string slug)
    {
        site = Site.None;
        category = string.Empty;
        slug = string.Empty;

        if (!Uri.TryCreate(token, UriKind.Absolute, out var uri) &&
            !Uri.TryCreate($"https://{token}", UriKind.Absolute, out uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        var segments = uri.AbsolutePath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);

        if (IsHostOf(uri.Host, "curseforge.com"))
        {
            if (segments.Length < 3 || !segments[0].Equals("minecraft", StringComparison.OrdinalIgnoreCase)) return false;
            site = Site.CurseForge;
            category = segments[1];
            slug = segments[2];
            return true;
        }

        if (IsHostOf(uri.Host, "modrinth.com"))
        {
            if (segments.Length < 2) return false;
            site = Site.Modrinth;
            category = segments[0];
            slug = segments[1];
            return true;
        }

        return false;
    }

    /// <summary>从文本中找出首个可识别的资源链接，找不到返回 null。</summary>
    public static string? FindFirstToken(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var token in text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            if (TryParse(token, out _, out _, out _)) return token;
        return null;
    }

    /// <summary>若文本中恰好含 1 条资源链接，返回该链接；含 0 条或 2 条以上时返回 null。</summary>
    public static string? TryExtractSingle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string? found = null;
        foreach (var token in text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParse(token, out _, out _, out _)) continue;
            if (found is not null) return null;
            found = token;
        }

        return found;
    }

    private static bool IsHostOf(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase);
}
