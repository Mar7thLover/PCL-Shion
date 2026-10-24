using System.Text;
using System.Text.Json.Nodes;

namespace PCL.Shion.Core.Accounts;

/// <summary>
/// Yggdrasil Connect 登录：从 OpenID 发现文档读取设备码与令牌端点，以设备码流程授权，
/// 再从 ID 令牌（或用户信息端点）读取可用的游戏档案。
/// </summary>
public sealed class YggdrasilConnectAccount
{
    private static readonly IReadOnlyDictionary<string, string> BuiltInClientIds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["littleskin.cn"] = "1514"
        };

    private static readonly string[] Scopes =
    [
        "openid",
        "offline_access",
        "Yggdrasil.PlayerProfiles.Select",
        "Yggdrasil.Server.Join"
    ];

    private readonly HttpClient _http;
    private string? _deviceEndpoint;
    private string? _tokenEndpoint;
    private string? _userInfoEndpoint;
    private string? _sharedClientId;

    /// <param name="http">网络客户端。</param>
    /// <param name="discoveryAddress">OpenID 发现文档地址。</param>
    /// <param name="clientId">客户端 ID；为空时依次使用内置客户端 ID 与服务器共享的客户端 ID。</param>
    /// <param name="serverAddress">Yggdrasil 服务器地址。</param>
    public YggdrasilConnectAccount(HttpClient http, string discoveryAddress, string? clientId = null, string? serverAddress = null)
    {
        _http = http;
        DiscoveryAddress = discoveryAddress;
        ServerAddress = serverAddress;
        ExplicitClientId = clientId?.Trim();
    }

    public string DiscoveryAddress { get; }

    public string? ServerAddress { get; }

    /// <summary>调用方指定的客户端 ID。</summary>
    public string? ExplicitClientId { get; }

    /// <summary>初始化后确定的客户端 ID。</summary>
    public string ClientId { get; private set; } = "";

    /// <summary>根据服务器主机名查找内置的客户端 ID。</summary>
    public static bool TryGetBuiltInClientId(string host, out string clientId)
    {
        var normalized = host.Trim().TrimEnd('.');
        if (BuiltInClientIds.TryGetValue(normalized, out var value))
        {
            clientId = value;
            return true;
        }

        clientId = "";
        return false;
    }

    /// <summary>读取发现文档，确定设备码端点、令牌端点与客户端 ID。</summary>
    /// <exception cref="AccountException">发现文档无效、不支持设备码，或没有可用的客户端 ID。</exception>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var response = await AccountHttp.SendAsync(_http, HttpMethod.Get, DiscoveryAddress, null, null, cancellationToken);
        if (!response.IsSuccess || response.Json is not JsonObject metadata)
            throw new AccountException("discovery_failed", "无法读取 Yggdrasil Connect 的 OpenID 配置。");

        _deviceEndpoint = AccountHttp.GetString(metadata, "device_authorization_endpoint");
        _tokenEndpoint = AccountHttp.GetString(metadata, "token_endpoint");
        _userInfoEndpoint = AccountHttp.GetString(metadata, "userinfo_endpoint");
        _sharedClientId = AccountHttp.GetString(metadata, "shared_client_id");
        if (string.IsNullOrEmpty(_deviceEndpoint) || string.IsNullOrEmpty(_tokenEndpoint))
            throw new AccountException("device_endpoint_missing", "该服务器不支持设备码登录。");

        var clientId = ExplicitClientId;
        if (string.IsNullOrEmpty(clientId) && ServerAddress is not null &&
            Uri.TryCreate(ServerAddress, UriKind.Absolute, out var serverUri) &&
            TryGetBuiltInClientId(serverUri.Host, out var builtInClientId))
            clientId = builtInClientId;
        if (string.IsNullOrEmpty(clientId))
            clientId = _sharedClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            throw new AccountException("client_id_missing", "Yggdrasil Connect 缺少客户端 ID。");
        ClientId = clientId;
    }

    /// <summary>申请设备码。</summary>
    public async Task<DeviceCodeInfo> RequestDeviceCodeAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var response = await AccountHttp.PostFormAsync(_http, _deviceEndpoint!,
        [
            KeyValuePair.Create("client_id", ClientId),
            KeyValuePair.Create("scope", string.Join(' ', Scopes))
        ], cancellationToken);
        if (!response.IsSuccess)
            throw AccountHttp.ToException(response, "device_authorization_failed");
        return AccountHttp.ParseDeviceCode(response.Json)
               ?? throw new AccountException("device_authorization_failed", "服务器没有返回设备码。");
    }

    /// <summary>轮询一次设备码授权结果。</summary>
    public async Task<TokenResponse> PollDeviceCodeAsync(string deviceCode, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var response = await AccountHttp.PostFormAsync(_http, _tokenEndpoint!,
        [
            KeyValuePair.Create("client_id", ClientId),
            KeyValuePair.Create("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
            KeyValuePair.Create("device_code", deviceCode)
        ], cancellationToken);
        return AccountHttp.ParseToken(response);
    }

    /// <summary>用刷新令牌换取新的令牌。</summary>
    /// <exception cref="AccountException">刷新失败。</exception>
    public async Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var response = await AccountHttp.PostFormAsync(_http, _tokenEndpoint!,
        [
            KeyValuePair.Create("client_id", ClientId),
            KeyValuePair.Create("grant_type", "refresh_token"),
            KeyValuePair.Create("refresh_token", refreshToken),
            KeyValuePair.Create("scope", string.Join(' ', Scopes))
        ], cancellationToken);
        var token = AccountHttp.ParseToken(response);
        if (token.IsError)
            throw new AccountException(token.Error!, token.ErrorDescription ?? "Yggdrasil Connect 令牌刷新失败。");
        return token with { RefreshToken = token.RefreshToken ?? refreshToken };
    }

    /// <summary>
    /// 由授权结果得到登录信息。档案来自 ID 令牌的 <c>selectedProfile</c> / <c>availableProfiles</c>，
    /// 不完整时回退到用户信息端点；有多个可选档案时调用 <paramref name="profileSelector"/> 让用户选择。
    /// </summary>
    /// <exception cref="AccountException">授权失败或没有可用的档案。</exception>
    public async Task<AccountLoginResult> CompleteAsync(TokenResponse oauth,
        Func<IReadOnlyList<AccountCandidate>, Task<AccountCandidate?>> profileSelector,
        string? preferredUuid = null, CancellationToken cancellationToken = default)
    {
        if (oauth.IsError)
            throw new AccountException(oauth.Error!, oauth.ErrorDescription ?? "Yggdrasil Connect 授权失败。");
        if (string.IsNullOrWhiteSpace(oauth.AccessToken))
            throw new AccountException("invalid_token", "Yggdrasil Connect 没有返回访问令牌。");
        if (string.IsNullOrWhiteSpace(oauth.IdToken))
            throw new AccountException("id_token_missing", "Yggdrasil Connect 没有返回 ID 令牌。");

        var (selected, available) = ReadClaims(ReadJwtPayload(oauth.IdToken));
        if (selected is null || available.Count == 0)
        {
            var (userSelected, userAvailable) = await FetchUserInfoAsync(oauth.AccessToken, cancellationToken);
            selected ??= userSelected;
            if (available.Count == 0)
                available = userAvailable;
        }

        var profile = selected;
        if (profile is null && !string.IsNullOrEmpty(preferredUuid))
            profile = available.FirstOrDefault(candidate => candidate.Id == preferredUuid);
        if (profile is null && available.Count > 1)
        {
            profile = await profileSelector(available)
                      ?? throw new AccountException("access_denied", "已取消选择 Yggdrasil Connect 档案。");
        }

        profile ??= available.FirstOrDefault()
                    ?? throw new AccountException("invalid_profile", "Yggdrasil Connect 没有返回玩家档案。");

        return new AccountLoginResult(ProfileType.YggdrasilConnect, profile.Name, profile.Id, oauth.AccessToken, profile.Id)
        {
            RefreshToken = oauth.RefreshToken,
            IdToken = oauth.IdToken,
            ExpiresAt = oauth.ExpiresIn > 0 ? DateTimeOffset.UtcNow.AddSeconds(oauth.ExpiresIn) : null,
            Server = GetServerAddress(),
            ServerName = await FetchServerNameAsync(cancellationToken),
            DiscoveryAddress = DiscoveryAddress,
            ClientId = ClientId
        };
    }

    /// <summary>得到 authserver 地址：优先使用调用方给出的服务器地址，否则从发现地址推导。</summary>
    public string GetServerAddress()
    {
        if (!string.IsNullOrWhiteSpace(ServerAddress))
        {
            var server = ServerAddress.TrimEnd('/');
            return server.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase) ? server : server + "/authserver";
        }

        var marker = DiscoveryAddress.IndexOf("/.well-known/", StringComparison.OrdinalIgnoreCase);
        return marker > 0 ? DiscoveryAddress[..marker].TrimEnd('/') : DiscoveryAddress.TrimEnd('/');
    }

    private void EnsureInitialized()
    {
        if (_deviceEndpoint is null || _tokenEndpoint is null)
            throw new InvalidOperationException("请先调用 InitializeAsync。");
    }

    private static JsonNode? ReadJwtPayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            throw new AccountException("invalid_id_token", "ID 令牌格式无效。");

        // ID 令牌直接从令牌端点经 TLS 取得，这里只读取声明，不再重复验证签名
        var base64 = parts[1].Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        try
        {
            return AccountHttp.ParseJson(Encoding.UTF8.GetString(Convert.FromBase64String(base64)))
                   ?? throw new AccountException("invalid_id_token", "ID 令牌内容无效。");
        }
        catch (FormatException)
        {
            throw new AccountException("invalid_id_token", "ID 令牌内容无效。");
        }
    }

    private static (AccountCandidate? Selected, List<AccountCandidate> Available) ReadClaims(JsonNode? claims)
    {
        return (AccountHttp.ReadCandidate(claims?["selectedProfile"]), AccountHttp.ReadCandidates(claims?["availableProfiles"]));
    }

    private async Task<(AccountCandidate? Selected, List<AccountCandidate> Available)> FetchUserInfoAsync(
        string accessToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_userInfoEndpoint))
            return (null, []);
        var response = await AccountHttp.SendAsync(_http, HttpMethod.Get, _userInfoEndpoint, null, accessToken,
            cancellationToken);
        return response.IsSuccess && response.Json is not null ? ReadClaims(response.Json) : (null, []);
    }

    private async Task<string?> FetchServerNameAsync(CancellationToken cancellationToken)
    {
        try
        {
            var root = GetServerAddress().TrimEnd('/');
            if (root.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase))
                root = root[..^"/authserver".Length];
            var response = await AccountHttp.SendAsync(_http, HttpMethod.Get, root, null, null, cancellationToken);
            return response.IsSuccess ? AccountHttp.GetString(response.Json?["meta"], "serverName") : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
