using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 规则筛选并去重后的库。
/// </summary>
/// <param name="RelativePath">相对于 libraries 目录的路径，以 / 分隔。</param>
/// <param name="ExtractExclude">
/// null 表示放入 classpath 的普通库；非 null 表示需要解压的旧式 natives，内容为解压时跳过的路径前缀。
/// </param>
/// <param name="Download">该文件在版本 JSON 中的下载信息（downloads.artifact 或对应的 classifier）；未声明时为 null。</param>
/// <param name="RepositoryUrl">库所在 Maven 仓库的根地址；未声明时为 null。</param>
internal sealed record SelectedLibrary(
    string RelativePath,
    IReadOnlyList<string>? ExtractExclude,
    LibraryDownload? Download,
    string? RepositoryUrl);
