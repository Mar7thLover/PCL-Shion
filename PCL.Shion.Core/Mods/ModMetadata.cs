namespace PCL.Shion.Core.Mods;

/// <summary>
/// 从 jar 内的元数据文件读出的信息。
/// </summary>
internal sealed record ModMetadata(
    string? Id,
    string? Name,
    string? Version,
    IReadOnlyList<string> Loaders,
    IReadOnlyList<ModDependency> Dependencies,
    IReadOnlyList<string> Provides)
{
    public static ModMetadata Empty { get; } = new(null, null, null, [], [], []);
}
