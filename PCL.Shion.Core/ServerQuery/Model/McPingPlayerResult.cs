using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Shion.Core.ServerQuery.Model;

/// <summary>
/// 服务器的玩家数量与在线玩家列表。
/// </summary>
public record McPingPlayerResult(
    [property: JsonPropertyName("max")] int Max,
    [property: JsonPropertyName("online")] int Online,
    [property: JsonPropertyName("sample")] List<McPingPlayerSampleResult>? Samples);
