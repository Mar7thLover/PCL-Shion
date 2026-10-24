using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.Shion.Core.Online;

/// <summary>
/// 资源工程信息，来自 CurseForge 或 Modrinth。
/// </summary>
public sealed class CompProject
{
    /// <summary>
    /// 已知工程信息的缓存，键为工程 ID。
    /// </summary>
    public static readonly ConcurrentDictionary<string, CompProject> Cache = new();

    private static readonly Dictionary<string, CompLoaderType> ModrinthLoaderTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["forge"] = CompLoaderType.Forge,
            ["fabric"] = CompLoaderType.Fabric,
            ["quilt"] = CompLoaderType.Quilt,
            ["neoforge"] = CompLoaderType.NeoForge
        };

    private static readonly Dictionary<int, string> CurseForgeCategoryTagKeys = new()
    {
        // Mod
        [406] = "Download.Comp.Category.WorldGen",
        [407] = "Download.Comp.Category.Biomes",
        [410] = "Download.Comp.Category.Dimensions",
        [408] = "Download.Comp.Category.OresResources",
        [409] = "Download.Comp.Category.Structures",
        [412] = "Download.Comp.Category.Technology",
        [415] = "Download.Comp.Category.PipesLogistics",
        [4843] = "Download.Comp.Category.Automation",
        [417] = "Download.Comp.Category.Energy",
        [4558] = "Download.Comp.Category.Redstone",
        [436] = "Download.Comp.Category.FoodCooking",
        [416] = "Download.Comp.Category.Farming",
        [414] = "Download.Comp.Category.Transportation",
        [420] = "Download.Comp.Category.Storage",
        [419] = "Download.Comp.Category.Magic",
        [422] = "Download.Comp.Category.Adventure",
        [424] = "Download.Comp.Category.Decoration",
        [411] = "Download.Comp.Category.Mobs",
        [434] = "Download.Comp.Category.Equipment",
        [6814] = "Download.Comp.Category.Optimization",
        [9026] = "Download.Comp.Category.Creative",
        [423] = "Download.Comp.Category.Display",
        [435] = "Download.Comp.Category.Server",
        [5191] = "Download.Comp.Category.Tweaks",
        [421] = "Download.Comp.Category.Library",

        // 整合包
        [4484] = "Download.Comp.Category.Multiplayer",
        [4479] = "Download.Comp.Category.Modpack.Hardcore",
        [4483] = "Download.Comp.Category.Combat",
        [4478] = "Download.Comp.Category.Modpack.Quests",
        [4472] = "Download.Comp.Category.Technology",
        [4473] = "Download.Comp.Category.Magic",
        [4475] = "Download.Comp.Category.Adventure",
        [4476] = "Download.Comp.Category.Modpack.Exploration",
        [4477] = "Download.Comp.Category.Modpack.MiniGame",
        [4471] = "Download.Comp.Category.Modpack.SciFi",
        [4736] = "Download.Comp.Category.Modpack.Skyblock",
        [5128] = "Download.Comp.Category.Modpack.VanillaPlus",
        [4487] = "Download.Comp.Category.Modpack.Ftb",
        [4480] = "Download.Comp.Category.Modpack.MapBased",
        [4481] = "Download.Comp.Category.Modpack.SmallLight",
        [4482] = "Download.Comp.Category.Modpack.ExtraLarge",

        // 资源包
        [403] = "Download.Comp.Category.VanillaLike",
        [400] = "Download.Comp.Category.Realistic",
        [401] = "Download.Comp.Category.Modern",
        [402] = "Download.Comp.Category.Medieval",
        [399] = "Download.Comp.Category.Steampunk",
        [5244] = "Download.Comp.Category.Fonts",
        [404] = "Download.Comp.Category.Animated",
        [4465] = "Download.Comp.Category.ModSupport",
        [393] = "Download.Comp.Category.ResourcePack.Resolution16x",
        [394] = "Download.Comp.Category.ResourcePack.Resolution32x",
        [395] = "Download.Comp.Category.ResourcePack.Resolution64x",
        [396] = "Download.Comp.Category.ResourcePack.Resolution128x",
        [397] = "Download.Comp.Category.ResourcePack.Resolution256x",
        [398] = "Download.Comp.Category.ResourcePack.Resolution512xOrHigher",
        [5193] = "Download.Comp.Type.DataPack",

        // 光影包
        [6553] = "Download.Comp.Category.Realistic",
        [6554] = "Download.Comp.Category.Fantasy",
        [6555] = "Download.Comp.Category.VanillaLike",

        // 数据包
        [6948] = "Download.Comp.Category.Adventure",
        [6949] = "Download.Comp.Category.DataPack.Fantasy",
        [6950] = "Download.Comp.Category.Library",
        [6952] = "Download.Comp.Category.Magic",
        [6946] = "Download.Comp.Category.Mod.ModRelated",
        [6951] = "Download.Comp.Category.Technology",
        [6953] = "Download.Comp.Category.Utility",

        // 世界
        [248] = "Download.Comp.Category.Adventure",
        [249] = "Download.Comp.Category.World.Creative",
        [250] = "Download.Comp.Category.Modpack.MiniGame",
        [251] = "Download.Comp.Category.World.Parkour",
        [252] = "Download.Comp.Category.World.Puzzle",
        [253] = "Download.Comp.Category.World.Survival",
        [4464] = "Download.Comp.Category.World.ModWorld"
    };

    private static readonly Dictionary<string, string> ResourcePackHiddenCategoryTagKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["decoration"] = "Download.Comp.Category.Decoration",
            ["mobs"] = "Download.Comp.Category.Mobs",
            ["equipment"] = "Download.Comp.Category.Equipment"
        };

    private static readonly Dictionary<string, string> ModrinthCategoryTagKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // 共用
            ["technology"] = "Download.Comp.Category.Technology",
            ["magic"] = "Download.Comp.Category.Magic",
            ["adventure"] = "Download.Comp.Category.Adventure",
            ["utility"] = "Download.Comp.Category.Utility",
            ["optimization"] = "Download.Comp.Category.Optimization",
            ["vanilla-like"] = "Download.Comp.Category.VanillaLike",
            ["realistic"] = "Download.Comp.Category.Realistic",

            // Mod / 数据包
            ["worldgen"] = "Download.Comp.Category.WorldGen",
            ["food"] = "Download.Comp.Category.FoodCooking",
            ["game-mechanics"] = "Download.Comp.Category.GameMechanics",
            ["transportation"] = "Download.Comp.Category.Transportation",
            ["storage"] = "Download.Comp.Category.Storage",
            ["social"] = "Download.Comp.Category.Server",
            ["library"] = "Download.Comp.Category.Library",

            // 整合包
            ["multiplayer"] = "Download.Comp.Category.Multiplayer",
            ["challenging"] = "Download.Comp.Category.Modpack.Hardcore",
            ["combat"] = "Download.Comp.Category.Combat",
            ["quests"] = "Download.Comp.Category.Modpack.Quests",
            ["kitchen-sink"] = "Download.Comp.Category.Modpack.KitchenSink",
            ["lightweight"] = "Download.Comp.Category.Modpack.SmallLight",

            // 资源包
            ["simplistic"] = "Download.Comp.Category.Simplistic",
            ["tweaks"] = "Download.Comp.Category.Tweaks",
            ["8x-"] = "Download.Comp.Category.ResourcePack.Resolution8xOrLower",
            ["16x"] = "Download.Comp.Category.ResourcePack.Resolution16x",
            ["32x"] = "Download.Comp.Category.ResourcePack.Resolution32x",
            ["48x"] = "Download.Comp.Category.ResourcePack.Resolution48x",
            ["64x"] = "Download.Comp.Category.ResourcePack.Resolution64x",
            ["128x"] = "Download.Comp.Category.ResourcePack.Resolution128x",
            ["256x"] = "Download.Comp.Category.ResourcePack.Resolution256x",
            ["512x+"] = "Download.Comp.Category.ResourcePack.Resolution512xOrHigher",
            ["audio"] = "Download.Comp.Category.Audio",
            ["fonts"] = "Download.Comp.Category.Fonts",
            ["models"] = "Download.Comp.Category.Models",
            ["gui"] = "Download.Comp.Category.Gui",
            ["locale"] = "Download.Comp.Category.Locale",
            ["core-shaders"] = "Download.Comp.Category.CoreShaders",
            ["modded"] = "Download.Comp.Category.ModSupport",

            // 光影包
            ["fantasy"] = "Download.Comp.Category.Fantasy",
            ["semi-realistic"] = "Download.Comp.Category.SemiRealistic",
            ["cartoon"] = "Download.Comp.Category.Cartoon",
            ["colored-lighting"] = "Download.Comp.Category.ColoredLighting",
            ["path-tracing"] = "Download.Comp.Category.PathTracing",
            ["pbr"] = "Download.Comp.Category.Pbr",
            ["reflections"] = "Download.Comp.Category.Reflections",
            ["iris"] = "Download.Comp.Category.Iris",
            ["optifine"] = "Download.Comp.Category.Optifine",
            ["vanilla"] = "Download.Comp.Filter.Loader.VanillaAvailable"
        };

    /// <summary>
    /// CurseForge 文件列表的数字 ID。Modrinth 工程的此项无效。
    /// </summary>
    public List<int> CurseForgeFileIds { get; private set; } = [];

    /// <summary>英文描述。</summary>
    public string Description { get; private set; } = "";

    /// <summary>下载量计数。注意，该计数仅为一个来源，无法反应两边加起来的下载量！</summary>
    public int DownloadCount { get; private set; }

    /// <summary>支持的 Drop 编号，从高到低排序。例如：261（26.1.x）、180（1.18.x）。</summary>
    public List<int> Drops { get; private set; } = [];

    /// <summary>该工程信息来自 CurseForge 还是 Modrinth。</summary>
    public bool FromCurseForge { get; private set; }

    /// <summary>CurseForge 工程的数字 ID。Modrinth 工程的乱码 ID。</summary>
    public string Id { get; private set; } = "";

    /// <summary>最后一次更新的时间。可能为 null。</summary>
    public DateTime? LastUpdate { get; private set; }

    /// <summary>支持的 Mod 加载器列表。可能为空。</summary>
    public List<CompLoaderType> ModLoaders { get; private set; } = [];

    /// <summary>原始的英文名称。</summary>
    public string RawName { get; private set; } = "";

    /// <summary>工程的短名。例如 technical-enchant。</summary>
    public string Slug { get; private set; } = "";

    /// <summary>
    /// 描述性标签的语言键，由界面层翻译。
    /// </summary>
    public List<string> TagKeys { get; private set; } = [];

    /// <summary>工程的种类。由于 Modrinth 混合使用 Mod 和数据包，结果不一定准确。</summary>
    public CompType Type { get; private set; }

    /// <summary>来源网站的工程页面网址。</summary>
    public string Website { get; private set; } = "";

    /// <summary>Logo 图片的下载地址。若为 null 则没有。</summary>
    public string? LogoUrl { get; private set; }

    /// <summary>
    /// 工程的显示名称。当前没有翻译数据源，等同于原始名称。
    /// </summary>
    public string TranslatedName => RawName;

    /// <summary>
    /// 实例化工程信息。若数据格式不符会抛出异常。
    /// </summary>
    public CompProject(JsonObject data)
    {
        var result = data.ContainsKey("summary")
            ? BuildFromCurseForge(data)
            : BuildFromModrinth(data);

        if (result.TagKeys.Count == 0)
            result.TagKeys.Add("Download.Comp.Category.Other");
        result.TagKeys = result.TagKeys.Distinct().ToList();
        result.TagKeys.Sort(StringComparer.Ordinal);
        result.ModLoaders = result.ModLoaders.Distinct().OrderBy(t => t).ToList();

        FromCurseForge = result.FromCurseForge;
        Type = result.Type;
        Slug = result.Slug;
        Id = result.Id;
        CurseForgeFileIds = result.CurseForgeFileIds;
        RawName = result.RawName;
        Description = result.Description;
        Website = result.Website;
        LastUpdate = result.LastUpdate;
        DownloadCount = result.DownloadCount;
        ModLoaders = result.ModLoaders;
        TagKeys = result.TagKeys;
        LogoUrl = result.LogoUrl;
        Drops = result.Drops;

        Cache[Id] = this;
    }

    /// <summary>
    /// 检查是否与某个工程是相同的工程，只是在不同的网站。
    /// </summary>
    public bool IsLike(CompProject project)
    {
        if ((Id ?? "") == (project.Id ?? ""))
            return true;

        static string GetRaw(string data) =>
            new string(data.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        if (FromCurseForge == project.FromCurseForge)
            return false;
        if (ModLoaders.Count != project.ModLoaders.Count || ModLoaders.Except(project.ModLoaders).Any())
            return false;
        if (Type != CompType.Shader && (Drops.Count != project.Drops.Count || Drops.Except(project.Drops).Any()))
            return false;
        if (LastUpdate is not null && project.LastUpdate is not null &&
            Math.Abs((LastUpdate.Value - project.LastUpdate.Value).TotalDays) > 7d)
            return false;
        if ((TranslatedName ?? "") == (project.TranslatedName ?? "") ||
            (RawName ?? "") == (project.RawName ?? "") || (Description ?? "") == (project.Description ?? "") ||
            GetRaw(Slug ?? "") == GetRaw(project.Slug ?? ""))
            return true;

        return false;
    }

    public override string ToString() => $"{Id} ({Slug}): {RawName}";

    public override bool Equals(object? obj) =>
        obj is CompProject project && (Id ?? "") == (project.Id ?? "");

    public override int GetHashCode() => (Id ?? "").GetHashCode();

    private sealed class BuildResult
    {
        public bool FromCurseForge;
        public CompType Type;
        public string Slug = "";
        public string Id = "";
        public List<int> CurseForgeFileIds = [];
        public string RawName = "";
        public string Description = "";
        public string Website = "";
        public DateTime? LastUpdate;
        public int DownloadCount;
        public List<CompLoaderType> ModLoaders = [];
        public List<string> TagKeys = [];
        public string? LogoUrl;
        public List<int> Drops = [];
    }

    private static BuildResult BuildFromCurseForge(JsonObject data)
    {
        var result = new BuildResult
        {
            FromCurseForge = true,
            Id = data["id"]!.ToString(),
            Slug = (string?)data["slug"] ?? "",
            RawName = (string?)data["name"] ?? "",
            Description = (string?)data["summary"] ?? "",
            Website = (data["links"]?["websiteUrl"]?.ToString() ?? "").TrimEnd('/'),
            LastUpdate = data["dateReleased"]?.GetValue<DateTime>(),
            DownloadCount = (int)data["downloadCount"]!
        };

        if (data["logo"] is JsonObject { Count: > 0 } logo)
        {
            var thumbnail = (string?)logo["thumbnailUrl"];
            result.LogoUrl = string.IsNullOrEmpty(thumbnail) ? (string?)logo["url"] : thumbnail;
        }

        if (string.IsNullOrEmpty(result.LogoUrl))
            result.LogoUrl = null;

        result.Type = GetCurseForgeTypeByWebsite(result.Website);

        var files = new List<KeyValuePair<int, List<string>>>();
        foreach (var file in (data["latestFiles"] as JsonArray) ?? new JsonArray())
        {
            var newFile = new CompFile((JsonObject)file!, result.Type);
            if (!newFile.Available)
                continue;

            result.ModLoaders.AddRange(newFile.ModLoaders);

            var gameVersions = file!["gameVersions"]?.Deserialize<List<string>>() ?? [];
            if (!gameVersions.Any(CompVersions.IsFormatFit))
                continue;

            files.Add(new KeyValuePair<int, List<string>>((int)file["id"]!, gameVersions));
        }

        files.AddRange(
            from indexed in (data["latestFilesIndexes"] as JsonArray) ?? new JsonArray()
            let gameVersion = indexed?["gameVersion"]?.ToString() ?? ""
            where CompVersions.IsFormatFit(gameVersion)
            select new KeyValuePair<int, List<string>>((int)indexed!["fileId"]!, [gameVersion]));

        result.CurseForgeFileIds = files.Select(f => f.Key).Distinct().ToList();

        result.Drops = files
            .SelectMany(f => f.Value)
            .Select(v => CompVersions.VersionToDrop(v))
            .Where(v => v > 0)
            .Distinct()
            .OrderByDescending(v => v)
            .ToList();

        result.ModLoaders = result.ModLoaders.Distinct().OrderBy(t => t).ToList();

        var categories = ((data["categories"] as JsonArray) ?? new JsonArray())
            .Select(t => t?["id"]?.GetValue<int?>())
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .Distinct()
            .OrderByDescending(t => t);

        foreach (var category in categories)
            if (CurseForgeCategoryTagKeys.TryGetValue(category, out var key) && !result.TagKeys.Contains(key))
                result.TagKeys.Add(key);

        return result;
    }

    private static BuildResult BuildFromModrinth(JsonObject data)
    {
        var projectType = (string?)data["project_type"] ?? "";
        var slug = (string?)data["slug"] ?? "";

        var result = new BuildResult
        {
            FromCurseForge = false,
            Id = (string?)(data["project_id"] ?? data["id"]) ?? "",
            Slug = slug,
            RawName = (string?)data["title"] ?? "",
            Description = (string?)data["description"] ?? "",
            LastUpdate = data["date_modified"]?.GetValue<DateTime>(),
            DownloadCount = (int)data["downloads"]!,
            LogoUrl = (string?)data["icon_url"],
            Website = $"https://modrinth.com/{projectType}/{slug}",
            Type = projectType switch
            {
                "modpack" => CompType.ModPack,
                "resourcepack" => CompType.ResourcePack,
                "shader" => CompType.Shader,
                _ => CompType.Mod
            }
        };

        if (string.IsNullOrEmpty(result.LogoUrl))
            result.LogoUrl = null;

        result.Drops = ((data["game_versions"] ?? data["versions"]) as JsonArray ?? new JsonArray())
            .Select(v => CompVersions.VersionToDrop((string?)v))
            .Where(v => v > 0)
            .Distinct()
            .OrderByDescending(v => v)
            .ToList();

        foreach (var category in (data["loaders"] as JsonArray)?.Select(t => t?.ToString()) ?? Array.Empty<string?>())
            if (ModrinthLoaderTypes.TryGetValue(category ?? "", out var loader))
                result.ModLoaders.Add(loader);

        foreach (var category in (data["categories"] as JsonArray)?.Select(t => t?.ToString()) ?? Array.Empty<string?>())
        {
            if (string.IsNullOrEmpty(category)) continue;
            if (ModrinthLoaderTypes.TryGetValue(category, out var loader))
            {
                result.ModLoaders.Add(loader);
                continue;
            }

            if (category.Equals("datapack", StringComparison.OrdinalIgnoreCase))
            {
                result.Type = CompType.DataPack;
                continue;
            }

            if (ResourcePackHiddenCategoryTagKeys.TryGetValue(category, out var hiddenKey))
            {
                if (result.Type != CompType.ResourcePack && !result.TagKeys.Contains(hiddenKey))
                    result.TagKeys.Add(hiddenKey);
            }
            else if (ModrinthCategoryTagKeys.TryGetValue(category, out var key) && !result.TagKeys.Contains(key))
            {
                result.TagKeys.Add(key);
            }
        }

        return result;
    }

    private static CompType GetCurseForgeTypeByWebsite(string website)
    {
        var websiteLower = (website ?? "").ToLowerInvariant();

        if (websiteLower.Contains("/mc-mods/") || websiteLower.Contains("/mod/"))
            return CompType.Mod;
        if (websiteLower.Contains("/modpacks/"))
            return CompType.ModPack;
        if (websiteLower.Contains("/resourcepacks/") || websiteLower.Contains("/texture-packs/"))
            return CompType.ResourcePack;
        if (websiteLower.Contains("/shaders/"))
            return CompType.Shader;
        if (websiteLower.Contains("/worlds/"))
            return CompType.World;

        return CompType.DataPack;
    }
}
