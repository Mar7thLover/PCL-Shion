namespace PCL.Shion.Core.Loaders;

/// <summary>
/// 某个原版版本上可安装的一个加载器版本。
/// </summary>
/// <param name="Kind">加载器种类。</param>
/// <param name="Version">加载器自己的版本号，如 Fabric 的 0.16.9。</param>
/// <param name="MinecraftVersion">适用的原版版本号。</param>
/// <param name="Stable">是否为稳定版，即不是测试版或候选版。</param>
public sealed record LoaderVersion(LoaderKind Kind, string Version, string MinecraftVersion, bool Stable);
