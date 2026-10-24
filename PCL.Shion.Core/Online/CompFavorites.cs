using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCL.Shion.Core.Online;

/// <summary>
/// 资源收藏夹。一个收藏夹包含若干工程 ID 与备注。
/// </summary>
public sealed class CompFavoriteFolder
{
    /// <summary>收藏夹名称。</summary>
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    /// <summary>收藏夹 ID。</summary>
    [JsonPropertyName("Id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>收藏的工程 ID 列表。</summary>
    [JsonPropertyName("Favs")]
    public HashSet<string> Favs { get; set; } = new();

    /// <summary>备注，键为工程 ID。</summary>
    [JsonPropertyName("Notes")]
    public Dictionary<string, string> Notes { get; set; } = new();
}

/// <summary>
/// 资源收藏夹的内存模型与序列化。持久化位置由调用方决定。
/// </summary>
public static class CompFavorites
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <summary>
    /// 从 JSON 解析收藏夹列表。兼容旧格式（仅工程 ID 集合），旧数据迁移到名为 <paramref name="defaultName"/> 的收藏夹。
    /// 结果至少包含一个收藏夹。
    /// </summary>
    public static List<CompFavoriteFolder> Parse(string? raw, string defaultName)
    {
        List<CompFavoriteFolder>? list = null;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                list = JsonSerializer.Deserialize<List<CompFavoriteFolder>>(raw, Options);
            }
            catch (JsonException)
            {
                try
                {
                    var legacy = JsonSerializer.Deserialize<HashSet<string>>(raw, Options);
                    if (legacy is not null)
                        list = [new CompFavoriteFolder { Name = defaultName, Favs = legacy }];
                }
                catch (JsonException)
                {
                }
            }
        }

        if (list is null || list.Count == 0)
            list = [new CompFavoriteFolder { Name = defaultName }];
        return list;
    }

    /// <summary>
    /// 序列化收藏夹列表。备注中的空白值会被移除。
    /// </summary>
    public static string Serialize(IEnumerable<CompFavoriteFolder> folders)
    {
        var list = folders.ToList();
        foreach (var folder in list)
            folder.Notes = folder.Notes
                .Where(n => !string.IsNullOrWhiteSpace(n.Value))
                .ToDictionary(n => n.Key, n => n.Value);
        return JsonSerializer.Serialize(list, Options);
    }

    /// <summary>
    /// 工程是否在任一收藏夹中。
    /// </summary>
    public static bool IsFavourite(IEnumerable<CompFavoriteFolder> folders, string id) =>
        folders.Any(f => f.Favs.Contains(id));
}
