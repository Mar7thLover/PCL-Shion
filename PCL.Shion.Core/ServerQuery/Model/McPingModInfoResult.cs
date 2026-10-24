using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Shion.Core.ServerQuery.Model;

/// <summary>
/// 服务器通过 Mod 列表扩展返回的模组信息。
/// </summary>
public record McPingModInfoResult(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("modList")] List<McPingModInfoModResult> ModList);
