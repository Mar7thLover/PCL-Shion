namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 版本 JSON 中 libraries 数组的一个元素。
/// </summary>
/// <param name="Name">Maven 名称：group:artifact:version[:classifier][@ext]。</param>
/// <param name="RepositoryUrl">库自身的 url，即所在 Maven 仓库的根地址（Fabric、旧版 Forge 等使用）；未声明时为 null。</param>
/// <param name="Artifact">downloads.artifact；未声明时为 null。</param>
/// <param name="Rules">库自身的规则。</param>
/// <param name="Natives">旧式 natives 表：系统名到 classifier 模板（可含 ${arch}）；新式库为空。</param>
/// <param name="Classifiers">downloads.classifiers 中的各个 classifier。</param>
/// <param name="ExtractExclude">解压旧式 natives 时跳过的路径前缀（extract.exclude）。</param>
public sealed record VersionLibrary(
    string Name,
    string? RepositoryUrl,
    LibraryDownload? Artifact,
    IReadOnlyList<VersionRule> Rules,
    IReadOnlyDictionary<string, string> Natives,
    IReadOnlyDictionary<string, LibraryDownload> Classifiers,
    IReadOnlyList<string> ExtractExclude);
