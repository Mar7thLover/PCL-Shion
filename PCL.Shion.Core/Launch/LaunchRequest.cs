using PCL.Shion.Core.Instances;

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
/// <param name="GameDirectoryOverride">游戏目录；为 null 时按实例的 PCL 隔离配置决定（隔离时为实例目录，否则为 .minecraft）。</param>
/// <param name="ExtraJvmArguments">追加的 JVM 参数，按 shell 规则拆分，排在主类之前。</param>
/// <param name="ExtraGameArguments">追加的游戏参数，按 shell 规则拆分，排在最后。</param>
/// <param name="WrapperCommand">包装命令，例如 "prime-run"；按 shell 规则拆分后放在 Java 之前。</param>
/// <param name="EnvironmentVariables">为游戏进程额外设置的环境变量。</param>
/// <param name="QuickPlay">启动后直接进入的存档或服务器。</param>
public sealed record LaunchRequest(
    string MinecraftRoot,
    string InstanceName,
    OfflineAccount Account,
    string JavaExecutable,
    int MaxMemoryMb = 4096,
    int WindowWidth = 854,
    int WindowHeight = 480,
    string LauncherName = "PCL Shion",
    string LauncherVersion = "0.1.0",
    string? GameDirectoryOverride = null,
    string? ExtraJvmArguments = null,
    string? ExtraGameArguments = null,
    string? WrapperCommand = null,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null,
    QuickPlayTarget? QuickPlay = null);
