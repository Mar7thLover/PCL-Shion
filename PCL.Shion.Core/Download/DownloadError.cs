namespace PCL.Shion.Core.Download;

/// <summary>
/// 一个地址下载失败的种类。与界面语言无关，由界面层决定怎样向用户描述。
/// </summary>
public enum DownloadErrorKind
{
    /// <summary>下载项没有任何候选地址。</summary>
    NoUrl,

    /// <summary>收到的字节数与期望不符。</summary>
    SizeMismatch,

    /// <summary>收到内容的 SHA-1 与期望不符。</summary>
    HashMismatch,

    /// <summary>超过超时时间一直没有收到数据。</summary>
    Timeout,

    /// <summary>其他失败：HTTP 状态码、网络或磁盘错误等，说明取自系统，没有本地化。</summary>
    Other
}

/// <summary>
/// 一个地址的下载失败原因。数值一律用不受区域影响的格式表示，由调用方转成面向用户的文字。
/// </summary>
/// <param name="Kind">失败的种类。</param>
/// <param name="Url">失败的地址；没有地址可言时（无候选地址、写入磁盘失败）为 null。</param>
/// <param name="Expected">
/// 期望值：<see cref="DownloadErrorKind.SizeMismatch"/> 为字节数，<see cref="DownloadErrorKind.HashMismatch"/> 为小写十六进制 SHA-1，
/// <see cref="DownloadErrorKind.Timeout"/> 为允许的无数据时长（秒）。其余种类为 null。
/// </param>
/// <param name="Actual">
/// 实际值：<see cref="DownloadErrorKind.SizeMismatch"/> 为收到的字节数，<see cref="DownloadErrorKind.HashMismatch"/> 为收到内容的小写十六进制 SHA-1，
/// <see cref="DownloadErrorKind.Other"/> 为系统给出的原始说明。其余种类为 null。
/// </param>
public sealed record DownloadError(DownloadErrorKind Kind, Uri? Url = null, string? Expected = null, string? Actual = null)
{
    /// <summary>
    /// 英文的诊断文字，只用于异常消息与调试；面向用户的文字应由界面层按 <see cref="Kind"/> 生成。
    /// </summary>
    public override string ToString()
    {
        var text = Kind switch
        {
            DownloadErrorKind.NoUrl => "No download URL",
            DownloadErrorKind.SizeMismatch => $"Size mismatch: expected {Expected} bytes, received {Actual}",
            DownloadErrorKind.HashMismatch => $"SHA-1 mismatch: expected {Expected}, received {Actual}",
            DownloadErrorKind.Timeout => $"Timed out after {Expected} s without data",
            _ => Actual
        };
        return Url is null ? text ?? "" : $"{Url}: {text}";
    }
}
