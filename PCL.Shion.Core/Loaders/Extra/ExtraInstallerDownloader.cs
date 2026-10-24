using PCL.Shion.Core.Download;

namespace PCL.Shion.Core.Loaders.Extra;

/// <summary>
/// 把加载器安装器（Fabric、LegacyFabric、OptiFine、LiteLoader、LabyMod）保存到用户选择的位置。
/// </summary>
public static class ExtraInstallerDownloader
{
    /// <summary>
    /// 下载 <paramref name="url"/> 到 <paramref name="target"/>。目标文件已存在时会被覆盖。
    /// </summary>
    /// <exception cref="InvalidOperationException">文件下载失败，原因写在异常信息中。</exception>
    /// <exception cref="OperationCanceledException">下载被取消。</exception>
    public static async Task SaveAsync(
        DownloadEngine engine,
        string url,
        string target,
        CancellationToken cancellationToken)
    {
        var item = new DownloadItem([new Uri(url)], target);
        var result = await engine.DownloadAsync([item], null, cancellationToken).ConfigureAwait(false);
        if (result.Failed.Count == 0)
            return;

        var reasons = result.Errors.TryGetValue(target, out var errors)
            ? string.Join("; ", errors.Select(error => error.ToString()))
            : "unknown error";
        throw new InvalidOperationException($"Failed to download {url}: {reasons}");
    }
}
