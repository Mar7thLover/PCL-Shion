using System.Text.Json;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Mods;

/// <summary>
/// 把各加载器的元数据文件内容解析成 <see cref="ModMetadata"/>。
/// 内容不合法时，JSON 文件抛出 <see cref="JsonException"/>，TOML 文件抛出 <see cref="FormatException"/>。
/// </summary>
internal static class ModMetadataParser
{
    // 这类文件常带注释和多余的逗号，加载器自己的解析器也都接受
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// 解析 fabric.mod.json：depends 为必需依赖，recommends 为可选依赖。
    /// </summary>
    public static ModMetadata ParseFabric(string json)
    {
        using var document = JsonDocument.Parse(json, JsonOptions);
        var root = document.RootElement;

        var dependencies = new List<ModDependency>();
        AddFabricDependencies(root, "depends", true, dependencies);
        AddFabricDependencies(root, "recommends", false, dependencies);
        return new ModMetadata(
            root.GetStringProperty("id"),
            root.GetStringProperty("name"),
            root.GetStringProperty("version"),
            ["fabric"],
            dependencies,
            ReadProvides(root));
    }

    /// <summary>
    /// 解析 quilt.mod.json。
    /// </summary>
    public static ModMetadata ParseQuilt(string json)
    {
        using var document = JsonDocument.Parse(json, JsonOptions);
        if (document.RootElement.GetObjectProperty("quilt_loader") is not { } loader)
            return ModMetadata.Empty with { Loaders = ["quilt"] };

        var dependencies = new List<ModDependency>();
        if (loader.GetArrayProperty("depends") is { } depends)
        {
            foreach (var item in depends.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    dependencies.Add(new ModDependency(item.GetString()!, null, true));
                }
                else if (item.GetStringProperty("id") is { } id)
                {
                    var range = item.TryGetProperty("versions", out var versions) ? ReadVersionRange(versions) : null;
                    var optional = item.TryGetProperty("optional", out var flag) && flag.ValueKind == JsonValueKind.True;
                    dependencies.Add(new ModDependency(id, range, !optional));
                }
            }
        }

        return new ModMetadata(
            loader.GetStringProperty("id"),
            loader.GetObjectProperty("metadata")?.GetStringProperty("name"),
            loader.GetStringProperty("version"),
            ["quilt"],
            dependencies,
            ReadProvides(loader));
    }

    /// <summary>
    /// 解析 Forge 的 mods.toml 或 NeoForge 的 neoforge.mods.toml。
    /// 第一个 [[mods]] 是主 Mod，其余的计入提供的 ID；依赖取自各 Mod 的 [[dependencies.&lt;modId&gt;]]。
    /// </summary>
    public static ModMetadata ParseForge(string toml, bool isNeoForgeFile)
    {
        var document = TomlReader.Parse(toml);
        var mods = document.GetTables("mods");
        var modIds = mods.Select(mod => mod.GetString("modId")).OfType<string>().ToList();

        var dependencies = new List<ModDependency>();
        if (document.GetTable("dependencies") is { } owners)
        {
            foreach (var table in modIds.SelectMany(modId => owners.GetTables(modId)))
                AddForgeDependency(table, dependencies);
        }

        // NeoForge 1.20.2 至 1.20.4 的 Mod 仍用 mods.toml，以只依赖 neoforge 区分
        var neoForge = isNeoForgeFile
            || (dependencies.Any(dependency => dependency.Id == "neoforge") && dependencies.All(dependency => dependency.Id != "forge"));
        var main = mods.Count > 0 ? mods[0] : null;
        return new ModMetadata(
            main?.GetString("modId"),
            main?.GetString("displayName"),
            main?.GetString("version"),
            [neoForge ? "neoforge" : "forge"],
            dependencies,
            modIds.Skip(1).ToList());
    }

    /// <summary>
    /// 解析旧版 Forge 的 mcmod.info，内容是数组，或带 modList 的对象；requiredMods 为必需依赖。
    /// </summary>
    public static ModMetadata ParseLegacy(string json)
    {
        using var document = JsonDocument.Parse(json, JsonOptions);
        var root = document.RootElement;
        var list = root.ValueKind == JsonValueKind.Array ? root : root.GetArrayProperty("modList");
        var entries = list?.EnumerateArray().Where(entry => entry.ValueKind == JsonValueKind.Object).ToList() ?? [];
        var main = entries.FirstOrDefault();

        var dependencies = new List<ModDependency>();
        if (main.GetArrayProperty("requiredMods") is { } requiredMods)
        {
            foreach (var item in requiredMods.EnumerateArray().Where(mod => mod.ValueKind == JsonValueKind.String))
            {
                var parts = item.GetString()!.Split('@', 2);
                dependencies.Add(new ModDependency(parts[0], parts.Length > 1 ? parts[1] : null, true));
            }
        }

        return new ModMetadata(
            main.GetStringProperty("modid"),
            main.GetStringProperty("name"),
            main.GetStringProperty("version"),
            ["forge"],
            dependencies,
            entries.Skip(1).Select(entry => entry.GetStringProperty("modid")).OfType<string>().ToList());
    }

    private static void AddFabricDependencies(JsonElement root, string key, bool required, List<ModDependency> target)
    {
        if (root.GetObjectProperty(key) is not { } table)
            return;

        foreach (var property in table.EnumerateObject())
            target.Add(new ModDependency(property.Name, ReadVersionRange(property.Value), required));
    }

    private static void AddForgeDependency(TomlTable table, List<ModDependency> target)
    {
        var id = table.GetString("modId");
        var type = table.GetString("type")?.ToLowerInvariant();
        var onlyServer = string.Equals(table.GetString("side"), "SERVER", StringComparison.OrdinalIgnoreCase);
        // 仅服务端的依赖不影响客户端；冲突与不推荐声明不是依赖
        if (id is null || onlyServer || type is "incompatible" or "discouraged")
            return;

        // 新格式用 type，旧格式用 mandatory，两者都没有时按必需处理
        var required = type is null ? table.GetBool("mandatory") ?? true : type == "required";
        var range = table.GetString("versionRange");
        target.Add(new ModDependency(id, string.IsNullOrEmpty(range) ? null : range, required));
    }

    // provides 里的项在 Fabric 中是字符串，在 Quilt 中还可以是带 id 的对象
    private static List<string> ReadProvides(JsonElement owner)
    {
        var ids = new List<string>();
        if (owner.GetArrayProperty("provides") is not { } provides)
            return ids;

        foreach (var item in provides.EnumerateArray())
        {
            var id = item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetStringProperty("id");
            if (id is not null)
                ids.Add(id);
        }
        return ids;
    }

    // 版本范围可以是字符串、字符串数组（任一满足），Quilt 还可以是 { "all": [...] } 或 { "any": [...] }
    private static string? ReadVersionRange(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return value.GetString();
            case JsonValueKind.Array:
                return JoinVersionRanges(value, " || ");
            case JsonValueKind.Object:
                if (value.GetArrayProperty("all") is { } all)
                    return JoinVersionRanges(all, " ");
                return value.GetArrayProperty("any") is { } any ? JoinVersionRanges(any, " || ") : null;
            default:
                return null;
        }
    }

    private static string? JoinVersionRanges(JsonElement array, string separator)
    {
        var ranges = array.EnumerateArray().Select(range => ReadVersionRange(range)).OfType<string>().ToList();
        return ranges.Count == 0 ? null : string.Join(separator, ranges);
    }
}
