using System.Text.Json;

namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 容错的 JSON 读取：属性不存在或类型不符时返回 null，而不是抛出异常。
/// </summary>
internal static class JsonElementExtensions
{
    public static JsonElement? GetObjectProperty(this JsonElement element, string name)
    {
        return GetProperty(element, name, JsonValueKind.Object);
    }

    public static JsonElement? GetArrayProperty(this JsonElement element, string name)
    {
        return GetProperty(element, name, JsonValueKind.Array);
    }

    public static string? GetStringProperty(this JsonElement element, string name)
    {
        return GetProperty(element, name, JsonValueKind.String)?.GetString();
    }

    public static int? GetInt32Property(this JsonElement element, string name)
    {
        var value = GetProperty(element, name, JsonValueKind.Number);
        return value is { } number && number.TryGetInt32(out var result) ? result : null;
    }

    private static JsonElement? GetProperty(JsonElement element, string name, JsonValueKind kind)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == kind)
        {
            return value;
        }
        return null;
    }
}
