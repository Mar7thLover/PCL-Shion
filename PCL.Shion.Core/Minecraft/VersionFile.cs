using System.Text.Json;

namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 单个版本 JSON 文件的内容，尚未处理 inheritsFrom。
/// </summary>
/// <param name="InheritsFrom">inheritsFrom，即父版本名称。</param>
/// <param name="MainClass">mainClass。</param>
/// <param name="Type">type。</param>
/// <param name="AssetIndexId">assetIndex.id，缺失时取 assets。</param>
/// <param name="JavaMajorVersion">javaVersion.majorVersion。</param>
/// <param name="ClientVersion">clientVersion（部分合并版 JSON 带有）。</param>
/// <param name="MinecraftArguments">旧格式的 minecraftArguments。</param>
/// <param name="JvmArguments">arguments.jvm。</param>
/// <param name="GameArguments">arguments.game。</param>
/// <param name="Libraries">libraries。</param>
/// <param name="DeclaresClientJar">是否声明了 downloads.client，即本版本自带客户端 jar。</param>
/// <param name="LoggingArgument">logging.client.argument。</param>
/// <param name="LoggingFileId">logging.client.file.id。</param>
internal sealed record VersionFile(
    string? InheritsFrom,
    string? MainClass,
    string? Type,
    string? AssetIndexId,
    int? JavaMajorVersion,
    string? ClientVersion,
    string? MinecraftArguments,
    IReadOnlyList<VersionArgument> JvmArguments,
    IReadOnlyList<VersionArgument> GameArguments,
    IReadOnlyList<VersionLibrary> Libraries,
    bool DeclaresClientJar,
    string? LoggingArgument,
    string? LoggingFileId)
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
        var logging = root.GetObjectProperty("logging")?.GetObjectProperty("client");

        return new VersionFile(
            InheritsFrom: root.GetStringProperty("inheritsFrom"),
            MainClass: root.GetStringProperty("mainClass"),
            Type: root.GetStringProperty("type"),
            AssetIndexId: root.GetObjectProperty("assetIndex")?.GetStringProperty("id") ?? root.GetStringProperty("assets"),
            JavaMajorVersion: root.GetObjectProperty("javaVersion")?.GetInt32Property("majorVersion"),
            ClientVersion: root.GetStringProperty("clientVersion"),
            MinecraftArguments: root.GetStringProperty("minecraftArguments"),
            JvmArguments: ParseArguments(arguments?.GetArrayProperty("jvm")),
            GameArguments: ParseArguments(arguments?.GetArrayProperty("game")),
            Libraries: ParseLibraries(root.GetArrayProperty("libraries")),
            DeclaresClientJar: root.GetObjectProperty("downloads")?.GetObjectProperty("client") is not null,
            LoggingArgument: logging?.GetStringProperty("argument"),
            LoggingFileId: logging?.GetObjectProperty("file")?.GetStringProperty("id"));
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

            var classifierPaths = new Dictionary<string, string>();
            if (downloads?.GetObjectProperty("classifiers") is { } classifiers)
            {
                foreach (var classifier in classifiers.EnumerateObject())
                {
                    if (classifier.Value.GetStringProperty("path") is { } path)
                        classifierPaths[classifier.Name] = path;
                }
            }

            libraries.Add(new VersionLibrary(
                Name: name,
                ArtifactPath: downloads?.GetObjectProperty("artifact")?.GetStringProperty("path"),
                Rules: ParseRules(item.GetArrayProperty("rules")),
                Natives: ParseNatives(item.GetObjectProperty("natives")),
                ClassifierPaths: classifierPaths,
                ExtractExclude: ParseExtractExclude(item.GetObjectProperty("extract")?.GetArrayProperty("exclude"))));
        }
        return libraries;
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
