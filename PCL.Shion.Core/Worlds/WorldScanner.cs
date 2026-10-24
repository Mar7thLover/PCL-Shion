using PCL.Shion.Core.Worlds.Nbt;

namespace PCL.Shion.Core.Worlds;

/// <summary>
/// 读取游戏目录下的单人存档与服务器列表。
/// </summary>
public static class WorldScanner
{
    private static readonly long MinTimestamp = DateTimeOffset.MinValue.ToUnixTimeMilliseconds();
    private static readonly long MaxTimestamp = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    /// <summary>
    /// 列出 saves 下含 level.dat 的文件夹，最近游玩的在前，没有游玩时间的排在最后。
    /// level.dat 无法读取的存档仍会列出，只是名称取文件夹名，其余信息为空。
    /// </summary>
    public static IReadOnlyList<WorldInfo> ScanWorlds(string gameDirectory)
    {
        var saves = new DirectoryInfo(Path.Combine(gameDirectory, "saves"));
        if (!saves.Exists)
            return [];

        return saves.EnumerateDirectories()
            .Where(directory => File.Exists(Path.Combine(directory.FullName, "level.dat")))
            .Select(DescribeWorld)
            .OrderByDescending(world => world.LastPlayed ?? DateTimeOffset.MinValue)
            .ThenBy(world => world.FolderName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 按文件中的顺序列出 servers.dat 里的服务器；文件不存在或无法读取时返回空列表。没有地址的条目会被忽略。
    /// </summary>
    public static IReadOnlyList<ServerEntry> ScanServers(string gameDirectory)
    {
        var entries = new List<ServerEntry>();
        foreach (var server in ReadServers(Path.Combine(gameDirectory, "servers.dat")))
        {
            var address = server.GetString("ip");
            if (string.IsNullOrEmpty(address))
                continue;

            var name = server.GetString("name");
            entries.Add(new ServerEntry(string.IsNullOrEmpty(name) ? address : name, address));
        }
        return entries;
    }

    private static WorldInfo DescribeWorld(DirectoryInfo directory)
    {
        var data = ReadLevelData(Path.Combine(directory.FullName, "level.dat"));
        var name = data?.GetString("LevelName");
        return new WorldInfo(
            directory.Name,
            string.IsNullOrEmpty(name) ? directory.Name : name,
            ToTimestamp(data?.GetLong("LastPlayed")),
            ToGameMode(data?.GetLong("GameType")));
    }

    private static NbtCompound? ReadLevelData(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return NbtReader.ReadGZip(stream).GetCompound("Data");
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return null;
        }
    }

    private static IEnumerable<NbtCompound> ReadServers(string path)
    {
        if (!File.Exists(path))
            return [];

        try
        {
            using var stream = File.OpenRead(path);
            return NbtReader.Read(stream).GetList("servers")?.OfType<NbtCompound>() ?? [];
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return [];
        }
    }

    private static bool IsReadFailure(Exception exception)
    {
        return exception is InvalidDataException or IOException or UnauthorizedAccessException;
    }

    private static DateTimeOffset? ToTimestamp(long? milliseconds)
    {
        return milliseconds is { } value && value >= MinTimestamp && value <= MaxTimestamp
            ? DateTimeOffset.FromUnixTimeMilliseconds(value)
            : null;
    }

    private static string? ToGameMode(long? gameType)
    {
        return gameType switch
        {
            0 => "survival",
            1 => "creative",
            2 => "adventure",
            3 => "spectator",
            _ => null,
        };
    }
}
