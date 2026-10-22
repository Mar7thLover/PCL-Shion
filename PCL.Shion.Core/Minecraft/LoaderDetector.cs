namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 根据库名识别模组加载器。
/// </summary>
internal static class LoaderDetector
{
    /// <summary>
    /// 返回 forge / neoforge / fabric / quilt，识别不了时返回 null。
    /// </summary>
    public static string? Detect(IEnumerable<string> libraryNames)
    {
        var names = libraryNames.ToList();
        if (HasPrefix(names, "org.quiltmc:quilt-loader:"))
            return "quilt";
        if (HasPrefix(names, "net.neoforged"))
            return "neoforge";
        if (HasPrefix(names, "net.minecraftforge:forge:", "net.minecraftforge:fmlloader:", "net.minecraftforge:minecraftforge:"))
            return "forge";
        if (HasPrefix(names, "net.fabricmc:fabric-loader:"))
            return "fabric";
        return null;
    }

    private static bool HasPrefix(List<string> names, params string[] prefixes)
    {
        return names.Any(name => prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));
    }
}
