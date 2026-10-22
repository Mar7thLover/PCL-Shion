using System.Text.Json;

namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 单个版本 JSON 文件的内容，尚未处理 inheritsFrom。
/// </summary>
/// <param name="InheritsFrom">inheritsFrom，即父版本名称。</param>
/// <param name="MainClass">mainClass。</param>
/// <param name="Type">type。</param>
/// <param name="AssetIndexId">assetIndex.id，缺失时取 assets。</param>
/// <param name="AssetIndexDownload">assetIndex 的下载信息。</param>
/// <param name="JavaMajorVersion">javaVersion.majorVersion。</param>
/// <param name="JavaComponent">javaVersion.component。</param>
/// <param name="ClientVersion">clientVersion（部分合并版 JSON 带有）。</param>
/// <param name="MinecraftArguments">旧格式的 minecraftArguments。</param>
/// <param name="JvmArguments">arguments.jvm。</param>
/// <param name="GameArguments">arguments.game。</param>
/// <param name="Libraries">libraries。</param>
/// <param name="ClientJarDownload">downloads.client；不为 null 即表示本版本自带客户端 jar。</param>
/// <param name="LoggingArgument">logging.client.argument。</param>
/// <param name="LoggingFileId">logging.client.file.id。</param>
/// <param name="LoggingFileDownload">logging.client.file 的下载信息。</param>
internal sealed record VersionFile(
    string? InheritsFrom,
    string? MainClass,
    string? Type,
    string? AssetIndexId,
    FileDownload? AssetIndexDownload,
    int? JavaMajorVersion,
    string? JavaComponent,
    string? ClientVersion,
    string? MinecraftArguments,
    IReadOnlyList<VersionArgument> JvmArguments,
    IReadOnlyList<VersionArgument> GameArguments,
    IReadOnlyList<VersionLibrary> Libraries,
    FileDownload? ClientJarDownload,
    string? LoggingArgument,
    string? LoggingFileId,
    FileDownload? LoggingFileDownload)
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public static VersionFile Load(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid version JSON: {path}", ex);
        }
    }

    public static VersionFile Parse(string json)
    {
        using var document = JsonDocument.Parse(json, DocumentOptions);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The version JSON root must be an object.");

        var arguments = root.GetObjectProperty("arguments");
        var assetIndex = root.GetObjectProperty("assetIndex");
        var javaVersion = root.GetObjectProperty("javaVersion");
        var logging = root.GetObjectProperty("logging")?.GetObjectProperty("client");
        var loggingFile = logging?.GetObjectProperty("file");

        return new VersionFile(
            InheritsFrom: root.GetStringProperty("inheritsFrom"),
            MainClass: root.GetStringProperty("mainClass"),
            Type: root.GetStringProperty("type"),
            AssetIndexId: assetIndex?.GetStringProperty("id") ?? root.GetStringProperty("assets"),
            AssetIndexDownload: ParseFileDownload(assetIndex),
            JavaMajorVersion: javaVersion?.GetInt32Property("majorVersion"),
            JavaComponent: javaVersion?.GetStringProperty("component"),
            ClientVersion: root.GetStringProperty("clientVersion"),
            MinecraftArguments: root.GetStringProperty("minecraftArguments"),
            JvmArguments: ParseArguments(arguments?.GetArrayProperty("jvm")),
            GameArguments: ParseArguments(arguments?.GetArrayProperty("game")),
            Libraries: ParseLibraries(root.GetArrayProperty("libraries")),
            ClientJarDownload: ParseFileDownload(root.GetObjectProperty("downloads")?.GetObjectProperty("client")),
            LoggingArgument: logging?.GetStringProperty("argument"),
            LoggingFileId: loggingFile?.GetStringProperty("id"),
            LoggingFileDownload: ParseFileDownload(loggingFile));
    }

    /// <summary>
    /// 读取含 url 的下载描述，sha1 与 size 可以缺省；对象不存在或没有 url 时返回 null。
    /// </summary>
    private static FileDownload? ParseFileDownload(JsonElement? element)
    {
        if (element is not { } value || value.GetStringProperty("url") is not { } url)
            return null;
        return new FileDownload(url, value.GetStringProperty("sha1"), value.GetInt64Property("size"));
    }

    private static List<VersionArgument> ParseArguments(JsonElement? array)
    {
        var result = new List<VersionArgument>();
        if (array is null)
            return result;

        foreach (var item in array.Value.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    result.Add(new VersionArgument([item.GetString()!], []));
                    break;
                case JsonValueKind.Object:
                    result.Add(new VersionArgument(ReadValues(item), ParseRules(item.GetArrayProperty("rules"))));
                    break;
            }
        }
        return result;
    }

    /// <summary>
    /// 读取参数项的 value，它可以是单个字符串，也可以是字符串数组。
    /// </summary>
    private static List<string> ReadValues(JsonElement argument)
    {
        var values = new List<string>();
        if (!argument.TryGetProperty("value", out var value))
            return values;

        if (value.ValueKind == JsonValueKind.String)
        {
            values.Add(value.GetString()!);
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in value.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                    values.Add(element.GetString()!);
            }
        }
        return values;
    }

    private static List<VersionRule> ParseRules(JsonElement? array)
    {
        var rules = new List<VersionRule>();
        if (array is null)
            return rules;

        foreach (var item in array.Value.EnumerateArray())
        {
            var os = item.GetObjectProperty("os");
            var features = new Dictionary<string, bool>();
            if (item.GetObjectProperty("features") is { } featureObject)
            {
                foreach (var feature in featureObject.EnumerateObject())
                {
                    if (feature.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        features[feature.Name] = feature.Value.GetBoolean();
                }
            }

            rules.Add(new VersionRule(
                Allow: item.GetStringProperty("action") == "allow",
                OsName: os?.GetStringProperty("name"),
                OsArch: os?.GetStringProperty("arch"),
                OsVersion: os?.GetStringProperty("version"),
                Features: features));
        }
        return rules;
    }

    private static List<VersionLibrary> ParseLibraries(JsonElement? array)
    {
        var libraries = new List<VersionLibrary>();
        if (array is null)
            return libraries;

        foreach (var item in array.Value.EnumerateArray())
        {
            var name = item.GetStringProperty("name")
                ?? throw new InvalidDataException("A library entry has no name.");
            var downloads = item.GetObjectProperty("downloads");

            var classifiers = new Dictionary<string, LibraryDownload>();
            if (downloads?.GetObjectProperty("classifiers") is { } classifierObject)
            {
                foreach (var classifier in classifierObject.EnumerateObject())
                {
                    if (classifier.Value.ValueKind == JsonValueKind.Object)
                        classifiers[classifier.Name] = ParseLibraryDownload(classifier.Value);
                }
            }

            libraries.Add(new VersionLibrary(
                Name: name,
                RepositoryUrl: item.GetStringProperty("url"),
                Artifact: downloads?.GetObjectProperty("artifact") is { } artifact ? ParseLibraryDownload(artifact) : null,
                Rules: ParseRules(item.GetArrayProperty("rules")),
                Natives: ParseNatives(item.GetObjectProperty("natives")),
                Classifiers: classifiers,
                ExtractExclude: ParseExtractExclude(item.GetObjectProperty("extract")?.GetArrayProperty("exclude"))));
        }
        return libraries;
    }

    private static LibraryDownload ParseLibraryDownload(JsonElement element)
    {
        return new LibraryDownload(
            Path: element.GetStringProperty("path"),
            Url: element.GetStringProperty("url"),
            Sha1: element.GetStringProperty("sha1"),
            Size: element.GetInt64Property("size"));
    }

    private static Dictionary<string, string> ParseNatives(JsonElement? natives)
    {
        var result = new Dictionary<string, string>();
        if (natives is null)
            return result;

        foreach (var native in natives.Value.EnumerateObject())
        {
            if (native.Value.ValueKind == JsonValueKind.String)
                result[native.Name] = native.Value.GetString()!;
        }
        return result;
    }

    private static List<string> ParseExtractExclude(JsonElement? array)
    {
        var result = new List<string>();
        if (array is null)
            return result;

        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                result.Add(item.GetString()!);
        }
        return result;
    }
}
