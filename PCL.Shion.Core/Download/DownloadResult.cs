namespace PCL.Shion.Core.Download;

/// <summary>
/// 一批下载的结果。
/// </summary>
/// <param name="Failed">所有地址都失败的项，按提交顺序排列。</param>
/// <param name="Errors">
/// 失败原因，键为 <see cref="DownloadItem.TargetPath"/>，值是每个地址最后一次失败的原因（没有地址可言的失败只有一项）。
/// 原因与语言无关，由调用方转成面向用户的文字。
/// </param>
public sealed record DownloadResult(IReadOnlyList<DownloadItem> Failed, IReadOnlyDictionary<string, IReadOnlyList<DownloadError>> Errors)
{
    public bool Succeeded => Failed.Count == 0;
}
