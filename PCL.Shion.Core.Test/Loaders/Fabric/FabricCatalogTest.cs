using System.Net;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Loaders;
using PCL.Shion.Core.Loaders.Fabric;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Test.Loaders.Fabric;

[TestClass]
public sealed class FabricCatalogTest
{
    [TestMethod]
    public async Task ListsFabricLoadersInTheOrderOfTheService()
    {
        using var http = new HttpClient(FabricTestData.CreateServer());

        var versions = await LoaderCatalog.GetVersionsAsync(
            LoaderKind.Fabric, FabricTestData.MinecraftVersion, http, DownloadSource.Official, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                new LoaderVersion(LoaderKind.Fabric, "0.17.0-beta.1", "1.21.1", false),
                new LoaderVersion(LoaderKind.Fabric, "0.16.9", "1.21.1", true),
                new LoaderVersion(LoaderKind.Fabric, "0.16.8", "1.21.1", true)
            },
            versions.ToArray());
    }

    [TestMethod]
    public async Task ListsQuiltLoadersAndTreatsPrereleaseSuffixesAsUnstable()
    {
        using var http = new HttpClient(FabricTestData.CreateServer());

        var versions = await LoaderCatalog.GetVersionsAsync(
            LoaderKind.Quilt, FabricTestData.MinecraftVersion, http, DownloadSource.Official, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                new LoaderVersion(LoaderKind.Quilt, "0.27.0-beta.2", "1.21.1", false),
                new LoaderVersion(LoaderKind.Quilt, "0.27.0-rc.1", "1.21.1", false),
                new LoaderVersion(LoaderKind.Quilt, "0.26.4", "1.21.1", true)
            },
            versions.ToArray());
    }

    [TestMethod]
    public async Task ReturnsAnEmptyListWhenNoLoaderSupportsTheGameVersion()
    {
        var server = new FakeMetaHandler();
        server.Add("https://meta.fabricmc.net/v2/versions/loader/1.2.5", "[]");
        using var http = new HttpClient(server);

        var versions = await LoaderCatalog.GetVersionsAsync(
            LoaderKind.Fabric, "1.2.5", http, DownloadSource.Official, CancellationToken.None);

        Assert.IsEmpty(versions);
    }

    [TestMethod]
    [DataRow(LoaderKind.Fabric, DownloadSource.Official, FabricTestData.FabricListUrl)]
    [DataRow(LoaderKind.Fabric, DownloadSource.Bmclapi, FabricTestData.FabricListMirrorUrl)]
    [DataRow(LoaderKind.Quilt, DownloadSource.Official, FabricTestData.QuiltListUrl)]
    [DataRow(LoaderKind.Quilt, DownloadSource.Bmclapi, FabricTestData.QuiltListMirrorUrl)]
    public async Task RequestsOnlyThePreferredSourceWhenItWorks(LoaderKind kind, DownloadSource source, string expectedUrl)
    {
        var server = FabricTestData.CreateServer();
        using var http = new HttpClient(server);

        await LoaderCatalog.GetVersionsAsync(kind, FabricTestData.MinecraftVersion, http, source, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { expectedUrl }, server.Requests.Select(uri => uri.AbsoluteUri).ToArray());
    }

    [TestMethod]
    public async Task FallsBackToTheMirrorWhenTheOfficialServiceIsDown()
    {
        var server = FabricTestData.CreateServer();
        server.TakeOffline(FabricTestData.FabricHost);
        using var http = new HttpClient(server);

        var versions = await LoaderCatalog.GetVersionsAsync(
            LoaderKind.Fabric, FabricTestData.MinecraftVersion, http, DownloadSource.Official, CancellationToken.None);

        Assert.HasCount(3, versions);
        CollectionAssert.AreEqual(
            new[] { FabricTestData.FabricListUrl, FabricTestData.FabricListMirrorUrl },
            server.Requests.Select(uri => uri.AbsoluteUri).ToArray());
    }

    [TestMethod]
    public async Task FallsBackToTheOfficialServiceWhenTheMirrorIsDown()
    {
        var server = FabricTestData.CreateServer();
        server.TakeOffline(FabricTestData.MirrorHost);
        using var http = new HttpClient(server);

        var versions = await LoaderCatalog.GetVersionsAsync(
            LoaderKind.Quilt, FabricTestData.MinecraftVersion, http, DownloadSource.Bmclapi, CancellationToken.None);

        Assert.HasCount(3, versions);
        CollectionAssert.AreEqual(
            new[] { FabricTestData.QuiltListMirrorUrl, FabricTestData.QuiltListUrl },
            server.Requests.Select(uri => uri.AbsoluteUri).ToArray());
    }

    [TestMethod]
    public async Task FallsBackWhenTheFirstSourceReturnsInvalidContent()
    {
        var server = FabricTestData.CreateServer();
        server.Add(FabricTestData.FabricListUrl, "<html>Bad gateway</html>");
        using var http = new HttpClient(server);

        var versions = await LoaderCatalog.GetVersionsAsync(
            LoaderKind.Fabric, FabricTestData.MinecraftVersion, http, DownloadSource.Official, CancellationToken.None);

        Assert.HasCount(3, versions);
        Assert.HasCount(2, server.Requests);
    }

    [TestMethod]
    public async Task ThrowsTheLastErrorWhenEverySourceFails()
    {
        var server = new FakeMetaHandler();
        using var http = new HttpClient(server);

        var error = await Assert.ThrowsExactlyAsync<HttpRequestException>(() => LoaderCatalog.GetVersionsAsync(
            LoaderKind.Fabric, FabricTestData.MinecraftVersion, http, DownloadSource.Official, CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.NotFound, error.StatusCode);
        CollectionAssert.AreEqual(
            new[] { FabricTestData.FabricHost, FabricTestData.MirrorHost },
            server.Requests.Select(uri => uri.Host).ToArray());
    }

    [TestMethod]
    public async Task DoesNotTryTheNextSourceWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var server = FabricTestData.CreateServer();
        server.OnRequest = _ => cancellation.Cancel();
        using var http = new HttpClient(server);

        await Assert.ThrowsAsync<OperationCanceledException>(() => LoaderCatalog.GetVersionsAsync(
            LoaderKind.Fabric, FabricTestData.MinecraftVersion, http, DownloadSource.Official, cancellation.Token));

        Assert.HasCount(1, server.Requests);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("<html></html>")]
    [DataRow("null")]
    [DataRow("{}")]
    [DataRow("[{}]")]
    [DataRow("""[{ "loader": {} }]""")]
    public void InvalidContentIsRejected(string json)
    {
        Assert.Throws<JsonException>(() => FabricCatalog.Parse(LoaderKind.Fabric, FabricTestData.MinecraftVersion, json));
    }
}
