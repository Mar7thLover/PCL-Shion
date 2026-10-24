using System.Text.Json.Nodes;

namespace PCL.Shion.Core.Online;

/// <summary>
/// 资源列表的已加载结果与翻页位置。
/// </summary>
public sealed class CompProjectStorage
{
    /// <summary>CurseForge 已加载的偏移量。</summary>
    public int CurseForgeOffset { get; set; }

    /// <summary>CurseForge 结果总数，-1 表示未知。</summary>
    public int CurseForgeTotal { get; set; } = -1;

    /// <summary>当前的错误信息，没有则为 null。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Modrinth 已加载的偏移量。</summary>
    public int ModrinthOffset { get; set; }

    /// <summary>Modrinth 结果总数，-1 表示未知。</summary>
    public int ModrinthTotal { get; set; } = -1;

    /// <summary>可供展示的所有工程的列表。</summary>
    public List<CompProject> Results { get; } = new();
}

/// <summary>
/// 一次资源搜索的筛选条件与结果要求。
/// </summary>
public sealed class CompProjectRequest(CompType type, CompProjectStorage storage, int targetResultCount)
{
    /// <summary>筛选 MC 版本。</summary>
    public string? GameVersion { get; set; }

    /// <summary>筛选 Mod 加载器类别。</summary>
    public CompLoaderType ModLoader { get; set; } = CompLoaderType.Any;

    /// <summary>搜索的文本内容。</summary>
    public string SearchText { get; set; } = "";

    /// <summary>CurseForge 的替代搜索文本。</summary>
    public string? CurseForgeAltSearchText { get; set; }

    /// <summary>搜索结果排序方式。</summary>
    public CompSortType Sort { get; set; } = CompSortType.Default;

    /// <summary>允许的来源。</summary>
    public CompSourceType Source { get; set; } = CompSourceType.Any;

    /// <summary>加载后应输出到的结果存储器。</summary>
    public CompProjectStorage Storage { get; set; } = storage;

    /// <summary>筛选资源标签。空字符串代表不限制。格式例如 "406/worldgen"，分别是 CurseForge 和 Modrinth 的 ID。</summary>
    public string Tag { get; set; } = "";

    /// <summary>应当尽量达成的结果数量。</summary>
    public int TargetResultCount { get; set; } = targetResultCount;

    /// <summary>筛选资源种类。</summary>
    public CompType Type { get; set; } = type;

    /// <summary>
    /// 根据加载位置记录，是否还可以继续获取内容。
    /// </summary>
    /// <param name="hasCurseForgeApiKey">是否配置了 CurseForge API Key。</param>
    public bool CanContinue(bool hasCurseForgeApiKey)
    {
        if (Tag.StartsWith('/') || !Source.HasFlag(CompSourceType.CurseForge) || !hasCurseForgeApiKey)
            Storage.CurseForgeTotal = 0;
        if (Tag.EndsWith('/') || !Source.HasFlag(CompSourceType.Modrinth))
            Storage.ModrinthTotal = 0;
        if (Storage.CurseForgeTotal == -1 || Storage.ModrinthTotal == -1)
            return true;
        return Storage.CurseForgeOffset < Storage.CurseForgeTotal ||
               Storage.ModrinthOffset < Storage.ModrinthTotal;
    }

    /// <summary>
    /// 获取对应的 CurseForge API 请求链接。若返回 null 则为不进行 CurseForge 请求。
    /// </summary>
    public string? GetCurseForgeAddress(bool hasCurseForgeApiKey)
    {
        if (!Source.HasFlag(CompSourceType.CurseForge))
            return null;
        if (Tag.StartsWith('/') || !hasCurseForgeApiKey)
            Storage.CurseForgeTotal = 0;
        if (!hasCurseForgeApiKey)
            return null;
        if (Storage.CurseForgeTotal > -1 && Storage.CurseForgeTotal <= Storage.CurseForgeOffset)
            return null;

        var address =
            new System.Text.StringBuilder(
                $"https://api.curseforge.com/v1/mods/search?gameId=432&sortOrder=desc&pageSize={CompSearchEngine.PageSize}");
        switch (Type)
        {
            case CompType.Mod:
                address.Append("&classId=6");
                break;
            case CompType.ModPack:
                address.Append("&classId=4471");
                break;
            case CompType.DataPack:
                address.Append("&classId=6945");
                break;
            case CompType.Shader:
                address.Append("&classId=6552");
                break;
            case CompType.ResourcePack:
                address.Append("&classId=12");
                break;
            case CompType.World:
                address.Append("&classId=17");
                break;
        }

        if (!string.IsNullOrEmpty(Tag))
            address.Append($"&categoryId={BeforeFirst(Tag, "/")}");
        if (ModLoader != CompLoaderType.Any)
            address.Append("&modLoaderType=").Append(((int)ModLoader).ToString());
        if (!string.IsNullOrEmpty(GameVersion))
            address.Append("&gameVersion=").Append(GameVersion);
        var curseForgeText = CurseForgeAltSearchText ?? SearchText;
        if (!string.IsNullOrEmpty(curseForgeText))
            address.Append("&searchFilter=").Append(System.Net.WebUtility.UrlEncode(curseForgeText));
        if (Storage.CurseForgeOffset > 0)
            address.Append("&index=").Append(Storage.CurseForgeOffset);
        address.Append("&sortField=").Append(Sort switch
        {
            CompSortType.Relevance => 4,
            CompSortType.Downloads => 6,
            CompSortType.Follows => 2,
            CompSortType.Newest => 11,
            CompSortType.Updated => 3,
            _ => 2
        });

        return address.ToString();
    }

    /// <summary>
    /// 获取对应的 Modrinth API 请求链接。若返回 null 则为不进行 Modrinth 请求。
    /// </summary>
    public string? GetModrinthAddress()
    {
        if (!Source.HasFlag(CompSourceType.Modrinth))
            return null;
        if (Tag.EndsWith('/'))
            Storage.ModrinthTotal = 0;
        if (Storage.ModrinthTotal > -1 && Storage.ModrinthTotal <= Storage.ModrinthOffset)
            return null;

        var index = Sort switch
        {
            CompSortType.Downloads => "downloads",
            CompSortType.Follows => "follows",
            CompSortType.Newest => "newest",
            CompSortType.Updated => "updated",
            _ => "relevance"
        };
        var address = $"https://api.modrinth.com/v2/search?limit={CompSearchEngine.PageSize}&index={index}";
        if (!string.IsNullOrEmpty(SearchText))
            address += "&query=" + System.Net.WebUtility.UrlEncode(SearchText);
        if (Storage.ModrinthOffset > 0)
            address += "&offset=" + Storage.ModrinthOffset;

        var facets = new List<string>
        {
            $"[\"project_type:{Type.ToString().ToLowerInvariant()}\"]"
        };
        if (!string.IsNullOrEmpty(Tag))
            facets.Add($"[\"categories:'{AfterLast(Tag, "/")}'\"]");
        if (ModLoader != CompLoaderType.Any)
            facets.Add($"[\"categories:'{ModLoader.ToString().ToLowerInvariant()}'\"]");
        if (!string.IsNullOrEmpty(GameVersion))
            facets.Add($"[\"versions:'{GameVersion}'\"]");
        address += "&facets=[" + string.Join(",", facets) + "]";
        return address;
    }

    /// <summary>
    /// 两个请求的筛选条件相同时视为相等，结果存储器不参与比较。
    /// </summary>
    public override bool Equals(object? obj) =>
        obj is CompProjectRequest request && Type == request.Type && TargetResultCount == request.TargetResultCount &&
        (Tag ?? "") == (request.Tag ?? "") && ModLoader == request.ModLoader && Source == request.Source &&
        (GameVersion ?? "") == (request.GameVersion ?? "") &&
        (SearchText ?? "") == (request.SearchText ?? "") && Sort == request.Sort;

    public override int GetHashCode() => HashCode.Combine(Type, TargetResultCount, Tag, ModLoader, Source, GameVersion, SearchText, Sort);

    private static string BeforeFirst(string text, string separator)
    {
        var index = text.IndexOf(separator, StringComparison.Ordinal);
        return index < 0 ? text : text[..index];
    }

    private static string AfterLast(string text, string separator)
    {
        var index = text.LastIndexOf(separator, StringComparison.Ordinal);
        return index < 0 ? text : text[(index + separator.Length)..];
    }
}

/// <summary>
/// 执行资源搜索，并把结果追加到 <see cref="CompProjectStorage"/>。
/// </summary>
public sealed class CompSearchEngine(CompApi api)
{
    /// <summary>每次请求的结果数量。</summary>
    public const int PageSize = 40;

    /// <summary>
    /// 根据搜索请求获取一批工程列表。失败时抛出 <see cref="CompException"/> 或网络异常。
    /// </summary>
    /// <param name="request">搜索请求。</param>
    /// <param name="hasCurseForgeApiKey">是否配置了 CurseForge API Key。</param>
    /// <param name="progress">进度，取值 0 至 1。</param>
    /// <param name="cancellationToken">取消搜索。</param>
    public async Task SearchAsync(CompProjectRequest request, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var storage = request.Storage;
        var hasKey = api.HasCurseForgeApiKey;

        var singleLink = CompLinks.TryExtractSingle(request.SearchText);
        if (singleLink is not null)
        {
            if (storage.Results.Count > 0) return;

            CompProject? project = null;
            try
            {
                var projectId = await api.ResolveLinkToProjectIdAsync(singleLink, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(projectId))
                    project = (await api.GetProjectsByIdsAsync([projectId], cancellationToken).ConfigureAwait(false))
                        .FirstOrDefault();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new CompException("Download.Comp.Link.ResolveFailed");
            }

            if (project is null)
                throw new CompException("Download.Comp.Link.ResolveFailed");
            if (request.Type != CompType.Any && project.Type != request.Type)
                throw new CompException("Download.Comp.List.NoMatchingResults");

            storage.Results.Add(project);
            storage.CurseForgeTotal = 0;
            storage.ModrinthTotal = 0;
            return;
        }

        if (storage.Results.Count >= request.TargetResultCount)
            return;

        if (!request.CanContinue(hasKey))
        {
            if (storage.Results.Count == 0) throw new CompException("Download.Comp.List.NoMatchingResults");
            return;
        }

        if (request.ModLoader == CompLoaderType.Quilt &&
            CompVersions.CompareVersion(request.GameVersion ?? "1.15", "1.14") == -1)
            throw new CompException("Minecraft.Error.QuiltUnsupported", request.GameVersion);

        var rawFilter = (request.SearchText ?? "").Trim();
        request.SearchText = rawFilter;

        if (request.CurseForgeAltSearchText is not null)
            request.CurseForgeAltSearchText = ProcessKeywords(request.CurseForgeAltSearchText, rawFilter);
        request.SearchText = ProcessKeywords(request.SearchText, rawFilter);
        progress?.Report(0.1);

        var realResults = new List<CompProject>();
        var lastError = (Exception?)null;

        while (true)
        {
            var rawResults = new List<CompProject>();
            lastError = null;
            var resultsLock = new object();

            var isOldForgeRequest = request.ModLoader == CompLoaderType.Forge &&
                                    CompVersions.VersionToDrop(request.GameVersion, true) < 140;
            if (isOldForgeRequest) request.ModLoader = CompLoaderType.Any;
            var curseForgeUrl = request.GetCurseForgeAddress(hasKey);
            var modrinthUrl = request.GetModrinthAddress();
            if (isOldForgeRequest) request.ModLoader = CompLoaderType.Forge;

            var tasks = new List<Task>();

            if (curseForgeUrl is not null)
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var json = await api.RequestJsonAsync(curseForgeUrl, cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                        var projects = ((JsonArray)json["data"]!)
                            .Select(j => new CompProject((System.Text.Json.Nodes.JsonObject)j!))
                            .Where(p => !(request.Type == CompType.ResourcePack &&
                                          p.TagKeys.Contains("Download.Comp.Type.DataPack")))
                            .ToList();
                        lock (resultsLock)
                            rawResults.AddRange(projects);

                        storage.CurseForgeOffset += projects.Count;
                        storage.CurseForgeTotal = (int)json["pagination"]!["totalCount"]!;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        lastError = ex;
                    }
                }, cancellationToken));

            if (modrinthUrl is not null)
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var json = await api.RequestJsonAsync(modrinthUrl, cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                        var projects = ((JsonArray)json["hits"]!)
                            .Select(j => new CompProject((System.Text.Json.Nodes.JsonObject)j!))
                            .ToList();
                        lock (resultsLock)
                            rawResults.AddRange(projects);

                        storage.ModrinthOffset += projects.Count;
                        storage.ModrinthTotal = (int)json["total_hits"]!;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        lastError = ex;
                    }
                }, cancellationToken));

            await Task.WhenAll(tasks).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(0.5);

            if (isOldForgeRequest)
                rawResults = rawResults.Where(p => p.ModLoaders.Count == 0 || p.ModLoaders.Contains(CompLoaderType.Forge))
                    .ToList();

            if (rawResults.Count == 0)
            {
                if (lastError is not null) throw lastError;
                throw new CompException("Download.Comp.List.NoResultsSimple");
            }

            var processedResults = rawResults.OrderBy(x => x.FromCurseForge)
                .Where(r => !realResults.Any(b => r.IsLike(b)) && !storage.Results.Any(b => r.IsLike(b)))
                .ToList();

            realResults.AddRange(processedResults);

            if (realResults.Count + storage.Results.Count < request.TargetResultCount && request.CanContinue(hasKey) &&
                lastError is null)
                continue;

            break;
        }

        if (request.Sort != CompSortType.Default)
        {
            storage.Results.AddRange(realResults);
            return;
        }

        var scores = new Dictionary<CompProject, double>();
        double DownloadCountMultiplier(CompProject p) => request.Type switch
        {
            CompType.Mod or CompType.ModPack => p.FromCurseForge ? 1 : 7,
            CompType.DataPack => p.FromCurseForge ? 10 : 1,
            CompType.ResourcePack or CompType.Shader => p.FromCurseForge ? 1 : 5,
            _ => 1
        };

        if (string.IsNullOrEmpty(rawFilter))
        {
            foreach (var res in realResults)
                scores.Add(res, res.DownloadCount * DownloadCountMultiplier(res));
        }
        else
        {
            var searchEntries = new List<CompSearchEntry<CompProject>>();
            foreach (var res in realResults)
            {
                scores.Add(res, Math.Log10(Math.Max(res.DownloadCount, 1) * DownloadCountMultiplier(res)) / 9);
                searchEntries.Add(new CompSearchEntry<CompProject>(res,
                [
                    new CompSearchSource(res.RawName.Split('/', StringSplitOptions.RemoveEmptyEntries), 1),
                    new CompSearchSource(res.Description, 0.05)
                ]));
            }

            var searchRes = CompSimilarity.Search(searchEntries, rawFilter, 101, -1);
            if (searchRes.Count > 0)
            {
                var first = searchRes[0];
                var denominator = first.AbsoluteRight ? 10 : first.Similarity;
                foreach (var item in searchRes)
                    scores[item.Item] += (item.AbsoluteRight ? 10 : item.Similarity) / denominator;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        storage.Results.AddRange(scores.OrderByDescending(s => s.Value).Select(s => s.Key));
        progress?.Report(1);
    }

    /// <summary>
    /// 最终处理关键字：分割、去重，并跳过加载器等无关词。
    /// </summary>
    private static string ProcessKeywords(string? text, string rawFilter)
    {
        if (text is null) return "";
        text = text.ToLowerInvariant();
        var words = new List<string>();
        foreach (var keyword in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var cleanKeyword = keyword.Trim('[', ']');
            if (string.IsNullOrEmpty(cleanKeyword)) continue;
            if (new[] { "forge", "fabric", "for", "mod", "quilt" }.Contains(cleanKeyword)) continue;
            words.Add(cleanKeyword);
        }

        var result = rawFilter.Length > 0 && words.Count == 0 ? rawFilter : string.Join(" ", words.Distinct());

        var compact = rawFilter.Replace(" ", "");
        if (compact.Contains("optiforge", StringComparison.OrdinalIgnoreCase)) result = "optiforge";
        if (compact.Contains("optifabric", StringComparison.OrdinalIgnoreCase)) result = "optifabric";
        return result;
    }
}
