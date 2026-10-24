using System;

namespace PCL.Shion.Core.ServerQuery;

/// <summary>
/// 服务器信息查询失败的原因。
/// </summary>
public enum ServerQueryError
{
    /// <summary>服务器地址超过协议允许的长度。</summary>
    AddressTooLong,

    /// <summary>服务器返回了空数据包。</summary>
    EmptyPacket,

    /// <summary>服务器返回的信息不是有效的状态响应。</summary>
    InvalidResponse
}

/// <summary>
/// 服务器信息查询失败。异常文本面向开发者，界面上应按 <see cref="Error"/> 显示对应的本地化文字。
/// </summary>
public sealed class ServerQueryException : Exception
{
    public ServerQueryException(ServerQueryError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public ServerQueryError Error { get; }
}
