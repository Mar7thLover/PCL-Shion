namespace PCL.Shion.Core.Versions;

/// <summary>
/// 版本清单中的一个版本条目。
/// </summary>
/// <param name="Id">版本号，如 1.21.1、24w36a。</param>
/// <param name="Type">版本类型：release、snapshot、old_beta 或 old_alpha。</param>
/// <param name="ReleaseTime">发布时间。</param>
/// <param name="JsonUrl">该版本的版本 JSON 地址。</param>
/// <param name="Sha1">版本 JSON 的 SHA-1，清单中没有时为 null。</param>
public sealed record RemoteVersion(string Id, string Type, DateTimeOffset ReleaseTime, Uri JsonUrl, string? Sha1);
