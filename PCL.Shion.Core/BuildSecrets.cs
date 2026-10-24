using System.Collections.Concurrent;

namespace PCL.Shion.Core;

/// <summary>
/// 构建时写入的密钥。构建前设置环境变量 <c>PCL_WRITE_SECRET</c> 后，所有 <c>PCL_</c> 开头的环境变量
/// 会由 PCL.Core.SourceGenerators 写入 <c>SecretDictionary</c>，键名去掉 <c>PCL_</c> 前缀。
/// 构建时没有写入的密钥会退回读取运行时的同名环境变量，便于本地开发调试。
/// </summary>
public static partial class BuildSecrets
{
    private static readonly ConcurrentDictionary<string, string?> Cache = new();

    /// <summary>读取密钥，例如 <c>Get("MS_CLIENT_ID")</c>。未配置时返回 null。</summary>
    public static string? Get(string key) => Cache.GetOrAdd(key, static k =>
    {
        if (SecretDictionary.TryGetValue(k, out var value) && !string.IsNullOrWhiteSpace(value))
            return value.Trim();
        var env = Environment.GetEnvironmentVariable("PCL_" + k);
        return string.IsNullOrWhiteSpace(env) ? null : env.Trim();
    });

    /// <summary>微软 OAuth 的客户端 ID。</summary>
    public static string? MicrosoftClientId => Get("MS_CLIENT_ID");

    /// <summary>CurseForge API Key。</summary>
    public static string? CurseForgeApiKey => Get("CURSEFORGE_API_KEY");
}
