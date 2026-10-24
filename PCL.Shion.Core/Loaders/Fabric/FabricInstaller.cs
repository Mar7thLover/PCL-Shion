using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Loaders.Fabric;

/// <summary>
/// 安装 Fabric 或 Quilt：meta 服务直接提供继承原版的版本 JSON，改名后写入实例目录，再补全它声明的库。
/// </summary>
internal static class FabricInstaller
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 前提：<paramref name="loader"/> 所属的原版已安装。
    /// </summary>
    /// <exception cref="InstallException">有库下载失败。</exception>
    public static async Task InstallAsync(
        DownloadEngine engine,
        DownloadSource source,
        string minecraftRoot,
        LoaderVersion loader,
        string instanceName,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new InstallProgress("version", null));
        var profile = await FabricMeta.FetchAsync(engine.Http, FabricMeta.GetProfileUrls(loader, source), ParseProfile, cancellationToken)
            .ConfigureAwait(false);
        profile["id"] = instanceName;

        var jsonPath = Path.Combine(minecraftRoot, "versions", instanceName, $"{instanceName}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        await File.WriteAllTextAsync(jsonPath, profile.ToJsonString(JsonWriteOptions), cancellationToken).ConfigureAwait(false);

        var result = await new GameInstaller(engine, source)
            .CompleteAsync(minecraftRoot, instanceName, progress, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InstallException(result);
    }

    private static JsonObject ParseProfile(string json)
    {
        return JsonNode.Parse(json) as JsonObject ?? throw new JsonException("The loader profile must be a JSON object.");
    }
}
