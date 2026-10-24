using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using PCL.Shion.Services;

namespace PCL.Shion.Pages.Homepage;

/// <summary>
/// 新闻列表与新闻正文共用的 HTTP 客户端。
/// </summary>
internal static class NewsHttp
{
    public static HttpClient Client { get; } = Create();

    private static HttpClient Create()
    {
        var handler = new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCL-Shion", AppInfo.VersionText));
        return client;
    }
}
