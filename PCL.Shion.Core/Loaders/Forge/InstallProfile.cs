using System.Text.Json;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Loaders.Forge;

/// <summary>
/// 新版安装器（Forge 1.12.2 末期起，以及 NeoForge）的 install_profile.json，只保留安装客户端需要的部分。
/// </summary>
/// <param name="Minecraft">原版版本号。</param>
/// <param name="VersionJsonEntry">安装器中版本 JSON 的条目名。</param>
/// <param name="Libraries">安装过程（主要是处理器）需要的库。</param>
/// <param name="Data">data 中每个变量在客户端一侧的原始取值。</param>
/// <param name="Processors">全部处理器，按运行顺序排列，包括只在服务端运行的。</param>
internal sealed record InstallProfile(
    string Minecraft,
    string VersionJsonEntry,
    IReadOnlyList<VersionLibrary> Libraries,
    IReadOnlyDictionary<string, string> Data,
    IReadOnlyList<InstallProcessor> Processors)
{
    /// <exception cref="JsonException">内容不是合法的 JSON。</exception>
    /// <exception cref="InvalidDataException">缺少必需的字段。</exception>
    public static InstallProfile Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var data = new Dictionary<string, string>();
        if (root.GetObjectProperty("data") is { } values)
        {
            foreach (var entry in values.EnumerateObject())
            {
                if (entry.Value.GetStringProperty("client") is { } value)
                    data[entry.Name] = value;
            }
        }

        var processors = new List<InstallProcessor>();
        if (root.GetArrayProperty("processors") is { } items)
        {
            foreach (var item in items.EnumerateArray())
                processors.Add(ParseProcessor(item));
        }

        return new InstallProfile(
            root.GetStringProperty("minecraft")
                ?? throw new InvalidDataException("The install profile does not name its Minecraft version."),
            root.GetStringProperty("json")
                ?? throw new InvalidDataException("The install profile does not point to a version JSON."),
            // libraries 的格式与版本 JSON 相同
            VersionFile.Parse(json).Libraries,
            data,
            processors);
    }

    private static InstallProcessor ParseProcessor(JsonElement item)
    {
        var outputs = new Dictionary<string, string>();
        if (item.GetObjectProperty("outputs") is { } outputObject)
        {
            foreach (var output in outputObject.EnumerateObject())
            {
                if (output.Value.ValueKind == JsonValueKind.String)
                    outputs[output.Name] = output.Value.GetString()!;
            }
        }

        return new InstallProcessor(
            item.GetStringProperty("jar") ?? throw new InvalidDataException("An install processor has no jar."),
            ReadStrings(item.GetArrayProperty("classpath")),
            ReadStrings(item.GetArrayProperty("args")),
            outputs,
            item.GetArrayProperty("sides") is { } sides ? ReadStrings(sides) : null);
    }

    private static List<string> ReadStrings(JsonElement? array)
    {
        if (array is null)
            return [];

        return array.Value.EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String)
            .Select(element => element.GetString()!)
            .ToList();
    }
}
