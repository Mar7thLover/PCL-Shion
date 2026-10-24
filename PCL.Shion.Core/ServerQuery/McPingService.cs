using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PCL.Shion.Core.ServerQuery.Model;

namespace PCL.Shion.Core.ServerQuery;

/// <summary>
/// 现代 Minecraft 协议（1.7 及以上）的服务器信息查询。
/// </summary>
public class McPingService : IMcPingService
{
    private const int DefaultTimeout = 10000;
    private const int ProtocolVersion = 772;

    private readonly IPEndPoint _endpoint;
    private readonly string _host;
    private readonly int _timeout;
    private bool _disposed;

    public McPingService(IPEndPoint endpoint, int timeout = DefaultTimeout)
    {
        _endpoint = endpoint;
        _host = endpoint.Address.ToString();
        _timeout = timeout;
    }

    public McPingService(string ip, int port = 25565, int timeout = DefaultTimeout)
    {
        _endpoint = IPAddress.TryParse(ip, out var address)
            ? new IPEndPoint(address, port)
            : new IPEndPoint(Dns.GetHostAddresses(ip).First(), port);
        _host = ip;
        _timeout = timeout;
    }

    public McPingService(string host, IPEndPoint endpoint, int timeout = DefaultTimeout)
    {
        _endpoint = endpoint;
        _host = host;
        _timeout = timeout;
    }

    public IPEndPoint Endpoint => _endpoint;

    public string Host => _host;

    public int Timeout => _timeout;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private static readonly Dictionary<string, string> ColorCodes = new()
    {
        ["black"] = "0",
        ["dark_blue"] = "1",
        ["dark_green"] = "2",
        ["dark_aqua"] = "3",
        ["dark_red"] = "4",
        ["dark_purple"] = "5",
        ["gold"] = "6",
        ["gray"] = "7",
        ["dark_gray"] = "8",
        ["blue"] = "9",
        ["green"] = "a",
        ["aqua"] = "b",
        ["red"] = "c",
        ["light_purple"] = "d",
        ["yellow"] = "e",
        ["white"] = "f"
    };

    public async Task<McPingResult?> PingAsync(CancellationToken cancellationToken = default)
    {
        // 握手包必须在连接前构建，地址过长时直接报告错误
        var handshakePacket = BuildHandshakePacket(_host, _endpoint.Port);

        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        using var timeoutSource = new CancellationTokenSource(_timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        var token = linkedSource.Token;

        byte[] statusPayload;
        long latency;
        try
        {
            await socket.ConnectAsync(_endpoint, token);
            await using var stream = new NetworkStream(socket, false);

            await stream.WriteAsync(handshakePacket, token);
            await stream.WriteAsync(BuildStatusRequestPacket(), token);
            var sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await stream.WriteAsync(BuildPingPacket(sentAt), token);

            (statusPayload, latency) = await ReadStatusAsync(stream, token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 超时视为无法取得服务器信息
            return null;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // 连接、读写失败同样视为无法取得服务器信息
            return null;
        }

        if (statusPayload.Length == 0)
            throw new ServerQueryException(ServerQueryError.EmptyPacket, "The server returned an empty status response.");

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(Encoding.UTF8.GetString(statusPayload));
        }
        catch (JsonException ex)
        {
            throw new ServerQueryException(ServerQueryError.InvalidResponse,
                "The server returned an invalid status response.", ex);
        }

        if (node is not JsonObject root)
            throw new ServerQueryException(ServerQueryError.InvalidResponse,
                "The server returned an invalid status response.");

        // 先把聊天组件形式的描述转换成带 § 格式代码的字符串
        if (TryGetProperty(root, "description", out var descriptionKey, out var descriptionNode)
            && descriptionNode is JsonObject descriptionObject)
            root[descriptionKey] = ConvertDescription(descriptionObject);

        McPingResult? response;
        try
        {
            response = JsonSerializer.Deserialize<McPingResult>(root, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new ServerQueryException(ServerQueryError.InvalidResponse,
                "The server returned an invalid status response.", ex);
        }

        if (response?.Version is null)
            throw new ServerQueryException(ServerQueryError.InvalidResponse,
                "The server returned an invalid status response.");

        return response with { Latency = latency };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>构建握手包。</summary>
    private static byte[] BuildHandshakePacket(string host, int port)
    {
        var hostBytes = Encoding.UTF8.GetBytes(host);
        if (hostBytes.Length > 255)
            throw new ServerQueryException(ServerQueryError.AddressTooLong,
                "The server address is longer than 255 bytes.");

        using var content = new MemoryStream();
        content.Write(VarIntHelper.Encode(0)); // 包 ID：握手
        content.Write(VarIntHelper.Encode(ProtocolVersion)); // 协议版本
        content.Write(VarIntHelper.Encode(hostBytes.Length)); // 服务器地址长度
        content.Write(hostBytes); // 服务器地址
        Span<byte> portBytes = stackalloc byte[2];
        VarIntHelper.WriteUInt16BigEndian(portBytes, (ushort)port); // 服务器端口
        content.Write(portBytes);
        content.Write(VarIntHelper.Encode(1)); // 下一状态：查询
        return WrapPacket(content.ToArray());
    }

    private static byte[] BuildStatusRequestPacket()
    {
        using var content = new MemoryStream();
        content.Write(VarIntHelper.Encode(0));
        return WrapPacket(content.ToArray());
    }

    private static byte[] BuildPingPacket(long timestamp)
    {
        using var content = new MemoryStream();
        content.Write(VarIntHelper.Encode(1));
        Span<byte> timestampBytes = stackalloc byte[8];
        VarIntHelper.WriteInt64BigEndian(timestampBytes, timestamp);
        content.Write(timestampBytes);
        return WrapPacket(content.ToArray());
    }

    private static byte[] WrapPacket(byte[] content)
    {
        using var packet = new MemoryStream();
        packet.Write(VarIntHelper.Encode(content.Length));
        packet.Write(content);
        return packet.ToArray();
    }

    /// <summary>读取状态响应和 pong 数据包，返回描述 JSON 与延迟。</summary>
    private static async Task<(byte[] StatusPayload, long Latency)> ReadStatusAsync(Stream stream,
        CancellationToken cancellationToken)
    {
        byte[]? statusPayload = null;
        long? latency = null;

        while (statusPayload is null || latency is null)
        {
            var packetLength = await VarIntHelper.ReadAsync(stream, cancellationToken);
            if (packetLength <= 0) throw new InvalidDataException("The server returned an empty packet.");

            var packetData = new byte[packetLength];
            await stream.ReadExactlyAsync(packetData, cancellationToken);

            using var packet = new MemoryStream(packetData, false);
            var packetId = await VarIntHelper.ReadAsync(packet, cancellationToken);
            switch (packetId)
            {
                case 0:
                {
                    var jsonLength = await VarIntHelper.ReadAsync(packet, cancellationToken);
                    var json = new byte[jsonLength];
                    await packet.ReadExactlyAsync(json, cancellationToken);
                    statusPayload = json;
                    break;
                }
                case 1:
                {
                    var pong = new byte[8];
                    await packet.ReadExactlyAsync(pong, cancellationToken);
                    latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                              - VarIntHelper.ReadInt64BigEndian(pong);
                    break;
                }
                default:
                    // 忽略不认识的包
                    break;
            }
        }

        return (statusPayload, latency.Value);
    }

    /// <summary>把聊天组件形式的服务器描述转换成带 § 格式代码的字符串。</summary>
    private static string ConvertDescription(JsonNode? node)
    {
        var builder = new StringBuilder();
        AppendDescription(node, builder);
        return builder.ToString();
    }

    private static void AppendDescription(JsonNode? node, StringBuilder builder)
    {
        switch (node)
        {
            case null:
                return;

            case JsonObject obj:
                var textNode = GetProperty(obj, "text");
                if (textNode is not null)
                {
                    builder.Append(GetTextStyleString(
                        GetString(GetProperty(obj, "color")) ?? string.Empty,
                        GetBoolean(GetProperty(obj, "bold")),
                        GetBoolean(GetProperty(obj, "obfuscated")),
                        GetBoolean(GetProperty(obj, "strikethrough")),
                        GetBoolean(GetProperty(obj, "underline")),
                        GetBoolean(GetProperty(obj, "italic"))));
                    builder.Append(GetString(textNode) ?? string.Empty);
                }

                if (GetProperty(obj, "extra") is JsonArray extra)
                    foreach (var child in extra)
                        AppendDescription(child, builder);
                break;

            case JsonArray array:
                foreach (var child in array)
                    AppendDescription(child, builder);
                break;

            case JsonValue value:
                if (value.TryGetValue<string>(out var text))
                    builder.Append(text);
                else
                    builder.Append(value.ToJsonString());
                break;
        }
    }

    private static string GetTextStyleString(string color, bool bold, bool obfuscated, bool strikethrough,
        bool underline, bool italic)
    {
        var builder = new StringBuilder();
        if (ColorCodes.TryGetValue(color, out var colorCode)) builder.Append('§').Append(colorCode);
        if (bold) builder.Append("§l");
        if (italic) builder.Append("§o");
        if (underline) builder.Append("§n");
        if (strikethrough) builder.Append("§m");
        if (color.StartsWith('#')) builder.Append(color);
        return builder.ToString();
    }

    private static bool TryGetProperty(JsonObject obj, string name, out string key, out JsonNode? value)
    {
        if (obj.TryGetPropertyValue(name, out value))
        {
            key = name;
            return true;
        }

        foreach (var pair in obj)
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                key = pair.Key;
                value = pair.Value;
                return true;
            }

        key = name;
        value = null;
        return false;
    }

    private static JsonNode? GetProperty(JsonObject obj, string name) =>
        TryGetProperty(obj, name, out _, out var value) ? value : null;

    private static string? GetString(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<string>(out var text)) return text;
        return node.ToJsonString();
    }

    private static bool GetBoolean(JsonNode? node)
    {
        if (node is not JsonValue value) return false;
        if (value.TryGetValue<bool>(out var boolean)) return boolean;
        return value.TryGetValue<string>(out var text) && bool.TryParse(text, out boolean) && boolean;
    }
}
