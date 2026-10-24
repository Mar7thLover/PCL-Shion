using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 直接进入存档或服务器所需的启动参数。
/// </summary>
/// <param name="Feature">要开启的规则特性；版本 JSON 没有对应的 quickPlay 参数时为 null。</param>
/// <param name="Placeholder">quickPlay 参数引用目标所用的占位符名，与 <paramref name="Feature"/> 同时为 null。</param>
/// <param name="Value">目标：存档文件夹名，或服务器地址 host[:port]。</param>
/// <param name="FallbackArguments">不支持 quickPlay 的旧版本追加在游戏参数之后的参数。</param>
internal sealed record QuickPlayArguments(string? Feature, string? Placeholder, string Value, IReadOnlyList<string> FallbackArguments)
{
    public const int DefaultPort = 25565;

    public static readonly QuickPlayArguments None = new(null, null, string.Empty, []);

    /// <summary>
    /// 1.20 起的版本 JSON 用带 is_quick_play_singleplayer / is_quick_play_multiplayer 特性规则的参数提供
    /// --quickPlaySingleplayer 与 --quickPlayMultiplayer，声明了对应特性就开启它。
    /// 否则多人游戏改用旧式的 --server 与 --port；更早的版本无法直接进入单人存档，忽略该目标。
    /// </summary>
    public static QuickPlayArguments Create(QuickPlayTarget? target, IReadOnlyList<VersionArgument> gameArguments)
    {
        if (target is null)
            return None;

        var (feature, placeholder) = target.Kind == QuickPlayKind.Singleplayer
            ? ("is_quick_play_singleplayer", "quickPlaySingleplayer")
            : ("is_quick_play_multiplayer", "quickPlayMultiplayer");
        if (gameArguments.Any(argument => argument.Rules.Any(rule => rule.Features.ContainsKey(feature))))
            return new QuickPlayArguments(feature, placeholder, target.Value, []);

        if (target.Kind == QuickPlayKind.Singleplayer)
            return None;

        var (host, port) = ParseAddress(target.Value);
        return new QuickPlayArguments(null, null, target.Value, ["--server", host, "--port", port.ToString()]);
    }

    /// <summary>
    /// 拆分 host[:port]，IPv6 地址带端口时须写成 [地址]:端口。没有端口或端口无效时为 <see cref="DefaultPort"/>。
    /// </summary>
    public static (string Host, int Port) ParseAddress(string address)
    {
        var host = address;
        string? port = null;
        var close = address.IndexOf(']');
        var colon = address.IndexOf(':');
        if (address.StartsWith('[') && close > 0)
        {
            host = address[1..close];
            if (address.Length > close + 1 && address[close + 1] == ':')
                port = address[(close + 2)..];
        }
        else if (colon >= 0 && colon == address.LastIndexOf(':'))
        {
            host = address[..colon];
            port = address[(colon + 1)..];
        }
        return (host, ushort.TryParse(port, out var number) && number > 0 ? number : DefaultPort);
    }
}
