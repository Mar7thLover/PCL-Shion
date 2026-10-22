using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Java.Runtime;
using PCL.Shion.Core.Platform;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Test.Java.Runtime;

[TestClass]
public sealed class JavaRuntimeInstallerTest
{
    private const string Component = "java-runtime-test";

    private static readonly byte[] JavaBinary = Encoding.UTF8.GetBytes("java binary");

    private static readonly byte[] License = Encoding.UTF8.GetBytes("license text");

    private static readonly byte[] ReleaseFile = Encoding.UTF8.GetBytes("JAVA_VERSION=\"21.0.7\"\nMODULES=\"java.base\"\n");

    [TestMethod]
    [DataRow(OsKind.Windows, Architecture.X64, "windows-x64")]
    [DataRow(OsKind.Windows, Architecture.Arm64, "windows-arm64")]
    [DataRow(OsKind.Windows, Architecture.X86, "windows-x86")]
    [DataRow(OsKind.MacOs, Architecture.X64, "mac-os")]
    [DataRow(OsKind.MacOs, Architecture.Arm64, "mac-os-arm64")]
    [DataRow(OsKind.Linux, Architecture.X64, "linux")]
    [DataRow(OsKind.Linux, Architecture.X86, "linux-i386")]
    public void GetPlatformKey_MapsSupportedPlatforms(OsKind os, Architecture architecture, string expected)
    {
        Assert.AreEqual(expected, JavaRuntimeInstaller.GetPlatformKey(os, architecture));
    }

    [TestMethod]
    [DataRow(OsKind.Linux, Architecture.Arm64)]
    [DataRow(OsKind.Linux, Architecture.Arm)]
    [DataRow(OsKind.MacOs, Architecture.X86)]
    [DataRow(OsKind.Windows, Architecture.Arm)]
    public void GetPlatformKey_ReturnsNullWhenMojangPublishesNoRuntime(OsKind os, Architecture architecture)
    {
        Assert.IsNull(JavaRuntimeInstaller.GetPlatformKey(os, architecture));
    }

    [TestMethod]
    public void Locate_ReadsTheVersionFromAReleaseFileWithoutArchitecture()
    {
        using var root = new FakeMinecraftRoot();
        var directory = CreateRuntime(root, "JAVA_VERSION=\"25.0.1\"\nMODULES=\"java.base java.desktop\"\n");

        var runtime = JavaRuntimeInstaller.Locate(directory, "windows-x64");

        Assert.AreEqual(25, runtime.MajorVersion);
        Assert.AreEqual("25.0.1", runtime.Version);
        Assert.IsTrue(runtime.Is64Bit);
        Assert.AreEqual("Launcher", runtime.Source);
    }

    [TestMethod]
    public void Locate_ReadsLegacyVersionNumbers()
    {
        using var root = new FakeMinecraftRoot();
        var directory = CreateRuntime(root, "JAVA_VERSION=\"1.8.0_51\"\r\nOS_NAME=\"Windows\"\r\n");

        var runtime = JavaRuntimeInstaller.Locate(directory, "windows-x64");

        Assert.AreEqual(8, runtime.MajorVersion);
        Assert.AreEqual("1.8.0_51", runtime.Version);
    }

    [TestMethod]
    [DataRow("windows-x64", true)]
    [DataRow("windows-arm64", true)]
    [DataRow("mac-os", true)]
    [DataRow("mac-os-arm64", true)]
    [DataRow("linux", true)]
    [DataRow("windows-x86", false)]
    [DataRow("linux-i386", false)]
    public void Locate_DerivesTheBitnessFromThePlatformKey(string platformKey, bool expected)
    {
        using var root = new FakeMinecraftRoot();
        var directory = CreateRuntime(root, "JAVA_VERSION=\"17.0.8\"\n");

        Assert.AreEqual(expected, JavaRuntimeInstaller.Locate(directory, platformKey).Is64Bit);
    }

    [TestMethod]
    public void Locate_ReturnsTheExecutableUsedToLaunchTheGame()
    {
        using var root = new FakeMinecraftRoot();
        var directory = CreateRuntime(root, "JAVA_VERSION=\"21.0.7\"\n");

        var runtime = JavaRuntimeInstaller.Locate(directory, "windows-x64");

        var launcher = OperatingSystem.IsWindows() ? "javaw.exe" : "java";
        Assert.AreEqual(Path.Combine(HomeOf(directory), "bin", launcher), runtime.Executable);
    }

    [TestMethod]
    public void Locate_FallsBackToJavaWhenThereIsNoWindowedLauncher()
    {
        using var root = new FakeMinecraftRoot();
        var directory = CreateRuntime(root, "JAVA_VERSION=\"21.0.7\"\n", withWindowedLauncher: false);

        var runtime = JavaRuntimeInstaller.Locate(directory, "windows-x64");

        var launcher = OperatingSystem.IsWindows() ? "java.exe" : "java";
        Assert.AreEqual(Path.Combine(HomeOf(directory), "bin", launcher), runtime.Executable);
    }

    [TestMethod]
    public void Locate_RejectsAReleaseFileWithoutAVersion()
    {
        using var root = new FakeMinecraftRoot();
        var directory = CreateRuntime(root, "MODULES=\"java.base\"\n");

        Assert.ThrowsExactly<InvalidDataException>(() => JavaRuntimeInstaller.Locate(directory, "windows-x64"));
    }

    [TestMethod]
    public async Task InstallAsync_DownloadsTheRuntimeIntoTheLauncherLayout()
    {
        var platformKey = GetPlatformKeyOrSkip();
        var server = new FakeRuntimeServer(platformKey);
        using var root = new FakeMinecraftRoot();
        using var engine = new DownloadEngine(handler: new FakeHandler(server.Responses));

        var runtime = await JavaRuntimeInstaller.InstallAsync(
            root.RootPath, Component, engine, DownloadSource.Official, null, CancellationToken.None);

        var directory = root.GetPath($"runtime/{Component}/{platformKey}/{Component}");
        var home = HomeOf(directory);
        var java = Path.Combine(home, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java");
        Assert.AreEqual(java, runtime.Executable);
        Assert.AreEqual(21, runtime.MajorVersion);
        Assert.AreEqual("21.0.7", runtime.Version);
        CollectionAssert.AreEqual(JavaBinary, File.ReadAllBytes(java));
        CollectionAssert.AreEqual(License, File.ReadAllBytes(Path.Combine(home, "legal", "java.base", "LICENSE")));
        Assert.IsTrue(Directory.Exists(Path.Combine(home, "legal", "java.compiler")));
    }

    [TestMethod]
    public async Task InstallAsync_SetsExecutableBitsAndCreatesLinksOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Executable bits and symbolic links only exist on Unix.");
            return;
        }

        var platformKey = GetPlatformKeyOrSkip();
        var server = new FakeRuntimeServer(platformKey);
        using var root = new FakeMinecraftRoot();
        using var engine = new DownloadEngine(handler: new FakeHandler(server.Responses));

        var runtime = await JavaRuntimeInstaller.InstallAsync(
            root.RootPath, Component, engine, DownloadSource.Official, null, CancellationToken.None);

        Assert.IsTrue(File.GetUnixFileMode(runtime.Executable).HasFlag(UnixFileMode.UserExecute));
        var home = HomeOf(root.GetPath($"runtime/{Component}/{platformKey}/{Component}"));
        var link = Path.Combine(home, "legal", "java.compiler", "LICENSE");
        Assert.AreEqual("../java.base/LICENSE", new FileInfo(link).LinkTarget);
        CollectionAssert.AreEqual(License, File.ReadAllBytes(link));
    }

    [TestMethod]
    public async Task InstallAsync_CanBeRunAgainOverAnExistingInstallation()
    {
        var platformKey = GetPlatformKeyOrSkip();
        var server = new FakeRuntimeServer(platformKey);
        using var root = new FakeMinecraftRoot();
        using var engine = new DownloadEngine(handler: new FakeHandler(server.Responses));

        var first = await JavaRuntimeInstaller.InstallAsync(
            root.RootPath, Component, engine, DownloadSource.Official, null, CancellationToken.None);
        var second = await JavaRuntimeInstaller.InstallAsync(
            root.RootPath, Component, engine, DownloadSource.Official, null, CancellationToken.None);

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public async Task InstallAsync_ReportsTheJavaStage()
    {
        var platformKey = GetPlatformKeyOrSkip();
        var server = new FakeRuntimeServer(platformKey);
        using var root = new FakeMinecraftRoot();
        using var engine = new DownloadEngine(handler: new FakeHandler(server.Responses));
        var progress = new ReportList();

        await JavaRuntimeInstaller.InstallAsync(
            root.RootPath, Component, engine, DownloadSource.Official, progress, CancellationToken.None);

        var reports = progress.Snapshot();
        Assert.IsNull(reports[0].Download);
        Assert.IsTrue(reports.All(report => report.Stage == "java"));
    }

    [TestMethod]
    public async Task InstallAsync_UsesTheMirrorWhenTheOfficialIndexIsUnavailable()
    {
        var platformKey = GetPlatformKeyOrSkip();
        var server = new FakeRuntimeServer(platformKey);
        var officialIndex = new Uri(JavaRuntimeInstaller.RuntimeIndexUrl);
        var mirrorIndex = MirrorUrls.Resolve(JavaRuntimeInstaller.RuntimeIndexUrl, DownloadSource.Official)[1];
        server.Responses[mirrorIndex] = server.Responses[officialIndex];
        server.Responses.Remove(officialIndex);
        using var root = new FakeMinecraftRoot();
        using var engine = new DownloadEngine(handler: new FakeHandler(server.Responses));

        var runtime = await JavaRuntimeInstaller.InstallAsync(
            root.RootPath, Component, engine, DownloadSource.Official, null, CancellationToken.None);

        Assert.AreEqual(21, runtime.MajorVersion);
    }

    [TestMethod]
    public async Task InstallAsync_UsesTheMirrorWhenTheOfficialManifestDoesNotMatchItsHash()
    {
        var platformKey = GetPlatformKeyOrSkip();
        var server = new FakeRuntimeServer(platformKey);
        var mirrorManifest = MirrorUrls.Resolve(server.ManifestUrl.AbsoluteUri, DownloadSource.Official)[1];
        server.Responses[mirrorManifest] = server.Responses[server.ManifestUrl];
        server.Responses[server.ManifestUrl] = Encoding.UTF8.GetBytes("""{ "files": {} }""");
        using var root = new FakeMinecraftRoot();
        using var engine = new DownloadEngine(handler: new FakeHandler(server.Responses));

        var runtime = await JavaRuntimeInstaller.InstallAsync(
            root.RootPath, Component, engine, DownloadSource.Official, null, CancellationToken.None);

        Assert.AreEqual(21, runtime.MajorVersion);
    }

    [TestMethod]
    public async Task InstallAsync_RejectsAComponentThePlatformDoesNotHave()
    {
        var platformKey = GetPlatformKeyOrSkip();
        var server = new FakeRuntimeServer(platformKey);
        using var root = new FakeMinecraftRoot();
        using var engine = new DownloadEngine(handler: new FakeHandler(server.Responses));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => JavaRuntimeInstaller.InstallAsync(
            root.RootPath, "jre-legacy", engine, DownloadSource.Official, null, CancellationToken.None));
    }

    private static string GetPlatformKeyOrSkip()
    {
        var platformKey = JavaRuntimeInstaller.GetPlatformKey();
        if (platformKey is null)
            Assert.Inconclusive("Mojang publishes no Java runtime for this platform.");
        return platformKey;
    }

    // 官方启动器的 macOS 运行时位于 jre.bundle/Contents/Home，其他系统直接位于组件目录
    private static string HomeOf(string componentDirectory) => PlatformInfo.Os == OsKind.MacOs
        ? Path.Combine(componentDirectory, "jre.bundle", "Contents", "Home")
        : componentDirectory;

    private static string CreateRuntime(FakeMinecraftRoot root, string release, bool withWindowedLauncher = true)
    {
        var directory = root.GetPath("runtime");
        var home = HomeOf(directory);
        Directory.CreateDirectory(Path.Combine(home, "bin"));
        File.WriteAllText(Path.Combine(home, "release"), release);
        File.WriteAllText(Path.Combine(home, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java"), string.Empty);
        if (withWindowedLauncher && OperatingSystem.IsWindows())
            File.WriteAllText(Path.Combine(home, "bin", "javaw.exe"), string.Empty);
        return directory;
    }

    /// <summary>
    /// 在内存里提供运行时索引、清单和文件，布局与 Mojang 的发布一致。
    /// </summary>
    private sealed class FakeRuntimeServer
    {
        public FakeRuntimeServer(string platformKey)
        {
            var prefix = PlatformInfo.Os == OsKind.MacOs ? "jre.bundle/Contents/Home/" : string.Empty;
            var files = new Dictionary<string, object>
            {
                [prefix + "bin"] = new { type = "directory" },
                [prefix + "bin/" + (OperatingSystem.IsWindows() ? "java.exe" : "java")] = AddFile(JavaBinary, "java", true),
                [prefix + "legal"] = new { type = "directory" },
                [prefix + "legal/java.base"] = new { type = "directory" },
                [prefix + "legal/java.base/LICENSE"] = AddFile(License, "LICENSE", false),
                [prefix + "legal/java.compiler"] = new { type = "directory" },
                [prefix + "legal/java.compiler/LICENSE"] = new { type = "link", target = "../java.base/LICENSE" },
                [prefix + "release"] = AddFile(ReleaseFile, "release", false)
            };

            var manifest = JsonSerializer.SerializeToUtf8Bytes(new { files });
            var manifestSha1 = Sha1(manifest);
            ManifestUrl = new Uri($"https://piston-meta.mojang.com/v1/packages/{manifestSha1}/manifest.json");
            Responses[ManifestUrl] = manifest;

            var index = new Dictionary<string, object>
            {
                [platformKey] = new Dictionary<string, object>
                {
                    [Component] = new[]
                    {
                        new { manifest = new { sha1 = manifestSha1, size = manifest.Length, url = ManifestUrl.AbsoluteUri } }
                    }
                }
            };
            Responses[new Uri(JavaRuntimeInstaller.RuntimeIndexUrl)] = JsonSerializer.SerializeToUtf8Bytes(index);
        }

        public Dictionary<Uri, byte[]> Responses { get; } = [];

        public Uri ManifestUrl { get; }

        private object AddFile(byte[] content, string name, bool executable)
        {
            var sha1 = Sha1(content);
            var url = $"https://piston-data.mojang.com/v1/objects/{sha1}/{name}";
            Responses[new Uri(url)] = content;
            return new { type = "file", executable, downloads = new { raw = new { sha1, size = content.Length, url } } };
        }

        private static string Sha1(byte[] content) => Convert.ToHexStringLower(SHA1.HashData(content));
    }

    private sealed class FakeHandler(IReadOnlyDictionary<Uri, byte[]> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var found = responses.TryGetValue(request.RequestUri!, out var content);
            return Task.FromResult(new HttpResponseMessage(found ? HttpStatusCode.OK : HttpStatusCode.NotFound)
            {
                Content = new ByteArrayContent(content ?? [])
            });
        }
    }

    private sealed class ReportList : IProgress<InstallProgress>
    {
        private readonly List<InstallProgress> _reports = [];

        public void Report(InstallProgress value)
        {
            lock (_reports)
                _reports.Add(value);
        }

        public IReadOnlyList<InstallProgress> Snapshot()
        {
            lock (_reports)
                return _reports.ToArray();
        }
    }
}
