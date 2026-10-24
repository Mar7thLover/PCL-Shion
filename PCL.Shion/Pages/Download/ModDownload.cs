using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Shion.Core.Loaders;
using PCL.Shion.Core.Loaders.Cleanroom;
using PCL.Shion.Core.Loaders.Extra;
using PCL.Shion.Core.Loaders.Forge;
using PCL.Shion.Core.Online;
using PCL.Shion.Core.Sources;
using PCL.Shion.Core.Versions;
using PCL.Shion.Navigation;

namespace PCL.Shion;

/// <summary>
/// 下载页与实例安装页共用的版本列表加载器，对应 WPF 的 ModDownload。
/// 数据由 PCL.Shion.Core 获取，这里把结果整理成与 WPF 相同的形状（JSON 对象、条目类型），供界面按原来的方式读取。
/// 结果类型使用引用类型，因此 <c>loader.output.Value</c> 访问的是结果中的字段。
/// </summary>
public static class ModDownload
{
    private const string OfficialName = "Official";
    private const string BmclapiName = "BMCLAPI";

    private const string FabricMetaOfficial = "https://meta.fabricmc.net/v2/versions";
    private const string FabricMetaBmclapi = "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions";
    private const string LegacyFabricMeta = "https://meta.legacyfabric.net/v2/versions";
    private const string LabyModProductionManifest = "https://releases.r2.labymod.net/api/v1/manifest/production/latest.json";
    private const string LabyModSnapshotManifest = "https://releases.r2.labymod.net/api/v1/manifest/snapshot/latest.json";
    private const string OptiFabricProjectId = "322385";
    private const string FabricApiProjectId = "fabric-api";
    private const string LegacyFabricApiProjectId = "legacy-fabric-api";

    #region 通用辅助

    private static HttpClient Http => App.Services.Engine.Http;

    private static DownloadSource CurrentSource => App.Services.Source;

    private static string SourceNameOf(DownloadSource source) => source == DownloadSource.Bmclapi ? BmclapiName : OfficialName;

    /// <summary>
    /// 按当前下载源决定候选地址的顺序：BMCLAPI 首选时镜像在前，否则官方在前。
    /// </summary>
    private static IReadOnlyList<Uri> OrderSources(Uri official, Uri mirror) =>
        CurrentSource == DownloadSource.Bmclapi ? [mirror, official] : [official, mirror];

    private static T Await<T>(Task<T> task) => task.GetAwaiter().GetResult();

    /// <summary>
    /// 依次请求候选地址，返回第一个成功解析为 JSON 对象的结果；全部失败时抛出最后一个错误。
    /// </summary>
    private static async Task<JsonObject> FetchJsonAsync(IReadOnlyList<Uri> urls, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        foreach (var url in urls)
        {
            try
            {
                var text = await Http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                return JsonNode.Parse(text) as JsonObject
                       ?? throw new JsonException($"{url} 返回的内容不是 JSON 对象。");
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new HttpRequestException("没有可用的下载地址。");
    }

    /// <summary>
    /// 请求 Fabric 或 LegacyFabric 的 meta 数据，并检查它包含各个必需的键。
    /// </summary>
    private static JsonObject FetchMetaJson(IReadOnlyList<Uri> urls, string name, CancellationToken cancellationToken, params string[] requiredKeys)
    {
        var json = Await(FetchJsonAsync(urls, cancellationToken));
        foreach (var key in requiredKeys)
        {
            if (json[key] is null)
                throw new InvalidDataException($"{name} 版本列表缺少 {key} 字段。");
        }

        return json;
    }

    /// <summary>
    /// 请求 CompApi 工程的全部文件，供 Fabric API、OptiFabric 等列表使用。
    /// </summary>
    private static List<CompFile> FetchCompFiles(string projectId, bool fromCurseForge, CancellationToken cancellationToken)
    {
        var api = new CompApi(Http);
        return Await(api.GetFilesAsync(projectId, fromCurseForge, cancellationToken));
    }

    #endregion

    #region 原版版本列表 DlClientList | Minecraft 版本列表

    /// <summary>
    /// Mojang 版本清单的结果。<see cref="DlClientListResult.Value"/> 为 JSON 对象，含 <c>latest</c> 与 <c>versions</c> 两项，
    /// 其中每个版本是带有 <c>id</c>、<c>type</c>、<c>url</c>、<c>releaseTime</c> 等键的对象。
    /// </summary>
    public sealed class DlClientListResult
    {
        /// <summary>数据来源名称，如“Official”，“BMCLAPI”。</summary>
        public string SourceName = "";

        /// <summary>是否为官方的实时数据。</summary>
        public bool IsOfficial;

        /// <summary>获取到的数据。</summary>
        public JsonObject? Value;
    }

    /// <summary>
    /// 原版版本列表，主加载器。输入为空。
    /// </summary>
    public static readonly ModLoader.LoaderTask<string, DlClientListResult> dlClientListLoader =
        new("DlClientList Main", DlClientListMain);

    private static void DlClientListMain(ModLoader.LoaderTask<string, DlClientListResult> loader)
    {
        var manifest = Await(VersionManifestClient.FetchAsync(Http, CurrentSource, loader.AbortedToken));
        var versions = new JsonArray();
        foreach (var version in manifest.Versions)
        {
            var time = version.ReleaseTime.ToString("o");
            versions.Add(new JsonObject
            {
                ["id"] = version.Id,
                ["type"] = version.Type,
                ["url"] = version.JsonUrl.AbsoluteUri,
                ["time"] = time,
                ["releaseTime"] = time,
                ["sha1"] = version.Sha1
            });
        }

        loader.output = new DlClientListResult
        {
            SourceName = SourceNameOf(CurrentSource),
            IsOfficial = CurrentSource == DownloadSource.Official,
            Value = new JsonObject
            {
                ["latest"] = new JsonObject
                {
                    ["release"] = manifest.LatestRelease,
                    ["snapshot"] = manifest.LatestSnapshot
                },
                ["versions"] = versions
            }
        };
    }

    #endregion

    #region OptiFine 列表 DlOptiFineList

    /// <summary>
    /// OptiFine 版本列表的结果。
    /// </summary>
    public sealed class DlOptiFineListResult
    {
        public string sourceName = "";

        public bool isOfficial;

        public List<DlOptiFineListEntry>? Value;
    }

    /// <summary>
    /// 一个 OptiFine 版本。
    /// </summary>
    public class DlOptiFineListEntry
    {
        /// <summary>显示名称，已去除 HD_U 字样，如“1.12.2 C8”。</summary>
        public string DisplayName = "";

        /// <summary>是否为测试版。</summary>
        public bool IsPreview;

        /// <summary>原始文件名称，如“OptiFine_1.12.2_HD_U_C8.jar”。</summary>
        public string NameFile = "";

        /// <summary>对应的版本名称，如“1.12.2-OptiFine_HD_U_C8”。</summary>
        public string NameVersion = "";

        /// <summary>发布时间，格式为“yyyy/mm/dd”。OptiFine 的 BMCLAPI 源无此数据，为空字符串。</summary>
        public string ReleaseTime = "";

        /// <summary>需要的最低 Forge 版本。空字符串为无限制，null 为不兼容，“28.1.56” 表示版本号，“1161” 表示版本号的最后一位。</summary>
        public string? RequiredForgeVersion;

        /// <summary>安装器的下载地址，Shion 额外保存，用于“保存”操作。</summary>
        public string? DownloadUrl;

        private string _inherit = "";

        /// <summary>对应的 Minecraft 版本，如“1.12.2”。末尾的 “.0” 会被去掉。</summary>
        public string Inherit
        {
            get => _inherit;
            set
            {
                if (value is not null && value.EndsWith(".0", StringComparison.Ordinal))
                    value = value.Substring(0, value.Length - 2);
                _inherit = value ?? "";
            }
        }
    }

    /// <summary>
    /// OptiFine 版本列表，主加载器。
    /// </summary>
    public static readonly ModLoader.LoaderTask<int, DlOptiFineListResult> dlOptiFineListLoader =
        new("DlOptiFineList Main", DlOptiFineListMain);

    private static void DlOptiFineListMain(ModLoader.LoaderTask<int, DlOptiFineListResult> loader)
    {
        var versions = Await(ExtraLoaderCatalog.GetOptiFineVersionsAsync(Http, CurrentSource, loader.AbortedToken));
        loader.output = new DlOptiFineListResult
        {
            sourceName = SourceNameOf(CurrentSource),
            isOfficial = CurrentSource == DownloadSource.Official,
            Value = versions.Select(version => new DlOptiFineListEntry
            {
                DisplayName = version.DisplayName,
                IsPreview = version.IsPreview,
                NameFile = version.FileName,
                NameVersion = version.VersionName,
                ReleaseTime = version.ReleaseTime,
                RequiredForgeVersion = version.RequiredForge,
                Inherit = version.MinecraftVersion,
                DownloadUrl = version.DownloadUrl
            }).ToList()
        };
    }

    #endregion

    #region Forge 版本列表 DlForgeVersion

    /// <summary>
    /// Forge 与 NeoForge、Cleanroom 等共用的基类，记录种类、对应的 Minecraft 版本与版本号。
    /// </summary>
    public abstract class DlForgelikeEntry : IComparable<DlForgelikeEntry>
    {
        public enum ForgelikeType
        {
            Forge,
            NeoForge,
            Cleanroom
        }

        /// <summary>Forgelike 种类。Forge、NeoForge、Cleanroom。</summary>
        public ForgelikeType forgeType;

        /// <summary>对应的 Minecraft 版本，如“1.12.2”。</summary>
        public string Inherit = "";

        /// <summary>
        /// 标准化后的版本号，仅可用于比较与排序。格式为 Major.Minor.Build.Revision。
        /// </summary>
        public System.Version version = new(0, 0);

        /// <summary>可对玩家显示的非格式化版本名。</summary>
        public string VersionName = "";

        /// <summary>加载器名称。Forge / NeoForge / Cleanroom。</summary>
        public string LoaderName => forgeType.ToString();

        /// <summary>文件扩展名。不以小数点开头。</summary>
        public string FileExtension
        {
            get
            {
                if (forgeType == ForgelikeType.Forge)
                    return ((DlForgeVersionEntry)this).Category == "installer" ? "jar" : "zip";
                return "jar";
            }
        }

        /// <summary>
        /// Forge：MC 版本是否小于 1.13。NeoForge：MC 版本是否为 1.20.1。Cleanroom：固定为 false。
        /// </summary>
        public bool IsLegacy
        {
            get
            {
                if (forgeType == ForgelikeType.Cleanroom)
                    return false;
                return version.Major < 20;
            }
        }

        public int CompareTo(DlForgelikeEntry? other)
        {
            if (other is null)
                return 1;
            if (version != other.version)
                return version.CompareTo(other.version);
            return McVersionComparer.CompareVersion(VersionName, other.VersionName);
        }
    }

    /// <summary>
    /// 一个 Forge 构建。
    /// </summary>
    public class DlForgeVersionEntry : DlForgelikeEntry
    {
        /// <summary>安装类型。有 installer、client、universal 三种。</summary>
        public string Category = "";

        /// <summary>用于下载的文件版本名。可能在 Version 的基础上添加了分支。</summary>
        public string FileVersion = "";

        /// <summary>文件的 MD5 或 SHA1。Shion 列表不提供哈希，为空。</summary>
        public string? Hash;

        /// <summary>是否为推荐版本。</summary>
        public bool IsRecommended;

        /// <summary>发布时间，格式为“yyyy/MM/dd HH:mm”。Shion 的列表不提供发布时间，为空字符串。</summary>
        public string ReleaseTime = "";

        /// <summary>由 PCL.Shion.Core 给出的安装器版本号（与 Forge 安装器文件名一致），为空表示由条目自身推导。</summary>
        internal string? InstallerVersion;

        public DlForgeVersionEntry(string version, string? branch, string inherit)
        {
            // 司马版本的特殊处理
            if (version == "11.15.1.2318" || version == "11.15.1.1902" || version == "11.15.1.1890")
                branch = "1.8.9";
            if (branch is null && inherit == "1.7.10")
            {
                var segments = version.Split('.');
                if (segments.Length > 3 && double.TryParse(segments[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var build) && build >= 1300d)
                    branch = "1.7.10";
            }

            // 为 DlForgelikeEntry 提供所有信息
            forgeType = ForgelikeType.Forge;
            VersionName = version;
            this.version = new System.Version(version);
            Inherit = inherit;
            FileVersion = version + (branch is null ? "" : "-" + branch);
        }
    }

    /// <summary>
    /// 把 PCL.Shion.Core 的 Forge 版本转换为列表条目。版本号形如 <c>47.4.0</c>，分支以 <c>-</c> 附在后面。
    /// 无法解析的版本号返回 null。
    /// </summary>
    internal static DlForgeVersionEntry? CreateForgeEntry(LoaderVersion loader, IReadOnlyDictionary<string, string> promotions)
    {
        var text = loader.Version;
        var dash = text.IndexOf('-');
        var number = dash < 0 ? text : text[..dash];
        var branch = dash < 0 ? null : text[(dash + 1)..];
        try
        {
            return new DlForgeVersionEntry(number, branch, loader.MinecraftVersion)
            {
                Category = "installer",
                IsRecommended = ForgeIndex.IsRecommended(loader, promotions),
                InstallerVersion = text
            };
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// Forge 版本列表，按 Minecraft 版本获取。输入为 Minecraft 版本号。
    /// 推荐版本表来自 <see cref="ModDownloadLib"/>，表未刷新时不标注推荐。
    /// </summary>
    public static void DlForgeVersionMain(ModLoader.LoaderTask<string, List<DlForgeVersionEntry>> loader)
    {
        var minecraftVersion = loader.input ?? "";
        var versions = Await(LoaderCatalog.GetVersionsAsync(LoaderKind.Forge, minecraftVersion, Http, CurrentSource, loader.AbortedToken));
        var promotions = ModDownloadLib.ForgePromotions;
        loader.output = versions
            .Select(version => CreateForgeEntry(version, promotions))
            .OfType<DlForgeVersionEntry>()
            .ToList();
    }

    #endregion

    #region Forge 原版列表 DlForgeList

    /// <summary>
    /// 有 Forge 构建的原版版本列表的结果。
    /// </summary>
    public sealed class DlForgeListResult
    {
        public string sourceName = "";

        public bool isOfficial;

        public List<string>? Value;
    }

    /// <summary>
    /// 有 Forge 构建的原版版本列表，主加载器。
    /// </summary>
    public static readonly ModLoader.LoaderTask<int, DlForgeListResult> dlForgeListLoader =
        new("DlForgeList Main", DlForgeListMain);

    private static void DlForgeListMain(ModLoader.LoaderTask<int, DlForgeListResult> loader)
    {
        var versions = Await(ForgeIndex.GetMinecraftVersionsAsync(Http, CurrentSource, loader.AbortedToken));
        loader.output = new DlForgeListResult
        {
            sourceName = SourceNameOf(CurrentSource),
            isOfficial = CurrentSource == DownloadSource.Official,
            Value = versions.ToList()
        };
    }

    #endregion

    #region NeoForge 版本列表 DlNeoForgeList

    /// <summary>
    /// NeoForge 版本列表的结果。
    /// </summary>
    public sealed class DlNeoForgeListResult
    {
        public string sourceName = "";

        public bool isOfficial;

        public List<DlNeoForgeListEntry>? Value;
    }

    /// <summary>
    /// 一个 NeoForge 版本。
    /// </summary>
    public class DlNeoForgeListEntry : DlForgelikeEntry
    {
        /// <summary>API 使用的原始版本字符串，如“20.4.30-beta”、“1.20.1-47.1.99”（Legacy）。</summary>
        public string ApiName = "";

        /// <summary>是否是 Beta 版。</summary>
        public bool IsBeta;

        public DlNeoForgeListEntry(string apiName)
        {
            forgeType = ForgelikeType.NeoForge;
            ApiName = apiName;
            IsBeta = apiName.Contains("beta", StringComparison.Ordinal) || apiName.Contains("alpha", StringComparison.Ordinal);

            const string legacyPrefix = "1.20.1-";
            if (apiName.StartsWith(legacyPrefix, StringComparison.Ordinal))
            {
                // 1.20.1-47.1.99
                VersionName = apiName[legacyPrefix.Length..];
                version = new System.Version("19." + VersionName);
                Inherit = "1.20.1";
            }
            else if (apiName.StartsWith("0.", StringComparison.Ordinal))
            {
                // 0.25w14craftmine.3-beta
                VersionName = apiName;
                var segments = apiName.Split('-')[0].Split('.');
                var build = segments.Length > 0 && int.TryParse(segments[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
                version = new System.Version(0, 0, build);
                Inherit = segments.Length > 1 ? segments[1] : "";
            }
            else
            {
                // 20.4.30-beta；26.1.0.0-alpha.1+snapshot-1
                VersionName = apiName;
                version = new System.Version(apiName.Split('-')[0]);
                if (version.Major >= 24)
                    Inherit = $"{version.Major}.{version.Minor}{(version.Build > 0 ? $".{version.Build}" : "")}";
                else
                    Inherit = "1." + version.Major + (version.Minor > 0 ? "." + version.Minor : "");
                var plus = VersionName.IndexOf('+');
                if (plus >= 0)
                    Inherit += "-" + VersionName[(plus + 1)..];
            }
        }

        /// <summary>文件在官网的基础地址，不包含后缀。</summary>
        public string UrlBase
        {
            get
            {
                var packageName = IsLegacy ? "forge" : "neoforge";
                return $"https://maven.neoforged.net/releases/net/neoforged/{packageName}/{ApiName}/{packageName}-{ApiName}";
            }
        }
    }

    /// <summary>
    /// NeoForge 版本列表，主加载器。
    /// </summary>
    public static readonly ModLoader.LoaderTask<int, DlNeoForgeListResult> dlNeoForgeListLoader =
        new("DlNeoForgeList Main", DlNeoForgeListMain);

    private static void DlNeoForgeListMain(ModLoader.LoaderTask<int, DlNeoForgeListResult> loader)
    {
        // NeoForge 的 Maven 没有镜像，只请求官方地址
        var cores = Await(ForgeIndex.GetNeoForgeVersionsAsync(Http, loader.AbortedToken));
        var entries = new List<DlNeoForgeListEntry>();
        foreach (var core in cores)
        {
            // 1.20.1 上沿用 Forge 坐标发布的版本，列表项名称带上 1.20.1 前缀
            var apiName = core.MinecraftVersion == "1.20.1" ? "1.20.1-" + core.Version : core.Version;
            try
            {
                entries.Add(new DlNeoForgeListEntry(apiName));
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
            {
                // 无法解析的版本号直接跳过
            }
        }

        loader.output = new DlNeoForgeListResult
        {
            sourceName = SourceNameOf(CurrentSource),
            isOfficial = CurrentSource == DownloadSource.Official,
            Value = entries
        };
    }

    #endregion

    #region Cleanroom 版本列表 DlCleanroomList

    /// <summary>
    /// Cleanroom 版本列表的结果。
    /// </summary>
    public sealed class DlCleanroomListResult
    {
        public string sourceName = "";

        public bool isOfficial;

        public List<DlCleanroomListEntry>? Value;
    }

    /// <summary>
    /// 一个 Cleanroom 版本。
    /// </summary>
    public class DlCleanroomListEntry : DlForgelikeEntry
    {
        /// <summary>API 使用的原始版本字符串，如“0.2.4-alpha”。</summary>
        public string ApiName = "";

        /// <summary>是否是 Beta 版。</summary>
        public bool IsBeta;

        public DlCleanroomListEntry(string apiName)
        {
            forgeType = ForgelikeType.Cleanroom;
            ApiName = apiName;
            IsBeta = apiName.Contains("alpha", StringComparison.Ordinal);
            VersionName = apiName;
            version = new System.Version(apiName.Split('-')[0]);
            Inherit = "1.12.2";
        }

        /// <summary>文件在官网的基础地址，不包含后缀。</summary>
        public string UrlBase => $"https://github.com/CleanroomMC/Cleanroom/releases/download/{ApiName}/cleanroom-{ApiName}";
    }

    /// <summary>
    /// Cleanroom 版本列表，主加载器。数据只来自 GitHub，没有镜像。
    /// </summary>
    public static readonly ModLoader.LoaderTask<int, DlCleanroomListResult> dlCleanroomListLoader =
        new("DlCleanroomList Main", DlCleanroomListMain);

    private static void DlCleanroomListMain(ModLoader.LoaderTask<int, DlCleanroomListResult> loader)
    {
        var versions = Await(CleanroomCatalog.GetVersionsAsync(Http, loader.AbortedToken));
        var entries = new List<DlCleanroomListEntry>();
        foreach (var version in versions)
        {
            try
            {
                entries.Add(new DlCleanroomListEntry(version.Tag));
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
            {
                // 无法解析的版本号直接跳过
            }
        }

        loader.output = new DlCleanroomListResult
        {
            sourceName = "GitHub",
            isOfficial = true,
            Value = entries
        };
    }

    #endregion

    #region LiteLoader 版本列表 DlLiteLoaderList

    /// <summary>
    /// LiteLoader 版本列表的结果。
    /// </summary>
    public sealed class DlLiteLoaderListResult
    {
        public string sourceName = "";

        public bool isOfficial;

        public List<DlLiteLoaderListEntry>? Value;

        /// <summary>官方源的失败原因。若没有则为 null。</summary>
        public Exception? officialError;
    }

    /// <summary>
    /// 一个 LiteLoader 版本。
    /// </summary>
    public class DlLiteLoaderListEntry
    {
        /// <summary>实际的文件名，如“liteloader-installer-1.12.2-00-SNAPSHOT.jar”。</summary>
        public string FileName = "";

        /// <summary>对应的 Minecraft 版本，如“1.12.2”。</summary>
        public string Inherit = "";

        /// <summary>是否为 1.7 及更早的远古版。</summary>
        public bool IsLegacy;

        /// <summary>是否为测试版。</summary>
        public bool IsPreview;

        /// <summary>对应的 JSON 项。Shion 的列表不保留原始 JSON，为空。</summary>
        public JsonNode? jsonToken;

        /// <summary>文件的 MD5。Shion 的列表不提供，为空。</summary>
        public string? MD5;

        /// <summary>发布时间，格式为“yyyy/MM/dd HH:mm”。</summary>
        public string ReleaseTime = "";

        /// <summary>安装器的下载地址，Shion 额外保存，用于“保存”操作。远古版本没有已知地址时为 null。</summary>
        public string? DownloadUrl;
    }

    /// <summary>
    /// LiteLoader 版本列表，主加载器。
    /// </summary>
    public static readonly ModLoader.LoaderTask<int, DlLiteLoaderListResult> dlLiteLoaderListLoader =
        new("DlLiteLoaderList Main", DlLiteLoaderListMain);

    private static void DlLiteLoaderListMain(ModLoader.LoaderTask<int, DlLiteLoaderListResult> loader)
    {
        var versions = Await(ExtraLoaderCatalog.GetLiteLoaderVersionsAsync(Http, CurrentSource, loader.AbortedToken));
        loader.output = new DlLiteLoaderListResult
        {
            sourceName = SourceNameOf(CurrentSource),
            isOfficial = CurrentSource == DownloadSource.Official,
            Value = versions.Select(version => new DlLiteLoaderListEntry
            {
                FileName = version.FileName,
                Inherit = version.MinecraftVersion,
                IsLegacy = version.IsLegacy,
                IsPreview = version.IsPreview,
                ReleaseTime = version.ReleaseTime,
                DownloadUrl = TryGetLiteLoaderUrl(version)
            }).ToList()
        };
    }

    private static string? TryGetLiteLoaderUrl(LiteLoaderVersion version)
    {
        try
        {
            return ExtraLoaderCatalog.GetLiteLoaderInstallerUrl(version);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    #endregion

    #region Fabric 版本列表 DlFabricList

    /// <summary>
    /// Fabric 版本列表的结果。<see cref="Value"/> 为 Fabric meta 的原始 JSON，含 <c>game</c>、<c>loader</c>、<c>installer</c> 三项。
    /// </summary>
    public sealed class DlFabricListResult
    {
        public string sourceName = "";

        public bool isOfficial;

        public JsonObject? Value;
    }

    /// <summary>
    /// Fabric 列表，主加载器。
    /// </summary>
    public static readonly ModLoader.LoaderTask<int, DlFabricListResult> dlFabricListLoader =
        new("DlFabricList Main", DlFabricListMain);

    private static void DlFabricListMain(ModLoader.LoaderTask<int, DlFabricListResult> loader)
    {
        var json = FetchMetaJson(OrderSources(new Uri(FabricMetaOfficial), new Uri(FabricMetaBmclapi)),
            "Fabric", loader.AbortedToken, "game", "loader", "installer");
        loader.output = new DlFabricListResult
        {
            sourceName = SourceNameOf(CurrentSource),
            isOfficial = CurrentSource == DownloadSource.Official,
            Value = json
        };
    }

    #endregion

    #region LegacyFabric 版本列表 DlLegacyFabricList

    /// <summary>
    /// LegacyFabric 版本列表的结果。<see cref="Value"/> 为 LegacyFabric meta 的原始 JSON。
    /// </summary>
    public sealed class DlLegacyFabricListResult
    {
        public string sourceName = "";

        public bool isOfficial;

        public JsonObject? Value;
    }

    /// <summary>
    /// LegacyFabric 列表，主加载器。LegacyFabric 没有镜像。
    /// </summary>
    public static readonly ModLoader.LoaderTask<int, DlLegacyFabricListResult> dlLegacyFabricListLoader =
        new("DlLegacyFabricList Main", DlLegacyFabricListMain);

    private static void DlLegacyFabricListMain(ModLoader.LoaderTask<int, DlLegacyFabricListResult> loader)
    {
        var json = FetchMetaJson([new Uri(LegacyFabricMeta)], "LegacyFabric", loader.AbortedToken, "game", "loader", "installer");
        loader.output = new DlLegacyFabricListResult
        {
            sourceName = "LegacyFabric",
            isOfficial = true,
            Value = json
        };
    }

    #endregion

    #region Fabric API 与 OptiFabric 列表

    /// <summary>
    /// Fabric API 列表，来自 Modrinth。
    /// </summary>
    public static readonly ModLoader.LoaderTask<int, List<CompFile>> dlFabricApiLoader =
        new("Fabric API List Loader", task => task.output = FetchCompFiles(FabricApiProjectId, false, task.AbortedToken));

    /// <summary>
    /// Legacy Fabric API 列表，来自 Modrinth。
    /// </summary>
    public static readonly ModLoader.LoaderTask<int, List<CompFile>> dlLegacyFabricApiLoader =
        new("Legacy Fabric API List Loader", task => task.output = FetchCompFiles(LegacyFabricApiProjectId, false, task.AbortedToken));

    /// <summary>
    /// OptiFabric 列表，来自 CurseForge。没有 CurseForge API Key 时，获取会失败。
    /// </summary>
    public static readonly ModLoader.LoaderTask<int, List<CompFile>> dlOptiFabricLoader =
        new("OptiFabric List Loader", task => task.output = FetchCompFiles(OptiFabricProjectId, true, task.AbortedToken));

    #endregion

    #region LabyMod 列表 DlLabyModList

    /// <summary>
    /// LabyMod 列表的结果。<see cref="Value"/> 含 <c>production</c> 与 <c>snapshot</c> 两项，各自是 LabyMod 清单的原始 JSON。
    /// </summary>
    public sealed class DlLabyModListResult
    {
        public JsonObject? Value;
    }

    /// <summary>
    /// LabyMod 列表，主加载器。
    /// </summary>
    public static readonly ModLoader.LoaderTask<int, DlLabyModListResult> dlLabyModListLoader =
        new("DlLabyModList Main", DlLabyModListMain);

    private static void DlLabyModListMain(ModLoader.LoaderTask<int, DlLabyModListResult> loader)
    {
        var production = Await(FetchJsonAsync([new Uri(LabyModProductionManifest)], loader.AbortedToken));
        var snapshot = Await(FetchJsonAsync([new Uri(LabyModSnapshotManifest)], loader.AbortedToken));
        if (production["labyModVersion"] is null || snapshot["labyModVersion"] is null)
            throw new InvalidDataException("LabyMod 版本列表缺少 labyModVersion 字段。");

        loader.output = new DlLabyModListResult
        {
            Value = new JsonObject
            {
                ["production"] = production,
                ["snapshot"] = snapshot
            }
        };
    }

    #endregion
}
