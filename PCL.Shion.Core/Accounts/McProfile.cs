namespace PCL.Shion.Core.Accounts;

/// <summary>
/// 档案的验证方式。
/// </summary>
public enum ProfileType
{
    /// <summary>离线档案，不需要网络验证。</summary>
    Offline,

    /// <summary>微软正版账号。</summary>
    Microsoft,

    /// <summary>authlib-injector 第三方验证服务器（Yggdrasil 协议）。</summary>
    Authlib,

    /// <summary>Yggdrasil Connect，通过 OpenID 设备码登录的第三方服务器。</summary>
    YggdrasilConnect
}

/// <summary>
/// 新建档案时选择的登录方式。
/// </summary>
public enum LoginType
{
    Microsoft,
    Authlib,
    Offline
}

/// <summary>
/// 启动器中保存的一个游戏档案，包含玩家名、UUID 与验证所需的令牌。
/// </summary>
public sealed class McProfile
{
    public string ProfileId { get; set; } = Guid.NewGuid().ToString("N");

    public ProfileType ProfileType { get; set; }

    public string UserName { get; set; } = "";

    public string Uuid { get; set; } = "";

    public string Description { get; set; } = "";

    public string AccessToken { get; set; } = "";

    public string ClientToken { get; set; } = "";

    public string? RefreshToken { get; set; }

    public string? IdToken { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>第三方验证服务器的 API 地址（authlib-injector 的 API 根，或 Yggdrasil Connect 的 authserver 地址）。</summary>
    public string? Server { get; set; }

    /// <summary>第三方验证服务器的名称。</summary>
    public string? ServerName { get; set; }

    /// <summary>第三方账号的登录名（邮箱或用户名）。</summary>
    public string? LoginName { get; set; }

    /// <summary>Yggdrasil Connect 的 OpenID 发现地址。</summary>
    public string? DiscoveryAddress { get; set; }

    /// <summary>Yggdrasil Connect 使用的客户端 ID。</summary>
    public string? ClientId { get; set; }

    /// <summary>头像缓存文件名使用的标识，与 UUID 一致。</summary>
    public string SkinHeadId => Uuid;

    public McProfile Clone() => (McProfile)MemberwiseClone();
}
