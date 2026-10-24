namespace PCL.Shion.Core.Download;

/// <summary>
/// 一个待下载的文件。
/// </summary>
/// <param name="Urls">候选地址，按优先级排列。</param>
/// <param name="TargetPath">保存位置的完整路径。</param>
/// <param name="Size">期望的字节数，未知时为 null。</param>
/// <param name="Sha1">期望的 SHA-1（十六进制，不区分大小写），未知时为 null。</param>
public sealed record DownloadItem(IReadOnlyList<Uri> Urls, string TargetPath, long? Size = null, string? Sha1 = null);
