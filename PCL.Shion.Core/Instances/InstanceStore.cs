using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Shion.Core.IO;

namespace PCL.Shion.Core.Instances;

/// <summary>
/// 读写实例设置，保存在 versions/&lt;名称&gt;/PCL Shion/instance.json。
/// </summary>
public static class InstanceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// 读取实例设置，文件不存在时返回默认值。
    /// </summary>
    /// <exception cref="JsonException">文件内容不是有效的设置 JSON。</exception>
    public static InstanceSettings Load(string minecraftRoot, string instanceName)
    {
        var path = GetPath(minecraftRoot, instanceName);
        if (!File.Exists(path))
            return new InstanceSettings();

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var settings = JsonSerializer.Deserialize<InstanceSettings>(reader.ReadToEnd(), JsonOptions) ?? new InstanceSettings();
        settings.EnvironmentVariables ??= new();
        return settings;
    }

    /// <summary>
    /// 保存实例设置，覆盖已有的文件。
    /// </summary>
    public static void Save(string minecraftRoot, string instanceName, InstanceSettings settings)
    {
        var path = GetPath(minecraftRoot, instanceName);
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions), ownerOnly: true);
    }

    internal static string GetPath(string minecraftRoot, string instanceName)
    {
        var directory = Minecraft.ManifestPath.ResolveName(Path.Combine(minecraftRoot, "versions"), instanceName);
        return Path.Combine(directory, "PCL Shion", "instance.json");
    }
}
