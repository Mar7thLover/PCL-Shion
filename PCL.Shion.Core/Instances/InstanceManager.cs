using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Instances;

/// <summary>
/// 实例的目录定位、重命名与删除。
/// </summary>
public static class InstanceManager
{
    private static readonly char[] ReservedNameChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    private static readonly JsonDocumentOptions VersionJsonReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions VersionJsonWriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 实例名能否用作目录名：非空、首尾无空白、不以点结尾，且不含各系统文件名里的保留字符与控制字符。
    /// 规则不随系统变化，这样实例目录可以在不同系统间搬运。
    /// </summary>
    public static bool IsValidName(string name)
    {
        return name.Length > 0
            && name == name.Trim()
            && !name.EndsWith('.')
            && name.IndexOfAny(ReservedNameChars) < 0
            && !name.Any(char.IsControl);
    }

    /// <summary>
    /// 游戏目录：实例隔离时是实例目录，否则是 .minecraft 本身。<see cref="InstanceSettings.Isolated"/> 优先于实例自身的隔离状态。
    /// </summary>
    public static string GetGameDirectory(string minecraftRoot, GameInstance instance, InstanceSettings settings)
    {
        return (settings.Isolated ?? instance.IsIsolated) ? instance.Directory : minecraftRoot;
    }

    /// <summary>
    /// 重命名实例：实例目录、版本 JSON、客户端 jar 与 natives 目录都改用新名称，版本 JSON 的 id 同步更新，
    /// 其他实例中指向旧名称的 inheritsFrom 也改为新名称。
    /// </summary>
    /// <exception cref="ArgumentException">名称不能用作实例名。</exception>
    /// <exception cref="InvalidDataException">原实例的版本 JSON 无效。</exception>
    /// <exception cref="IOException">原实例不存在，新名称已被占用，或文件被占用。</exception>
    public static void Rename(string minecraftRoot, string oldName, string newName)
    {
        var versionsDirectory = Path.Combine(minecraftRoot, "versions");
        var oldDirectory = GetInstanceDirectory(versionsDirectory, oldName);
        var newDirectory = GetInstanceDirectory(versionsDirectory, newName);
        if (Directory.Exists(newDirectory))
            throw new IOException($"Instance '{newName}' already exists.");

        // 先读取版本 JSON，无效时在改动任何文件之前失败
        var versionJson = ReadVersionJson(Path.Combine(oldDirectory, $"{oldName}.json"));

        Directory.Move(oldDirectory, newDirectory);
        MoveEntry(newDirectory, $"{oldName}.json", $"{newName}.json");
        MoveEntry(newDirectory, $"{oldName}.jar", $"{newName}.jar");
        MoveEntry(newDirectory, $"{oldName}-natives", $"{newName}-natives");

        versionJson["id"] = newName;
        WriteVersionJson(Path.Combine(newDirectory, $"{newName}.json"), versionJson);
        RepointChildren(versionsDirectory, oldName, newName);
    }

    /// <summary>
    /// 把整个实例目录移到回收站。
    /// </summary>
    /// <exception cref="ArgumentException">名称不能用作实例名。</exception>
    public static void Delete(string minecraftRoot, string instanceName)
    {
        Delete(minecraftRoot, instanceName, Trash.MoveToTrash);
    }

    internal static void Delete(string minecraftRoot, string instanceName, Action<string> moveToTrash)
    {
        moveToTrash(GetInstanceDirectory(Path.Combine(minecraftRoot, "versions"), instanceName));
    }

    /// <summary>
    /// 实例目录的完整路径。名称必须合法，否则 ".." 之类的名称会指到 versions 之外。
    /// </summary>
    private static string GetInstanceDirectory(string versionsDirectory, string name)
    {
        if (!IsValidName(name))
            throw new ArgumentException($"'{name}' is not a valid instance name.", nameof(name));
        return Path.Combine(versionsDirectory, name);
    }

    private static void MoveEntry(string directory, string oldName, string newName)
    {
        var source = Path.Combine(directory, oldName);
        var target = Path.Combine(directory, newName);
        if (File.Exists(source))
            File.Move(source, target);
        else if (Directory.Exists(source))
            Directory.Move(source, target);
    }

    /// <summary>
    /// 把继承自旧名称的实例改为继承新名称，否则重命名会让它们无法解析。内容无效的版本 JSON 无从判断继承关系，直接跳过。
    /// </summary>
    private static void RepointChildren(string versionsDirectory, string oldName, string newName)
    {
        foreach (var directory in Directory.EnumerateDirectories(versionsDirectory))
        {
            var path = Path.Combine(directory, $"{Path.GetFileName(directory)}.json");
            if (!File.Exists(path))
                continue;

            JsonObject versionJson;
            try
            {
                versionJson = ReadVersionJson(path);
            }
            catch (InvalidDataException)
            {
                continue;
            }

            if (versionJson["inheritsFrom"] is JsonValue parent && parent.TryGetValue<string>(out var parentName) && parentName == oldName)
            {
                versionJson["inheritsFrom"] = newName;
                WriteVersionJson(path, versionJson);
            }
        }
    }

    private static JsonObject ReadVersionJson(string path)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(path), documentOptions: VersionJsonReadOptions) as JsonObject
                ?? throw new InvalidDataException($"Invalid version JSON: {path}");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid version JSON: {path}", ex);
        }
    }

    private static void WriteVersionJson(string path, JsonObject versionJson)
    {
        File.WriteAllText(path, versionJson.ToJsonString(VersionJsonWriteOptions));
    }
}
