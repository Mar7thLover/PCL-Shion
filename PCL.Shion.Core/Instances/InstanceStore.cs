using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        return JsonSerializer.Deserialize<InstanceSettings>(File.ReadAllText(path), JsonOptions) ?? new InstanceSettings();
    }

    /// <summary>
    /// 保存实例设置，覆盖已有的文件。
    /// </summary>
    public static void Save(string minecraftRoot, string instanceName, InstanceSettings settings)
    {
        var path = GetPath(minecraftRoot, instanceName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // 先写临时文件再替换，写入被打断时不会留下残缺的设置
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }

    internal static string GetPath(string minecraftRoot, string instanceName)
    {
        return Path.Combine(minecraftRoot, "versions", instanceName, "PCL Shion", "instance.json");
    }
}
