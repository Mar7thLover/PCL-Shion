namespace PCL.Shion.Core.Download;

/// <summary>
/// <see cref="DownloadEngine"/> 的选项。
/// </summary>
public sealed class DownloadOptions
{
    /// <summary>同时进行的下载数上限，对同一个引擎上的所有下载共同生效。</summary>
    public int MaxConcurrency { get; init; } = 32;

    /// <summary>每个地址在首次失败后最多重试的次数，只对网络错误、超时、408、429 与 5xx 生效。</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>等待响应头以及两次收到数据之间的最长时间，同时作为 <see cref="DownloadEngine.Http"/> 的请求超时。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>第一次重试前的等待时间，之后每次翻倍。</summary>
    internal TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);
}
