namespace PCL.Shion.Core.Accounts;

/// <summary>
/// 账号验证失败。<see cref="Error"/> 是协议返回的错误码，例如 <c>xsts_2148916233</c>、<c>minecraft_not_owned</c>。
/// </summary>
public sealed class AccountException(string error, string message, Exception? inner = null) : Exception(message, inner)
{
    public string Error { get; } = error;
}

/// <summary>
/// OAuth 2.0 设备码授权的第一步结果：用户需要在网页上输入 <see cref="UserCode"/>。
/// </summary>
public sealed record DeviceCodeInfo(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    string? VerificationUriComplete,
    int ExpiresIn,
    int Interval);

/// <summary>
/// 令牌端点的响应。授权未完成时 <see cref="Error"/> 为 <c>authorization_pending</c> 或 <c>slow_down</c> 等。
/// </summary>
public sealed record TokenResponse(
    string? AccessToken,
    string? RefreshToken,
    string? IdToken,
    string? TokenType,
    int ExpiresIn,
    string? Error,
    string? ErrorDescription)
{
    public bool IsError => !string.IsNullOrEmpty(Error);
}

/// <summary>
/// 第三方服务器返回的一个可选游戏档案。
/// </summary>
public sealed record AccountCandidate(string Id, string Name);

/// <summary>
/// 一次成功登录得到的账号信息，由 <see cref="ProfileService.ApplyLogin"/> 写入档案。
/// </summary>
public sealed record AccountLoginResult(ProfileType ProfileType, string UserName, string Uuid, string AccessToken, string ClientToken)
{
    public string? RefreshToken { get; init; }

    public string? IdToken { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public string? Server { get; init; }

    public string? ServerName { get; init; }

    public string? LoginName { get; init; }

    public string? DiscoveryAddress { get; init; }

    public string? ClientId { get; init; }
}
