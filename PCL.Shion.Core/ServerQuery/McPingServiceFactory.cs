using System.Net;

namespace PCL.Shion.Core.ServerQuery;

/// <summary>
/// Minecraft 服务器查询服务的创建入口。
/// </summary>
public static class McPingServiceFactory
{
    /// <summary>用端点创建现代协议查询服务。</summary>
    public static IMcPingService CreateService(IPEndPoint endpoint, int timeout = 10000) =>
        new McPingService(endpoint, timeout);

    /// <summary>用地址与端口创建现代协议查询服务。</summary>
    public static IMcPingService CreateService(string ip, int port = 25565, int timeout = 10000) =>
        new McPingService(ip, port, timeout);

    /// <summary>用原始主机名与已解析的 IP 创建现代协议查询服务。</summary>
    public static IMcPingService CreateService(string host, string? ip, int port = 25565) =>
        CreateService(host, ip, port, 10000);

    /// <summary>用原始主机名与已解析的 IP 创建现代协议查询服务。</summary>
    public static IMcPingService CreateService(string host, string? ip, int port, int timeout) =>
        !string.IsNullOrWhiteSpace(ip) && IPAddress.TryParse(ip, out var address)
            ? new McPingService(host, new IPEndPoint(address, port), timeout)
            : new McPingService(host, port, timeout);

    /// <summary>用端点创建旧版协议查询服务。</summary>
    public static IMcPingService CreateLegacyService(IPEndPoint endpoint, int timeout = 10000) =>
        new LegacyMcPingService(endpoint, timeout);

    /// <summary>用地址与端口创建旧版协议查询服务。</summary>
    public static IMcPingService CreateLegacyService(string ip, int port = 25565, int timeout = 10000) =>
        new LegacyMcPingService(ip, port, timeout);
}
