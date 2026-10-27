using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Download;

/// <summary>
/// 并发下载器：限制同时下载数，依次尝试候选地址并对临时错误指数退避重试，内容写入临时文件并通过校验后才替换目标文件。
/// </summary>
public sealed class DownloadEngine : IDisposable
{
    private const int BufferSize = 81920;

    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(200);

    private static readonly StringComparer PathComparer =
        PlatformInfo.Os == OsKind.Linux ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private readonly DownloadOptions _options;
    private readonly SemaphoreSlim _concurrency;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Lock _lifetimeGate = new();
    private readonly Lock _targetsGate = new();
    private readonly Dictionary<string, TargetLock> _targets = new(PathComparer);
    private int _activeBatches;
    private bool _disposed;
    private bool _disposing;

    /// <param name="options">下载选项，为 null 时使用默认值。</param>
    /// <param name="handler">HTTP 消息处理器，为 null 时使用默认的连接池；传入的处理器随引擎一起释放。</param>
    public DownloadEngine(DownloadOptions? options = null, HttpMessageHandler? handler = null)
    {
        _options = options ?? new DownloadOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.MaxConcurrency);
        ArgumentOutOfRangeException.ThrowIfNegative(_options.MaxRetries);
        if (_options.Timeout <= TimeSpan.Zero || _options.Timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(options), "The download timeout must be positive and finite.");
        if (_options.RetryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "The retry delay must not be negative.");
        _concurrency = new SemaphoreSlim(_options.MaxConcurrency);
        Http = new HttpClient(handler ?? CreateDefaultHandler()) { Timeout = _options.Timeout };
        // 版本号取自应用程序集（PCL Shion 本体），没有入口程序集时退回本库
        var version = (Assembly.GetEntryAssembly() ?? typeof(DownloadEngine).Assembly).GetName().Version!;
        Http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCL-Shion", version.ToString(3)));
    }

    /// <summary>
    /// 带 PCL Shion User-Agent 的 HTTP 客户端，供清单与 JSON 请求复用，请求超时为 <see cref="DownloadOptions.Timeout"/>。
    /// </summary>
    public HttpClient Http { get; }

    /// <summary>
    /// 下载一批文件。已存在且通过校验的文件直接计为完成，目标路径相同的项只处理第一个。
    /// 单个文件失败不影响其他文件，失败的项与原因在结果中返回。
    /// </summary>
    /// <param name="items">要下载的文件。</param>
    /// <param name="progress">进度，约每 200 ms 汇报一次，全部结束后再汇报一次最终状态。</param>
    /// <param name="cancellationToken">取消下载。</param>
    /// <exception cref="OperationCanceledException">下载被取消，进行中的临时文件已删除。</exception>
    public async Task<DownloadResult> DownloadAsync(
        IReadOnlyList<DownloadItem> items,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _activeBatches++;
        }
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
            return await DownloadBatchAsync(items, progress, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            lock (_lifetimeGate)
            {
                _activeBatches--;
                if (_disposed && !_disposing && _activeBatches == 0)
                    DisposeSynchronization();
            }
        }
    }

    private async Task<DownloadResult> DownloadBatchAsync(
        IReadOnlyList<DownloadItem> items,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var targets = items.DistinctBy(item => Path.GetFullPath(item.TargetPath), PathComparer).ToList();
        var tracker = new DownloadTracker(targets);
        var timer = new PeriodicTimer(ReportInterval);
        var reporting = progress is null ? Task.CompletedTask : ReportAsync(timer, tracker, progress);
        var errors = new IReadOnlyList<DownloadError>?[targets.Count];
        try
        {
            // 资源索引可含数万个文件，只创建有限的工作任务；不同批次仍共同受 _concurrency 限制。
            await Parallel.ForEachAsync(Enumerable.Range(0, targets.Count), new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaxConcurrency,
                CancellationToken = cancellationToken
            }, async (index, token) =>
            {
                errors[index] = await DownloadItemAsync(targets[index], tracker, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        finally
        {
            timer.Dispose();
            await reporting.ConfigureAwait(false);
        }
        progress?.Report(tracker.Snapshot());

        var failed = new List<DownloadItem>();
        var reasons = new Dictionary<string, IReadOnlyList<DownloadError>>();
        for (var i = 0; i < targets.Count; i++)
        {
            if (errors[i] is not { } error) continue;
            failed.Add(targets[i]);
            reasons[targets[i].TargetPath] = error;
        }
        return new DownloadResult(failed, reasons);
    }

    public void Dispose()
    {
        lock (_lifetimeGate)
        {
            if (_disposed) return;
            _disposed = true;
            _disposing = true;
        }
        try
        {
            // 先取消正在下载或等待的批次，等它们结束后再释放信号量，避免 finally.Release 抛出异常。
            _shutdown.Cancel();
            Http.Dispose();
        }
        finally
        {
            lock (_lifetimeGate)
            {
                _disposing = false;
                if (_activeBatches == 0)
                    DisposeSynchronization();
            }
        }
    }

    private void DisposeSynchronization()
    {
        _concurrency.Dispose();
        _shutdown.Dispose();
    }

    private static SocketsHttpHandler CreateDefaultHandler() => new()
    {
        AutomaticDecompression = DecompressionMethods.All,
        // 引擎在应用内长期共享，定期更换连接以跟上 DNS 变化
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    };

    private static async Task ReportAsync(PeriodicTimer timer, DownloadTracker tracker, IProgress<DownloadProgress> progress)
    {
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
            progress.Report(tracker.Snapshot());
    }

    private async Task<IReadOnlyList<DownloadError>?> DownloadItemAsync(DownloadItem item, DownloadTracker tracker, CancellationToken cancellationToken)
    {
        // 相同路径可能来自同时安装的多个实例。先等目标锁再占下载名额，避免重复写入共享库。
        using var target = await LockTargetAsync(item.TargetPath, cancellationToken).ConfigureAwait(false);
        await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var error = await EnsureFileAsync(item, tracker, cancellationToken);
            if (error is null)
                tracker.Complete(item);
            else
                tracker.Fail();
            return error;
        }
        finally
        {
            _concurrency.Release();
        }
    }

    /// <summary>
    /// 让目标文件就绪：已通过校验则不下载，否则下载到同目录的临时文件再替换。成功返回 null，失败返回原因。
    /// </summary>
    private async Task<IReadOnlyList<DownloadError>?> EnsureFileAsync(DownloadItem item, DownloadTracker tracker, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await FileVerifier.MatchesAsync(item.TargetPath, item.Size, item.Sha1, cancellationToken).ConfigureAwait(false)) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(item.TargetPath))!);
            var tempPath = $"{item.TargetPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                var error = await FetchAsync(item, tempPath, tracker, cancellationToken);
                if (error is null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Move(tempPath, item.TargetPath, overwrite: true);
                }
                return error;
            }
            finally
            {
                DeleteQuietly(tempPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [new DownloadError(DownloadErrorKind.Other, Actual: ex.Message)];
        }
    }

    /// <summary>
    /// 依次尝试各地址，直到临时文件的内容通过校验。成功返回 null，否则返回每个地址最后一次失败的原因。
    /// </summary>
    private async Task<IReadOnlyList<DownloadError>?> FetchAsync(DownloadItem item, string tempPath, DownloadTracker tracker, CancellationToken cancellationToken)
    {
        var errors = new List<DownloadError>();
        foreach (var url in item.Urls)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await FetchOnceAsync(url, item, tempPath, tracker, cancellationToken);
                    return null;
                }
                catch (Exception ex)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (attempt >= _options.MaxRetries || !IsTransient(ex))
                    {
                        errors.Add(Describe(url, ex));
                        break;
                    }
                }
                var delay = Math.Min(30_000, _options.RetryDelay.TotalMilliseconds * Math.Pow(2, Math.Min(attempt, 30)));
                await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken);
            }
        }
        if (errors.Count == 0)
            errors.Add(new DownloadError(DownloadErrorKind.NoUrl));
        return errors;
    }

    private async Task FetchOnceAsync(Uri url, DownloadItem item, string tempPath, DownloadTracker tracker, CancellationToken cancellationToken)
    {
        // 每收到一段数据就重新计时，只有长时间收不到数据才算超时，大文件不受总时长限制
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stall.CancelAfter(_options.Timeout);
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, stall.Token);
        response.EnsureSuccessStatusCode();

        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        long received = 0;
        try
        {
            await using var source = await response.Content.ReadAsStreamAsync(stall.Token);
            await using var target = new FileStream(tempPath, new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Options = FileOptions.Asynchronous,
                BufferSize = 0
            });
            int read;
            while ((read = await source.ReadAsync(buffer, stall.Token)) > 0)
            {
                stall.CancelAfter(_options.Timeout);
                if (item.Size is { } size && received + read > size)
                    throw new VerificationException(DownloadErrorKind.SizeMismatch, size.ToString(CultureInfo.InvariantCulture),
                        (received + read).ToString(CultureInfo.InvariantCulture));
                sha1.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), stall.Token);
                received += read;
                tracker.Receive(item, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            tracker.EndAttempt(item, received);
        }
        Verify(item, received, sha1.GetHashAndReset());
    }

    private async Task<IDisposable> LockTargetAsync(string targetPath, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(targetPath);
        TargetLock target;
        lock (_targetsGate)
        {
            if (!_targets.TryGetValue(path, out target!))
                _targets[path] = target = new TargetLock();
            target.Users++;
        }
        try
        {
            await target.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new TargetLease(this, path, target);
        }
        catch
        {
            ReleaseTarget(path, target, acquired: false);
            throw;
        }
    }

    private void ReleaseTarget(string path, TargetLock target, bool acquired)
    {
        lock (_targetsGate)
        {
            if (acquired) target.Semaphore.Release();
            if (--target.Users != 0) return;
            _targets.Remove(path);
            target.Semaphore.Dispose();
        }
    }

    private sealed class TargetLock
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int Users;
    }

    private sealed class TargetLease(DownloadEngine engine, string path, TargetLock target) : IDisposable
    {
        public void Dispose() => engine.ReleaseTarget(path, target, acquired: true);
    }

    private static void Verify(DownloadItem item, long received, byte[] hash)
    {
        if (item.Size is { } size && received != size)
            throw new VerificationException(DownloadErrorKind.SizeMismatch, size.ToString(CultureInfo.InvariantCulture), received.ToString(CultureInfo.InvariantCulture));
        if (item.Sha1 is { } sha1 && !FileVerifier.HashEquals(hash, sha1))
            throw new VerificationException(DownloadErrorKind.HashMismatch, sha1.ToLowerInvariant(), Convert.ToHexStringLower(hash));
    }

    // 网络错误、超时、408、429 与 5xx 可能只是暂时的，值得在同一地址重试；其余错误（包括校验不符）直接换下一个地址
    private static bool IsTransient(Exception exception) => exception switch
    {
        HttpRequestException { StatusCode: { } status } =>
            status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500,
        HttpRequestException or IOException or OperationCanceledException => true,
        _ => false
    };

    // 调用方的取消已在外层处理，这里的 OperationCanceledException 只会来自超时
    private DownloadError Describe(Uri url, Exception exception) => exception switch
    {
        VerificationException verification => new DownloadError(verification.Kind, url, verification.Expected, verification.Actual),
        OperationCanceledException => new DownloadError(
            DownloadErrorKind.Timeout, url, Expected: _options.Timeout.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)),
        _ => new DownloadError(DownloadErrorKind.Other, url, Actual: exception.Message)
    };

    // 临时文件可能正被杀毒软件扫描而暂时删不掉，此时只会留下一个孤立的临时文件，不影响下载结果
    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// 内容与期望的大小或 SHA-1 不符。携带结构化信息，供 <see cref="Describe"/> 转成 <see cref="DownloadError"/>。
    /// </summary>
    private sealed class VerificationException(DownloadErrorKind kind, string expected, string actual)
        : Exception($"{kind}: expected {expected}, received {actual}")
    {
        public DownloadErrorKind Kind { get; } = kind;

        public string Expected { get; } = expected;

        public string Actual { get; } = actual;
    }
}
