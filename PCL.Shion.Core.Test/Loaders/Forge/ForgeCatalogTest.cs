using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Loaders;
using PCL.Shion.Core.Loaders.Forge;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Test.Loaders.Forge;

[TestClass]
public sealed class ForgeCatalogTest
{
    private const string ForgeMetadata = """
        {
          "1.7.10": [ "1.7.10-10.13.0.1150", "1.7.10-10.13.4.1614-1.7.10", "1.7.10-10.13.2.1300-1.7.10" ],
          "1.20.1": [ "1.20.1-47.0.0", "1.20.1-47.4.28", "1.20.1-47.4.3" ]
        }
        """;

    private const string BmclapiForge = """
        [
          { "version": "47.0.1", "mcversion": "1.20.1", "files": [ { "format": "jar", "category": "installer" } ] },
          { "version": "47.0.0", "mcversion": "1.20.1", "files": [ { "format": "txt", "category": "changelog" } ] },
          { "version": "47.0.12", "mcversion": "1.20.1", "branch": null, "files": [ { "format": "jar", "category": "installer" } ] },
          { "version": "10.13.4.1614", "mcversion": "1.20.1", "branch": "1.7.10", "files": [ { "format": "jar", "category": "installer" } ] }
        ]
        """;

    private const string NeoForgeVersions = """
        { "isSnapshot": false, "versions": [ "21.0.0-beta", "21.1.1", "21.1.77", "21.1.9", "21.4.1-beta", "26.1.0.0-alpha.1+snapshot-1" ] }
        """;

    private const string LegacyNeoForgeVersions = """
        { "isSnapshot": false, "versions": [ "1.20.1-47.1.5", "1.20.1-47.1.106", "47.1.82", "1.20.1-47.1.11" ] }
        """;

    [TestMethod]
    public void ParseForgeMetadata_ListsOneMinecraftVersionNewestFirst()
    {
        var versions = ForgeCatalog.ParseForgeMetadata(ForgeMetadata, "1.20.1");

        CollectionAssert.AreEqual(new[] { "47.4.28", "47.4.3", "47.0.0" }, versions.Select(version => version.Version).ToArray());
        Assert.IsTrue(versions.All(version => version is { Kind: LoaderKind.Forge, MinecraftVersion: "1.20.1", Stable: true }));
    }

    [TestMethod]
    public void ParseForgeMetadata_KeepsTheBranchSuffix()
    {
        var versions = ForgeCatalog.ParseForgeMetadata(ForgeMetadata, "1.7.10");

        CollectionAssert.AreEqual(
            new[] { "10.13.4.1614-1.7.10", "10.13.2.1300-1.7.10", "10.13.0.1150" },
            versions.Select(version => version.Version).ToArray());
    }

    [TestMethod]
    public void ParseForgeMetadata_ReturnsNothingForAMinecraftVersionWithoutForge()
    {
        Assert.IsEmpty(ForgeCatalog.ParseForgeMetadata(ForgeMetadata, "1.21.1"));
    }

    [TestMethod]
    [DataRow("1.2.5")]
    [DataRow("1.4.7")]
    [DataRow("1.5")]
    [DataRow("1.5.1")]
    public void ParseForgeMetadata_SkipsVersionsOlderThanTheInstaller(string minecraftVersion)
    {
        var metadata = $$"""{ "{{minecraftVersion}}": [ "{{minecraftVersion}}-1.0.0.1" ] }""";

        Assert.IsEmpty(ForgeCatalog.ParseForgeMetadata(metadata, minecraftVersion));
    }

    [TestMethod]
    [DataRow("1.5.2")]
    [DataRow("1.6.4")]
    [DataRow("26.1")]
    public void ParseForgeMetadata_KeepsVersionsWithAnInstaller(string minecraftVersion)
    {
        var metadata = $$"""{ "{{minecraftVersion}}": [ "{{minecraftVersion}}-1.0.0.1" ] }""";

        Assert.AreEqual("1.0.0.1", ForgeCatalog.ParseForgeMetadata(metadata, minecraftVersion).Single().Version);
    }

    [TestMethod]
    public void ParseBmclapiForge_SkipsBuildsWithoutAnInstallerAndAppendsTheBranch()
    {
        var versions = ForgeCatalog.ParseBmclapiForge(BmclapiForge, "1.20.1");

        CollectionAssert.AreEqual(
            new[] { "47.0.12", "47.0.1", "10.13.4.1614-1.7.10" },
            versions.Select(version => version.Version).ToArray());
        Assert.IsTrue(versions.All(version => version is { Kind: LoaderKind.Forge, MinecraftVersion: "1.20.1", Stable: true }));
    }

    [TestMethod]
    [DataRow("20.2.3-beta", "1.20.2")]
    [DataRow("20.4.80-beta", "1.20.4")]
    [DataRow("21.0.1", "1.21")]
    [DataRow("21.1.77", "1.21.1")]
    [DataRow("21.11.5", "1.21.11")]
    [DataRow("26.1.0.5-beta", "26.1")]
    [DataRow("26.1.2.122", "26.1.2")]
    [DataRow("26.1.0.0-alpha.1+snapshot-1", "26.1-snapshot-1")]
    [DataRow("26.1.0.0-alpha.15+pre-3", "26.1-pre-3")]
    [DataRow("0.25w14craftmine.3-beta", "25w14craftmine")]
    public void GetNeoForgeMinecraftVersion_FollowsBothNumberingSchemes(string version, string expected)
    {
        Assert.AreEqual(expected, ForgeCatalog.GetNeoForgeMinecraftVersion(version));
    }

    [TestMethod]
    public void ParseNeoForge_FiltersByMinecraftVersionAndMarksBetasUnstable()
    {
        var versions = ForgeCatalog.ParseNeoForge(NeoForgeVersions, "1.21.1");

        CollectionAssert.AreEqual(new[] { "21.1.77", "21.1.9", "21.1.1" }, versions.Select(version => version.Version).ToArray());
        Assert.IsTrue(versions.All(version => version is { Kind: LoaderKind.NeoForge, MinecraftVersion: "1.21.1", Stable: true }));
        Assert.IsFalse(ForgeCatalog.ParseNeoForge(NeoForgeVersions, "1.21").Single().Stable);
        Assert.AreEqual("26.1.0.0-alpha.1+snapshot-1", ForgeCatalog.ParseNeoForge(NeoForgeVersions, "26.1-snapshot-1").Single().Version);
    }

    [TestMethod]
    public void ParseLegacyNeoForge_StripsTheMinecraftPrefix()
    {
        var versions = ForgeCatalog.ParseLegacyNeoForge(LegacyNeoForgeVersions);

        CollectionAssert.AreEqual(new[] { "47.1.106", "47.1.11", "47.1.5" }, versions.Select(version => version.Version).ToArray());
        Assert.IsTrue(versions.All(version => version is { Kind: LoaderKind.NeoForge, MinecraftVersion: "1.20.1", Stable: true }));
    }

    [TestMethod]
    public void ParseBmclapiNeoForge_SortsNewestFirst()
    {
        const string json = """
            [
              { "rawVersion": "neoforge-21.1.10", "mcversion": "1.21.1", "version": "21.1.10" },
              { "rawVersion": "neoforge-21.1.1-beta", "mcversion": "1.21.1", "version": "21.1.1-beta" },
              { "rawVersion": "neoforge-21.1.100", "mcversion": "1.21.1", "version": "21.1.100" }
            ]
            """;

        var versions = ForgeCatalog.ParseBmclapiNeoForge(json, "1.21.1");

        CollectionAssert.AreEqual(new[] { "21.1.100", "21.1.10", "21.1.1-beta" }, versions.Select(version => version.Version).ToArray());
        CollectionAssert.AreEqual(new[] { true, true, false }, versions.Select(version => version.Stable).ToArray());
    }

    [TestMethod]
    public async Task GetVersionsAsync_FallsBackToTheMirrorWhenTheOfficialListFails()
    {
        var server = new StubServer();
        server.Add("https://bmclapi2.bangbang93.com/forge/minecraft/1.20.1", BmclapiForge);
        using var http = new HttpClient(server);

        var versions = await ForgeCatalog.GetVersionsAsync(LoaderKind.Forge, "1.20.1", http, DownloadSource.Official, CancellationToken.None);

        Assert.AreEqual("47.0.12", versions[0].Version);
        CollectionAssert.AreEqual(
            new[] { ForgeCatalog.ForgeMetadataUrl, "https://bmclapi2.bangbang93.com/forge/minecraft/1.20.1" },
            server.Requests.Select(uri => uri.AbsoluteUri).ToArray());
    }

    [TestMethod]
    public async Task GetVersionsAsync_AsksThePreferredMirrorFirst()
    {
        var server = new StubServer();
        server.Add("https://bmclapi2.bangbang93.com/neoforge/list/1.21.1", "[]");
        server.Add(ForgeCatalog.NeoForgeVersionsUrl, NeoForgeVersions);
        using var http = new HttpClient(server);

        var versions = await ForgeCatalog.GetVersionsAsync(LoaderKind.NeoForge, "1.21.1", http, DownloadSource.Bmclapi, CancellationToken.None);

        Assert.IsEmpty(versions);
        Assert.AreEqual("bmclapi2.bangbang93.com", server.Requests.Single().Host);
    }

    [TestMethod]
    public async Task GetVersionsAsync_UsesTheForgeCoordinatesOfNeoForgeFor1201()
    {
        var server = new StubServer();
        server.Add(ForgeCatalog.LegacyNeoForgeVersionsUrl, LegacyNeoForgeVersions);
        using var http = new HttpClient(server);

        var versions = await ForgeCatalog.GetVersionsAsync(LoaderKind.NeoForge, "1.20.1", http, DownloadSource.Official, CancellationToken.None);

        Assert.AreEqual("47.1.106", versions[0].Version);
        Assert.AreEqual(ForgeCatalog.LegacyNeoForgeVersionsUrl, server.Requests.Single().AbsoluteUri);
    }

    [TestMethod]
    public async Task GetVersionsAsync_ThrowsWhenBothSourcesFail()
    {
        using var http = new HttpClient(new StubServer());

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            ForgeCatalog.GetVersionsAsync(LoaderKind.Forge, "1.20.1", http, DownloadSource.Official, CancellationToken.None));
    }
}
