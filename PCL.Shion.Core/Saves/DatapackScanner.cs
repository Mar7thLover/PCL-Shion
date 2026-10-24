using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace PCL.Shion.Core.Saves;

/// <summary>
/// 数据包的启用状态。禁用的数据包会在文件名后加上 <c>.disabled</c>。
/// </summary>
public enum DatapackState
{
    Enabled,
    Disabled,
    Unavailable,
}

/// <summary>
/// 存档 datapacks 目录中的一个数据包（文件夹或 zip 文件）。
/// </summary>
/// <param name="Path">当前的完整路径，禁用时带有 <c>.disabled</c> 后缀。</param>
/// <param name="RawPath">去掉 <c>.disabled</c> 后缀的完整路径，即启用时的路径。</param>
/// <param name="FileName">当前的文件名。</param>
/// <param name="Name">显示名称，即去掉扩展名的文件名。</param>
/// <param name="Description">pack.mcmeta 中的描述，没有时为 null。</param>
/// <param name="PackFormat">pack.mcmeta 中的 pack_format，没有时为 null。</param>
/// <param name="State">启用状态。</param>
/// <param name="UnavailableReason">无法读取时的原因。</param>
/// <param name="Length">文件大小（字节），文件夹为 0。</param>
/// <param name="CreationTime">创建时间。</param>
public sealed record DatapackEntry(
    string Path,
    string RawPath,
    string FileName,
    string Name,
    string? Description,
    int? PackFormat,
    DatapackState State,
    string? UnavailableReason,
    long Length,
    DateTime CreationTime);

/// <summary>
/// 扫描存档的 datapacks 目录。
/// </summary>
public static class DatapackScanner
{
    private const string DisabledSuffix = ".disabled";

    /// <summary>
    /// 列出目录中的全部数据包，不存在目录时返回空列表。
    /// </summary>
    public static IReadOnlyList<DatapackEntry> Scan(string datapackFolder)
    {
        var result = new List<DatapackEntry>();
        if (!Directory.Exists(datapackFolder))
            return result;

        foreach (var path in Directory.EnumerateFileSystemEntries(datapackFolder))
        {
            var fileName = Path.GetFileName(path);
            var disabled = fileName.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase);
            var rawFileName = disabled ? fileName[..^DisabledSuffix.Length] : fileName;
            var isDirectory = Directory.Exists(path);
            if (!isDirectory && !rawFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                continue;

            var rawPath = disabled ? path[..^DisabledSuffix.Length] : path;
            var name = isDirectory ? rawFileName : Path.GetFileNameWithoutExtension(rawFileName);
            var (length, creationTime) = isDirectory
                ? (0L, Directory.GetCreationTime(path))
                : (new FileInfo(path).Length, File.GetCreationTime(path));

            try
            {
                var (description, packFormat) = ReadPackMeta(path, isDirectory);
                result.Add(new DatapackEntry(path, rawPath, fileName, name, description, packFormat,
                    disabled ? DatapackState.Disabled : DatapackState.Enabled, null, length, creationTime));
            }
            catch (Exception ex)
            {
                result.Add(new DatapackEntry(path, rawPath, fileName, name, null, null,
                    DatapackState.Unavailable, ex.Message, length, creationTime));
            }
        }

        return result;
    }

    /// <summary>
    /// 读取 pack.mcmeta，返回描述与 pack_format。缺少或格式错误时抛出异常。
    /// </summary>
    private static (string? Description, int? PackFormat) ReadPackMeta(string path, bool isDirectory)
    {
        string json;
        if (isDirectory)
        {
            var metaPath = Path.Combine(path, "pack.mcmeta");
            if (!File.Exists(metaPath))
                throw new FileNotFoundException("pack.mcmeta not found", metaPath);
            json = File.ReadAllText(metaPath, Encoding.UTF8);
        }
        else
        {
            using var archive = ZipFile.OpenRead(path);
            var entry = archive.GetEntry("pack.mcmeta")
                        ?? throw new InvalidDataException("pack.mcmeta not found");
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            json = reader.ReadToEnd();
        }

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });
        var pack = document.RootElement.GetProperty("pack");

        string? description = null;
        if (pack.TryGetProperty("description", out var descriptionElement))
            description = ExtractText(descriptionElement);

        int? packFormat = pack.TryGetProperty("pack_format", out var formatElement)
                          && formatElement.ValueKind == JsonValueKind.Number
                          && formatElement.TryGetInt32(out var format)
            ? format
            : null;

        return (description, packFormat);
    }

    /// <summary>
    /// 把 JSON 文本组件展开为纯文本。
    /// </summary>
    private static string ExtractText(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString() ?? "";
            case JsonValueKind.Array:
                return string.Concat(element.EnumerateArray().Select(ExtractText));
            case JsonValueKind.Object:
            {
                var builder = new StringBuilder();
                if (element.TryGetProperty("text", out var text))
                    builder.Append(ExtractText(text));
                if (element.TryGetProperty("extra", out var extra))
                    builder.Append(ExtractText(extra));
                return builder.ToString();
            }
            default:
                return "";
        }
    }
}
