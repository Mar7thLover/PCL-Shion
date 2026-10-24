using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Shion.Core.Download;

namespace PCL.Shion.Core.Online;

/// <summary>
/// 资源的单个文件（版本）信息，来自 CurseForge 或 Modrinth。
/// </summary>
public sealed class CompFile
{
    /// <summary>该文件的所有必要依赖工程的 Project.Id。</summary>
    public List<string> Dependencies { get; private set; } = new();

    /// <summary>下载量计数。注意，该计数仅为一个来源，无法反应两边加起来的下载量，且 CurseForge 可能错误地返回 0。</summary>
    public int DownloadCount { get; private set; }

    /// <summary>下载的文件名。</summary>
    public string FileName { get; private set; } = "";

    /// <summary>该文件来自 CurseForge 还是 Modrinth。</summary>
    public bool FromCurseForge { get; private set; }

    /// <summary>未经处理的支持的游戏版本列表。</summary>
    public List<string> RawGameVersions { get; private set; } = new();

    /// <summary>支持的游戏版本列表。</summary>
    public List<string> GameVersions { get; private set; } = new();

    /// <summary>文件的 SHA1 或 MD5。</summary>
    public string? Hash { get; private set; }

    /// <summary>用于唯一性鉴别该文件的 ID。</summary>
    public string Id { get; private set; } = "";

    /// <summary>支持的 Mod 加载器列表。可能为空。</summary>
    public List<CompLoaderType> ModLoaders { get; private set; } = new();

    /// <summary>该文件的所有可选依赖工程的 Project.Id。</summary>
    public List<string> OptionalDependencies { get; private set; } = new();

    /// <summary>该文件所属项目的 ID。</summary>
    public string ProjectId { get; private set; } = "";

    /// <summary>该文件的所有必要依赖工程的原始 ID。</summary>
    public List<string> RawDependencies { get; private set; } = new();

    /// <summary>该文件的所有可选依赖工程的原始 ID。</summary>
    public List<string> RawOptionalDependencies { get; private set; } = new();

    /// <summary>发布时间。</summary>
    public DateTime ReleaseDate { get; private set; }

    /// <summary>发布状态：Release/Beta/Alpha。</summary>
    public CompFileStatus Status { get; private set; }

    /// <summary>文件的种类。</summary>
    public CompType Type { get; private set; }

    /// <summary>文件描述名（并非文件名，是自定义的字段）。</summary>
    public string DisplayName { get; private set; } = "";

    /// <summary>文件所有可能的下载源。</summary>
    public List<string>? DownloadUrls { get; private set; }

    /// <summary>Mod 版本号。不一定是标准格式。</summary>
    public string? Version { get; private set; }

    /// <summary>
    /// 从文件 Json 中初始化实例。若出错会抛出异常。
    /// </summary>
    /// <param name="data">文件 Json。</param>
    /// <param name="defaultType">资源种类。</param>
    /// <param name="strategy">镜像源策略。</param>
    public CompFile(JsonObject data, CompType defaultType, CompSourceStrategy strategy = CompSourceStrategy.Balanced)
    {
        Type = defaultType;
        FromCurseForge = data.ContainsKey("gameId");
        if (FromCurseForge)
        {
            Id = data["id"]!.ToString();
            ProjectId = data["modId"]!.ToString();
            DisplayName = data["displayName"]!.ToString().Replace("\t", "").Trim(' ');
            Version = null;
            ReleaseDate = data["fileDate"]!.GetValue<DateTime>();
            Status = (CompFileStatus)(int)data["releaseType"]!;
            DownloadCount = (int)data["downloadCount"]!;
            FileName = (string)data["fileName"]!;
            var hashes = (data["hashes"] as JsonArray) ?? new JsonArray();
            Hash = (string?)hashes.FirstOrDefault(s => (int)s!["algo"]! == 1)?["value"];
            Hash ??= (string?)hashes.FirstOrDefault(s => (int)s!["algo"]! == 2)?["value"];

            var url = data["downloadUrl"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(url))
                url = $"https://edge.forgecdn.net/files/{int.Parse(Id[..4])}/{int.Parse(Id[4..])}/{FileName}";
            url = url.Replace(FileName, WebUtility.UrlEncode(FileName));
            url = url.Replace("+", "%20");
            DownloadUrls = CompSources.OrderDownloadUrls(HandleCurseForgeDownloadUrls(url), strategy);

            if (data.ContainsKey("dependencies"))
            {
                var dependencies = (data["dependencies"] as JsonArray) ?? new JsonArray();
                RawDependencies = dependencies
                    .Where(d => (int)d!["relationType"]! == 3 &&
                                (int)d["modId"]! != 306612 && (int)d["modId"]! != 634179)
                    .Select(d => d!["modId"]!.ToString()).ToList();
                RawOptionalDependencies = dependencies
                    .Where(d => (int)d!["relationType"]! == 2 &&
                                (int)d["modId"]! != 306612 && (int)d["modId"]! != 634179)
                    .Select(d => d!["modId"]!.ToString()).ToList();
            }

            RawGameVersions = (data["gameVersions"] as JsonArray ?? new JsonArray())
                .Select(t => (t?.ToString() ?? "").Trim().ToLowerInvariant()).ToList();
            GameVersions = RawGameVersions.Where(CompVersions.IsFormatFit).Distinct().ToList();
            if (GameVersions.Count > 1)
            {
                GameVersions = GameVersions.OrderByDescending(v => v, new CompVersions.VersionComparer()).ToList();
                if (Type == CompType.ModPack)
                    GameVersions = new List<string> { GameVersions[0] };
            }
            else if (GameVersions.Count == 0)
            {
                GameVersions = new List<string> { "" };
            }

            ModLoaders = new List<CompLoaderType>();
            if (RawGameVersions.Contains("forge"))
                ModLoaders.Add(CompLoaderType.Forge);
            if (RawGameVersions.Contains("fabric"))
                ModLoaders.Add(CompLoaderType.Fabric);
            if (RawGameVersions.Contains("quilt"))
                ModLoaders.Add(CompLoaderType.Quilt);
            if (RawGameVersions.Contains("neoforge"))
                ModLoaders.Add(CompLoaderType.NeoForge);
        }
        else
        {
            Id = (string)data["id"]!;
            ProjectId = (string)data["project_id"]!;
            DisplayName = data["name"]!.ToString().Replace("\t", "").Trim(' ');
            Version = (string?)data["version_number"];
            ReleaseDate = data["date_published"]!.GetValue<DateTime>();
            var versionType = data["version_type"]!.ToString();
            Status = versionType == "release" ? CompFileStatus.Release :
                versionType == "beta" ? CompFileStatus.Beta : CompFileStatus.Alpha;
            DownloadCount = (int)data["downloads"]!;
            if (((data["files"] as JsonArray) ?? new JsonArray()).Any())
            {
                var file = data["files"]![0]!;
                FileName = (string)file["filename"]!;
                DownloadUrls = CompSources.OrderDownloadUrls(file["url"]!.ToString(), strategy);
                Hash = (string?)file["hashes"]?["sha1"];
            }

            var rawLoaders = (data["loaders"] as JsonArray ?? new JsonArray()).Select(v => v?.ToString() ?? "").ToList();
            ModLoaders = new List<CompLoaderType>();
            if (Type == CompType.Mod)
            {
                if (rawLoaders.Intersect(new[] { "bukkit", "folia", "paper", "purpur", "spigot" }).Any())
                    Type = CompType.Plugin;
                if (rawLoaders.Contains("datapack"))
                    Type = CompType.DataPack;
                AddModrinthLoaders(rawLoaders);
            }
            else if (Type == CompType.DataPack)
            {
                if (rawLoaders.Intersect(new[] { "bukkit", "folia", "paper", "purpur", "spigot" }).Any())
                    Type = CompType.Plugin;
                AddModrinthLoaders(rawLoaders);
                if (rawLoaders.Contains("datapack"))
                    Type = CompType.DataPack;
            }

            if (data.ContainsKey("dependencies"))
            {
                var dependencies = (data["dependencies"] as JsonArray) ?? new JsonArray();
                RawDependencies = dependencies
                    .Where(d => (string?)d!["dependency_type"] == "required" &&
                                d!["project_id"] is not null &&
                                (string?)d["project_id"] != "P7dR8mSH" &&
                                (string?)d["project_id"] != "qvIfYCYJ")
                    .Select(d => d!["project_id"]!.ToString()).ToList();
                RawOptionalDependencies = dependencies
                    .Where(d => (string?)d!["dependency_type"] == "optional" &&
                                d!["project_id"] is not null &&
                                (string?)d["project_id"] != "P7dR8mSH" &&
                                (string?)d["project_id"] != "qvIfYCYJ")
                    .Select(d => d!["project_id"]!.ToString()).ToList();
            }

            RawGameVersions = (data["game_versions"] as JsonArray ?? new JsonArray())
                .Select(t => (t?.ToString() ?? "").Trim().ToLowerInvariant()).ToList();
            GameVersions = RawGameVersions.Where(v => v.Contains('.')).Select(v =>
                v.Contains('-') ? v[..v.IndexOf('-')] + "-snapshot" : v).Distinct().ToList();
            if (GameVersions.Count > 1)
            {
                GameVersions = GameVersions.OrderByDescending(v => v, new CompVersions.VersionComparer()).ToList();
                if (Type == CompType.ModPack)
                    GameVersions = new List<string> { GameVersions[0] };
            }
            else if (GameVersions.Count == 0 && RawGameVersions.Any(v => System.Text.RegularExpressions.Regex.IsMatch(v, "[0-9]{2}w[0-9]{2}[a-z]")))
            {
                GameVersions = RawGameVersions.Where(v => System.Text.RegularExpressions.Regex.IsMatch(v, "[0-9]{2}w[0-9]{2}[a-z]")).ToList();
            }
            else if (GameVersions.Count == 0)
            {
                GameVersions = new List<string> { "" };
            }
        }
    }

    /// <summary>
    /// 文件是否可以下载，即已有文件名与下载地址。
    /// </summary>
    public bool Available => FileName is not null && DownloadUrls is not null;

    /// <summary>
    /// 获取下载信息。
    /// </summary>
    /// <param name="localAddress">目标本地文件夹，或完整的文件路径。</param>
    /// <param name="reason">下载原因。</param>
    /// <param name="version">实例版本。</param>
    /// <param name="modLoader">实例的模组加载器。</param>
    public DownloadItem ToDownloadItem(string localAddress, DownloadReason reason = DownloadReason.Standalone,
        string? version = null, CompLoaderType modLoader = CompLoaderType.Any)
    {
        if (DownloadUrls is null)
            throw new InvalidOperationException("DownloadUrls 为空");

        var urls = HandleModrinthDownloadUrls(DownloadUrls, reason, version, modLoader)
            .Select(u => new Uri(u)).ToList();
        var target = localAddress.EndsWith('\\') || localAddress.EndsWith('/')
            ? Path.Combine(localAddress, CompNaming.SanitizeFileName(FileName))
            : localAddress;
        var sha1 = Hash is { Length: 40 } hash ? hash : null;
        return new DownloadItem(urls, target, null, sha1);
    }

    /// <summary>
    /// 对之前错误的 CurseForge 的下载地址进行修正。
    /// </summary>
    public static string HandleCurseForgeDownloadUrls(string url) =>
        url.Replace("-service.overwolf.wtf", ".forgecdn.net").Replace("://media.", "://edge.")
            .Replace("://mediafilez.", "://edge.");

    /// <summary>
    /// 对 Modrinth 下载链接添加上报参数。
    /// </summary>
    public static IEnumerable<string> HandleModrinthDownloadUrls(
        IEnumerable<string> urls,
        DownloadReason reason = DownloadReason.Standalone,
        string? version = null,
        CompLoaderType modLoader = CompLoaderType.Any)
    {
        foreach (var url in urls)
        {
            if (!url.Contains("modrinth", StringComparison.OrdinalIgnoreCase))
            {
                yield return url;
                continue;
            }

            var sb = new StringBuilder(url);
            sb.Append(url.Contains('?') ? "&mr_download_reason=" : "?mr_download_reason=");
            sb.Append(reason.ToString().ToLowerInvariant());

            if (version is not null)
            {
                sb.Append("&mr_game_version=");
                sb.Append(version);
            }

            if (modLoader != CompLoaderType.Any)
            {
                sb.Append("&mr_loader=");
                sb.Append(modLoader.ToString().ToLowerInvariant());
            }

            yield return sb.ToString();
        }
    }

    public override string ToString() => $"{Id}: {FileName}";

    private void AddModrinthLoaders(List<string> rawLoaders)
    {
        if (rawLoaders.Contains("forge"))
        {
            ModLoaders.Add(CompLoaderType.Forge);
            Type = CompType.Mod;
        }

        if (rawLoaders.Contains("neoforge"))
        {
            ModLoaders.Add(CompLoaderType.NeoForge);
            Type = CompType.Mod;
        }

        if (rawLoaders.Contains("fabric"))
        {
            ModLoaders.Add(CompLoaderType.Fabric);
            Type = CompType.Mod;
        }

        if (rawLoaders.Contains("quilt"))
        {
            ModLoaders.Add(CompLoaderType.Quilt);
            Type = CompType.Mod;
        }
    }
}
