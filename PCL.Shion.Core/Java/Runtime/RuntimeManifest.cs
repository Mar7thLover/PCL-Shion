using System.Text.Json;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Java.Runtime;

/// <summary>
/// 运行时清单中的文件，只保留未压缩的 raw 下载。
/// </summary>
/// <param name="RelativePath">相对运行时根目录的路径，以 / 分隔。</param>
/// <param name="Url">raw 下载的官方地址。</param>
/// <param name="Size">文件大小。</param>
/// <param name="Sha1">文件的 SHA-1。</param>
/// <param name="Executable">在 Unix 上是否需要可执行位。</param>
internal sealed record RuntimeFile(string RelativePath, string Url, long? Size, string? Sha1, bool Executable);

/// <summary>
/// 运行时清单中的符号链接。
/// </summary>
/// <param name="RelativePath">链接自身相对运行时根目录的路径，以 / 分隔。</param>
/// <param name="Target">链接目标，相对链接所在目录。</param>
internal sealed record RuntimeLink(string RelativePath, string Target);

/// <summary>
/// 一个 Java 运行时组件的文件清单。
/// </summary>
/// <param name="Files">需要下载的文件。</param>
/// <param name="Links">需要创建的符号链接。</param>
/// <param name="Directories">需要创建的目录，包含清单声明的目录以及文件、链接所在的目录，按路径排序。</param>
internal sealed record RuntimeManifest(
    IReadOnlyList<RuntimeFile> Files,
    IReadOnlyList<RuntimeLink> Links,
    IReadOnlyList<string> Directories)
{
    /// <summary>
    /// 解析清单 JSON。缺少文件列表、链接目标，或文件没有 raw 下载时抛出 <see cref="InvalidDataException"/>。
    /// </summary>
    public static RuntimeManifest Parse(ReadOnlyMemory<byte> json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.GetObjectProperty("files") is not { } entries)
            throw new InvalidDataException("The Java runtime manifest has no file list.");

        var files = new List<RuntimeFile>();
        var links = new List<RuntimeLink>();
        var directories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.EnumerateObject())
        {
            switch (entry.Value.GetStringProperty("type"))
            {
                case "directory":
                    directories.Add(entry.Name);
                    break;
                case "file":
                    files.Add(ParseFile(entry.Name, entry.Value));
                    AddParent(directories, entry.Name);
                    break;
                case "link":
                    links.Add(ParseLink(entry.Name, entry.Value));
                    AddParent(directories, entry.Name);
                    break;
            }
        }
        return new RuntimeManifest(files, links, directories.Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// 把相对路径转换成 <paramref name="root"/> 下的本地路径。
    /// </summary>
    public static string ResolvePath(string root, string relativePath) =>
        Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// 把清单中的文件转换成下载项，目标路径位于 <paramref name="root"/> 下。
    /// </summary>
    public IReadOnlyList<DownloadItem> ToDownloadItems(string root, DownloadSource source) =>
        Files.Select(file => new DownloadItem(
                MirrorUrls.Resolve(file.Url, source), ResolvePath(root, file.RelativePath), file.Size, file.Sha1))
            .ToList();

    private static RuntimeFile ParseFile(string relativePath, JsonElement entry)
    {
        var raw = entry.GetObjectProperty("downloads")?.GetObjectProperty("raw");
        if (raw?.GetStringProperty("url") is not { } url)
            throw new InvalidDataException($"The Java runtime file '{relativePath}' has no raw download.");

        var executable = entry.TryGetProperty("executable", out var flag) && flag.ValueKind == JsonValueKind.True;
        return new RuntimeFile(relativePath, url, GetInt64(raw.Value, "size"), raw.Value.GetStringProperty("sha1"), executable);
    }

    private static RuntimeLink ParseLink(string relativePath, JsonElement entry)
    {
        var target = entry.GetStringProperty("target")
            ?? throw new InvalidDataException($"The Java runtime link '{relativePath}' has no target.");
        return new RuntimeLink(relativePath, target);
    }

    private static long? GetInt64(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : null;

    private static void AddParent(HashSet<string> directories, string relativePath)
    {
        var separator = relativePath.LastIndexOf('/');
        if (separator > 0)
            directories.Add(relativePath[..separator]);
    }
}
