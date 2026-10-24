using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Shion.Core.ServerQuery;

/// <summary>
/// 通过系统 DNS 服务器查询 SRV 记录（RFC 1035 报文格式，RFC 2782 排序）。
/// </summary>
internal static class DnsSrvResolver
{
    private const ushort TypeSrv = 33;
    private const ushort ClassIn = 1;
    private const int DnsPort = 53;
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(3);

    /// <summary>查询 SRV 记录，返回按优先级与权重排序后的 (目标主机, 端口) 列表；查询失败时返回空列表。</summary>
    public static async Task<List<(string Target, int Port)>> QueryAsync(string name, CancellationToken cancelToken)
    {
        var id = (ushort)Random.Shared.Next(1, ushort.MaxValue);
        var query = BuildQuery(id, name);
        foreach (var server in GetDnsServers())
        {
            var response = await SendAsync(server, query, cancelToken);
            if (response is null) continue;
            try
            {
                return ParseResponse(response, id);
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidDataException)
            {
                return [];
            }
        }

        return [];
    }

    private static IEnumerable<IPAddress> GetDnsServers() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().DnsAddresses)
            .Distinct()
            .OrderBy(a => a.AddressFamily == AddressFamily.InterNetworkV6 ? 1 : 0);

    private static byte[] BuildQuery(ushort id, string name)
    {
        using var stream = new MemoryStream();
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header, id);
        BinaryPrimitives.WriteUInt16BigEndian(header[2..], 0x0100); // 标志：期望递归
        BinaryPrimitives.WriteUInt16BigEndian(header[4..], 1); // 问题数
        stream.Write(header);

        foreach (var label in name.TrimEnd('.').Split('.'))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes);
        }

        stream.WriteByte(0);
        Span<byte> tail = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(tail, TypeSrv);
        BinaryPrimitives.WriteUInt16BigEndian(tail[2..], ClassIn);
        stream.Write(tail);
        return stream.ToArray();
    }

    private static async Task<byte[]?> SendAsync(IPAddress server, byte[] query, CancellationToken cancelToken)
    {
        using var udp = new UdpClient(server.AddressFamily);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        timeout.CancelAfter(QueryTimeout);
        try
        {
            udp.Connect(new IPEndPoint(server, DnsPort));
            await udp.SendAsync(query, timeout.Token);
            var result = await udp.ReceiveAsync(timeout.Token);
            return result.Buffer;
        }
        catch (Exception) when (!cancelToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static List<(string Target, int Port)> ParseResponse(byte[] data, ushort id)
    {
        if (data.Length < 12 || BinaryPrimitives.ReadUInt16BigEndian(data) != id) return [];

        // 响应码不为 0 表示查询失败（如 NXDOMAIN）
        var flags = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2));
        if ((flags & 0x000F) != 0) return [];

        var questionCount = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
        var answerCount = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(6));
        var offset = 12;
        for (var i = 0; i < questionCount; i++)
        {
            ReadName(data, ref offset);
            offset += 4; // 类型与类
        }

        var records = new List<(int Priority, int Weight, string Target, int Port)>();
        for (var i = 0; i < answerCount; i++)
        {
            ReadName(data, ref offset);
            var type = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));
            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 8));
            offset += 10;
            var end = offset + length;

            if (type == TypeSrv)
            {
                var priority = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));
                var weight = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2));
                var port = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 4));
                var targetOffset = offset + 6;
                var target = ReadName(data, ref targetOffset);
                // 目标为根（空名）表示服务不可用
                if (target.Length > 0) records.Add((priority, weight, target, port));
            }

            offset = end;
        }

        return records
            .OrderBy(r => r.Priority)
            .ThenByDescending(r => r.Weight)
            .Select(r => (r.Target, r.Port))
            .ToList();
    }

    /// <summary>读取域名（支持压缩指针），并把 <paramref name="offset"/> 移动到该域名之后。</summary>
    private static string ReadName(byte[] data, ref int offset)
    {
        var labels = new List<string>();
        var position = offset;
        var resume = -1;
        var jumps = 0;
        while (true)
        {
            var length = data[position];
            if ((length & 0xC0) == 0xC0)
            {
                if (++jumps > 32) throw new InvalidDataException("DNS 名称的压缩指针循环。");
                if (resume < 0) resume = position + 2;
                position = ((length & 0x3F) << 8) | data[position + 1];
                continue;
            }

            position++;
            if (length == 0) break;
            labels.Add(Encoding.ASCII.GetString(data, position, length));
            position += length;
        }

        offset = resume >= 0 ? resume : position;
        return string.Join('.', labels);
    }
}
