namespace PCL.Shion.Core.Mods;

/// <summary>
/// Mod 声明的一项依赖。
/// </summary>
/// <param name="Id">被依赖 Mod 的 ID。</param>
/// <param name="VersionRange">版本范围，写法随加载器而异（Fabric 的比较串、Forge 的区间等）；未限定时为 null。</param>
/// <param name="Required">是否为必需依赖。</param>
public sealed record ModDependency(string Id, string? VersionRange, bool Required);
