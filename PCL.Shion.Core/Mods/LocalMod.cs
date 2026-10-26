namespace PCL.Shion.Core.Mods;

/// <summary>
/// mods 目录下的一个 Mod 文件及其元数据。读不到元数据时 Id、Name、Version 为 null，Loaders 与 Dependencies 为空。
/// </summary>
/// <param name="FilePath">文件的路径，已禁用的 Mod 带 .disabled 后缀。</param>
/// <param name="FileName">文件名。</param>
/// <param name="Enabled">是否启用，即文件名不以 .disabled 结尾。</param>
/// <param name="Id">Mod ID。</param>
/// <param name="Name">显示名称。</param>
/// <param name="Version">版本号。</param>
/// <param name="Loaders">支持的加载器：fabric / quilt / forge / neoforge。</param>
/// <param name="Dependencies">声明的依赖。</param>
/// <param name="Sha1">文件内容的 SHA-1，小写十六进制。</param>
public sealed record LocalMod(
    string FilePath,
    string FileName,
    bool Enabled,
    string? Id,
    string? Name,
    string? Version,
    IReadOnlyList<string> Loaders,
    IReadOnlyList<ModDependency> Dependencies,
    string Sha1)
{
    /// <summary>
    /// 该文件额外提供的 Mod ID：provides 别名、同一文件里的其他 Mod，以及内嵌（Jar-in-Jar）的 Mod。
    /// </summary>
    public IReadOnlyList<string> Provides { get; init; } = [];

    /// <summary>解析关闭、读取失败或预算耗尽时为 false；此时不能可靠地判断缺少依赖。</summary>
    public bool MetadataComplete { get; init; } = true;

    /// <summary>该文件的读取或解析问题；不妨碍用户在列表中禁用、删除这个文件。</summary>
    public string? ReadWarning { get; init; }
}
