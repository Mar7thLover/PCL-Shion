using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Launch;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Sources;
using PCL.Shion.Core.Versions;

namespace PCL.Shion.Core.Test.Install.EndToEnd;

/// <summary>
/// 从版本清单开始，经由真实的 <see cref="DownloadEngine"/> 与 <see cref="GameInstaller"/>，
/// 对着内存里模拟的 Mojang 官方服务与 BMCLAPI 镜像安装原版，并检查磁盘上的结果。
/// </summary>
[TestClass]
public sealed class GameInstallerEndToEndTest
{
    private const string Instance = FakeMojangRelease.VersionId;

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(60);

    private FakeMinecraftRoot _root = null!;
    private FakeMojangHandler _server = null!;
    private FakeMojangRelease _release = null!;
    private CancellationTokenSource _timeout = null!;

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => _timeout.Token;

    [TestInitialize]
    public void Setup()
    {
        _root = new FakeMinecraftRoot();
        _server = new FakeMojangHandler();
        _release = new FakeMojangRelease(_server);
        _timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        _timeout.CancelAfter(TestTimeout);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _timeout.Dispose();
        _root.Dispose();
    }

    [TestMethod]
    [DataRow("1.21.1")]
    [DataRow("daily-survival")]
    public async Task InstallsEveryRequiredFile(string instanceName)
    {
        await InstallAsync(instanceName, DownloadSource.Official);

        AssertInstalled(instanceName);
    }

    [TestMethod]
    public async Task InstalledInstanceHasNothingLeftToDownload()
    {
        await InstallAsync(Instance, DownloadSource.Official);

        var plan = LaunchPlanner.Build(new LaunchRequest(_root.RootPath, Instance, OfflineAccount.Create("Steve"), "java"));
        Assert.HasCount(0, plan.MissingFiles);

        var version = VersionResolver.Resolve(_root.RootPath, Instance);
        var requirements = RequirementCollector.Collect(_root.RootPath, version, DownloadSource.Official)
            .Concat(RequirementCollector.CollectAssetObjects(_root.RootPath, version.AssetIndexId, DownloadSource.Official))
            .ToArray();
        Assert.IsNotEmpty(requirements);
        foreach (var item in requirements)
            Assert.IsTrue(FileVerifier.Matches(item.TargetPath, item.Size, item.Sha1), item.TargetPath);
    }

    [TestMethod]
    public async Task NeverRequestsNativesOfOtherSystems()
    {
        await InstallAsync(Instance, DownloadSource.Official);

        foreach (var os in FakeMojangRelease.OtherOsNames)
            Assert.IsFalse(_server.Requests.Any(uri => uri.AbsolutePath.Contains($"natives-{os}")), os);
    }

    [TestMethod]
    public async Task ReportsInstallStagesInOrder()
    {
        var progress = new RecordingProgress<InstallProgress>();

        await InstallAsync(Instance, DownloadSource.Official, progress);

        CollectionAssert.AreEqual(new[] { "version", "files", "assets" }, GetStages(progress.Reports));
    }

    [TestMethod]
    public async Task CompleteRestoresDeletedFiles()
    {
        await InstallAsync(Instance, DownloadSource.Official);
        var progress = new RecordingProgress<InstallProgress>();
        string[] deleted =
        [
            FakeMojangRelease.ClientJarPath(Instance),
            FakeMojangRelease.GsonJarPath,
            FakeMojangRelease.LogConfigPath,
            FakeMojangRelease.AssetIndexPath,
            _release.AssetObjectPath(FakeMojangRelease.LanguageAsset)
        ];
        foreach (var path in deleted)
            File.Delete(_root.GetPath(path));

        var result = await CompleteAsync(Instance, DownloadSource.Official, progress);

        Assert.IsTrue(result.Succeeded);
        AssertInstalled(Instance);
        CollectionAssert.AreEqual(new[] { "files", "assets" }, GetStages(progress.Reports));
    }

    [TestMethod]
    public async Task CompleteReplacesCorruptedFiles()
    {
        await InstallAsync(Instance, DownloadSource.Official);
        // 与原文件等长的错误内容只能靠校验和发现
        Tamper(FakeMojangRelease.ClientJarPath(Instance), keepSize: true);
        Tamper(FakeMojangRelease.GsonJarPath, keepSize: true);
        Tamper(FakeMojangRelease.LogConfigPath, keepSize: false);
        Tamper(_release.AssetObjectPath(FakeMojangRelease.LanguageAsset), keepSize: false);

        var result = await CompleteAsync(Instance, DownloadSource.Official);

        Assert.IsTrue(result.Succeeded);
        AssertInstalled(Instance);
    }

    [TestMethod]
    public async Task CompleteDownloadsNothingWhenEverythingIsValid()
    {
        await InstallAsync(Instance, DownloadSource.Official);
        _server.ClearRequests();

        var result = await CompleteAsync(Instance, DownloadSource.Official);

        Assert.IsTrue(result.Succeeded);
        Assert.HasCount(0, _server.Requests);
    }

    [TestMethod]
    [DataRow(DownloadSource.Official, DownloadSource.Bmclapi)]
    [DataRow(DownloadSource.Bmclapi, DownloadSource.Official)]
    public async Task FallsBackToTheOtherSourceWhenThePreferredOneIsDown(DownloadSource preferred, DownloadSource fallback)
    {
        _server.TakeOffline(preferred);

        await InstallAsync(Instance, preferred);

        AssertInstalled(Instance);
        var sources = _server.Requests.Select(FakeMojangHandler.SourceOf).ToArray();
        Assert.AreEqual(preferred, sources[0]);
        Assert.Contains(fallback, sources);
    }

    [TestMethod]
    public async Task RejectsCorruptedDownloadsAndTriesTheOtherSource()
    {
        _server.ServeCorrupted(DownloadSource.Bmclapi, _release.ClientJarUrl);
        _server.ServeCorrupted(DownloadSource.Bmclapi, _release.AssetObjectUrl(FakeMojangRelease.LanguageAsset));

        await InstallAsync(Instance, DownloadSource.Bmclapi);

        AssertInstalled(Instance);
        Assert.IsTrue(_server.Requests.Any(uri =>
            FakeMojangHandler.SourceOf(uri) == DownloadSource.Official && uri.AbsolutePath.EndsWith("/client.jar")));
    }

    [TestMethod]
    public async Task FailsWhenAFileIsAvailableFromNoSource()
    {
        _server.Remove(_release.LwjglJarUrl);

        await Assert.ThrowsAsync<InstallException>(() => InstallAsync(Instance, DownloadSource.Official));
    }

    [TestMethod]
    public async Task CancellingLeavesNoPartialFilesBehind()
    {
        var stalled = _server.Stall(_release.ClientJarUrl);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        using var engine = CreateEngine();
        var version = await FetchVersionAsync(engine, DownloadSource.Official);

        var install = new GameInstaller(engine, DownloadSource.Official)
            .InstallVanillaAsync(_root.RootPath, version, Instance, null, cancellation.Token);
        await Task.WhenAny(stalled, install).WaitAsync(Token);
        Assert.IsTrue(stalled.IsCompleted, "the installation ended before reaching the stalled download");
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => install);
        var finished = _release.GetInstalledFiles(Instance).Keys.Append(FakeMojangRelease.VersionJsonPath(Instance));
        CollectionAssert.IsSubsetOf(ListFiles(_root.RootPath), finished.ToArray());
        Assert.IsFalse(File.Exists(_root.GetPath(FakeMojangRelease.ClientJarPath(Instance))));
    }

    private DownloadEngine CreateEngine()
    {
        return new DownloadEngine(new DownloadOptions { MaxRetries = 1 }, _server);
    }

    private async Task<RemoteVersion> FetchVersionAsync(DownloadEngine engine, DownloadSource source)
    {
        var manifest = await VersionManifestClient.FetchAsync(engine.Http, source, Token);
        return manifest.Versions.Single(version => version.Id == FakeMojangRelease.VersionId);
    }

    private async Task InstallAsync(string instanceName, DownloadSource source, IProgress<InstallProgress>? progress = null)
    {
        using var engine = CreateEngine();
        var version = await FetchVersionAsync(engine, source);
        await new GameInstaller(engine, source).InstallVanillaAsync(_root.RootPath, version, instanceName, progress, Token);
    }

    private async Task<DownloadResult> CompleteAsync(string instanceName, DownloadSource source, IProgress<InstallProgress>? progress = null)
    {
        using var engine = CreateEngine();
        return await new GameInstaller(engine, source).CompleteAsync(_root.RootPath, instanceName, progress, Token);
    }

    /// <summary>
    /// 磁盘上恰好是应有的文件：一个不多、一个不少，内容逐字节一致，版本 JSON 的 id 已改成实例名。
    /// </summary>
    private void AssertInstalled(string instanceName)
    {
        var expected = _release.GetInstalledFiles(instanceName);
        var versionJsonPath = FakeMojangRelease.VersionJsonPath(instanceName);

        CollectionAssert.AreEqual(
            expected.Keys.Append(versionJsonPath).Order(StringComparer.Ordinal).ToArray(),
            ListFiles(_root.RootPath));
        foreach (var (path, content) in expected)
            CollectionAssert.AreEqual(content, File.ReadAllBytes(_root.GetPath(path)), path);

        using var versionJson = JsonDocument.Parse(File.ReadAllText(_root.GetPath(versionJsonPath)));
        Assert.AreEqual(instanceName, versionJson.RootElement.GetProperty("id").GetString());
        Assert.AreEqual("net.minecraft.client.main.Main", versionJson.RootElement.GetProperty("mainClass").GetString());
    }

    /// <summary>
    /// 把文件改成错误内容：<paramref name="keepSize"/> 为 true 时保持长度，否则截成一半。
    /// </summary>
    private void Tamper(string relativePath, bool keepSize)
    {
        var path = _root.GetPath(relativePath);
        var original = File.ReadAllBytes(path);
        File.WriteAllBytes(path, keepSize ? original.Select(value => (byte)~value).ToArray() : original[..(original.Length / 2)]);
    }

    private static string[] ListFiles(string root)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 按出现顺序列出阶段，相邻的相同阶段合并。
    /// </summary>
    private static string[] GetStages(IEnumerable<InstallProgress> reports)
    {
        var stages = new List<string>();
        foreach (var report in reports)
        {
            if (stages.Count == 0 || stages[^1] != report.Stage)
                stages.Add(report.Stage);
        }
        return [.. stages];
    }

    /// <summary>
    /// 同步记录进度（<see cref="Progress{T}"/> 会异步投递，顺序无法保证）。
    /// </summary>
    private sealed class RecordingProgress<T> : IProgress<T>
    {
        private readonly List<T> _reports = [];

        public IReadOnlyList<T> Reports
        {
            get
            {
                lock (_reports)
                    return _reports.ToArray();
            }
        }

        public void Report(T value)
        {
            lock (_reports)
                _reports.Add(value);
        }
    }
}
