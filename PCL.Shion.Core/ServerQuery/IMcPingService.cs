using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PCL.Shion.Core.ServerQuery.Model;

namespace PCL.Shion.Core.ServerQuery;

/// <summary>
/// Minecraft 服务器信息查询服务。
/// </summary>
public interface IMcPingService : IDisposable
{
    /// <summary>
    /// 查询服务器信息。无法连接或读写超时返回 null；服务器返回了无法解析的内容时抛出 <see cref="ServerQueryException"/>。
    /// </summary>
    Task<McPingResult?> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>实际连接到的端点。</summary>
    IPEndPoint Endpoint { get; }

    /// <summary>用于握手的主机名。</summary>
    string Host { get; }

    /// <summary>超时时间（毫秒）。</summary>
    int Timeout { get; }
}
