using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Loaders.Fabric;
using PCL.Shion.Core.Loaders.Forge;
using PCL.Shion.Core.Sources;
using PCL.Shion.Core.Versions;

namespace PCL.Shion.Core.Loaders;

/// <summary>
/// 安装模组加载器，生成一个继承自原版的实例。
/// </summary>
/// <param name="engine">执行下载的引擎，由调用方负责释放。</param>
/// <param name="source">首选下载源，失败时换用另一个源。</param>
public sealed class LoaderInstaller(DownloadEngine engine, DownloadSource source)
{
    /// <summary>
    /// 把 <paramref name="loader"/> 安装为名为 <paramref name="instanceName"/> 的实例，写出 <c>versions/&lt;实例名&gt;/&lt;实例名&gt;.json</c>
    /// 并补全所需的库。原版尚未安装时先安装原版；实例通过 inheritsFrom 继承原版，可以直接交给 <c>VersionResolver</c> 解析。
    /// </summary>
    /// <param name="javaExecutable">运行 Forge 与 NeoForge 安装处理器的 Java；Fabric 与 Quilt 不使用。</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="instanceName"/> 与原版版本号相同（原版安装在同名目录），或版本清单里没有 <paramref name="loader"/> 所属的原版版本。
    /// </exception>
    /// <exception cref="InstallException">有文件下载失败。</exception>
    public async Task InstallAsync(
        string minecraftRoot,
        LoaderVersion loader,
        string instanceName,
        string javaExecutable,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InstallationSession.ValidateInstanceName(loader.MinecraftVersion);
        if (string.Equals(instanceName, loader.MinecraftVersion, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The instance name must differ from the Minecraft version it inherits from.", nameof(instanceName));

        using var installation = InstallationSession.Begin(minecraftRoot, instanceName);
        await EnsureVanillaAsync(minecraftRoot, loader, progress, cancellationToken).ConfigureAwait(false);

        var install = loader.Kind switch
        {
            LoaderKind.Fabric or LoaderKind.Quilt =>
                FabricInstaller.InstallAsync(engine, source, minecraftRoot, loader, instanceName, progress, cancellationToken),
            LoaderKind.Forge or LoaderKind.NeoForge =>
                ForgeInstaller.InstallAsync(engine, source, minecraftRoot, loader, instanceName, javaExecutable, progress, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(loader), loader.Kind, null)
        };
        await install.ConfigureAwait(false);
    }

    /// <summary>
    /// 加载器实例继承的原版版本 JSON 不存在时，按版本清单把它安装到同名目录。
    /// </summary>
    private async Task EnsureVanillaAsync(
        string minecraftRoot,
        LoaderVersion loader,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var minecraftVersion = loader.MinecraftVersion;
        if (InstallationSession.IsActive(Path.Combine(minecraftRoot, "versions", minecraftVersion)))
            throw new IOException($"Minecraft version '{minecraftVersion}' is still being installed.");
        if (File.Exists(Path.Combine(minecraftRoot, "versions", minecraftVersion, $"{minecraftVersion}.json")))
            return;

        var manifest = await VersionManifestClient.FetchAsync(engine.Http, source, cancellationToken).ConfigureAwait(false);
        var version = manifest.Versions.FirstOrDefault(item => item.Id == minecraftVersion)
            ?? throw new ArgumentException($"Minecraft version '{minecraftVersion}' is not in the version manifest.", nameof(loader));
        await new GameInstaller(engine, source)
            .InstallVanillaAsync(minecraftRoot, version, minecraftVersion, progress, cancellationToken)
            .ConfigureAwait(false);
    }
}
