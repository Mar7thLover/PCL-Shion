using System.Text.Json;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 检查资源索引与资源对象是否齐全，并为旧版虚拟资源准备目录。
/// </summary>
internal static class GameAssets
{
    /// <summary>
    /// 旧版虚拟资源的目录：游戏按资源原名而不是哈希读取其中的文件。
    /// </summary>
    public static string GetVirtualDirectory(string assetsDirectory)
    {
        return Path.Combine(assetsDirectory, "virtual", "legacy");
    }

    /// <summary>
    /// 返回缺失的文件：索引文件，或索引里列出而 objects 中不存在的资源。
    /// <paramref name="isVirtual"/> 为 true 时，同时把资源按原名复制到虚拟资源目录（已存在的跳过）。
    /// </summary>
    public static IReadOnlyList<string> Prepare(string assetsDirectory, string indexId, bool isVirtual)
    {
        var indexPath = Path.Combine(assetsDirectory, "indexes", $"{indexId}.json");
        if (!File.Exists(indexPath))
            return [indexPath];

        using var document = JsonDocument.Parse(File.ReadAllText(indexPath));
        var virtualDirectory = GetVirtualDirectory(assetsDirectory);
        var missing = new HashSet<string>();

        foreach (var asset in document.RootElement.GetProperty("objects").EnumerateObject())
        {
            var hash = asset.Value.GetProperty("hash").GetString()!;
            var objectPath = Path.Combine(assetsDirectory, "objects", hash[..2], hash);
            if (!File.Exists(objectPath))
            {
                missing.Add(objectPath);
                continue;
            }

            if (!isVirtual)
                continue;

            var virtualPath = Path.Combine(virtualDirectory, asset.Name.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(virtualPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(virtualPath)!);
                File.Copy(objectPath, virtualPath);
            }
        }
        return missing.ToList();
    }
}
