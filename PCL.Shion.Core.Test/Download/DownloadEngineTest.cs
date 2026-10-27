using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Download;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class DownloadEngineTest
{
    private const string PrimaryUrl = "https://primary.invalid/files/data.bin";
    private const string MirrorUrl = "https://mirror.invalid/files/data.bin";
    private const string ContentText = "PCL Shion download engine test content";

    private static readonly byte[] Content = Encoding.UTF8.GetBytes(ContentText);
    private static readonly string ContentSha1 = Convert.ToHexStringLower(SHA1.HashData(Content));
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private FakeMinecraftRoot _root = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = new FakeMinecraftRoot();
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        _root.Dispose();
    }

    [TestMethod]
    public async Task DownloadsAndVerifiesAFile()
    {
        var handler = new FakeHttpHandler((_, _) => Ok(Content));
        using var engine = CreateEngine(handler);
        var target = _root.GetPath("libraries/com/example/data.bin");

        var result = await engine.DownloadAsync([CreateItem(target, PrimaryUrl)], null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.IsEmpty(result.Errors);
        CollectionAssert.AreEqual(Content, File.ReadAllBytes(target));
        CollectionAssert.AreEqual(new[] { target }, Directory.GetFiles(Path.GetDirectoryName(target)!));
    }

    [TestMethod]
    public async Task SendsThePclShionUserAgent()
    {
        var handler = new FakeHttpHandler((_, _) => Ok(Content));
        using var engine = CreateEngine(handler);

        await engine.DownloadAsync([CreateItem(_root.GetPath("data.bin"), PrimaryUrl)], null, CancellationToken.None);

        var product = handler.Requests.Single().Headers.UserAgent.Single().Product!;
        Assert.AreEqual("PCL-Shion", product.Name);
        Assert.IsFalse(string.IsNullOrEmpty(product.Version));
    }

    [TestMethod]
    public async Task RetriesTransientErrorsOnTheSameUrl()
    {
        var handler = new FakeHttpHandler((_, attempt) => attempt <= 2 ? Status(HttpStatusCode.ServiceUnavailable) : Ok(Content));
        using var engine = CreateEngine(handler, maxRetries: 3);
        var target = _root.GetPath("data.bin");

        var result = await engine.DownloadAsync([CreateItem(target, PrimaryUrl)], null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(3, handler.CountRequests(PrimaryUrl));
        CollectionAssert.AreEqual(Content, File.ReadAllBytes(target));
    }

    [TestMethod]
    public async Task MovesToTheNextUrlAfterExhaustingRetries()
    {
        var handler = new FakeHttpHandler((url, _) =>
            url == new Uri(PrimaryUrl) ? Status(HttpStatusCode.BadGateway) : Ok(Content));
        using var engine = CreateEngine(handler, maxRetries: 2);
        var target = _root.GetPath("data.bin");

        var result = await engine.DownloadAsync([CreateItem(target, PrimaryUrl, MirrorUrl)], null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(3, handler.CountRequests(PrimaryUrl));
        Assert.AreEqual(1, handler.CountRequests(MirrorUrl));
    }

    [TestMethod]
    public async Task DoesNotRetryClientErrors()
    {
        var handler = new FakeHttpHandler((url, _) =>
            url == new Uri(PrimaryUrl) ? Status(HttpStatusCode.NotFound) : Ok(Content));
        using var engine = CreateEngine(handler, maxRetries: 3);

        var result = await engine.DownloadAsync([CreateItem(_root.GetPath("data.bin"), PrimaryUrl, MirrorUrl)], null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, handler.CountRequests(PrimaryUrl));
        Assert.AreEqual(1, handler.CountRequests(MirrorUrl));
    }

    [TestMethod]
    public async Task FallsBackToTheNextUrlWhenTheHashDoesNotMatch()
    {
        // 与正确内容等长，只有 SHA-1 能发现差异
        var corrupted = Content.ToArray();
        corrupted[0] ^= 0xFF;
        var handler = new FakeHttpHandler((url, _) => Ok(url == new Uri(PrimaryUrl) ? corrupted : Content));
        using var engine = CreateEngine(handler, maxRetries: 3);
        var target = _root.GetPath("data.bin");

        var result = await engine.DownloadAsync([CreateItem(target, PrimaryUrl, MirrorUrl)], null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, handler.CountRequests(PrimaryUrl), "a hash mismatch should not be retried on the same URL");
        Assert.AreEqual(1, handler.CountRequests(MirrorUrl));
        CollectionAssert.AreEqual(Content, File.ReadAllBytes(target));
    }

    [TestMethod]
    public async Task ReportsAFailureWhenEveryUrlFails()
    {
        var handler = new FakeHttpHandler((url, _) =>
            url == new Uri(PrimaryUrl) ? Ok(Content) : Status(HttpStatusCode.InternalServerError));
        using var engine = CreateEngine(handler, maxRetries: 1);
        var target = _root.GetPath("files/data.bin");
        var item = new DownloadItem([new Uri(PrimaryUrl), new Uri(MirrorUrl)], target, Content.Length + 1L);

        var result = await engine.DownloadAsync([item], null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreSame(item, result.Failed.Single());
        var errors = result.Errors[target];
        Assert.HasCount(2, errors);
        Assert.AreEqual(new DownloadError(DownloadErrorKind.SizeMismatch, new Uri(PrimaryUrl), $"{Content.Length + 1}", $"{Content.Length}"), errors[0]);
        Assert.AreEqual(DownloadErrorKind.Other, errors[1].Kind);
        Assert.AreEqual(new Uri(MirrorUrl), errors[1].Url);
        Assert.Contains("500", errors[1].ToString());
        Assert.AreEqual(1, handler.CountRequests(PrimaryUrl));
        Assert.AreEqual(2, handler.CountRequests(MirrorUrl));
        Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(target)!));
    }

    [TestMethod]
    public async Task SkipsFilesThatAreAlreadyValid()
    {
        var target = _root.WriteFile("files/data.bin", ContentText);
        var handler = new FakeHttpHandler((_, _) => Status(HttpStatusCode.InternalServerError));
        using var engine = CreateEngine(handler);
        var progress = new RecordingProgress();

        var result = await engine.DownloadAsync([CreateItem(target, PrimaryUrl)], progress, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.IsEmpty(handler.Requests);
        Assert.AreEqual(1, progress.Last.CompletedFiles);
        Assert.AreEqual((long)Content.Length, progress.Last.DownloadedBytes);
    }

    [TestMethod]
    public async Task ReplacesACorruptedExistingFile()
    {
        var target = _root.WriteFile("files/data.bin", "stale");
        var handler = new FakeHttpHandler((_, _) => Ok(Content));
        using var engine = CreateEngine(handler);

        var result = await engine.DownloadAsync([CreateItem(target, PrimaryUrl)], null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, handler.CountRequests(PrimaryUrl));
        CollectionAssert.AreEqual(Content, File.ReadAllBytes(target));
    }

    [TestMethod]
    public async Task RetriesAStalledResponseAfterTheTimeout()
    {
        var handler = new FakeHttpHandler((_, attempt) => attempt == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream(Content[..4])) }
            : Ok(Content));
        using var engine = CreateEngine(handler, maxRetries: 1, timeout: TimeSpan.FromMilliseconds(200));
        var target = _root.GetPath("data.bin");

        var result = await engine.DownloadAsync([CreateItem(target, PrimaryUrl)], null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(2, handler.CountRequests(PrimaryUrl));
        CollectionAssert.AreEqual(Content, File.ReadAllBytes(target));
    }

    [TestMethod]
    public async Task CancellationRemovesTheTemporaryFile()
    {
        var stream = new StallingStream(Content[..4]);
        var handler = new FakeHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) });
        using var engine = CreateEngine(handler);
        var target = _root.GetPath("files/data.bin");
        var directory = Path.GetDirectoryName(target)!;
        using var cancellation = new CancellationTokenSource();

        var download = engine.DownloadAsync([CreateItem(target, PrimaryUrl)], null, cancellation.Token);
        await stream.Stalled.WaitAsync(WaitLimit);
        Assert.AreEqual(1, Directory.GetFiles(directory).Length, "the partial content should be in a temporary file");
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => download);
        Assert.IsEmpty(Directory.GetFiles(directory));
    }

    [TestMethod]
    public async Task LimitsTheNumberOfConcurrentDownloads()
    {
        var gate = new object();
        var active = 0;
        var peak = 0;
        var handler = new FakeHttpHandler(async (_, _, cancellationToken) =>
        {
            lock (gate)
                peak = Math.Max(peak, ++active);
            await Task.Delay(100, cancellationToken);
            lock (gate)
                active--;
            return Ok(Content);
        });
        using var engine = CreateEngine(handler, maxConcurrency: 2);
        var items = Enumerable.Range(0, 6).Select(i => CreateItem(_root.GetPath($"files/{i}.bin"), PrimaryUrl)).ToList();

        var result = await engine.DownloadAsync(items, null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(6, handler.CountRequests(PrimaryUrl));
        Assert.AreEqual(2, peak);
    }

    [TestMethod]
    public async Task DownloadsADuplicateTargetOnlyOnce()
    {
        var handler = new FakeHttpHandler((_, _) => Ok(Content));
        using var engine = CreateEngine(handler);
        var target = _root.GetPath("assets/objects/ab/data");
        var progress = new RecordingProgress();

        var result = await engine.DownloadAsync([CreateItem(target, PrimaryUrl), CreateItem(target, MirrorUrl)], progress, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.HasCount(1, handler.Requests);
        Assert.AreEqual(1, progress.Last.TotalFiles);
    }

    [TestMethod]
    public async Task ReportsTheFinalProgress()
    {
        var handler = new FakeHttpHandler((url, _) =>
            url == new Uri(PrimaryUrl) ? Ok(Content) : Status(HttpStatusCode.NotFound));
        using var engine = CreateEngine(handler);
        var good = CreateItem(_root.GetPath("good.bin"), PrimaryUrl);
        var bad = new DownloadItem([new Uri(MirrorUrl)], _root.GetPath("bad.bin"), 100);
        var progress = new RecordingProgress();

        var result = await engine.DownloadAsync([good, bad], progress, CancellationToken.None);

        var last = progress.Last;
        Assert.AreEqual(2, last.TotalFiles);
        Assert.AreEqual(1, last.CompletedFiles);
        Assert.AreEqual(1, last.FailedFiles);
        Assert.AreEqual(Content.Length + 100L, last.TotalBytes);
        Assert.AreEqual((long)Content.Length, last.DownloadedBytes);
        Assert.AreSame(bad, result.Failed.Single());
        Assert.Contains("404", result.Errors[bad.TargetPath].Single().ToString());
    }

    [TestMethod]
    public async Task ReportsAHashMismatchWithBothDigests()
    {
        // 与正确内容等长，只有 SHA-1 能发现差异
        var corrupted = Content.ToArray();
        corrupted[0] ^= 0xFF;
        var handler = new FakeHttpHandler((_, _) => Ok(corrupted));
        using var engine = CreateEngine(handler);
        var target = _root.GetPath("data.bin");

        var result = await engine.DownloadAsync([CreateItem(target, PrimaryUrl)], null, CancellationToken.None);

        var error = result.Errors[target].Single();
        Assert.AreEqual(DownloadErrorKind.HashMismatch, error.Kind);
        Assert.AreEqual(new Uri(PrimaryUrl), error.Url);
        Assert.AreEqual((ContentSha1, Convert.ToHexStringLower(SHA1.HashData(corrupted))), (error.Expected, error.Actual));
    }

    [TestMethod]
    public async Task ReportsATimeoutWhenNoDataArrives()
    {
        var handler = new FakeHttpHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream(Content[..4])) });
        using var engine = CreateEngine(handler, maxRetries: 0, timeout: TimeSpan.FromMilliseconds(200));
        var target = _root.GetPath("data.bin");

        var result = await engine.DownloadAsync([CreateItem(target, PrimaryUrl)], null, CancellationToken.None);

        var error = result.Errors[target].Single();
        Assert.AreEqual(DownloadErrorKind.Timeout, error.Kind);
        Assert.AreEqual(new Uri(PrimaryUrl), error.Url);
        Assert.AreEqual("0.2", error.Expected);
    }

    [TestMethod]
    public async Task ReportsAnItemWithoutAnyUrl()
    {
        var handler = new FakeHttpHandler((_, _) => Ok(Content));
        using var engine = CreateEngine(handler);
        var target = _root.GetPath("data.bin");

        var result = await engine.DownloadAsync([CreateItem(target)], null, CancellationToken.None);

        Assert.AreEqual(new DownloadError(DownloadErrorKind.NoUrl), result.Errors[target].Single());
        Assert.IsEmpty(handler.Requests);
    }

    [TestMethod]
    public async Task AForbiddenFileDoesNotStopOtherDownloads()
    {
        var handler = new FakeHttpHandler((url, _) => url.AbsoluteUri == PrimaryUrl
            ? Status(HttpStatusCode.Forbidden) : Ok(Content));
        using var engine = CreateEngine(handler);
        var denied = CreateItem(_root.GetPath("denied.bin"), PrimaryUrl);
        var allowed = CreateItem(_root.GetPath("allowed.bin"), MirrorUrl);

        var result = await engine.DownloadAsync([denied, allowed], null, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { denied }, result.Failed.ToArray());
        Assert.Contains("403", result.Errors[denied.TargetPath].Single().ToString());
        CollectionAssert.AreEqual(Content, File.ReadAllBytes(allowed.TargetPath));
        Assert.AreEqual(1, handler.CountRequests(PrimaryUrl));
    }

    [TestMethod]
    public async Task ConcurrentBatchesShareATargetWithoutRedownloadingIt()
    {
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var duplicateRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler(async (_, attempt, token) =>
        {
            if (attempt > 1) duplicateRequest.TrySetResult();
            requested.TrySetResult();
            await release.Task.WaitAsync(token);
            return Ok(Content);
        });
        using var engine = CreateEngine(handler);
        var item = CreateItem(_root.GetPath("shared.bin"), PrimaryUrl);
        var first = engine.DownloadAsync([item], null, CancellationToken.None);
        await requested.Task.WaitAsync(WaitLimit);
        var second = engine.DownloadAsync([item], null, CancellationToken.None);
        try
        {
            await Task.WhenAny(duplicateRequest.Task, Task.Delay(200));
        }
        finally
        {
            release.TrySetResult();
        }

        var results = await Task.WhenAll(first, second).WaitAsync(WaitLimit);

        Assert.IsTrue(results.All(result => result.Succeeded));
        Assert.IsFalse(duplicateRequest.Task.IsCompleted, "the second batch must wait and recheck the completed file");
        Assert.AreEqual(1, handler.CountRequests(PrimaryUrl));
        CollectionAssert.AreEqual(Content, File.ReadAllBytes(item.TargetPath));
    }

    [TestMethod]
    public async Task CancellingATargetWaiterDoesNotCancelTheDownloadThatOwnsIt()
    {
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler(async (_, _, token) =>
        {
            requested.TrySetResult();
            await release.Task.WaitAsync(token);
            return Ok(Content);
        });
        using var engine = CreateEngine(handler);
        using var cancellation = new CancellationTokenSource();
        var item = CreateItem(_root.GetPath("shared.bin"), PrimaryUrl);
        var first = engine.DownloadAsync([item], null, CancellationToken.None);
        await requested.Task.WaitAsync(WaitLimit);
        var waiting = engine.DownloadAsync([item], null, cancellation.Token);
        await cancellation.CancelAsync();
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => waiting.WaitAsync(WaitLimit));
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.IsTrue((await first.WaitAsync(WaitLimit)).Succeeded);
        Assert.AreEqual(1, handler.CountRequests(PrimaryUrl));
        Assert.IsTrue((await engine.DownloadAsync([item], null, CancellationToken.None)).Succeeded);
    }

    [TestMethod]
    public async Task RejectsOversizedContentBeforeReadingToTheEndAndKeepsTheOldFile()
    {
        var stream = new StallingStream(Content);
        var handler = new FakeHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) });
        using var engine = CreateEngine(handler);
        var target = _root.WriteFile("data.bin", "previous file");
        var item = new DownloadItem([new Uri(PrimaryUrl)], target, Content.Length - 1);

        var result = await engine.DownloadAsync([item], null, CancellationToken.None).WaitAsync(WaitLimit);

        Assert.AreEqual(DownloadErrorKind.SizeMismatch, result.Errors[target].Single().Kind);
        Assert.IsFalse(stream.Stalled.IsCompleted, "an oversized response should stop before the next read");
        Assert.AreEqual("previous file", File.ReadAllText(target));
        CollectionAssert.AreEqual(new[] { target }, Directory.GetFiles(_root.RootPath));
    }

    [TestMethod]
    public async Task CancellationAtEndOfResponseDoesNotReplaceTheOldFile()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new FakeHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new CancelAtEndStream(Content, cancellation))
        });
        using var engine = CreateEngine(handler);
        var target = _root.WriteFile("data.bin", "previous file");

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            engine.DownloadAsync([CreateItem(target, PrimaryUrl)], null, cancellation.Token));

        Assert.AreEqual("previous file", File.ReadAllText(target));
        CollectionAssert.AreEqual(new[] { target }, Directory.GetFiles(_root.RootPath));
    }

    [TestMethod]
    public async Task DisposingTheEngineCancelsActiveAndQueuedWorkWithoutLeakingTemporaryFiles()
    {
        var stream = new StallingStream(Content[..4]);
        var handler = new FakeHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) });
        using var engine = CreateEngine(handler, maxConcurrency: 1);
        var first = engine.DownloadAsync([CreateItem(_root.GetPath("first.bin"), PrimaryUrl)], null, CancellationToken.None);
        await stream.Stalled.WaitAsync(WaitLimit);
        var queued = engine.DownloadAsync([CreateItem(_root.GetPath("second.bin"), PrimaryUrl)], null, CancellationToken.None);

        engine.Dispose();

        await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitLimit));
        await Assert.ThrowsAsync<OperationCanceledException>(() => queued.WaitAsync(WaitLimit));
        Assert.IsEmpty(Directory.GetFiles(_root.RootPath));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.DownloadAsync([], null, CancellationToken.None));
    }

    [TestMethod]
    public async Task AnAlreadyCancelledEmptyBatchStillReportsCancellation()
    {
        using var engine = CreateEngine(new FakeHttpHandler((_, _) => Ok(Content)));

        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.DownloadAsync([], null, new CancellationToken(true)));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void RejectsNonPositiveConcurrencyInsteadOfWaitingForever(int concurrency)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DownloadEngine(new DownloadOptions { MaxConcurrency = concurrency }));
    }

    [TestMethod]
    public void RejectsInvalidRetryAndTimeoutOptions()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DownloadEngine(new DownloadOptions { MaxRetries = -1 }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DownloadEngine(new DownloadOptions { Timeout = TimeSpan.Zero }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DownloadEngine(new DownloadOptions { Timeout = Timeout.InfiniteTimeSpan }));
    }

    private static DownloadEngine CreateEngine(FakeHttpHandler handler, int maxConcurrency = 4, int maxRetries = 3, TimeSpan? timeout = null) =>
        new(new DownloadOptions
        {
            MaxConcurrency = maxConcurrency,
            MaxRetries = maxRetries,
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
            RetryDelay = TimeSpan.Zero
        }, handler);

    private static DownloadItem CreateItem(string target, params string[] urls) =>
        new(urls.Select(url => new Uri(url)).ToList(), target, Content.Length, ContentSha1);

    private static HttpResponseMessage Ok(byte[] content) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    private sealed class CancelAtEndStream(byte[] bytes, CancellationTokenSource cancellation) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position < Length) return base.ReadAsync(buffer, cancellationToken);
            cancellation.Cancel();
            return ValueTask.FromResult(0);
        }
    }

    /// <summary>
    /// 按回调生成响应并记录请求；回调的第二个参数是同一地址的第几次请求（从 1 开始）。
    /// </summary>
    private sealed class FakeHttpHandler(Func<Uri, int, CancellationToken, Task<HttpResponseMessage>> respondAsync) : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<Uri, int> _counts = new();
        private readonly ConcurrentQueue<HttpRequestMessage> _requests = new();

        public FakeHttpHandler(Func<Uri, int, HttpResponseMessage> respond)
            : this((url, attempt, _) => Task.FromResult(respond(url, attempt)))
        {
        }

        public IReadOnlyCollection<HttpRequestMessage> Requests => _requests;

        public int CountRequests(string url) => _counts.TryGetValue(new Uri(url), out var count) ? count : 0;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requests.Enqueue(request);
            var attempt = _counts.AddOrUpdate(request.RequestUri!, 1, (_, count) => count + 1);
            return respondAsync(request.RequestUri!, attempt, cancellationToken);
        }
    }

    /// <summary>
    /// 先返回一段内容，之后的读取一直等到被取消。
    /// </summary>
    private sealed class StallingStream(byte[] head) : Stream
    {
        private readonly TaskCompletionSource _stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _headSent;

        /// <summary>开始等待时完成，此时第一段内容已经交给读取方。</summary>
        public Task Stalled => _stalled.Task;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_headSent)
            {
                _headSent = true;
                head.AsSpan().CopyTo(buffer.Span);
                return head.Length;
            }
            _stalled.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class RecordingProgress : IProgress<DownloadProgress>
    {
        private readonly List<DownloadProgress> _reports = [];

        public DownloadProgress Last
        {
            get
            {
                lock (_reports)
                    return _reports[^1];
            }
        }

        public void Report(DownloadProgress value)
        {
            lock (_reports)
                _reports.Add(value);
        }
    }
}
