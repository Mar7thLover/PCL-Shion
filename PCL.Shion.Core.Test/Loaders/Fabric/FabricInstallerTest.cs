using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Loaders;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Test.Loaders.Fabric;

[TestClass]
public sealed class FabricInstallerTest
{
    private FakeMinecraftRoot _root = null!;
    private FakeMetaHandler _server = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = new FakeMinecraftRoot();
        _server = FabricTestData.CreateServer();
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        _root.Dispose();
    }

    [TestMethod]
    public async Task WritesTheFabricProfileUnderTheInstanceName()
    {
        InstallVanilla();
        using var engine = CreateEngine();

        await InstallAsync(engine, FabricTestData.FabricLoader, "My Fabric");

        var json = JsonNode.Parse(File.ReadAllText(_root.GetPath("versions/My Fabric/My Fabric.json")))!;
        Assert.AreEqual("My Fabric", json["id"]!.GetValue<string>());
        Assert.AreEqual("1.21.1", json["inheritsFrom"]!.GetValue<string>());
        Assert.AreEqual("net.fabricmc.loader.impl.launch.knot.KnotClient", json["mainClass"]!.GetValue<string>());
        Assert.AreEqual(FabricTestData.FabricJvmArgument, json["arguments"]!["jvm"]![0]!.GetValue<string>());
        Assert.AreEqual(2, json["libraries"]!.AsArray().Count);
    }

    [TestMethod]
    public async Task DownloadsTheFabricLibraries()
    {
        InstallVanilla();
        using var engine = CreateEngine();

        await InstallAsync(engine, FabricTestData.FabricLoader, "fabric");

        CollectionAssert.AreEqual(FabricTestData.IntermediaryJar, File.ReadAllBytes(_root.GetPath(FabricTestData.IntermediaryJarPath)));
        CollectionAssert.AreEqual(FabricTestData.FabricLoaderJar, File.ReadAllBytes(_root.GetPath(FabricTestData.FabricLoaderJarPath)));
    }

    [TestMethod]
    public async Task FabricInstanceResolvesOnTopOfTheVanillaVersion()
    {
        InstallVanilla();
        using var engine = CreateEngine();

        await InstallAsync(engine, FabricTestData.FabricLoader, "fabric");

        var version = VersionResolver.Resolve(_root.RootPath, "fabric");
        Assert.AreEqual("fabric", version.Id);
        Assert.AreEqual("fabric", version.Loader);
        Assert.AreEqual("1.21.1", version.MinecraftVersion);
        Assert.AreEqual("net.fabricmc.loader.impl.launch.knot.KnotClient", version.MainClass);
        Assert.AreEqual(_root.GetPath("versions/1.21.1/1.21.1.jar"), version.ClientJarPath);
        CollectionAssert.AreEqual(
            new[] { "net.fabricmc:intermediary:1.21.1", "net.fabricmc:fabric-loader:0.16.9" },
            version.Libraries.Select(library => library.Name).ToArray());
    }

    [TestMethod]
    public async Task InstallsQuiltTheSameWay()
    {
        InstallVanilla();
        using var engine = CreateEngine();

        await InstallAsync(engine, FabricTestData.QuiltLoader, "quilt");

        var version = VersionResolver.Resolve(_root.RootPath, "quilt");
        Assert.AreEqual("quilt", version.Loader);
        Assert.AreEqual("1.21.1", version.MinecraftVersion);
        Assert.AreEqual("org.quiltmc.loader.impl.launch.knot.KnotClient", version.MainClass);
        var json = JsonNode.Parse(File.ReadAllText(_root.GetPath("versions/quilt/quilt.json")))!;
        Assert.AreEqual("quilt", json["id"]!.GetValue<string>());
        CollectionAssert.AreEqual(FabricTestData.IntermediaryJar, File.ReadAllBytes(_root.GetPath(FabricTestData.IntermediaryJarPath)));
        CollectionAssert.AreEqual(FabricTestData.QuiltLoaderJar, File.ReadAllBytes(_root.GetPath(FabricTestData.QuiltLoaderJarPath)));
    }

    [TestMethod]
    public async Task LeavesAnInstalledVanillaVersionUntouched()
    {
        InstallVanilla();
        using var engine = CreateEngine();

        await InstallAsync(engine, FabricTestData.FabricLoader, "fabric");

        Assert.AreEqual(FabricTestData.InstalledVanillaJson, File.ReadAllText(_root.GetPath("versions/1.21.1/1.21.1.json")));
        Assert.IsFalse(_server.Requests.Any(uri => uri.AbsolutePath == new Uri(FabricTestData.ManifestUrl).AbsolutePath));
    }

    [TestMethod]
    public async Task InstallsTheVanillaVersionFirstWhenItIsMissing()
    {
        using var engine = CreateEngine();

        await InstallAsync(engine, FabricTestData.FabricLoader, "fabric");

        var vanilla = JsonNode.Parse(File.ReadAllText(_root.GetPath("versions/1.21.1/1.21.1.json")))!;
        Assert.AreEqual("1.21.1", vanilla["id"]!.GetValue<string>());
        CollectionAssert.AreEqual(FabricTestData.ClientJar, File.ReadAllBytes(_root.GetPath("versions/1.21.1/1.21.1.jar")));
        var version = VersionResolver.Resolve(_root.RootPath, "fabric");
        Assert.AreEqual("fabric", version.Loader);
        Assert.AreEqual(_root.GetPath("versions/1.21.1/1.21.1.jar"), version.ClientJarPath);
        CollectionAssert.AreEqual(FabricTestData.FabricLoaderJar, File.ReadAllBytes(_root.GetPath(FabricTestData.FabricLoaderJarPath)));
    }

    [TestMethod]
    public async Task RejectsAGameVersionMissingFromTheManifest()
    {
        using var engine = CreateEngine();
        var loader = FabricTestData.FabricLoader with { MinecraftVersion = "9.9.9" };

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => InstallAsync(engine, loader, "fabric"));

        Assert.IsFalse(Directory.Exists(_root.GetPath("versions")));
    }

    [TestMethod]
    public async Task RejectsAnInstanceNamedLikeTheGameVersion()
    {
        using var engine = CreateEngine();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => InstallAsync(engine, FabricTestData.FabricLoader, "1.21.1"));

        Assert.IsFalse(Directory.Exists(_root.GetPath("versions")));
        Assert.IsEmpty(_server.Requests);
    }

    [TestMethod]
    public async Task FetchesTheProfileFromTheMirrorWhenItIsPreferred()
    {
        InstallVanilla();
        using var engine = CreateEngine();

        await new LoaderInstaller(engine, DownloadSource.Bmclapi)
            .InstallAsync(_root.RootPath, FabricTestData.FabricLoader, "fabric", "java", null, CancellationToken.None);

        Assert.AreEqual(FabricTestData.FabricProfileMirrorUrl, _server.Requests.First().AbsoluteUri);
        Assert.AreEqual("fabric", VersionResolver.Resolve(_root.RootPath, "fabric").Loader);
    }

    [TestMethod]
    public async Task FallsBackToTheMirrorWhenTheOfficialServiceIsDown()
    {
        InstallVanilla();
        _server.TakeOffline(FabricTestData.FabricHost);
        using var engine = CreateEngine();

        await InstallAsync(engine, FabricTestData.FabricLoader, "fabric");

        CollectionAssert.AreEqual(
            new[] { FabricTestData.FabricProfileUrl, FabricTestData.FabricProfileMirrorUrl },
            _server.Requests.Take(2).Select(uri => uri.AbsoluteUri).ToArray());
        Assert.AreEqual("fabric", VersionResolver.Resolve(_root.RootPath, "fabric").Loader);
    }

    [TestMethod]
    public async Task FailsWithoutWritingTheInstanceWhenNoSourceHasTheProfile()
    {
        InstallVanilla();
        _server.Remove(FabricTestData.FabricProfileUrl);
        _server.Remove(FabricTestData.FabricProfileMirrorUrl);
        using var engine = CreateEngine();

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => InstallAsync(engine, FabricTestData.FabricLoader, "fabric"));

        Assert.IsFalse(Directory.Exists(_root.GetPath("versions/fabric")));
    }

    [TestMethod]
    public async Task FailedLibraryRaisesInstallException()
    {
        InstallVanilla();
        _server.Remove(FabricTestData.FabricLoaderJarUrl);
        using var engine = CreateEngine();

        var exception = await Assert.ThrowsExactlyAsync<InstallException>(() => InstallAsync(engine, FabricTestData.FabricLoader, "fabric"));

        Assert.AreEqual(_root.GetPath(FabricTestData.FabricLoaderJarPath), exception.FailedFiles.Single().TargetPath);
        Assert.IsTrue(File.Exists(_root.GetPath(FabricTestData.IntermediaryJarPath)));
    }

    [TestMethod]
    public async Task ReportsTheVersionStageBeforeTheFileStage()
    {
        InstallVanilla();
        var progress = new RecordingProgress();
        using var engine = CreateEngine();

        await new LoaderInstaller(engine, DownloadSource.Official)
            .InstallAsync(_root.RootPath, FabricTestData.FabricLoader, "fabric", "java", progress, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "version", "files" }, progress.Reports.Select(report => report.Stage).Distinct().ToArray());
    }

    private void InstallVanilla()
    {
        _root.WriteVersion("1.21.1", FabricTestData.InstalledVanillaJson);
    }

    private Task InstallAsync(DownloadEngine engine, LoaderVersion loader, string instanceName)
    {
        return new LoaderInstaller(engine, DownloadSource.Official)
            .InstallAsync(_root.RootPath, loader, instanceName, "java", null, CancellationToken.None);
    }

    private DownloadEngine CreateEngine()
    {
        return new DownloadEngine(new DownloadOptions { RetryDelay = TimeSpan.Zero }, _server);
    }

    /// <summary>
    /// 同步记录收到的进度，避免 <see cref="Progress{T}"/> 把回调推迟到线程池。
    /// </summary>
    private sealed class RecordingProgress : IProgress<InstallProgress>
    {
        private readonly ConcurrentQueue<InstallProgress> _reports = new();

        public IReadOnlyCollection<InstallProgress> Reports => _reports;

        public void Report(InstallProgress value)
        {
            _reports.Enqueue(value);
        }
    }
}
