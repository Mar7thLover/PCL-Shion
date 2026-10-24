using System.Net;
using System.Text.Json.Nodes;

namespace PCL.Shion.Core.Accounts;

/// <summary>
/// 微软正版账号登录：设备码授权 → Xbox Live 用户令牌 → XSTS 令牌 → Minecraft 服务令牌 → 正版校验 → 玩家档案。
/// 依据微软 OAuth 2.0 设备码流程，以及 Xbox Live 与 Minecraft 服务的公开接口实现。
/// </summary>
public static class MicrosoftAccount
{
    /// <summary>保存 Microsoft Entra 应用客户端 ID 的环境变量。客户端 ID 不写入源码，发布构建时由 <see cref="BuildSecrets"/> 写入。</summary>
    public const string ClientIdEnvironmentVariable = "PCL_MS_CLIENT_ID";

    private const string DeviceCodeEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    private const string TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string Scope = "XboxLive.signin offline_access";
    private const string XboxUserAuthEndpoint = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsEndpoint = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string MinecraftLoginEndpoint = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string EntitlementsEndpoint = "https://api.minecraftservices.com/entitlements/mcstore";
    private const string ProfileEndpoint = "https://api.minecraftservices.com/minecraft/profile";

    /// <summary>读取微软登录使用的客户端 ID。</summary>
    /// <exception cref="AccountException">构建时未写入，且环境变量未设置。</exception>
    public static string GetClientId()
    {
        var clientId = BuildSecrets.MicrosoftClientId;
        if (string.IsNullOrEmpty(clientId))
            throw new AccountException("client_id_missing",
                $"未配置微软登录的客户端 ID，请设置环境变量 {ClientIdEnvironmentVariable}。");
        return clientId;
    }

    /// <summary>申请设备码，用户需要在网页上输入返回的用户代码。</summary>
    public static async Task<DeviceCodeInfo> RequestDeviceCodeAsync(HttpClient http, CancellationToken cancellationToken = default)
    {
        var response = await AccountHttp.PostFormAsync(http, DeviceCodeEndpoint,
        [
            KeyValuePair.Create("client_id", GetClientId()),
            KeyValuePair.Create("scope", Scope)
        ], cancellationToken);
        if (!response.IsSuccess)
            throw AccountHttp.ToException(response, "device_authorization_failed");
        return AccountHttp.ParseDeviceCode(response.Json)
               ?? throw new AccountException("invalid_response", "微软设备码响应无效。");
    }

    /// <summary>轮询一次设备码授权结果。尚未授权时返回 <c>authorization_pending</c> 错误。</summary>
    public static async Task<TokenResponse> PollDeviceCodeAsync(HttpClient http, DeviceCodeInfo device,
        CancellationToken cancellationToken = default)
    {
        var response = await AccountHttp.PostFormAsync(http, TokenEndpoint,
        [
            KeyValuePair.Create("client_id", GetClientId()),
            KeyValuePair.Create("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
            KeyValuePair.Create("device_code", device.DeviceCode)
        ], cancellationToken);
        return AccountHttp.ParseToken(response);
    }

    /// <summary>用刷新令牌换取新的微软令牌。</summary>
    /// <exception cref="AccountException">刷新失败，例如令牌已经失效。</exception>
    public static async Task<TokenResponse> RefreshAsync(HttpClient http, string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var response = await AccountHttp.PostFormAsync(http, TokenEndpoint,
        [
            KeyValuePair.Create("client_id", GetClientId()),
            KeyValuePair.Create("grant_type", "refresh_token"),
            KeyValuePair.Create("refresh_token", refreshToken),
            KeyValuePair.Create("scope", Scope)
        ], cancellationToken);
        var token = AccountHttp.ParseToken(response);
        if (token.IsError)
            throw new AccountException(token.Error!, token.ErrorDescription ?? "微软令牌刷新失败。");
        return token with { RefreshToken = token.RefreshToken ?? refreshToken };
    }

    /// <summary>用刷新令牌完成一次完整的重新登录。</summary>
    public static async Task<AccountLoginResult> LoginWithRefreshTokenAsync(HttpClient http, string refreshToken,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var oauth = await RefreshAsync(http, refreshToken, cancellationToken);
        return await CompleteAsync(http, oauth, progress, cancellationToken);
    }

    /// <summary>
    /// 从微软令牌出发，依次完成 Xbox Live、XSTS、Minecraft 登录、正版校验与玩家档案查询。
    /// </summary>
    /// <param name="progress">进度，取值 0 到 1。</param>
    /// <exception cref="AccountException">任何一步失败。错误码如 <c>xsts_2148916233</c>、<c>minecraft_not_owned</c>、<c>profile_not_created</c>。</exception>
    public static async Task<AccountLoginResult> CompleteAsync(HttpClient http, TokenResponse oauth,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (oauth.IsError)
            throw new AccountException(oauth.Error!, oauth.ErrorDescription ?? "微软授权失败。");
        if (string.IsNullOrWhiteSpace(oauth.AccessToken))
            throw new AccountException("invalid_token", "微软授权没有返回访问令牌。");

        progress?.Report(0.1);
        var xboxUser = await RequestXboxTokenAsync(http, XboxUserAuthEndpoint, new JsonObject
        {
            ["Properties"] = new JsonObject
            {
                ["AuthMethod"] = "RPS",
                ["SiteName"] = "user.auth.xboxlive.com",
                ["RpsTicket"] = $"d={oauth.AccessToken}"
            },
            ["RelyingParty"] = "http://auth.xboxlive.com",
            ["TokenType"] = "JWT"
        }, isXsts: false, cancellationToken);

        progress?.Report(0.3);
        var xsts = await RequestXboxTokenAsync(http, XstsEndpoint, new JsonObject
        {
            ["Properties"] = new JsonObject
            {
                ["SandboxId"] = "RETAIL",
                ["UserTokens"] = new JsonArray(JsonValue.Create(xboxUser.Token))
            },
            ["RelyingParty"] = "rp://api.minecraftservices.com/",
            ["TokenType"] = "JWT"
        }, isXsts: true, cancellationToken);
        if (string.IsNullOrEmpty(xsts.UserHash))
            throw new AccountException("xsts_missing_user_hash", "XSTS 响应中没有用户标识。");

        progress?.Report(0.5);
        var minecraftResponse = await AccountHttp.PostJsonAsync(http, MinecraftLoginEndpoint, new JsonObject
        {
            ["identityToken"] = $"XBL3.0 x={xsts.UserHash};{xsts.Token}"
        }, null, cancellationToken);
        var minecraftToken = AccountHttp.GetString(minecraftResponse.Json, "access_token");
        if (!minecraftResponse.IsSuccess || string.IsNullOrEmpty(minecraftToken))
            throw AccountHttp.ToException(minecraftResponse, "minecraft_authentication_failed");
        var expiresIn = AccountHttp.GetInt(minecraftResponse.Json, "expires_in", 0);

        progress?.Report(0.7);
        var entitlements = await AccountHttp.SendAsync(http, HttpMethod.Get, EntitlementsEndpoint, null, minecraftToken,
            cancellationToken);
        if (!entitlements.IsSuccess)
            throw AccountHttp.ToException(entitlements, "minecraft_entitlements_failed");
        if (entitlements.Json?["items"] is not JsonArray { Count: > 0 })
            throw new AccountException("minecraft_not_owned", "该微软账号没有购买 Minecraft。");

        progress?.Report(0.9);
        var profileResponse = await AccountHttp.SendAsync(http, HttpMethod.Get, ProfileEndpoint, null, minecraftToken,
            cancellationToken);
        if (profileResponse.Status == HttpStatusCode.NotFound)
            throw new AccountException("profile_not_created", "该微软账号还没有创建 Minecraft 玩家档案。");
        if (!profileResponse.IsSuccess)
            throw AccountHttp.ToException(profileResponse, "minecraft_profile_failed");
        var uuid = AccountHttp.GetString(profileResponse.Json, "id");
        var name = AccountHttp.GetString(profileResponse.Json, "name");
        if (string.IsNullOrEmpty(uuid) || string.IsNullOrEmpty(name))
            throw new AccountException("minecraft_profile_failed", "Minecraft 玩家档案信息不完整。");

        progress?.Report(1d);
        return new AccountLoginResult(ProfileType.Microsoft, name, uuid, minecraftToken, uuid)
        {
            RefreshToken = oauth.RefreshToken,
            ExpiresAt = expiresIn > 0 ? DateTimeOffset.UtcNow.AddSeconds(expiresIn) : null
        };
    }

    private sealed record XboxToken(string Token, string? UserHash);

    private static async Task<XboxToken> RequestXboxTokenAsync(HttpClient http, string url, JsonNode body, bool isXsts,
        CancellationToken cancellationToken)
    {
        var response = await AccountHttp.PostJsonAsync(http, url, body, null, cancellationToken);
        var json = response.Json;
        if (!response.IsSuccess)
        {
            // XSTS 失败时返回 XErr 错误码，例如 2148916233 表示账号尚未注册 Xbox
            if (isXsts && AccountHttp.GetLong(json, "XErr") is { } code)
                throw new AccountException($"xsts_{code}", $"XSTS 认证失败（错误码 {code}）。");
            throw new AccountException(isXsts ? "xsts_authentication_failed" : "xbox_authentication_failed",
                $"Xbox Live 认证失败（HTTP {(int)response.Status}）。");
        }

        var token = AccountHttp.GetString(json, "Token");
        if (string.IsNullOrEmpty(token))
            throw new AccountException(isXsts ? "xsts_authentication_failed" : "xbox_authentication_failed",
                "Xbox Live 没有返回令牌。");
        var userHash = json?["DisplayClaims"]?["xui"] is JsonArray { Count: > 0 } xui
            ? AccountHttp.GetString(xui[0], "uhs")
            : null;
        return new XboxToken(token, userHash);
    }
}
