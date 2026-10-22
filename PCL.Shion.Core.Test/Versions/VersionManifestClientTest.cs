using System.Net;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Sources;
using PCL.Shion.Core.Versions;

namespace PCL.Shion.Core.Test.Versions;

[TestClass]
public sealed class VersionManifestClientTest
{
    private const string OfficialHost = "piston-meta.mojang.com";
    private const string BmclapiHost = "bmclapi2.bangbang93.com";

    /// <summary>
    /// 从真实的 version_manifest_v2.json 中摘取五条，覆盖四种类型，字段保持原样（包括不使用的 time 与 complianceLevel）。
    /// </summary>
    private const string ManifestJson = """
        {
          "latest": { "release": "1.21.1", "snapshot": "24w36a" },
          "versions": [
            {
              "id": "24w36a",
              "type": "snapshot",
              "url": "https://piston-meta.mojang.com/v1/packages/1c8ddf5919a1699b266c9e802c41e56470e04f7a/24w36a.json",
              "time": "2026-10-20T06:32:42+00:00",
              "releaseTime": "2024-09-04T12:44:12+00:00",
              "sha1": "1c8ddf5919a1699b266c9e802c41e56470e04f7a",
              "complianceLevel": 1
            },
            {
              "id": "1.21.1",
              "type": "release",
              "url": "https://piston-meta.mojang.com/v1/packages/1757c8b1fbfc6b638504e1f6cceb05893f7389d5/1.21.1.json",
              "time": "2026-10-20T06:32:00+00:00",
              "releaseTime": "2024-08-08T12:24:45+00:00",
              "sha1": "1757c8b1fbfc6b638504e1f6cceb05893f7389d5",
              "complianceLevel": 1
            },
            {
              "id": "1.0",
              "type": "release",
              "url": "https://piston-meta.mojang.com/v1/packages/75062586b830dd5160f13f1c9130eb365e01f1b9/1.0.json",
              "time": "2022-03-10T09:51:38+00:00",
              "releaseTime": "2011-11-17T22:00:00+00:00",
              "sha1": "75062586b830dd5160f13f1c9130eb365e01f1b9",
              "complianceLevel": 0
            },
            {
              "id": "b1.8.1",
              "type": "old_beta",
              "url": "https://piston-meta.mojang.com/v1/packages/440e3b845c3991492a3d0c5f0ccfda78ab90d9b6/b1.8.1.json",
              "time": "2022-03-10T09:51:38+00:00",
              "releaseTime": "2011-09-18T22:00:00+00:00",
              "sha1": "440e3b845c3991492a3d0c5f0ccfda78ab90d9b6",
              "complianceLevel": 0
            },
            {
              "id": "c0.30_01c",
              "type": "old_alpha",
              "url": "https://piston-meta.mojang.com/v1/packages/9392d3f635770ac4dfd3f8c9444f319b00b08945/c0.30_01c.json",
              "time": "2022-03-10T09:51:38+00:00",
              "releaseTime": "2009-12-21T22:00:00+00:00",
              "sha1": "9392d3f635770ac4dfd3f8c9444f319b00b08945",
              "complianceLevel": 0
            }
          ]
        }
        """;

    [TestMethod]
    public void ParsesLatestVersions()
    {
        var manifest = VersionManifestClient.Parse(ManifestJson);

        Assert.AreEqual("1.21.1", manifest.LatestRelease);
        Assert.AreEqual("24w36a", manifest.LatestSnapshot);
    }

    [TestMethod]
    public void KeepsManifestOrder()
    {
        var manifest = VersionManifestClient.Parse(ManifestJson);

        CollectionAssert.AreEqual(
            new[] { "24w36a", "1.21.1", "1.0", "b1.8.1", "c0.30_01c" },
            manifest.Versions.Select(version => version.Id).ToArray());
    }

    [TestMethod]
    [DataRow("24w36a", "snapshot")]
    [DataRow("1.21.1", "release")]
    [DataRow("b1.8.1", "old_beta")]
    [DataRow("c0.30_01c", "old_alpha")]
    public void ParsesVersionType(string id, string type)
    {
        var version = VersionManifestClient.Parse(ManifestJson).Versions.Single(item => item.Id == id);

        Assert.AreEqual(type, version.Type);
    }

    [TestMethod]
    public void ParsesReleaseTimeJsonUrlAndSha1()
    {
        var release = VersionManifestClient.Parse(ManifestJson).Versions.Single(item => item.Id == "1.21.1");

        Assert.AreEqual(new DateTimeOffset(2024, 8, 8, 12, 24, 45, TimeSpan.Zero), release.ReleaseTime);
        Assert.AreEqual(
            new Uri("https://piston-meta.mojang.com/v1/packages/1757c8b1fbfc6b638504e1f6cceb05893f7389d5/1.21.1.json"),
            release.JsonUrl);
        Assert.AreEqual("1757c8b1fbfc6b638504e1f6cceb05893f7389d5", release.Sha1);
    }

    [TestMethod]
    public void ParsesReleaseTimeBefore2010()
    {
        var alpha = VersionManifestClient.Parse(ManifestJson).Versions.Single(item => item.Id == "c0.30_01c");

        Assert.AreEqual(new DateTimeOffset(2009, 12, 21, 22, 0, 0, TimeSpan.Zero), alpha.ReleaseTime);
    }

    [TestMethod]
    public void Sha1IsNullWhenManifestOmitsIt()
    {
        var manifest = VersionManifestClient.Parse("""
            {
              "latest": { "release": "1.0", "snapshot": "1.0" },
              "versions": [
                { "id": "1.0", "type": "release", "url": "https://example.invalid/1.0.json", "releaseTime": "2011-11-17T22:00:00+00:00" }
              ]
            }
            """);

        Assert.IsNull(manifest.Versions[0].Sha1);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("<html></html>")]
    [DataRow("null")]
    [DataRow("""{ "versions": [] }""")]
    [DataRow("""{ "latest": { "release": "1.0", "snapshot": "1.0" } }""")]
    [DataRow("""{ "latest": { "release": "1.0" }, "versions": [] }""")]
    [DataRow("""{ "latest": { "release": "1.0", "snapshot": "1.0" }, "versions": [ { "id": "1.0", "type": "release", "releaseTime": "2011-11-17T22:00:00+00:00" } ] }""")]
    [DataRow("""{ "latest": { "release": "1.0", "snapshot": "1.0" }, "versions": [ { "id": "1.0", "type": "release", "url": "https://example.invalid/1.0.json", "releaseTime": "yesterday" } ] }""")]
    public void InvalidManifestIsRejected(string json)
    {
        Assert.Throws<JsonException>(() => VersionManifestClient.Parse(json));
    }

    [TestMethod]
    [DataRow(DownloadSource.Official, OfficialHost)]
    [DataRow(DownloadSource.Bmclapi, BmclapiHost)]
    public async Task FetchAsync_RequestsPreferredSourceFirst(DownloadSource source, string expectedHost)
    {
        var handler = new StubHandler(_ => Respond(HttpStatusCode.OK, ManifestJson));
        using var http = new HttpClient(handler);

        var manifest = await VersionManifestClient.FetchAsync(http, source, CancellationToken.None);

        Assert.HasCount(5, manifest.Versions);
        Assert.HasCount(1, handler.Requests);
        Assert.AreEqual(expectedHost, handler.Requests[0].Host);
        Assert.AreEqual("/mc/game/version_manifest_v2.json", handler.Requests[0].AbsolutePath);
    }

    [TestMethod]
    [DataRow(DownloadSource.Official, OfficialHost, BmclapiHost)]
    [DataRow(DownloadSource.Bmclapi, BmclapiHost, OfficialHost)]
    public async Task FetchAsync_FallsBackWhenPreferredSourceReturnsError(DownloadSource source, string failingHost, string workingHost)
    {
        var handler = new StubHandler(request => request.RequestUri!.Host == failingHost
            ? Respond(HttpStatusCode.ServiceUnavailable, string.Empty)
            : Respond(HttpStatusCode.OK, ManifestJson));
        using var http = new HttpClient(handler);

        var manifest = await VersionManifestClient.FetchAsync(http, source, CancellationToken.None);

        Assert.AreEqual("1.21.1", manifest.LatestRelease);
        CollectionAssert.AreEqual(
            new[] { failingHost, workingHost },
            handler.Requests.Select(uri => uri.Host).ToArray());
    }

    [TestMethod]
    public async Task FetchAsync_FallsBackWhenPreferredSourceReturnsInvalidContent()
    {
        var handler = new StubHandler(request => request.RequestUri!.Host == OfficialHost
            ? Respond(HttpStatusCode.OK, "<html>Login required</html>")
            : Respond(HttpStatusCode.OK, ManifestJson));
        using var http = new HttpClient(handler);

        var manifest = await VersionManifestClient.FetchAsync(http, DownloadSource.Official, CancellationToken.None);

        Assert.AreEqual("24w36a", manifest.LatestSnapshot);
        Assert.HasCount(2, handler.Requests);
    }

    [TestMethod]
    public async Task FetchAsync_ThrowsLastErrorWhenAllSourcesFail()
    {
        var handler = new StubHandler(_ => Respond(HttpStatusCode.BadGateway, string.Empty));
        using var http = new HttpClient(handler);

        var error = await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            VersionManifestClient.FetchAsync(http, DownloadSource.Official, CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.BadGateway, error.StatusCode);
        CollectionAssert.AreEqual(
            new[] { OfficialHost, BmclapiHost },
            handler.Requests.Select(uri => uri.Host).ToArray());
    }

    [TestMethod]
    public async Task FetchAsync_DoesNotTryNextSourceWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHandler(_ =>
        {
            cancellation.Cancel();
            return Respond(HttpStatusCode.ServiceUnavailable, string.Empty);
        });
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            VersionManifestClient.FetchAsync(http, DownloadSource.Official, cancellation.Token));

        Assert.HasCount(1, handler.Requests);
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(body) };
    }

    /// <summary>
    /// 记录收到的请求地址，响应由测试给出。
    /// </summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var response = respond(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response);
        }
    }
}
