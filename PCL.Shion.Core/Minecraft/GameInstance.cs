namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 一个已安装的游戏实例（versions 下的一个目录）。解析失败时 <paramref name="Error"/> 不为空。
/// </summary>
/// <param name="Name">实例名称，即目录名。</param>
/// <param name="Directory">实例目录的完整路径。</param>
/// <param name="MinecraftVersion">原版 Minecraft 版本号，无法确定时为 null。</param>
/// <param name="Loader">forge / neoforge / fabric / quilt，无法识别时为 null。</param>
/// <param name="IsIsolated">是否使用实例目录作为游戏目录。</param>
/// <param name="Error">解析失败的原因。</param>
public sealed record GameInstance(
    string Name,
    string Directory,
    string? MinecraftVersion,
    string? Loader,
    bool IsIsolated,
    string? Error);
