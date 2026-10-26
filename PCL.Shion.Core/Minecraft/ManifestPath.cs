namespace PCL.Shion.Core.Minecraft;

/// <summary>清单提供的路径必须留在指定目录内，不能解释为盘符、UNC、父目录或备用数据流。</summary>
internal static class ManifestPath
{
    public static string Resolve(string directory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidDataException("A manifest path must not be empty.");

        // Windows 与 Unix 都按同样的规则检查来自网络的路径，包括反斜杠形式的路径穿越。
        var normalized = relativePath.Replace('\\', '/');
        var segments = normalized.Split('/');
        if (Path.IsPathRooted(normalized) || normalized.Contains(':')
            || segments.Any(segment => segment.Length == 0 || segment is "." or ".."
                || segment.EndsWith('.') || segment.EndsWith(' ')
                || segment.Any(character => char.IsControl(character) || "<>\"|?*".Contains(character))))
            throw new InvalidDataException($"Unsafe manifest path '{relativePath}'.");

        var root = Path.GetFullPath(directory);
        var path = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(prefix, comparison))
            throw new InvalidDataException($"Manifest path '{relativePath}' escapes its directory.");
        return path;
    }

    public static string ResolveName(string directory, string name, string suffix = "")
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['/', '\\']) >= 0 || name is "." or "..")
            throw new InvalidDataException($"Unsafe manifest file name '{name}'.");
        return Resolve(directory, name + suffix);
    }

    public static string AssetHash(string? hash)
    {
        if (hash is null || hash.Length != 40 || !hash.All(Uri.IsHexDigit))
            throw new InvalidDataException("An asset hash must contain exactly 40 hexadecimal characters.");
        return hash.ToLowerInvariant();
    }
}
