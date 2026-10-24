using System.Text.RegularExpressions;

namespace PCL.Core.App.Widgets;

/// <summary>
/// 一次游玩进入的单人世界或服务器。
/// </summary>
/// <param name="IsServer">是否为服务器。</param>
/// <param name="Value">世界的存档文件夹名，或服务器地址（host:port）。</param>
public readonly record struct RecentPlayTarget(bool IsServer, string Value)
{
    // 客户端连接服务器时输出 “Connecting to host, port”
    private static readonly Regex _ServerPattern = new(@"Connecting to ([^,\s]+), (\d{1,5})\b", RegexOptions.Compiled);

    // 内置服务器载入单人世界时输出 “Preparing level "存档文件夹名"”
    private static readonly Regex _WorldPattern = new("Preparing level \"([^\"]+)\"", RegexOptions.Compiled);

    /// <summary>
    /// 从一行游戏日志中识别进入的世界或服务器。日志可以是普通文本，也可以是 log4j 的 XML 格式。
    /// </summary>
    public static bool TryParse(string? line, out RecentPlayTarget target)
    {
        target = default;
        if (string.IsNullOrEmpty(line) || line.Contains("[CHAT]")) return false;

        var server = _ServerPattern.Match(line);
        if (server.Success)
        {
            target = new RecentPlayTarget(true, $"{server.Groups[1].Value}:{server.Groups[2].Value}");
            return true;
        }

        var world = _WorldPattern.Match(line);
        if (world.Success)
        {
            target = new RecentPlayTarget(false, world.Groups[1].Value);
            return true;
        }

        return false;
    }
}
