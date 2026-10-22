using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Sources;
using PCL.Shion.Core.Versions;

namespace PCL.Shion.Core.Install;

/// <summary>
/// 安装原版实例，以及补全已安装实例缺失或损坏的文件。
/// </summary>
/// <param name="engine">执行下载的引擎，由调用方负责释放。</param>
/// <param name="source">首选下载源，失败时换用另一个源。</param>
public sealed class GameInstaller(DownloadEngine engine, DownloadSource source)
{
    private const string VersionStage = "version";
    private const string FilesStage = "files";
    private const string AssetsStage = "assets";

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 把 <paramref name="version"/> 安装为名为 <paramref name="instanceName"/> 的实例：版本 JSON、客户端 jar、库、资源与日志配置。
    /// 实例名与版本号不同时，版本 JSON 中的 id 改为实例名，客户端 jar 也以实例名命名。
    /// </summary>
    /// <exception cref="InstallException">有文件下载失败。</exception>
    public async Task InstallVanillaAsync(
        string minecraftRoot,
        RemoteVersion version,
        string instanceName,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var jsonPath = Path.Combine(minecraftRoot, "versions", instanceName, $"{instanceName}.json");
        var versionJson = new DownloadItem(MirrorUrls.Resolve(version.JsonUrl.AbsoluteUri, source), jsonPath, Sha1: version.Sha1);
        ThrowIfFailed(await engine.DownloadAsync([versionJson], BeginStage(progress, VersionStage), cancellationToken).ConfigureAwait(false));
        if (instanceName != version.Id)
            RenameVersion(jsonPath, instanceName);

        ThrowIfFailed(await CompleteAsync(minecraftRoot, instanceName, progress, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 补全实例运行所需的文件，已存在且通过校验的不会重新下载。分两个阶段：
    /// 先是客户端 jar、库、资源索引与日志配置，再是资源索引列出的资源对象；资源索引没能就位时跳过第二阶段。
    /// </summary>
    /// <returns>两个阶段合并后的下载结果。</returns>
    /// <exception cref="IOException">版本 JSON（包括其父版本）不存在或无法读取。</exception>
    /// <exception cref="InvalidDataException">版本 JSON 内容无效。</exception>
    public async Task<DownloadResult> CompleteAsync(
        string minecraftRoot,
        string instanceName,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var version = VersionResolver.Resolve(minecraftRoot, instanceName);
        var files = await engine.DownloadAsync(
            RequirementCollector.Collect(minecraftRoot, version, source),
            BeginStage(progress, FilesStage),
            cancellationToken).ConfigureAwait(false);

        // 下载失败时留下的可能是旧的或损坏的索引，不能据此列出资源对象
        var indexPath = RequirementCollector.GetAssetIndexPath(minecraftRoot, version.AssetIndexId);
        if (!File.Exists(indexPath) || files.Failed.Any(item => item.TargetPath == indexPath))
            return files;

        var assets = await engine.DownloadAsync(
            RequirementCollector.CollectAssetObjects(minecraftRoot, version.AssetIndexId, source),
            BeginStage(progress, AssetsStage),
            cancellationToken).ConfigureAwait(false);
        return new DownloadResult([.. files.Failed, .. assets.Failed], files.Errors.Concat(assets.Errors).ToDictionary());
    }

    /// <summary>
    /// 把版本 JSON 的 id 改为实例名，与所在目录保持一致。
    /// </summary>
    private static void RenameVersion(string jsonPath, string instanceName)
    {
        var root = JsonNode.Parse(File.ReadAllText(jsonPath))!.AsObject();
        root["id"] = instanceName;
        File.WriteAllText(jsonPath, root.ToJsonString(JsonWriteOptions));
    }

    private static void ThrowIfFailed(DownloadResult result)
    {
        if (!result.Succeeded)
            throw new InstallException(result);
    }

    /// <summary>
    /// 报告进入新阶段，并返回把下载进度转发为该阶段进度的对象。
    /// </summary>
    private static IProgress<DownloadProgress>? BeginStage(IProgress<InstallProgress>? progress, string stage)
    {
        if (progress is null)
            return null;

        progress.Report(new InstallProgress(stage, null));
        return new StageProgress(progress, stage);
    }

    private sealed class StageProgress(IProgress<InstallProgress> target, string stage) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value)
        {
            target.Report(new InstallProgress(stage, value));
        }
    }
}
