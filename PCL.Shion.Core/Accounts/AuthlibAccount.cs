using System.Text.Json.Nodes;

namespace PCL.Shion.Core.Accounts;

/// <summary>
/// authlib-injector 第三方验证（Yggdrasil 协议）：解析 API 地址，用账号密码认证，刷新、校验令牌并绑定游戏档案。
/// 依据 Yggdrasil 认证协议与 authlib-injector 规范实现。
/// </summary>
public static class AuthlibAccount
{
    /// <summary>预设的第三方验证服务器。</summary>
    public const string DefaultServer = "https://littleskin.cn/api/yggdrasil";

    /// <summary>预设服务器的注册页面。</summary>
    public const string RegisterAddress = "https://littleskin.cn/auth/register";

    private const string ApiLocationHeader = "X-Authlib-Injector-API-Location";
    private const string AgentName = "Minecraft";

    private sealed record Session(string AccessToken, string ClientToken, AccountCandidate? Selected);

    /// <summary>
    /// 解析服务器的 API 地址。服务器响应中带有 <c>X-Authlib-Injector-API-Location</c> 时使用该地址，否则使用原地址。
    /// 请求失败时返回原地址。
    /// </summary>
    public static async Task<string> ResolveApiLocationAsync(HttpClient http, string serverUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, serverUrl);
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues(ApiLocationHeader, out var values) &&
                values.FirstOrDefault() is { Length: > 0 } location)
                return new Uri(new Uri(serverUrl), location).AbsoluteUri;
        }
        catch (HttpRequestException)
        {
            // 无法访问时沿用原地址，后续登录会给出具体的错误
        }

        return serverUrl;
    }

    /// <summary>读取服务器的元数据（根路径返回的 JSON）。失败时返回 null。</summary>
    public static async Task<JsonNode?> GetMetadataAsync(HttpClient http, string apiLocation,
        CancellationToken cancellationToken = default)
    {
        var response = await AccountHttp.SendAsync(http, HttpMethod.Get, apiLocation, null, null, cancellationToken);
        return response.IsSuccess ? response.Json : null;
    }

    /// <summary>从元数据得到 OpenID 发现地址，服务器不支持 Yggdrasil Connect 时返回 null。</summary>
    public static string? GetOpenIdDiscoveryAddress(JsonNode? metadata)
    {
        return AccountHttp.GetString(metadata?["meta"], "feature.openid_configuration_url") ??
               AccountHttp.GetString(metadata, "feature.openid_configuration_url");
    }

    /// <summary>从元数据得到服务器名称。</summary>
    public static string? GetServerName(JsonNode? metadata)
    {
        return AccountHttp.GetString(metadata?["meta"], "serverName");
    }

    /// <summary>
    /// 使用账号密码登录。服务器没有直接指定游戏档案时，调用 <paramref name="profileSelector"/> 让用户选择。
    /// </summary>
    /// <exception cref="AccountException">账号或密码错误，或服务器返回无效数据。</exception>
    public static async Task<AccountLoginResult> AuthenticateAsync(HttpClient http, string apiLocation, string username,
        string password, Func<IReadOnlyList<AccountCandidate>, Task<AccountCandidate?>> profileSelector,
        CancellationToken cancellationToken = default)
    {
        var clientToken = Guid.NewGuid().ToString("N");
        var response = await AccountHttp.PostJsonAsync(http, Endpoint(apiLocation, "authserver/authenticate"), new JsonObject
        {
            ["agent"] = new JsonObject { ["name"] = AgentName, ["version"] = JsonValue.Create(1) },
            ["username"] = username,
            ["password"] = password,
            ["clientToken"] = clientToken,
            ["requestUser"] = JsonValue.Create(false)
        }, null, cancellationToken);
        if (!response.IsSuccess)
            throw AccountHttp.ToException(response, "authentication_failed");

        var json = response.Json;
        var accessToken = AccountHttp.GetString(json, "accessToken")
                          ?? throw new AccountException("invalid_response", "服务器没有返回访问令牌。");
        var session = new Session(accessToken, AccountHttp.GetString(json, "clientToken") ?? clientToken,
            AccountHttp.ReadCandidate(json?["selectedProfile"]));

        if (session.Selected is null)
        {
            var available = AccountHttp.ReadCandidates(json?["availableProfiles"]);
            if (available.Count == 0)
                throw new AccountException("invalid_profile", "该账号没有可用的游戏档案。");
            var chosen = available.Count == 1
                ? available[0]
                : await profileSelector(available) ?? throw new AccountException("access_denied", "已取消选择游戏档案。");
            session = await RefreshSessionAsync(http, apiLocation, session.AccessToken, session.ClientToken, chosen,
                cancellationToken);
        }

        var profile = session.Selected ?? throw new AccountException("invalid_profile", "服务器没有绑定游戏档案。");
        var serverName = GetServerName(await GetMetadataAsync(http, apiLocation, cancellationToken));
        return new AccountLoginResult(ProfileType.Authlib, profile.Name, profile.Id, session.AccessToken, session.ClientToken)
        {
            LoginName = username,
            Server = apiLocation,
            ServerName = serverName
        };
    }

    /// <summary>
    /// 刷新已保存的第三方档案的令牌。Yggdrasil Connect 档案使用 OpenID 刷新令牌，authlib-injector 档案使用 authserver/refresh。
    /// </summary>
    /// <exception cref="AccountException">令牌刷新失败，需要重新登录。</exception>
    public static async Task<AccountLoginResult> RefreshProfileAsync(HttpClient http, McProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (profile.ProfileType == ProfileType.YggdrasilConnect)
            return await RefreshYggdrasilConnectAsync(http, profile, cancellationToken);

        var apiLocation = profile.Server ?? throw new AccountException("server_missing", "第三方档案缺少服务器地址。");
        var session = await RefreshSessionAsync(http, apiLocation, profile.AccessToken, profile.ClientToken, null,
            cancellationToken);
        var selected = session.Selected ?? new AccountCandidate(profile.Uuid, profile.UserName);
        return new AccountLoginResult(ProfileType.Authlib, selected.Name, selected.Id, session.AccessToken, session.ClientToken)
        {
            LoginName = profile.LoginName,
            Server = apiLocation,
            ServerName = profile.ServerName
        };
    }

    private static async Task<AccountLoginResult> RefreshYggdrasilConnectAsync(HttpClient http, McProfile profile,
        CancellationToken cancellationToken)
    {
        var refreshToken = profile.RefreshToken
                           ?? throw new AccountException("invalid_grant", "Yggdrasil Connect 档案缺少刷新令牌。");
        var discovery = profile.DiscoveryAddress
                        ?? throw new AccountException("discovery_missing", "Yggdrasil Connect 档案缺少发现地址。");
        var account = new YggdrasilConnectAccount(http, discovery, profile.ClientId, profile.Server);
        await account.InitializeAsync(cancellationToken);
        var refreshed = await account.RefreshAsync(refreshToken, cancellationToken);
        // 已保存的档案 UUID 优先；若服务器返回多个档案且无法匹配，则要求重新登录
        return await account.CompleteAsync(refreshed,
            _ => throw new AccountException("profile_selection_required", "Yggdrasil Connect 返回了多个档案，请重新登录。"),
            profile.Uuid, cancellationToken);
    }

    /// <summary>校验访问令牌是否仍然有效。</summary>
    public static async Task<bool> ValidateAsync(HttpClient http, string apiLocation, string accessToken,
        string clientToken, CancellationToken cancellationToken = default)
    {
        var response = await AccountHttp.PostJsonAsync(http, Endpoint(apiLocation, "authserver/validate"), new JsonObject
        {
            ["accessToken"] = accessToken,
            ["clientToken"] = clientToken
        }, null, cancellationToken);
        return response.IsSuccess;
    }

    private static async Task<Session> RefreshSessionAsync(HttpClient http, string apiLocation, string accessToken,
        string clientToken, AccountCandidate? profile, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["accessToken"] = accessToken,
            ["clientToken"] = clientToken,
            ["requestUser"] = JsonValue.Create(false)
        };
        if (profile is not null)
        {
            body["selectedProfile"] = new JsonObject
            {
                ["id"] = profile.Id,
                ["name"] = profile.Name
            };
        }

        var response = await AccountHttp.PostJsonAsync(http, Endpoint(apiLocation, "authserver/refresh"), body, null,
            cancellationToken);
        if (!response.IsSuccess)
            throw AccountHttp.ToException(response, "refresh_failed");

        var json = response.Json;
        var newAccessToken = AccountHttp.GetString(json, "accessToken")
                             ?? throw new AccountException("invalid_response", "服务器没有返回访问令牌。");
        return new Session(newAccessToken, AccountHttp.GetString(json, "clientToken") ?? clientToken,
            AccountHttp.ReadCandidate(json?["selectedProfile"]));
    }

    private static string Endpoint(string apiLocation, string path) => $"{apiLocation.TrimEnd('/')}/{path}";
}
