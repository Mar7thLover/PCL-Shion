using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Link.Natayark;

/// <summary>
///     Natayark ID 的 OAuth 登录：在本机回环地址上起一个临时服务，打开浏览器授权页，等待授权回调后换取令牌。
///     对应 WPF 中 ModWebServer.StartNaidAuthorize。
/// </summary>
public static class NatayarkAuthorizer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
    private static int _running;

    /// <summary>
    ///     开始登录并等待完成。授权被拒绝或换取令牌失败时抛出异常；成功后 <see cref="NatayarkProfileManager.NaidProfile" /> 已填充。
    /// </summary>
    /// <exception cref="InvalidOperationException">已有登录流程正在进行，或未配置 Natayark 客户端。</exception>
    public static async Task AuthorizeAsync(CancellationToken cancellationToken = default)
    {
        var clientId = EnvironmentInterop.GetSecret("NAID_CLIENT_ID");
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Natayark client is not configured.");
        if (Interlocked.Exchange(ref _running, 1) == 1)
            throw new InvalidOperationException("Natayark authorization is already running.");

        try
        {
            var port = NetworkHelper.NewTcpPort();
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            LogWrapper.Info("OAuth", $"NatayarkID: 已开始监听 {port} 端口");

            var redirect = Uri.EscapeDataString($"http://localhost:{port}/callback");
            SystemShell.OpenUrl(
                $"https://account.naids.com/oauth2/authorize?response_type=code&client_id={clientId}&redirect_uri={redirect}");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);
            await using var registration = cts.Token.Register(listener.Close);

            while (true)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception) when (cts.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("Natayark authorization timed out.");
                }

                if (context.Request.Url?.AbsolutePath != "/callback" || !context.Request.IsLocal)
                {
                    Respond(context, 404, "Not Found");
                    continue;
                }

                var query = HttpUtility.ParseQueryString(context.Request.Url.Query);
                var error = query["error_description"] ?? query["error"];
                if (!string.IsNullOrEmpty(error))
                {
                    Respond(context, 200, Page(Lang.Text("Tools.GameLink.Natayark.LoginFailed")));
                    throw new InvalidOperationException(error);
                }

                var code = query["code"];
                if (string.IsNullOrEmpty(code))
                {
                    Respond(context, 400, "Bad Request");
                    continue;
                }

                try
                {
                    await NatayarkProfileManager.GetNaidDataAsync(code, port: (ushort)port).ConfigureAwait(false);
                    Respond(context, 200, Page(NatayarkProfileManager.NaidProfile.Username ?? ""));
                }
                catch
                {
                    Respond(context, 200, Page(Lang.Text("Tools.GameLink.Natayark.LoginFailed")));
                    throw;
                }

                return;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private static string Page(string message) =>
        "<!doctype html><html><head><meta charset=\"utf-8\"><title>PCL Shion</title></head>" +
        "<body style=\"font-family:sans-serif;text-align:center;margin-top:20vh\">" +
        $"<h2>{WebUtility.HtmlEncode(message)}</h2></body></html>";

    private static void Respond(HttpListenerContext context, int status, string body)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = status;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "OAuth", "回复回调请求失败");
        }
    }
}
