using System.Collections.Concurrent;
using System.Net;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Test.Install.EndToEnd;

/// <summary>
/// 在内存里同时扮演 Mojang 官方服务与 BMCLAPI 镜像：同一份内容按两边各自的地址规则提供，所有请求都会被记录。
/// 添加内容、下线、损坏、卡住等配置都要在发出请求之前完成。
/// </summary>
internal sealed class FakeMojangHandler : HttpMessageHandler
{
    public const string MirrorHost = "bmclapi2.bangbang93.com";

    private readonly Dictionary<string, byte[]> _files = [];
    private readonly HashSet<DownloadSource> _offline = [];
    private readonly HashSet<(DownloadSource Source, string Key)> _corrupted = [];
    private readonly Dictionary<string, TaskCompletionSource> _stalls = [];
    private readonly ConcurrentQueue<Uri> _requests = new();

    public IReadOnlyCollection<Uri> Requests => _requests;

    public static DownloadSource SourceOf(Uri uri)
    {
        return uri.Host == MirrorHost ? DownloadSource.Bmclapi : DownloadSource.Official;
    }

    /// <summary>
    /// 登记一份内容。官方地址与对应的镜像地址都会提供它。
    /// </summary>
    public void Add(string officialUrl, byte[] content)
    {
        _files[GetKey(officialUrl)] = content;
    }

    public void Remove(string officialUrl)
    {
        _files.Remove(GetKey(officialUrl));
    }

    /// <summary>
    /// 让整个下载源对所有请求返回 503。
    /// </summary>
    public void TakeOffline(DownloadSource source)
    {
        _offline.Add(source);
    }

    /// <summary>
    /// 让该下载源提供与原文件等长、但内容不同的数据。
    /// </summary>
    public void ServeCorrupted(DownloadSource source, string officialUrl)
    {
        _corrupted.Add((source, GetKey(officialUrl)));
    }

    /// <summary>
    /// 让该文件只传回前一半内容，之后一直挂起直到请求被取消。返回的任务在客户端读到卡住的位置时完成。
    /// </summary>
    public Task Stall(string officialUrl)
    {
        var stalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _stalls[GetKey(officialUrl)] = stalled;
        return stalled.Task;
    }

    public void ClearRequests()
    {
        _requests.Clear();
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        _requests.Enqueue(uri);

        var source = SourceOf(uri);
        if (_offline.Contains(source))
            return Respond(HttpStatusCode.ServiceUnavailable);

        var key = GetKey(uri);
        if (!_files.TryGetValue(key, out var content))
            return Respond(HttpStatusCode.NotFound);

        if (_corrupted.Contains((source, key)))
            content = content.Select(value => (byte)~value).ToArray();

        HttpContent body = _stalls.TryGetValue(key, out var stalled)
            ? new StreamContent(new StallingStream(content, stalled))
            : new ByteArrayContent(content);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body });
    }

    private static Task<HttpResponseMessage> Respond(HttpStatusCode status)
    {
        return Task.FromResult(new HttpResponseMessage(status));
    }

    private static string GetKey(string officialUrl)
    {
        return GetKey(new Uri(officialUrl));
    }

    /// <summary>
    /// 把官方地址与镜像地址归一成同一个键：镜像的路径规则与 <c>MirrorUrls</c> 约定的映射一致。
    /// </summary>
    private static string GetKey(Uri uri)
    {
        return uri.Host switch
        {
            "resources.download.minecraft.net" => "/assets" + uri.AbsolutePath,
            "libraries.minecraft.net" => "/maven" + uri.AbsolutePath,
            _ => uri.AbsolutePath
        };
    }

    /// <summary>
    /// 先交出前一半内容，之后的读取一直挂起，直到被取消。
    /// </summary>
    private sealed class StallingStream(byte[] content, TaskCompletionSource stalled) : Stream
    {
        private readonly int _available = content.Length / 2;
        private int _position;

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
            if (_position < _available)
            {
                var count = Math.Min(buffer.Length, _available - _position);
                content.AsMemory(_position, count).CopyTo(buffer);
                _position += count;
                return count;
            }

            stalled.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
