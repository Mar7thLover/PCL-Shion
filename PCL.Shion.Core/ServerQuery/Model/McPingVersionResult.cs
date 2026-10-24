using System.Text.Json.Serialization;

namespace PCL.Shion.Core.ServerQuery.Model;

/// <summary>
/// 服务器报告的版本信息。
/// </summary>
public record McPingVersionResult(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("protocol")] int Protocol);
