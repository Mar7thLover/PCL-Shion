using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Loaders;
using PCL.Shion.Core.Loaders.Forge;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Test.Loaders.Forge;

[TestClass]
public sealed class ForgeInstallerTest
{
    private const string LegacyInstance = "Forge 1.6.4";
    private const string LegacyInstallerUrl =
        "https://maven.minecraftforge.net/net/minecraftforge/forge/1.6.4-9.11.1.1345/forge-1.6.4-9.11.1.1345-installer.jar";
    private const string ScalaUrl = "https://maven.minecraftforge.net/org/scala-lang/scala-library/2.10.2/scala-library-2.10.2.jar";
    private const string LaunchWrapperUrl = "https://libraries.minecraft.net/net/minecraft/launchwrapper/1.8/launchwrapper-1.8.jar";

    private const string ModernInstance = "NeoForge";
    private const string ModernInstallerUrl =
        "https://maven.neoforged.net/releases/net/neoforged/neoforge/21.1.77/neoforge-21.1.77-installer.jar";
    private const string InstallerToolsUrl =
        "https://maven.neoforged.net/releases/net/neoforged/installertools/installertools/2.1.2/installertools-2.1.2.jar";
    private const string EventBusUrl = "https://maven.neoforged.net/releases/net/neoforged/bus/8.0.2/bus-8.0.2.jar";

    private const string ServerOnlyProcessor = """
        {
          "sides": [ "server" ],
          "jar": "net.neoforged.installertools:installertools:2.1.2",
          "args": [ "--task", "BUNDLER_EXTRACT", "--input", "{MINECRAFT_JAR}", "--output", "{ROOT}/libraries/" ]
        }
        """;

    private const string MappingsProcessor = """
        {
          "jar": "net.neoforged.installertools:installertools:2.1.2",
          "args": [ "--task", "DOWNLOAD_MOJMAPS", "--version", "1.21.1", "--side", "{SIDE}", "--output", "{MOJMAPS}" ]
        }
        """;

    private const string PatchProcessor = """
        {
          "sides": [ "client" ],
          "jar": "net.neoforged.installertools:binarypatcher:2.1.2",
          "args": [ "--clean", "{MINECRAFT_JAR}", "--output", "{PATCHED}", "--apply", "{BINPATCH}" ],
          "outputs": { "{PATCHED}": "{PATCHED_SHA}" }
        }
        """;

    private static readonly LoaderVersion LegacyForge = new(LoaderKind.Forge, "9.11.1.1345", "1.6.4", true);
    private static readonly LoaderVersion ModernNeoForge = new(LoaderKind.NeoForge, "21.1.77", "1.21.1", true);

    private static readonly byte[] Universal = "forge universal"u8.ToArray();
    private static readonly byte[] Scala = "scala library"u8.ToArray();
    private static readonly byte[] LaunchWrapper = "launchwrapper"u8.ToArray();
    private static readonly byte[] InstallerTools = "installer tools"u8.ToArray();
    private static readonly byte[] BundledUniversal = "neoforge universal"u8.ToArray();
    private static readonly byte[] EventBus = "event bus"u8.ToArray();
    private static readonly byte[] Mappings = "client mappings"u8.ToArray();
    private static readonly byte[] Patched = "patched client"u8.ToArray();
    private static readonly byte[] VanillaJar = CreateJar(
        ("META-INF/MANIFEST.MF", "Manifest-Version: 1.0\n"u8.ToArray()),
        ("META-INF/MOJANG_C.SF", "signature"u8.ToArray()),
        ("net/minecraft/client/Minecraft.class", "class"u8.ToArray()));

    private FakeMinecraftRoot _root = null!;
    private StubServer _server = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = new FakeMinecraftRoot();
        _server = new StubServer();
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        _root.Dispose();
    }

    [TestMethod]
    [DataRow(LoaderKind.Forge, "47.4.0", "1.20.1",
        "https://maven.minecraftforge.net/net/minecraftforge/forge/1.20.1-47.4.0/forge-1.20.1-47.4.0-installer.jar")]
    [DataRow(LoaderKind.Forge, "10.13.4.1614-1.7.10", "1.7.10",
        "https://maven.minecraftforge.net/net/minecraftforge/forge/1.7.10-10.13.4.1614-1.7.10/forge-1.7.10-10.13.4.1614-1.7.10-installer.jar")]
    [DataRow(LoaderKind.NeoForge, "47.1.106", "1.20.1",
        "https://maven.neoforged.net/releases/net/neoforged/forge/1.20.1-47.1.106/forge-1.20.1-47.1.106-installer.jar")]
    [DataRow(LoaderKind.NeoForge, "21.1.77", "1.21.1", ModernInstallerUrl)]
    public void GetInstallerUrl_RebuildsTheMavenCoordinates(LoaderKind kind, string version, string minecraftVersion, string expected)
    {
        Assert.AreEqual(expected, ForgeInstaller.GetInstallerUrl(new LoaderVersion(kind, version, minecraftVersion, true)));
    }

    [TestMethod]
    public async Task InstallAsync_WritesALegacyInstanceThatInheritsFromVanilla()
    {
        InstallVanilla("1.6.4");
        ServeLegacyInstaller(stripMeta: false);

        await InstallAsync(LegacyForge, LegacyInstance);

        var json = ReadInstanceJson(LegacyInstance);
        Assert.AreEqual(LegacyInstance, json["id"]!.GetValue<string>());
        Assert.AreEqual("1.6.4", json["inheritsFrom"]!.GetValue<string>());
        Assert.AreEqual(Sha1(VanillaJar), json["downloads"]!["client"]!["sha1"]!.GetValue<string>());
        CollectionAssert.AreEqual(Universal, ReadLibrary("net/minecraftforge/minecraftforge/9.11.1.1345/minecraftforge-9.11.1.1345.jar"));
        CollectionAssert.AreEqual(Scala, ReadLibrary("org/scala-lang/scala-library/2.10.2/scala-library-2.10.2.jar"));
        CollectionAssert.AreEqual(LaunchWrapper, ReadLibrary("net/minecraft/launchwrapper/1.8/launchwrapper-1.8.jar"));
        CollectionAssert.AreEqual(VanillaJar, File.ReadAllBytes(_root.GetPath($"versions/{LegacyInstance}/{LegacyInstance}.jar")));

        var version = VersionResolver.Resolve(_root.RootPath, LegacyInstance);
        Assert.AreEqual("forge", version.Loader);
        Assert.AreEqual("1.6.4", version.MinecraftVersion);
        Assert.AreEqual("net.minecraft.launchwrapper.Launch", version.MainClass);
        Assert.AreEqual(_root.GetPath($"versions/{LegacyInstance}/{LegacyInstance}.jar"), version.ClientJarPath);
        AssertTemporaryFilesRemoved();
    }

    [TestMethod]
    public async Task InstallAsync_StripsTheSignatureWhenTheLegacyInstallerAsks()
    {
        InstallVanilla("1.6.4");
        ServeLegacyInstaller(stripMeta: true);

        await InstallAsync(LegacyForge, LegacyInstance);

        using var jar = ZipFile.OpenRead(_root.GetPath($"versions/{LegacyInstance}/{LegacyInstance}.jar"));
        CollectionAssert.AreEqual(new[] { "net/minecraft/client/Minecraft.class" }, jar.Entries.Select(entry => entry.FullName).ToArray());
        Assert.IsNull(ReadInstanceJson(LegacyInstance)["downloads"]);
    }

    [TestMethod]
    public async Task InstallAsync_WritesAModernInstanceWithoutRunningServerProcessors()
    {
        InstallVanilla("1.21.1");
        ServeModernInstaller(ServerOnlyProcessor);
        var progress = new ReportList();

        await InstallAsync(ModernNeoForge, ModernInstance, progress);

        var json = ReadInstanceJson(ModernInstance);
        Assert.AreEqual(ModernInstance, json["id"]!.GetValue<string>());
        Assert.AreEqual("1.21.1", json["inheritsFrom"]!.GetValue<string>());
        Assert.AreEqual("forgeclient", json["arguments"]!["game"]![1]!.GetValue<string>());
        CollectionAssert.AreEqual(InstallerTools, ReadLibrary("net/neoforged/installertools/installertools/2.1.2/installertools-2.1.2.jar"));
        CollectionAssert.AreEqual(BundledUniversal, ReadLibrary("net/neoforged/neoforge/21.1.77/neoforge-21.1.77-universal.jar"));
        CollectionAssert.AreEqual(EventBus, ReadLibrary("net/neoforged/bus/8.0.2/bus-8.0.2.jar"));
        CollectionAssert.AreEqual(VanillaJar, File.ReadAllBytes(_root.GetPath($"versions/{ModernInstance}/{ModernInstance}.jar")));

        var version = VersionResolver.Resolve(_root.RootPath, ModernInstance);
        Assert.AreEqual("neoforge", version.Loader);
        Assert.AreEqual(Sha1(VanillaJar), version.ClientJarDownload!.Sha1);
        CollectionAssert.AreEqual(new[] { "version", "files" }, progress.Stages());
        AssertTemporaryFilesRemoved();
    }

    [TestMethod]
    public async Task InstallAsync_DownloadsTheMojangMappingsInsteadOfRunningTheProcessor()
    {
        InstallVanilla("1.21.1");
        ServeModernInstaller(MappingsProcessor);
        var progress = new ReportList();

        await InstallAsync(ModernNeoForge, ModernInstance, progress);

        CollectionAssert.AreEqual(Mappings, ReadLibrary("net/minecraft/client/1.21.1-20240808.144430/client-1.21.1-20240808.144430-mappings.txt"));
        CollectionAssert.AreEqual(new[] { "version", "files", "processors" }, progress.Stages());
    }

    [TestMethod]
    public async Task InstallAsync_SkipsProcessorsWhoseOutputsAreInPlace()
    {
        InstallVanilla("1.21.1");
        ServeModernInstaller(PatchProcessor);
        var patched = _root.GetPath("libraries/net/neoforged/neoforge/21.1.77/neoforge-21.1.77-client.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(patched)!);
        File.WriteAllBytes(patched, Patched);

        await InstallAsync(ModernNeoForge, ModernInstance);

        CollectionAssert.AreEqual(Patched, File.ReadAllBytes(patched));
        Assert.IsTrue(File.Exists(_root.GetPath($"versions/{ModernInstance}/{ModernInstance}.json")));
        AssertTemporaryFilesRemoved();
    }

    [TestMethod]
    public async Task InstallAsync_FallsBackToTheMirrorForTheInstaller()
    {
        InstallVanilla("1.6.4");
        ServeLegacyInstaller(stripMeta: false);
        _server.Add(ForgeMirrors.Resolve(LegacyInstallerUrl, DownloadSource.Official)[1].AbsoluteUri, CreateLegacyInstaller(stripMeta: false));
        _server.Remove(LegacyInstallerUrl);

        await InstallAsync(LegacyForge, LegacyInstance);

        Assert.IsTrue(File.Exists(_root.GetPath($"versions/{LegacyInstance}/{LegacyInstance}.json")));
        Assert.IsTrue(_server.Requests.Any(uri => uri.Host == "bmclapi2.bangbang93.com"));
    }

    [TestMethod]
    public async Task InstallAsync_LeavesNoInstanceWhenALibraryFails()
    {
        InstallVanilla("1.6.4");
        ServeLegacyInstaller(stripMeta: false);
        _server.Remove(ScalaUrl);

        var exception = await Assert.ThrowsExactlyAsync<InstallException>(() => InstallAsync(LegacyForge, LegacyInstance));

        Assert.AreEqual(
            _root.GetPath("libraries/org/scala-lang/scala-library/2.10.2/scala-library-2.10.2.jar"),
            exception.FailedFiles.Single().TargetPath);
        Assert.IsFalse(Directory.Exists(_root.GetPath($"versions/{LegacyInstance}")));
        AssertTemporaryFilesRemoved();
    }

    private async Task InstallAsync(LoaderVersion loader, string instanceName, IProgress<InstallProgress>? progress = null)
    {
        using var engine = new DownloadEngine(handler: _server);
        await ForgeInstaller.InstallAsync(
            engine,
            DownloadSource.Official,
            _root.RootPath,
            loader,
            instanceName,
            // 测试中的处理器都不应真正运行，Java 不存在可以保证这一点
            _root.GetPath("missing-java"),
            progress,
            CancellationToken.None);
    }

    /// <summary>
    /// 安装原版：版本 JSON 带客户端 jar 与映射表的下载信息，客户端 jar 已经就位。
    /// </summary>
    private void InstallVanilla(string version)
    {
        var mappingsUrl = $"https://piston-data.mojang.com/v1/objects/{Sha1(Mappings)}/client.txt";
        _server.Add(mappingsUrl, Mappings);
        _root.WriteVersion(version, $$"""
            {
              "id": "{{version}}",
              "type": "release",
              "mainClass": "net.minecraft.client.main.Main",
              "downloads": {
                "client": { "sha1": "{{Sha1(VanillaJar)}}", "size": {{VanillaJar.Length}}, "url": "https://piston-data.mojang.com/v1/objects/{{Sha1(VanillaJar)}}/client.jar" },
                "client_mappings": { "sha1": "{{Sha1(Mappings)}}", "size": {{Mappings.Length}}, "url": "{{mappingsUrl}}" }
              },
              "libraries": []
            }
            """);
        File.WriteAllBytes(_root.GetPath($"versions/{version}/{version}.jar"), VanillaJar);
    }

    private void ServeLegacyInstaller(bool stripMeta)
    {
        _server.Add(LegacyInstallerUrl, CreateLegacyInstaller(stripMeta));
        _server.Add(ScalaUrl, Scala);
        _server.Add(LaunchWrapperUrl, LaunchWrapper);
    }

    /// <summary>
    /// 仿照 Forge 1.6.4 的旧版安装器：versionInfo 不继承原版，universal jar 放在安装器根目录。
    /// </summary>
    private static byte[] CreateLegacyInstaller(bool stripMeta)
    {
        var profile = $$"""
            {
              "install": {
                "profileName": "Forge",
                "target": "1.6.4-Forge9.11.1.1345",
                "path": "net.minecraftforge:minecraftforge:9.11.1.1345",
                "filePath": "minecraftforge-universal-1.6.4-9.11.1.1345.jar",
                "minecraft": "1.6.4",
                "stripMeta": {{(stripMeta ? "true" : "false")}}
              },
              "versionInfo": {
                "id": "1.6.4-Forge9.11.1.1345",
                "type": "release",
                "minecraftArguments": "--username ${auth_player_name} --tweakClass cpw.mods.fml.common.launcher.FMLTweaker",
                "mainClass": "net.minecraft.launchwrapper.Launch",
                "libraries": [
                  { "name": "net.minecraftforge:minecraftforge:9.11.1.1345", "url": "https://maven.minecraftforge.net/" },
                  { "name": "net.minecraft:launchwrapper:1.8", "serverreq": true },
                  { "name": "org.scala-lang:scala-library:2.10.2", "url": "https://maven.minecraftforge.net/", "clientreq": true, "serverreq": true }
                ]
              }
            }
            """;
        return CreateJar(
            ("install_profile.json", Encoding.UTF8.GetBytes(profile)),
            ("minecraftforge-universal-1.6.4-9.11.1.1345.jar", Universal));
    }

    /// <summary>
    /// 仿照 NeoForge 21.1 的新版安装器：自带 universal jar 与二进制补丁，处理器由 <paramref name="processors"/> 给出。
    /// </summary>
    private void ServeModernInstaller(string processors)
    {
        var profile = $$"""
            {
              "spec": 1,
              "profile": "NeoForge",
              "version": "neoforge-21.1.77",
              "minecraft": "1.21.1",
              "json": "/version.json",
              "data": {
                "MOJMAPS": {
                  "client": "[net.minecraft:client:1.21.1-20240808.144430:mappings@txt]",
                  "server": "[net.minecraft:server:1.21.1-20240808.144430:mappings@txt]"
                },
                "BINPATCH": { "client": "/data/client.lzma", "server": "/data/server.lzma" },
                "PATCHED": { "client": "[net.neoforged:neoforge:21.1.77:client]", "server": "[net.neoforged:neoforge:21.1.77:server]" },
                "PATCHED_SHA": { "client": "'{{Sha1(Patched)}}'", "server": "'0000'" }
              },
              "processors": [ {{processors}} ],
              "libraries": [
                {{Library("net.neoforged.installertools:installertools:2.1.2", "net/neoforged/installertools/installertools/2.1.2/installertools-2.1.2.jar", InstallerToolsUrl, InstallerTools)}},
                {{Library("net.neoforged:neoforge:21.1.77:universal", "net/neoforged/neoforge/21.1.77/neoforge-21.1.77-universal.jar", "", BundledUniversal)}}
              ]
            }
            """;
        var version = $$"""
            {
              "id": "neoforge-21.1.77",
              "inheritsFrom": "1.21.1",
              "type": "release",
              "mainClass": "cpw.mods.bootstraplauncher.BootstrapLauncher",
              "arguments": { "game": [ "--launchTarget", "forgeclient" ], "jvm": [] },
              "libraries": [
                {{Library("net.neoforged:bus:8.0.2", "net/neoforged/bus/8.0.2/bus-8.0.2.jar", EventBusUrl, EventBus)}},
                {{Library("net.neoforged:neoforge:21.1.77:universal", "net/neoforged/neoforge/21.1.77/neoforge-21.1.77-universal.jar", "", BundledUniversal)}}
              ]
            }
            """;
        _server.Add(ModernInstallerUrl, CreateJar(
            ("install_profile.json", Encoding.UTF8.GetBytes(profile)),
            ("version.json", Encoding.UTF8.GetBytes(version)),
            ("maven/net/neoforged/neoforge/21.1.77/neoforge-21.1.77-universal.jar", BundledUniversal),
            ("data/client.lzma", "binary patches"u8.ToArray())));
        _server.Add(InstallerToolsUrl, InstallerTools);
        _server.Add(EventBusUrl, EventBus);
    }

    private static string Library(string name, string path, string url, byte[] content)
    {
        return $$"""
            { "name": "{{name}}", "downloads": { "artifact": { "path": "{{path}}", "url": "{{url}}", "sha1": "{{Sha1(content)}}", "size": {{content.Length}} } } }
            """;
    }

    private JsonNode ReadInstanceJson(string instanceName)
    {
        return JsonNode.Parse(File.ReadAllText(_root.GetPath($"versions/{instanceName}/{instanceName}.json")))!;
    }

    private byte[] ReadLibrary(string relativePath)
    {
        return File.ReadAllBytes(_root.GetPath($"libraries/{relativePath}"));
    }

    private void AssertTemporaryFilesRemoved()
    {
        CollectionAssert.AreEqual(Array.Empty<string>(), Directory.GetDirectories(_root.RootPath, ".pcl-shion-*"));
    }

    private static byte[] CreateJar(params (string Name, byte[] Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(content);
            }
        }
        return stream.ToArray();
    }

    private static string Sha1(byte[] data)
    {
        return Convert.ToHexStringLower(SHA1.HashData(data));
    }

    private sealed class ReportList : IProgress<InstallProgress>
    {
        private readonly List<InstallProgress> _reports = [];

        public void Report(InstallProgress value)
        {
            lock (_reports)
                _reports.Add(value);
        }

        /// <summary>依次进入过的阶段。</summary>
        public string[] Stages()
        {
            lock (_reports)
                return _reports.Where(report => report.Download is null).Select(report => report.Stage).ToArray();
        }
    }
}
