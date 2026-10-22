using System.Diagnostics;

namespace PCL.Shion.Core.Download;

/// <summary>
/// 汇总一批下载的进度。计数方法可在多个线程上并发调用，<see cref="Snapshot"/> 须依次调用。
/// </summary>
internal sealed class DownloadTracker
{
    private static readonly TimeSpan SpeedWindow = TimeSpan.FromSeconds(2);

    private readonly int _totalFiles;
    private readonly long _totalBytes;
    private readonly Queue<(long Timestamp, long Bytes)> _samples = new();
    private int _completedFiles;
    private int _failedFiles;
    private long _completedBytes;
    private long _receivingBytes;
    private long _transferredBytes;

    public DownloadTracker(IReadOnlyCollection<DownloadItem> items)
    {
        _totalFiles = items.Count;
        _totalBytes = items.Sum(item => item.Size ?? 0);
        _samples.Enqueue((Stopwatch.GetTimestamp(), 0));
    }

    /// <summary>一次请求收到了数据。</summary>
    public void Receive(DownloadItem item, int bytes)
    {
        Interlocked.Add(ref _transferredBytes, bytes);
        if (item.Size is not null)
            Interlocked.Add(ref _receivingBytes, bytes);
    }

    /// <summary>一次请求结束，无论成败都撤下它正在传输的字节；成功的文件随后由 <see cref="Complete"/> 按完整大小计入。</summary>
    public void EndAttempt(DownloadItem item, long received)
    {
        if (item.Size is not null)
            Interlocked.Add(ref _receivingBytes, -received);
    }

    public void Complete(DownloadItem item)
    {
        Interlocked.Add(ref _completedBytes, item.Size ?? 0);
        Interlocked.Increment(ref _completedFiles);
    }

    public void Fail()
    {
        Interlocked.Increment(ref _failedFiles);
    }

    /// <summary>
    /// 生成当前进度，速度按最近约 <see cref="SpeedWindow"/> 内的传输量计算。
    /// </summary>
    public DownloadProgress Snapshot()
    {
        var now = Stopwatch.GetTimestamp();
        var transferred = Interlocked.Read(ref _transferredBytes);
        while (_samples.Count > 1 && Stopwatch.GetElapsedTime(_samples.Peek().Timestamp, now) > SpeedWindow)
            _samples.Dequeue();
        var (since, baseline) = _samples.Peek();
        _samples.Enqueue((now, transferred));

        var seconds = Stopwatch.GetElapsedTime(since, now).TotalSeconds;
        // 服务器返回的内容比期望的大时，在校验失败前会短暂超出总量
        var downloaded = Interlocked.Read(ref _completedBytes) + Interlocked.Read(ref _receivingBytes);
        return new DownloadProgress(
            _totalFiles,
            Volatile.Read(ref _completedFiles),
            Volatile.Read(ref _failedFiles),
            _totalBytes,
            Math.Min(downloaded, _totalBytes),
            seconds > 0 ? (transferred - baseline) / seconds : 0);
    }
}
