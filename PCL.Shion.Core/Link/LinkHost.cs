using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Shion.Core.Link;

/// <summary>
///     联机组件的宿主环境。WPF 版的联机代码（PCL.Core.Link）直接依赖应用层的配置、日志、语言与提示，
///     Shion 中这些由界面层提供，核心库只通过这里的委托与接口访问。
/// </summary>
public static class LinkHost
{
    /// <summary>联机设置与状态的存取。</summary>
    public static ILinkSettings Settings { get; set; } = new MemoryLinkSettings();

    /// <summary>日志：级别、文本、异常。</summary>
    public static Action<LinkLogLevel, string, Exception?> Log { get; set; } = static (_, _, _) => { };

    /// <summary>按键取本地化文本并格式化；找不到时返回键本身。</summary>
    public static Func<string, object?[], string> Text { get; set; } = static (key, _) => key;

    /// <summary>在界面上显示提示。</summary>
    public static Action<string, HintTheme> Hint { get; set; } = static (_, _) => { };

    /// <summary>在界面线程上执行。</summary>
    public static Func<Action, Task> RunInUi { get; set; } = static action =>
    {
        action();
        return Task.CompletedTask;
    };

    /// <summary>启动器版本号（用于联机时向其他玩家展示）。</summary>
    public static string VersionName { get; set; } = "Shion";

    /// <summary>启动器内部版本号，公告按它过滤适用范围。</summary>
    public static int VersionCode { get; set; } = 1000;

    /// <summary>联机根服务器列表，以竖线分隔配置。</summary>
    public static string[] LinkServers => (SecretProvider("LINK_SERVER_ROOT") ?? "")
        .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>联机组件的共享数据目录，EasyTier 解压在其下的 EasyTier 子目录。</summary>
    public static string DataDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCL Shion", "Link");

    /// <summary>机器识别码，用作联机时的设备标识。</summary>
    public static Func<string> LauncherIdProvider { get; set; } = static () => Identify.Fallback();

    /// <summary>读取密钥，失败返回 null。</summary>
    public static Func<string, string?> SecretProvider { get; set; } = BuildSecrets.Get;
}

public enum LinkLogLevel
{
    Debug,
    Info,
    Warn,
    Error
}

public enum HintTheme
{
    Info,
    Success,
    Error
}

/// <summary>联机协议偏好，与 WPF 的 <c>LinkProtocolPreference</c> 一致。</summary>
public enum LinkProtocolPreference
{
    Tcp,
    Udp
}

/// <summary>联机中继行为，与 WPF 的 <c>LinkRelayBehavior</c> 一致。</summary>
public enum LinkRelayBehavior
{
    Default = 0,
    ForceRelay = 1
}

/// <summary>联机相关的设置（WPF 的 Config.Link）与状态（WPF 的 States.Link）。</summary>
public interface ILinkSettings
{
    string Username { get; set; }
    LinkRelayBehavior RelayType { get; set; }
    int ServerType { get; set; }
    bool UseLatencyFirstMode { get; set; }
    string CustomRelayServer { get; set; }
    LinkProtocolPreference ProtocolPreference { get; set; }
    bool TryPunchSym { get; set; }
    bool EnableIPv6 { get; set; }
    bool EnableCliOutput { get; set; }

    bool LinkEula { get; set; }
    string AnnounceCache { get; set; }
    int AnnounceCacheVer { get; set; }
    string NaidRefreshToken { get; set; }
    string NaidRefreshExpireTime { get; set; }
    bool DoFirstTimeNetTest { get; set; }

    void Save();
}

internal sealed class MemoryLinkSettings : ILinkSettings
{
    public string Username { get; set; } = "";
    public LinkRelayBehavior RelayType { get; set; }
    public int ServerType { get; set; } = 1;
    public bool UseLatencyFirstMode { get; set; } = true;
    public string CustomRelayServer { get; set; } = "";
    public LinkProtocolPreference ProtocolPreference { get; set; }
    public bool TryPunchSym { get; set; } = true;
    public bool EnableIPv6 { get; set; } = true;
    public bool EnableCliOutput { get; set; }
    public bool LinkEula { get; set; }
    public string AnnounceCache { get; set; } = "";
    public int AnnounceCacheVer { get; set; }
    public string NaidRefreshToken { get; set; } = "";
    public string NaidRefreshExpireTime { get; set; } = "";
    public bool DoFirstTimeNetTest { get; set; } = true;

    public void Save()
    {
    }
}

/// <summary>对应 WPF 的 <c>Config.Link</c>。</summary>
public static class Config
{
    public static ILinkSettings Link => LinkHost.Settings;
}

/// <summary>对应 WPF 的 <c>States.Link</c>。</summary>
public static class States
{
    public static ILinkSettings Link => LinkHost.Settings;
}

/// <summary>对应 WPF 的 <c>LogWrapper</c>。</summary>
public static class LogWrapper
{
    public static void Debug(string msg) => LinkHost.Log(LinkLogLevel.Debug, msg, null);
    public static void Debug(string module, string msg) => LinkHost.Log(LinkLogLevel.Debug, $"[{module}] {msg}", null);
    public static void Debug(Exception ex, string module, string msg) => LinkHost.Log(LinkLogLevel.Debug, $"[{module}] {msg}", ex);

    public static void Info(string msg) => LinkHost.Log(LinkLogLevel.Info, msg, null);
    public static void Info(string module, string msg) => LinkHost.Log(LinkLogLevel.Info, $"[{module}] {msg}", null);

    public static void Warn(string module, string msg) => LinkHost.Log(LinkLogLevel.Warn, $"[{module}] {msg}", null);
    public static void Warn(Exception? ex, string module, string msg) => LinkHost.Log(LinkLogLevel.Warn, $"[{module}] {msg}", ex);

    public static void Error(string module, string msg) => LinkHost.Log(LinkLogLevel.Error, $"[{module}] {msg}", null);
    public static void Error(Exception? ex, string module, string msg) => LinkHost.Log(LinkLogLevel.Error, $"[{module}] {msg}", ex);
    public static void Error(Exception? ex, string msg) => LinkHost.Log(LinkLogLevel.Error, msg, ex);
}

/// <summary>对应 WPF 的 <c>Lang</c>。</summary>
public static class Lang
{
    public static string Text(string key, params object?[] args) => LinkHost.Text(key, args);
}

/// <summary>对应 WPF 的 <c>HintWrapper</c>。</summary>
public static class HintWrapper
{
    public static void Show(string text, HintTheme theme = HintTheme.Info) => LinkHost.Hint(text, theme);
}

/// <summary>对应 WPF 的 <c>Basics</c>。</summary>
public static class Basics
{
    public static string VersionName => LinkHost.VersionName;
}

/// <summary>对应 WPF 的 <c>EnvironmentInterop</c> 中读取密钥的部分。</summary>
public static class EnvironmentInterop
{
    public static string? GetSecret(string key) => LinkHost.SecretProvider(key);
}

/// <summary>对应 WPF 的 <c>Paths</c>。</summary>
public static class Paths
{
    public static string SharedLocalData => LinkHost.DataDirectory;
}

/// <summary>对应 WPF 的 <c>Identify</c>。</summary>
public static class Identify
{
    public static string LauncherId => LinkHost.LauncherIdProvider();

    /// <summary>
    ///     WPF 版根据主板、CPU 等 WMI 信息计算识别码，这里改用系统的机器标识（Windows 为 MachineGuid，其他系统为机器名），
    ///     取哈希的前 16 位。识别码与 WPF 版不同，但在同一台机器上保持稳定。
    /// </summary>
    internal static string Fallback()
    {
        var machine = Environment.MachineName;
        if (OperatingSystem.IsWindows())
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                if (key?.GetValue("MachineGuid") is string guid && !string.IsNullOrWhiteSpace(guid))
                    machine = guid;
            }
            catch
            {
                // 读取失败时退回机器名
            }

        var source = "PCL-CE|" + machine + "|LauncherId";
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(hash)[..16];
    }
}

/// <summary>对应 WPF 的 <c>NetworkHelper</c>。</summary>
public static class NetworkHelper
{
    public static int NewTcpPort()
    {
        using var so = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        so.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return so.LocalEndPoint == null ? 0 : ((IPEndPoint)so.LocalEndPoint).Port;
    }

    public static bool IsNetworkAvailable() => NetworkInterface.GetIsNetworkAvailable();
}

/// <summary>对应 WPF 的 <c>HttpRequest</c> 及其扩展方法中联机用到的部分。</summary>
public static class HttpRequest
{
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-Shion");
        return client;
    }

    public static HttpRequestMessage Create(string url) => new(HttpMethod.Get, new Uri(url));

    public static HttpRequestMessage CreatePost(string url) => new(HttpMethod.Post, new Uri(url));

    public static HttpRequestMessage WithContent(this HttpRequestMessage message, HttpContent content)
    {
        message.Content = content;
        return message;
    }

    public static HttpRequestMessage WithBearerToken(this HttpRequestMessage message, string token)
    {
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    public static async Task<HttpResponseMessage> SendAsync(this HttpRequestMessage message, CancellationToken cancellationToken = default)
    {
        using var request = message;
        return await Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
    }

    public static Task<string> AsStringAsync(this HttpResponseMessage message, CancellationToken cancellationToken = default) =>
        message.Content.ReadAsStringAsync(cancellationToken);

    public static async Task<T?> AsJsonAsync<T>(this HttpResponseMessage message, CancellationToken cancellationToken = default)
    {
        await using var stream = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken)
            .ConfigureAwait(false);
    }

    extension(HttpResponseMessage message)
    {
        public bool IsSuccess => message.IsSuccessStatusCode;
    }

    /// <summary>公开的共享客户端，供下载公告等场景使用。</summary>
    public static HttpClient Shared => Client;
}

public static class LinkStringExtensions
{
    public static string ReplaceNullOrEmpty(this string? input, string? replacement = null) =>
        string.IsNullOrEmpty(input) ? replacement ?? string.Empty : input;
}
