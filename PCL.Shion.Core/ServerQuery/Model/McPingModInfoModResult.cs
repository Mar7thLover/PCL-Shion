using System.Text.Json.Serialization;

namespace PCL.Shion.Core.ServerQuery.Model;

/// <summary>
/// 模组信息中的一项。
/// </summary>
public record McPingModInfoModResult(
    [property: JsonPropertyName("modid")] string Id,
    [property: JsonPropertyName("version")] string Version);
