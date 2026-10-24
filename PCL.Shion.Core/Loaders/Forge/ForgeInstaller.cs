using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Launch;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Loaders.Forge;

/// <summary>
/// 用官方安装器 jar 安装 Forge 或 NeoForge。新版安装器带 version.json 与处理器：下载库之后用给定的 Java 依次运行客户端一侧的处理器；
/// 旧版安装器（1.12.2 及更早）只有 versionInfo：直接写出版本 JSON，并把安装器自带的 universal jar 放进 libraries。
/// </summary>
internal static class ForgeInstaller
{
    private const string ForgeMaven = "https://maven.minecraftforge.net/";
    private const string NeoForgeMaven = "https://maven.neoforged.net/releases/";
    private const string LibrariesUrl = "https://libraries.minecraft.net/";

    private const string VersionStage = "version";
    private const string FilesStage = "files";
    private const string ProcessorsStage = "processors";

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly RuleEnvironment CurrentPlatform = RuleEnvironment.ForCurrentPlatform(new HashSet<string>());

    /// <summary>
    /// 把 <paramref name="loader"/> 安装为名为 <paramref name="instanceName"/> 的实例，实例通过 inheritsFrom 继承原版。
    /// 前提：所属的原版已安装在与原版版本号同名的目录。安装器的临时文件放在 <paramref name="minecraftRoot"/> 下的临时目录，结束后删除。
    /// </summary>
    /// <param name="javaExecutable">运行处理器的 Java，版本须满足安装器工具的要求，通常就用实例自己的 Java。</param>
    /// <param name="progress">依次报告 "version"（下载安装器）、"files"（库）与 "processors"（运行处理器，没有下载进度）阶段。</param>
    /// <exception cref="InstallException">安装器或库下载失败。</exception>
    /// <exception cref="ForgeProcessorException">有处理器运行失败。</exception>
    /// <exception cref="InvalidDataException">安装器的内容无效，或处理器的输出没有通过校验。</exception>
    /// <exception cref="IOException">原版的版本 JSON 不存在或无法读取。</exception>
    public static async Task InstallAsync(
        DownloadEngine engine,
        DownloadSource source,
        string minecraftRoot,
        LoaderVersion loader,
        string instanceName,
        string javaExecutable,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        // 处理器在临时目录里运行，交给它们的路径必须是绝对路径
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(minecraftRoot));
        var vanilla = VersionResolver.Resolve(root, loader.MinecraftVersion);
        var tempDirectory = Path.Combine(root, $".pcl-shion-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var installerUrl = GetInstallerUrl(loader);
            var installerPath = Path.Combine(tempDirectory, installerUrl[(installerUrl.LastIndexOf('/') + 1)..]);
            ThrowIfFailed(await engine.DownloadAsync(
                [new DownloadItem(ForgeMirrors.Resolve(installerUrl, source), installerPath)],
                BeginStage(progress, VersionStage),
                cancellationToken).ConfigureAwait(false));

            using var installer = new InstallerArchive(installerPath);
            var installation = new Installation(engine, source, root, tempDirectory, installer, vanilla, progress);
            var profileJson = installer.ReadText("install_profile.json");
            if (LegacyInstallProfile.TryParse(profileJson) is { } legacy)
            {
                await installation.InstallLegacyAsync(legacy, cancellationToken).ConfigureAwait(false);
                installation.WriteInstance(instanceName, legacy.VersionInfo, legacy.StripMeta);
            }
            else
            {
                var profile = InstallProfile.Parse(profileJson);
                var versionJson = installer.ReadText(profile.VersionJsonEntry);
                await installation.InstallModernAsync(profile, versionJson, javaExecutable, cancellationToken).ConfigureAwait(false);
                installation.WriteInstance(instanceName, ParseObject(versionJson), stripMeta: false);
            }
        }
        finally
        {
            DeleteQuietly(tempDirectory);
        }
    }

    /// <summary>
    /// 官方 Maven 仓库中安装器 jar 的地址。Forge 的完整版本号为 &lt;原版&gt;-&lt;Forge&gt;；
    /// 1.20.1 上的 NeoForge 沿用 net.neoforged:forge 与同样的版本号形式，其后的 NeoForge 为 net.neoforged:neoforge。
    /// </summary>
    internal static string GetInstallerUrl(LoaderVersion loader)
    {
        var (repository, coordinate) = loader switch
        {
            { Kind: LoaderKind.Forge } =>
                (ForgeMaven, $"net.minecraftforge:forge:{loader.MinecraftVersion}-{loader.Version}"),
            { MinecraftVersion: ForgeCatalog.LegacyNeoForgeMinecraftVersion } =>
                (NeoForgeMaven, $"net.neoforged:forge:{loader.MinecraftVersion}-{loader.Version}"),
            _ => (NeoForgeMaven, $"net.neoforged:neoforge:{loader.Version}")
        };
        return repository + MavenName.GetPath(coordinate + ":installer");
    }

    private static JsonObject ParseObject(string json)
    {
        return JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("The version JSON in the installer must be an object.");
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

    // 临时目录可能正被杀毒软件扫描而暂时删不掉，此时只会留下一个孤立的临时目录，不影响安装结果
    private static void DeleteQuietly(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// 一次安装的上下文：已下载并打开的安装器、它所属的原版，以及下载与进度报告。
    /// </summary>
    private sealed class Installation(
        DownloadEngine engine,
        DownloadSource source,
        string root,
        string tempDirectory,
        InstallerArchive installer,
        ResolvedVersion vanilla,
        IProgress<InstallProgress>? progress)
    {
        private string LibrariesDirectory => Path.Combine(root, "libraries");

        /// <summary>
        /// 旧版安装器：取出 universal jar，再准备 versionInfo 声明的库。
        /// </summary>
        public Task InstallLegacyAsync(LegacyInstallProfile profile, CancellationToken cancellationToken)
        {
            installer.Extract(profile.UniversalEntry, GetLibraryPath(MavenName.GetPath(profile.UniversalName)));
            var libraries = VersionFile.Parse(profile.VersionInfo.ToJsonString()).Libraries;
            return InstallLibrariesAsync(LibrarySelector.Select(libraries, CurrentPlatform), cancellationToken);
        }

        /// <summary>
        /// 新版安装器：准备安装配置与版本 JSON 声明的库，再依次运行客户端一侧的处理器。
        /// </summary>
        public async Task InstallModernAsync(InstallProfile profile, string versionJson, string javaExecutable, CancellationToken cancellationToken)
        {
            // 处理器常常需要同一个库的不同版本，安装配置中的库只按路径去重，不能像版本 JSON 的库那样按坐标合并
            var profileLibraries = profile.Libraries.Select(library =>
                new SelectedLibrary(library.Artifact?.Path ?? MavenName.GetPath(library.Name), null, library.Artifact, library.RepositoryUrl));
            var versionLibraries = LibrarySelector.Select(VersionFile.Parse(versionJson).Libraries, CurrentPlatform);
            await InstallLibrariesAsync(profileLibraries.Concat(versionLibraries), cancellationToken).ConfigureAwait(false);

            var processors = profile.Processors.Where(processor => processor.RunsOnClient).ToList();
            if (processors.Count == 0)
                return;

            progress?.Report(new InstallProgress(ProcessorsStage, null));
            var variables = CreateVariables(profile);
            foreach (var processor in processors)
            {
                var prepared = processor.Prepare(LibrariesDirectory, variables);
                if (!await TryDownloadMappingsAsync(prepared, cancellationToken).ConfigureAwait(false))
                    await ProcessorRunner.RunAsync(prepared, javaExecutable, tempDirectory, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 写出实例目录：原版客户端 jar 复制为 &lt;实例名&gt;.jar，版本 JSON 的 id 改为实例名。
        /// 新版 Forge 与 NeoForge 按 ${version_name}.jar 把客户端 jar 排除在模块路径之外，jar 必须以实例名命名，官方启动器也是这样做的。
        /// </summary>
        public void WriteInstance(string instanceName, JsonObject version, bool stripMeta)
        {
            var directory = Path.Combine(root, "versions", instanceName);
            Directory.CreateDirectory(directory);
            var jarPath = Path.Combine(directory, $"{instanceName}.jar");
            if (stripMeta)
            {
                CopyWithoutMetaInf(vanilla.ClientJarPath, jarPath);
            }
            else
            {
                File.Copy(vanilla.ClientJarPath, jarPath, overwrite: true);
                // 复制出的 jar 与原版相同，沿用原版的下载信息，文件丢失或损坏时补全流程可以重新下载
                if (vanilla.ClientJarDownload is { } client)
                {
                    version["downloads"] = new JsonObject
                    {
                        ["client"] = new JsonObject { ["sha1"] = client.Sha1, ["size"] = client.Size, ["url"] = client.Url }
                    };
                }
            }

            version["id"] = instanceName;
            // 很早的安装器给出的是不继承原版的完整版本 JSON，补上 inheritsFrom 以沿用原版的资源索引与 Java 要求
            version["inheritsFrom"] ??= vanilla.Id;
            File.WriteAllText(Path.Combine(directory, $"{instanceName}.json"), version.ToJsonString(JsonWriteOptions));
        }

        /// <summary>
        /// 准备库文件：安装器 maven/ 目录自带的直接解压，其余与原版客户端 jar 一起下载；
        /// 既没有下载地址、又不在安装器中的库由处理器生成。
        /// </summary>
        private async Task InstallLibrariesAsync(IEnumerable<SelectedLibrary> libraries, CancellationToken cancellationToken)
        {
            // 处理器要读原版客户端 jar，实例也要复制它
            var downloads = new List<DownloadItem>();
            if (vanilla.ClientJarDownload is { } client)
                downloads.Add(new DownloadItem(MirrorUrls.Resolve(client.Url, source), vanilla.ClientJarPath, client.Size, client.Sha1));

            foreach (var library in libraries.DistinctBy(item => item.RelativePath))
            {
                var path = GetLibraryPath(library.RelativePath);
                var (size, sha1) = (library.Download?.Size, library.Download?.Sha1);
                var entry = $"maven/{library.RelativePath}";
                if (installer.Contains(entry))
                {
                    if (!FileVerifier.Matches(path, size, sha1))
                        installer.Extract(entry, path);
                }
                else if (GetLibraryUrl(library) is { } url)
                {
                    downloads.Add(new DownloadItem(ForgeMirrors.Resolve(url, source), path, size, sha1));
                }
            }

            ThrowIfFailed(await engine.DownloadAsync(downloads, BeginStage(progress, FilesStage), cancellationToken).ConfigureAwait(false));
        }

        /// <summary>
        /// data 生成的变量，加上安装器内置的变量。
        /// </summary>
        private Dictionary<string, string> CreateVariables(InstallProfile profile)
        {
            var variables = ProcessorTokens.CreateVariables(profile.Data, LibrariesDirectory, entry =>
            {
                var path = Path.Combine(tempDirectory, entry.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                installer.Extract(entry, path);
                return path;
            });
            variables["SIDE"] = "client";
            variables["MINECRAFT_JAR"] = vanilla.ClientJarPath;
            variables["MINECRAFT_VERSION"] = profile.Minecraft;
            variables["ROOT"] = root;
            variables["INSTALLER"] = installer.FilePath;
            variables["LIBRARY_DIR"] = LibrariesDirectory;
            return variables;
        }

        /// <summary>
        /// DOWNLOAD_MOJMAPS 处理器只是下载原版的混淆映射表，改由下载引擎完成，以便使用镜像源与重试。
        /// 不是这个处理器、或原版 JSON 没有声明映射表时返回 false，交给处理器自己运行。
        /// </summary>
        private async Task<bool> TryDownloadMappingsAsync(PreparedProcessor processor, CancellationToken cancellationToken)
        {
            if (processor.GetOption("--task") != "DOWNLOAD_MOJMAPS"
                || processor.GetOption("--output") is not { } output
                || ReadClientMappings() is not { } mappings)
            {
                return false;
            }

            var item = new DownloadItem(MirrorUrls.Resolve(mappings.Url, source), output, mappings.Size, mappings.Sha1);
            ThrowIfFailed(await engine.DownloadAsync([item], null, cancellationToken).ConfigureAwait(false));
            return true;
        }

        private FileDownload? ReadClientMappings()
        {
            var path = Path.Combine(root, "versions", vanilla.Id, $"{vanilla.Id}.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.GetObjectProperty("downloads")?.GetObjectProperty("client_mappings") is not { } mappings
                || mappings.GetStringProperty("url") is not { } url)
            {
                return null;
            }
            return new FileDownload(url, mappings.GetStringProperty("sha1"), mappings.GetInt64Property("size"));
        }

        private string GetLibraryPath(string relativePath)
        {
            return Path.Combine(LibrariesDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// 优先使用声明的地址；未声明时在库所在的 Maven 仓库（没有则为 Mojang 的库服务器）下按路径拼接。
        /// 声明为空字符串表示这个库没有下载地址，返回 null。
        /// </summary>
        private static string? GetLibraryUrl(SelectedLibrary library)
        {
            return library.Download?.Url switch
            {
                null => (library.RepositoryUrl ?? LibrariesUrl).TrimEnd('/') + "/" + library.RelativePath,
                "" => null,
                var url => url
            };
        }

        // stripMeta 的旧版 Forge 会改写原版的类，原版 jar 的签名会让改写后的类无法加载，复制时去掉 META-INF
        private static void CopyWithoutMetaInf(string sourceJar, string targetJar)
        {
            File.Delete(targetJar);
            using var sourceArchive = ZipFile.OpenRead(sourceJar);
            using var targetArchive = ZipFile.Open(targetJar, ZipArchiveMode.Create);
            foreach (var entry in sourceArchive.Entries)
            {
                if (entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var copy = targetArchive.CreateEntry(entry.FullName);
                copy.LastWriteTime = entry.LastWriteTime;
                using var input = entry.Open();
                using var output = copy.Open();
                input.CopyTo(output);
            }
        }
    }

    private sealed class StageProgress(IProgress<InstallProgress> target, string stage) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value)
        {
            target.Report(new InstallProgress(stage, value));
        }
    }
}
