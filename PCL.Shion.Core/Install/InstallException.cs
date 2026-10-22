using PCL.Shion.Core.Download;

namespace PCL.Shion.Core.Install;

/// <summary>
/// 安装未能完成：有文件在尝试了全部下载地址后仍然失败。
/// </summary>
public sealed class InstallException(DownloadResult result)
    : Exception($"Failed to download {result.Failed.Count} file(s): {string.Join(", ", result.Failed.Take(3).Select(item => item.TargetPath))}")
{
    /// <summary>下载失败的文件。</summary>
    public IReadOnlyList<DownloadItem> FailedFiles { get; } = result.Failed;

    /// <summary>各文件的失败原因，键为 <see cref="DownloadItem.TargetPath"/>。</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<DownloadError>> Errors { get; } = result.Errors;
}
