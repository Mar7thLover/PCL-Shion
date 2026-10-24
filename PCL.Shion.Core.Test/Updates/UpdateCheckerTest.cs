using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Updates;

namespace PCL.Shion.Core.Test.Updates;

[TestClass]
public sealed class UpdateCheckerTest
{
    /// <summary>
    /// 仿照 GitHub 发布列表接口的响应（最新的在前），保留了不使用的字段。
    /// </summary>
    private const string ReleasesJson = """
        [
          {
            "url": "https://api.github.com/repos/Mar7thLover/PCL-Shion/releases/300003",
            "html_url": "https://github.com/Mar7thLover/PCL-Shion/releases/tag/v0.3.0-beta.1",
            "id": 300003,
            "author": { "login": "Mar7thLover", "id": 1, "type": "User", "site_admin": false },
            "tag_name": "v0.3.0-beta.1",
            "target_commitish": "dev",
            "name": "PCL Shion 0.3.0 Beta 1",
            "draft": false,
            "prerelease": true,
            "created_at": "2026-10-20T09:00:00Z",
            "published_at": "2026-10-20T09:30:00Z",
            "assets": [],
            "body": "Beta release."
          },
          {
            "url": "https://api.github.com/repos/Mar7thLover/PCL-Shion/releases/300002",
            "html_url": "https://github.com/Mar7thLover/PCL-Shion/releases/tag/v0.2.0",
            "id": 300002,
            "author": { "login": "Mar7thLover", "id": 1, "type": "User", "site_admin": false },
            "tag_name": "v0.2.0",
            "target_commitish": "main",
            "name": "PCL Shion 0.2.0",
            "draft": false,
            "prerelease": false,
            "created_at": "2026-10-10T07:00:00Z",
            "published_at": "2026-10-10T07:30:00Z",
            "assets": [
              { "name": "PCL.Shion-win-x64.zip", "size": 52428800, "browser_download_url": "https://github.com/Mar7thLover/PCL-Shion/releases/download/v0.2.0/PCL.Shion-win-x64.zip" }
            ],
            "body": "Second release."
          },
          {
            "url": "https://api.github.com/repos/Mar7thLover/PCL-Shion/releases/300001",
            "html_url": "https://github.com/Mar7thLover/PCL-Shion/releases/tag/v0.1.0",
            "id": 300001,
            "author": { "login": "Mar7thLover", "id": 1, "type": "User", "site_admin": false },
            "tag_name": "v0.1.0",
            "target_commitish": "main",
            "name": "PCL Shion 0.1.0",
            "draft": false,
            "prerelease": false,
            "created_at": "2026-09-01T07:00:00Z",
            "published_at": "2026-09-01T07:30:00Z",
            "assets": [],
            "body": "First release."
          }
        ]
        """;

    [TestMethod]
    public async Task GetLatestAsync_RequestsReleaseListWithGitHubHeaders()
    {
        var handler = new StubHandler(_ => Respond(ReleasesJson));
        using var http = new HttpClient(handler);

        await UpdateChecker.GetLatestAsync(http, false, CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.AreEqual(HttpMethod.Get, request.Method);
        Assert.AreEqual(UpdateChecker.LatestReleaseApi, request.Uri.AbsoluteUri);
        Assert.AreEqual("application/vnd.github+json", request.Accept);
    }

    [TestMethod]
    public async Task GetLatestAsync_ReturnsNewestStableRelease()
    {
        using var http = CreateClient(ReleasesJson);

        var release = await UpdateChecker.GetLatestAsync(http, false, CancellationToken.None);

        Assert.IsNotNull(release);
        Assert.AreEqual("v0.2.0", release.Tag);
        Assert.AreEqual(new Version(0, 2, 0), release.Version);
        Assert.AreEqual("PCL Shion 0.2.0", release.Name);
        Assert.AreEqual(new Uri("https://github.com/Mar7thLover/PCL-Shion/releases/tag/v0.2.0"), release.HtmlUrl);
        Assert.AreEqual(new DateTimeOffset(2026, 10, 10, 7, 30, 0, TimeSpan.Zero), release.PublishedAt);
        Assert.IsFalse(release.Prerelease);
    }

    [TestMethod]
    public async Task GetLatestAsync_ReturnsPrereleaseWhenIncluded()
    {
        using var http = CreateClient(ReleasesJson);

        var release = await UpdateChecker.GetLatestAsync(http, true, CancellationToken.None);

        Assert.IsNotNull(release);
        Assert.AreEqual("v0.3.0-beta.1", release.Tag);
        Assert.AreEqual(new Version(0, 3, 0), release.Version);
        Assert.IsTrue(release.Prerelease);
    }

    [TestMethod]
    public async Task GetLatestAsync_ComparesVersionsNumericallyRegardlessOfOrder()
    {
        using var http = CreateClient(Json(Release("v0.9.0"), Release("v0.10.0"), Release("v0.1.0")));

        var release = await UpdateChecker.GetLatestAsync(http, false, CancellationToken.None);

        Assert.AreEqual("v0.10.0", release?.Tag);
    }

    [TestMethod]
    public async Task GetLatestAsync_PrefersStableReleaseOverPrereleaseOfSameVersion()
    {
        using var http = CreateClient(Json(
            Release("v0.3.0-rc.1", prerelease: true, publishedAt: "2026-10-02T08:00:00Z"),
            Release("v0.3.0", publishedAt: "2026-10-01T08:00:00Z"),
            Release("v0.3.0-beta.1", prerelease: true, publishedAt: "2026-09-30T08:00:00Z")));

        var release = await UpdateChecker.GetLatestAsync(http, true, CancellationToken.None);

        Assert.AreEqual("v0.3.0", release?.Tag);
    }

    [TestMethod]
    public async Task GetLatestAsync_PrefersMostRecentlyPublishedAmongSameVersion()
    {
        using var http = CreateClient(Json(
            Release("v0.3.0-beta.1", prerelease: true, publishedAt: "2026-10-01T08:00:00Z"),
            Release("v0.3.0-beta.2", prerelease: true, publishedAt: "2026-10-05T08:00:00Z")));

        var release = await UpdateChecker.GetLatestAsync(http, true, CancellationToken.None);

        Assert.AreEqual("v0.3.0-beta.2", release?.Tag);
    }

    [TestMethod]
    public async Task GetLatestAsync_IgnoresDrafts()
    {
        using var http = CreateClient(Json(
            Release("v0.9.0", draft: true, publishedAt: null),
            Release("v0.2.0")));

        var release = await UpdateChecker.GetLatestAsync(http, false, CancellationToken.None);

        Assert.AreEqual("v0.2.0", release?.Tag);
    }

    [TestMethod]
    public async Task GetLatestAsync_IgnoresTagsThatAreNotVersions()
    {
        using var http = CreateClient(Json(Release("nightly"), Release("v0.1.0")));

        var release = await UpdateChecker.GetLatestAsync(http, false, CancellationToken.None);

        Assert.AreEqual("v0.1.0", release?.Tag);
    }

    [TestMethod]
    public async Task GetLatestAsync_ReturnsNullWithoutReleases()
    {
        using var http = CreateClient("[]");

        Assert.IsNull(await UpdateChecker.GetLatestAsync(http, true, CancellationToken.None));
    }

    [TestMethod]
    public async Task GetLatestAsync_ReturnsNullWhenOnlyPrereleasesExistAndAreNotIncluded()
    {
        using var http = CreateClient(Json(Release("v0.1.0-beta.1", prerelease: true)));

        Assert.IsNull(await UpdateChecker.GetLatestAsync(http, false, CancellationToken.None));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task GetLatestAsync_UsesTagWhenReleaseHasNoName(string? name)
    {
        using var http = CreateClient(Json(Release("v0.2.0", name: name)));

        var release = await UpdateChecker.GetLatestAsync(http, false, CancellationToken.None);

        Assert.AreEqual("v0.2.0", release?.Name);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Forbidden)]
    [DataRow(HttpStatusCode.NotFound)]
    [DataRow(HttpStatusCode.InternalServerError)]
    public async Task GetLatestAsync_ThrowsWhenGitHubReturnsError(HttpStatusCode status)
    {
        using var http = new HttpClient(new StubHandler(_ => Respond("""{ "message": "error" }""", status)));

        var error = await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            UpdateChecker.GetLatestAsync(http, false, CancellationToken.None));

        Assert.AreEqual(status, error.StatusCode);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("<html>Service unavailable</html>")]
    [DataRow("null")]
    [DataRow("{}")]
    [DataRow("""[ { "name": "no tag" } ]""")]
    [DataRow("""[ { "tag_name": "v0.1.0" } ]""")]
    public async Task GetLatestAsync_ThrowsJsonExceptionForUnexpectedContent(string body)
    {
        using var http = CreateClient(body);

        await Assert.ThrowsAsync<JsonException>(() =>
            UpdateChecker.GetLatestAsync(http, false, CancellationToken.None));
    }

    [TestMethod]
    public async Task GetLatestAsync_ThrowsWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var http = CreateClient(ReleasesJson);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            UpdateChecker.GetLatestAsync(http, false, cancellation.Token));
    }

    private static HttpClient CreateClient(string body)
    {
        return new HttpClient(new StubHandler(_ => Respond(body)));
    }

    private static HttpResponseMessage Respond(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private static string Json(params string[] releases)
    {
        return $"[{string.Join(',', releases)}]";
    }

    private static string Release(string tag, bool prerelease = false, bool draft = false, string? name = "Release", string? publishedAt = "2026-10-01T08:00:00Z")
    {
        return $$"""
            {
              "tag_name": {{JsonSerializer.Serialize(tag)}},
              "name": {{JsonSerializer.Serialize(name)}},
              "html_url": "https://github.com/Mar7thLover/PCL-Shion/releases/tag/{{tag}}",
              "draft": {{JsonSerializer.Serialize(draft)}},
              "prerelease": {{JsonSerializer.Serialize(prerelease)}},
              "published_at": {{JsonSerializer.Serialize(publishedAt)}}
            }
            """;
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Accept);

    /// <summary>
    /// 记录收到的请求，响应由测试给出。
    /// </summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Accept.ToString()));
            return Task.FromResult(respond(request));
        }
    }
}
