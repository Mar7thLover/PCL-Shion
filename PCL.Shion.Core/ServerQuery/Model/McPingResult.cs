using System.Text.Json.Serialization;

namespace PCL.Shion.Core.ServerQuery.Model;

/// <summary>
/// 服务器状态查询的结果。
/// </summary>
public record McPingResult(
    [property: JsonPropertyName("version")] McPingVersionResult Version,
    [property: JsonPropertyName("players")] McPingPlayerResult Players,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("favicon")] string? Favicon,
    [property: JsonPropertyName("latency")] long Latency,
    [property: JsonPropertyName("modinfo")] McPingModInfoResult? ModInfo,
    [property: JsonPropertyName("preventsChatReports")] bool? PreventsChatReports);
