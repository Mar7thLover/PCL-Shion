namespace PCL.Shion.Core.Launch;

/// <summary>
/// 规则筛选并去重后的库。
/// </summary>
/// <param name="RelativePath">相对于 libraries 目录的路径，以 / 分隔。</param>
/// <param name="ExtractExclude">
/// null 表示放入 classpath 的普通库；非 null 表示需要解压的旧式 natives，内容为解压时跳过的路径前缀。
/// </param>
internal sealed record SelectedLibrary(string RelativePath, IReadOnlyList<string>? ExtractExclude);
