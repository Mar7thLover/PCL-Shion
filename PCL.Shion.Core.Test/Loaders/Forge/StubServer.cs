using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace PCL.Shion.Core.Test.Loaders.Forge;

/// <summary>
/// 按完整地址返回预置内容，其余地址一律 404，并记录收到的每个请求。内容要在发出请求之前登记好。
/// </summary>
internal sealed class StubServer : HttpMessageHandler
{
    private readonly Dictionary<string, byte[]> _files = [];
    private readonly ConcurrentQueue<Uri> _requests = new();

    public IReadOnlyCollection<Uri> Requests => _requests;

    public void Add(string url, byte[] content)
    {
        _files[new Uri(url).AbsoluteUri] = content;
    }

    public void Add(string url, string content)
    {
        Add(url, Encoding.UTF8.GetBytes(content));
    }

    public void Remove(string url)
    {
        _files.Remove(new Uri(url).AbsoluteUri);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(request.RequestUri!);
        var response = _files.TryGetValue(request.RequestUri!.AbsoluteUri, out var content)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) }
            : new HttpResponseMessage(HttpStatusCode.NotFound);
        return Task.FromResult(response);
    }
}
