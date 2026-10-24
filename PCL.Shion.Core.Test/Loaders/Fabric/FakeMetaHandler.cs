using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace PCL.Shion.Core.Test.Loaders.Fabric;

/// <summary>
/// 按完整地址返回预置内容，其余地址一律 404；可以让某个主机的所有请求返回 503。所有请求都会被记录。
/// 添加内容与下线都要在发出请求之前完成。
/// </summary>
internal sealed class FakeMetaHandler : HttpMessageHandler
{
    private readonly Dictionary<string, byte[]> _files = [];
    private readonly HashSet<string> _offlineHosts = [];
    private readonly ConcurrentQueue<Uri> _requests = new();

    public IReadOnlyCollection<Uri> Requests => _requests;

    /// <summary>
    /// 每个请求到达时先调用，用来在请求途中改变测试状态，例如取消令牌。
    /// </summary>
    public Action<Uri>? OnRequest { get; set; }

    public void Add(string url, string content)
    {
        Add(url, Encoding.UTF8.GetBytes(content));
    }

    public void Add(string url, byte[] content)
    {
        _files[new Uri(url).AbsoluteUri] = content;
    }

    public void Remove(string url)
    {
        _files.Remove(new Uri(url).AbsoluteUri);
    }

    public void TakeOffline(string host)
    {
        _offlineHosts.Add(host);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        _requests.Enqueue(uri);
        OnRequest?.Invoke(uri);
        cancellationToken.ThrowIfCancellationRequested();

        if (_offlineHosts.Contains(uri.Host))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var response = _files.TryGetValue(uri.AbsoluteUri, out var content)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) }
            : new HttpResponseMessage(HttpStatusCode.NotFound);
        return Task.FromResult(response);
    }
}
