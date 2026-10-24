using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Shion.Core.ServerQuery;

/// <summary>
/// 解析 Minecraft 服务器地址，得到用于连接的主机名、IP 与端口。
/// </summary>
public static class ServerAddressResolver
{
    /// <summary>解析结果：<see cref="Host"/> 用于握手，<see cref="Ip"/> 用于连接。</summary>
    public readonly record struct ResolvedServerAddress(string Host, string? Ip, int Port);

    private const int DefaultPort = 25565;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2.5);

    private static readonly Regex BracketedIpv6 =
        new(@"^\[(?<ip>.+?)\](?::(?<port>\d{1,5}))?$", RegexOptions.CultureInvariant);

    private static readonly Regex TrailingPort = new(@":(?<port>\d{1,5})$", RegexOptions.CultureInvariant);

    /// <summary>
    /// 解析服务器地址。显式指定端口时不查询 SRV；未指定端口时先查询 _minecraft._tcp SRV 记录，没有则使用默认端口 25565。
    /// </summary>
    public static async Task<ResolvedServerAddress> GetResolvedServerAddressAsync(string address,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("The server address is empty.", nameof(address));

        var (rawHost, port) = ParseHostAndPort(NormalizeAddress(address));
        var host = ToAsciiHost(rawHost);

        if (port is { } explicitPort)
            return new ResolvedServerAddress(host, await ResolveIpAsync(host, explicitPort, cancelToken), explicitPort);

        if (IPAddress.TryParse(host, out _))
            return new ResolvedServerAddress(host, host, DefaultPort);

        foreach (var (target, srvPort) in await DnsSrvResolver.QueryAsync("_minecraft._tcp." + host, cancelToken))
        {
            var ip = await ResolveIpAsync(target, srvPort, cancelToken);
            if (ip is not null) return new ResolvedServerAddress(host, ip, srvPort);
        }

        return new ResolvedServerAddress(host, await ResolveIpAsync(host, DefaultPort, cancelToken), DefaultPort);
    }

    /// <summary>去掉 scheme（如 <c>minecraft://</c>）、首尾空白与末尾的 <c>/</c>。</summary>
    private static string NormalizeAddress(string input)
    {
        var address = input.Trim();
        var schemeIndex = address.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex > 0) address = address[(schemeIndex + 3)..];
        return address.TrimEnd('/');
    }

    private static string ToAsciiHost(string host)
    {
        try
        {
            return new IdnMapping().GetAscii(host);
        }
        catch (ArgumentException)
        {
            return host;
        }
    }

    /// <summary>拆分主机与端口；只有 <c>host:port</c>、<c>[ipv6]:port</c> 形式才视为带端口。</summary>
    private static (string Host, int? Port) ParseHostAndPort(string input)
    {
        var bracketed = BracketedIpv6.Match(input);
        if (bracketed.Success)
        {
            int? bracketPort = null;
            if (bracketed.Groups["port"].Success)
                bracketPort = int.Parse(bracketed.Groups["port"].Value, CultureInfo.InvariantCulture);
            return (bracketed.Groups["ip"].Value, bracketPort);
        }

        if (IPAddress.TryParse(input, out _)) return (input, null);

        var trailing = TrailingPort.Match(input);
        if (trailing.Success && input.Count(c => c == ':') == 1)
        {
            var port = int.Parse(trailing.Groups["port"].Value, CultureInfo.InvariantCulture);
            return (input[..^trailing.Length], port);
        }

        return (input, null);
    }

    /// <summary>
    /// 解析主机名并返回可以连通的 IP：优先返回能连上的地址，都连不上时返回第一个解析结果；解析失败返回 null。
    /// </summary>
    private static async Task<string?> ResolveIpAsync(string host, int port, CancellationToken cancelToken)
    {
        var addresses = await ResolveAddressesAsync(host, cancelToken);
        foreach (var ip in addresses)
            if (await CanConnectAsync(ip, port, cancelToken))
                return ip.ToString();
        return addresses.FirstOrDefault()?.ToString();
    }

    private static async Task<IPAddress[]> ResolveAddressesAsync(string host, CancellationToken cancelToken)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancelToken);
            return addresses.OrderBy(a => a.AddressFamily == AddressFamily.InterNetworkV6 ? 0 : 1).ToArray();
        }
        catch (Exception) when (!cancelToken.IsCancellationRequested)
        {
            return [];
        }
    }

    private static async Task<bool> CanConnectAsync(IPAddress ip, int port, CancellationToken cancelToken)
    {
        using var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        timeout.CancelAfter(ConnectTimeout);
        try
        {
            await socket.ConnectAsync(new IPEndPoint(ip, port), timeout.Token);
            return true;
        }
        catch (Exception) when (!cancelToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
