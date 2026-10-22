namespace PCL.Shion.Core.Launch;

/// <summary>
/// 一次启动所需的全部输入。
/// </summary>
/// <param name="MinecraftRoot">.minecraft 目录。</param>
/// <param name="InstanceName">实例名称（versions 下的目录名）。</param>
/// <param name="Account">登录用的账户。</param>
/// <param name="JavaExecutable">Java 可执行文件的完整路径。</param>
/// <param name="MaxMemoryMb">最大堆内存（MB）。</param>
/// <param name="WindowWidth">窗口宽度；不大于 0 时不指定窗口大小。</param>
/// <param name="WindowHeight">窗口高度；不大于 0 时不指定窗口大小。</param>
/// <param name="LauncherName">传给游戏的启动器名称。</param>
/// <param name="LauncherVersion">传给游戏的启动器版本。</param>
public sealed record LaunchRequest(
    string MinecraftRoot,
    string InstanceName,
    OfflineAccount Account,
    string JavaExecutable,
    int MaxMemoryMb = 4096,
    int WindowWidth = 854,
    int WindowHeight = 480,
    string LauncherName = "PCL Shion",
    string LauncherVersion = "0.1.0");
