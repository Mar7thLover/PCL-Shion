using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Sources;
using PCL.Shion.Core.Versions;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class GameInstallerTest
{
    private const string VersionId = "1.0-test";
    private const string JsonUrl = "https://piston-meta.mojang.com/v1/packages/0a0b/1.0-test.json";
    private const string ClientUrl = "https://piston-data.mojang.com/v1/objects/0c0d/client.jar";
    private const string LibraryUrl = "https://libraries.minecraft.net/com/example/lib/1.0/lib-1.0.jar";
    private const string AssetIndexUrl = "https://piston-meta.mojang.com/v1/packages/0e0f/test.json";

    private static readonly byte[] ClientJar = "client jar"u8.ToArray();
    private static readonly byte[] Library = "library"u8.ToArray();
    private static readonly byte[] Asset = "asset"u8.ToArray();
    private static readonly string AssetHash = Sha1(Asset);
    private static readonly string AssetUrl = $"https://resources.download.minecraft.net/{AssetHash[..2]}/{AssetHash}";
    private static readonly byte[] AssetIndex = Encoding.UTF8.GetBytes(
        $$"""{ "objects": { "minecraft/sounds/a.ogg": { "hash": "{{AssetHash}}", "size": {{Asset.Length}} } } }""");
    private static readonly byte[] VersionJson = CreateVersionJson();
    private static readonly RemoteVersion Remote = new(VersionId, "release", DateTimeOffset.UnixEpoch, new Uri(JsonUrl), Sha1(VersionJson));

    private FakeMinecraftRoot _root = null!;
    private Dictionary<string, byte[]> _server = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = new FakeMinecraftRoot();
        _server = new Dictionary<string, byte[]>
        {
            [JsonUrl] = VersionJson,
            [ClientUrl] = ClientJar,
            [LibraryUrl] = Library,
            [AssetIndexUrl] = AssetIndex,
            [AssetUrl] = Asset
        };
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        _root.Dispose();
    }

    [TestMethod]
    public async Task InstallsUnderTheInstanceNameAndRewritesTheId()
    {
        using var engine = CreateEngine();

        await new GameInstaller(engine, DownloadSource.Official)
            .InstallVanillaAsync(_root.RootPath, Remote, "My World", null, CancellationToken.None);

        var json = JsonNode.Parse(File.ReadAllText(_root.GetPath("versions/My World/My World.json")))!;
        Assert.AreEqual("My World", json["id"]!.GetValue<string>());
        Assert.AreEqual("net.minecraft.client.main.Main", json["mainClass"]!.GetValue<string>());
        Assert.AreEqual(ClientUrl, json["downloads"]!["client"]!["url"]!.GetValue<string>());
        CollectionAssert.AreEqual(ClientJar, File.ReadAllBytes(_root.GetPath("versions/My World/My World.jar")));
        Assert.IsFalse(Directory.Exists(_root.GetPath($"versions/{VersionId}")));

        CollectionAssert.AreEqual(Library, File.ReadAllBytes(_root.GetPath("libraries/com/example/lib/1.0/lib-1.0.jar")));
        CollectionAssert.AreEqual(AssetIndex, File.ReadAllBytes(_root.GetPath("assets/indexes/test.json")));
        CollectionAssert.AreEqual(Asset, File.ReadAllBytes(AssetObjectPath()));
        Assert.AreEqual("My World", InstanceScanner.Scan(_root.RootPath).Single().Name);
    }

    [TestMethod]
    public async Task KeepsTheOriginalJsonWhenTheNameMatchesTheVersion()
    {
        using var engine = CreateEngine();

        await new GameInstaller(engine, DownloadSource.Official)
            .InstallVanillaAsync(_root.RootPath, Remote, VersionId, null, CancellationToken.None);

        CollectionAssert.AreEqual(VersionJson, File.ReadAllBytes(_root.GetPath($"versions/{VersionId}/{VersionId}.json")));
        CollectionAssert.AreEqual(ClientJar, File.ReadAllBytes(_root.GetPath($"versions/{VersionId}/{VersionId}.jar")));
    }

    [TestMethod]
    public async Task CompleteDownloadsTheAssetIndexAndThenItsObjects()
    {
        _root.WriteVersion(VersionId, Encoding.UTF8.GetString(VersionJson));
        using var engine = CreateEngine();

        var result = await new GameInstaller(engine, DownloadSource.Official)
            .CompleteAsync(_root.RootPath, VersionId, null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        CollectionAssert.AreEqual(ClientJar, File.ReadAllBytes(_root.GetPath($"versions/{VersionId}/{VersionId}.jar")));
        CollectionAssert.AreEqual(AssetIndex, File.ReadAllBytes(_root.GetPath("assets/indexes/test.json")));
        CollectionAssert.AreEqual(Asset, File.ReadAllBytes(AssetObjectPath()));
    }

    [TestMethod]
    public async Task CompleteSkipsAssetObjectsWhenTheIndexCannotBeDownloaded()
    {
        _server.Remove(AssetIndexUrl);
        _root.WriteVersion(VersionId, Encoding.UTF8.GetString(VersionJson));
        using var engine = CreateEngine();
        var indexPath = _root.GetPath("assets/indexes/test.json");

        var result = await new GameInstaller(engine, DownloadSource.Official)
            .CompleteAsync(_root.RootPath, VersionId, null, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { indexPath }, result.Failed.Select(item => item.TargetPath).ToArray());
        Assert.IsTrue(result.Errors.ContainsKey(indexPath));
        Assert.IsTrue(File.Exists(_root.GetPath($"versions/{VersionId}/{VersionId}.jar")));
        Assert.IsFalse(File.Exists(AssetObjectPath()));
    }

    [TestMethod]
    public async Task FailedFileRaisesInstallException()
    {
        _server.Remove(ClientUrl);
        using var engine = CreateEngine();
        var jarPath = _root.GetPath($"versions/{VersionId}/{VersionId}.jar");

        var exception = await Assert.ThrowsExactlyAsync<InstallException>(() => new GameInstaller(engine, DownloadSource.Official)
            .InstallVanillaAsync(_root.RootPath, Remote, VersionId, null, CancellationToken.None));

        Assert.AreEqual(jarPath, exception.FailedFiles.Single().TargetPath);
        Assert.IsTrue(exception.Errors.ContainsKey(jarPath));
    }

    [TestMethod]
    public async Task FailedVersionJsonStopsTheInstallation()
    {
        _server.Remove(JsonUrl);
        using var engine = CreateEngine();

        var exception = await Assert.ThrowsExactlyAsync<InstallException>(() => new GameInstaller(engine, DownloadSource.Official)
            .InstallVanillaAsync(_root.RootPath, Remote, "My World", null, CancellationToken.None));

        Assert.AreEqual(_root.GetPath("versions/My World/My World.json"), exception.FailedFiles.Single().TargetPath);
        Assert.IsFalse(Directory.Exists(_root.GetPath("libraries")));
    }

    private DownloadEngine CreateEngine()
    {
        return new DownloadEngine(handler: new StubHandler(_server));
    }

    private string AssetObjectPath()
    {
        return _root.GetPath($"assets/objects/{AssetHash[..2]}/{AssetHash}");
    }

    private static byte[] CreateVersionJson()
    {
        return Encoding.UTF8.GetBytes($$"""
            {
              "id": "{{VersionId}}",
              "type": "release",
              "mainClass": "net.minecraft.client.main.Main",
              "assets": "test",
              "assetIndex": { "id": "test", "sha1": "{{Sha1(AssetIndex)}}", "size": {{AssetIndex.Length}}, "url": "{{AssetIndexUrl}}" },
              "downloads": { "client": { "sha1": "{{Sha1(ClientJar)}}", "size": {{ClientJar.Length}}, "url": "{{ClientUrl}}" } },
              "javaVersion": { "component": "java-runtime-delta", "majorVersion": 21 },
              "libraries": [
                {
                  "name": "com.example:lib:1.0",
                  "downloads": {
                    "artifact": { "path": "com/example/lib/1.0/lib-1.0.jar", "sha1": "{{Sha1(Library)}}", "size": {{Library.Length}}, "url": "{{LibraryUrl}}" }
                  }
                }
              ]
            }
            """);
    }

    private static string Sha1(byte[] data)
    {
        return Convert.ToHexStringLower(SHA1.HashData(data));
    }

    /// <summary>
    /// 按完整地址返回预置内容，其余地址一律 404。
    /// </summary>
    private sealed class StubHandler(IReadOnlyDictionary<string, byte[]> files) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = files.TryGetValue(request.RequestUri!.AbsoluteUri, out var content)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(response);
        }
    }
}
