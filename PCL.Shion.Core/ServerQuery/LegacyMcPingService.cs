using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PCL.Shion.Core.ServerQuery.Model;

namespace PCL.Shion.Core.ServerQuery;

/// <summary>
/// 旧版 Minecraft 协议（1.6 及以下）的服务器信息查询。
/// </summary>
public class LegacyMcPingService : IMcPingService
{
    private const int DefaultTimeout = 10000;

    private readonly IPEndPoint _endpoint;
    private readonly string _host;
    private readonly int _timeout;
    private bool _disposed;

    public LegacyMcPingService(IPEndPoint endpoint, int timeout = DefaultTimeout)
    {
        _endpoint = endpoint;
        _host = endpoint.Address.ToString();
        _timeout = timeout;
    }

    public LegacyMcPingService(string ip, int port = 25565, int timeout = DefaultTimeout)
    {
        _endpoint = IPAddress.TryParse(ip, out var address)
            ? new IPEndPoint(address, port)
            : new IPEndPoint(Dns.GetHostAddresses(ip).First(), port);
        _host = ip;
        _timeout = timeout;
    }

    public IPEndPoint Endpoint => _endpoint;

    public string Host => _host;

    public int Timeout => _timeout;

    public async Task<McPingResult?> PingAsync(CancellationToken cancellationToken = default)
    {
        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        using var timeoutSource = new CancellationTokenSource(_timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        var token = linkedSource.Token;

        byte[] data;
        try
        {
            await socket.ConnectAsync(_endpoint, token);
            await using var stream = new NetworkStream(socket, false);

            // 旧版查询：0xFE 0x01，服务器返回 0xFF 开头的 UTF-16BE 文本
            await stream.WriteAsync(new byte[] { 0xFE, 0x01 }, token);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, token);
            data = buffer.ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (data.Length < 5 || data[0] != 0xFF) return null;

        // 跳过状态字节与 2 字节长度，剩余内容是 UTF-16BE 字符串
        var text = Encoding.BigEndianUnicode.GetString(data, 3, data.Length - 3);
        var parts = text.Split('\0');
        if (parts.Length < 6) return null;

        // §1\0协议版本\0游戏版本\0描述\0在线人数\0人数上限
        if (!int.TryParse(parts[1], out var protocol)
            || !int.TryParse(parts[4], out var online)
            || !int.TryParse(parts[5], out var max))
            return null;

        return new McPingResult(
            new McPingVersionResult(parts[2], protocol),
            new McPingPlayerResult(max, online, []),
            parts[3],
            string.Empty,
            0,
            null,
            null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
