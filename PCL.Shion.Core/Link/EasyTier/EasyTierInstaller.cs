using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PCL.Shion.Core.Download;

namespace PCL.Shion.Core.Link.EasyTier;

/// <summary>
///     下载并解压 EasyTier，对应 WPF 中 ModLink.DownloadEasyTier 的流程：
///     下载压缩包，解压到共享数据目录，清理压缩包与旧版本。
/// </summary>
public static class EasyTierInstaller
{
    /// <summary>当前系统是否有对应的 EasyTier 构建（联机目前只支持 Windows）。</summary>
    public static bool IsSupported => OperatingSystem.IsWindows();

    public static IReadOnlyList<Uri> GetDownloadUrls()
    {
        var architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x86_64";
        var file = $"easytier-windows-{architecture}-v{ETInfoProvider.ETVersion}.zip";
        return
        [
            new Uri($"https://staticassets.naids.com/resources/pclce/static/easytier/{file}"),
            new Uri($"https://s3.pysio.online/pcl2-ce/static/easytier/{file}")
        ];
    }

    /// <summary>
    ///     下载、解压并清理。<paramref name="engine" /> 为下载引擎，<paramref name="onStage" /> 在阶段切换时回调（0 下载，1 解压，2 清理）。
    /// </summary>
    public static async Task InstallAsync(DownloadEngine engine, string cacheDirectory,
        IProgress<DownloadProgress>? progress, Action<int>? onStage, CancellationToken cancellationToken)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException("EasyTier is only bundled for Windows.");

        var zipPath = Path.Combine(cacheDirectory, "EasyTier", $"EasyTier-{ETInfoProvider.ETVersion}.zip");
        var installRoot = Path.Combine(Paths.SharedLocalData, "EasyTier", ETInfoProvider.ETVersion);
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);

        onStage?.Invoke(0);
        var result = await engine.DownloadAsync([new DownloadItem(GetDownloadUrls(), zipPath)], progress, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
            throw new IOException("EasyTier download failed.");

        onStage?.Invoke(1);
        await Task.Run(() =>
        {
            Directory.CreateDirectory(installRoot);
            ZipFile.ExtractToDirectory(zipPath, installRoot, true);
        }, cancellationToken).ConfigureAwait(false);

        onStage?.Invoke(2);
        try
        {
            File.Delete(zipPath);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Link", "清理 EasyTier 压缩包出错");
        }

        CleanupOldVersions();
    }

    private static void CleanupOldVersions()
    {
        var root = Path.Combine(Paths.SharedLocalData, "EasyTier");
        if (!Directory.Exists(root)) return;
        foreach (var folder in Directory.GetDirectories(root))
        {
            if (Path.GetFileName(folder).Equals(ETInfoProvider.ETVersion, StringComparison.Ordinal)) continue;
            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Link", "清理旧版本 EasyTier 出错");
            }
        }
    }
}
