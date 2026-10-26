using System.Text.Json;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Instances;

namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 扫描 .minecraft 下已安装的游戏实例。
/// </summary>
public static class InstanceScanner
{
    /// <summary>
    /// 列出 versions 下每个含 &lt;名称&gt;.json 的目录，按名称排序。解析失败的实例也会列出，其 Error 不为空。
    /// 最近游玩时间与累计时长取自 <see cref="InstanceStore"/>，读取不到时视为从未游玩。
    /// </summary>
    public static IReadOnlyList<GameInstance> Scan(string minecraftRoot)
    {
        var versionsDirectory = new DirectoryInfo(Path.Combine(minecraftRoot, "versions"));
        if (!versionsDirectory.Exists)
            return [];

        return versionsDirectory.EnumerateDirectories()
            .Where(directory => !InstallationSession.IsActive(directory.FullName))
            .Where(directory => File.Exists(Path.Combine(directory.FullName, $"{directory.Name}.json")))
            .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
            .Select(directory => Describe(minecraftRoot, directory))
            .ToList();
    }

    private static GameInstance Describe(string minecraftRoot, DirectoryInfo directory)
    {
        var isolated = false;
        var (lastPlayed, totalPlaySeconds) = ReadPlayRecord(minecraftRoot, directory.Name);
        try
        {
            isolated = InstanceSettings.IsIsolated(directory.FullName);
            var version = VersionResolver.Resolve(minecraftRoot, directory.Name);
            return new GameInstance(
                directory.Name, directory.FullName, version.MinecraftVersion, version.Loader, isolated, null, lastPlayed, totalPlaySeconds);
        }
        catch (Exception ex)
        {
            return new GameInstance(directory.Name, directory.FullName, null, null, isolated, ex.Message, lastPlayed, totalPlaySeconds);
        }
    }

    private static (DateTimeOffset? LastPlayed, long TotalPlaySeconds) ReadPlayRecord(string minecraftRoot, string instanceName)
    {
        try
        {
            var settings = InstanceStore.Load(minecraftRoot, instanceName);
            return (settings.LastPlayed, settings.TotalPlaySeconds);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return (null, 0);
        }
    }
}
