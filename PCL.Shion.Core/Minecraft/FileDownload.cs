namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 版本 JSON 声明的一个下载文件：客户端 jar（downloads.client）、资源索引（assetIndex）或日志配置（logging.client.file）。
/// </summary>
/// <param name="Url">下载地址。</param>
/// <param name="Sha1">SHA-1 校验值，未声明时为 null。</param>
/// <param name="Size">文件大小（字节），未声明时为 null。</param>
public sealed record FileDownload(string Url, string? Sha1, long? Size);
