namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// Maven 名称（group:artifact:version[:classifier][@ext]）的解析与库文件路径推导。
/// </summary>
internal static class MavenName
{
    /// <summary>
    /// 推导相对于 libraries 目录的路径，以 / 分隔。<paramref name="classifier"/> 不为 null 时覆盖名称中自带的 classifier。
    /// </summary>
    public static string GetPath(string name, string? classifier = null)
    {
        var coordinate = Parse(name);
        classifier ??= coordinate.Classifier;
        var suffix = classifier is null ? string.Empty : $"-{classifier}";
        var fileName = $"{coordinate.Artifact}-{coordinate.Version}{suffix}.{coordinate.Extension}";
        return $"{coordinate.Group.Replace('.', '/')}/{coordinate.Artifact}/{coordinate.Version}/{fileName}";
    }

    /// <summary>
    /// 库去重用的键：group:artifact[:classifier]，不含版本与扩展名。
    /// </summary>
    public static string GetKey(string name)
    {
        var coordinate = Parse(name);
        return coordinate.Classifier is null
            ? $"{coordinate.Group}:{coordinate.Artifact}"
            : $"{coordinate.Group}:{coordinate.Artifact}:{coordinate.Classifier}";
    }

    private static Coordinate Parse(string name)
    {
        var extension = "jar";
        var coordinates = name;
        var at = name.IndexOf('@');
        if (at >= 0)
        {
            extension = name[(at + 1)..];
            coordinates = name[..at];
        }

        var parts = coordinates.Split(':');
        if (parts.Length < 3)
            throw new InvalidDataException($"Invalid library name: {name}");
        return new Coordinate(parts[0], parts[1], parts[2], parts.Length > 3 ? parts[3] : null, extension);
    }

    private readonly record struct Coordinate(string Group, string Artifact, string Version, string? Classifier, string Extension);
}
