namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 读取实例目录下的启动器配置。
/// </summary>
internal static class InstanceSettings
{
    private const string IsolationKey = "VersionArgumentIndieV2:";

    /// <summary>
    /// 读取 PCL/config.v1.yml 中的 VersionArgumentIndieV2；文件或字段不存在时视为未隔离。
    /// </summary>
    public static bool IsIsolated(string instanceDirectory)
    {
        var path = Path.Combine(instanceDirectory, "PCL", "config.v1.yml");
        if (!File.Exists(path))
            return false;

        foreach (var line in File.ReadLines(path))
        {
            var text = line.Trim();
            if (text.StartsWith(IsolationKey, StringComparison.Ordinal))
                return bool.TryParse(text[IsolationKey.Length..].Trim(), out var isolated) && isolated;
        }
        return false;
    }
}
