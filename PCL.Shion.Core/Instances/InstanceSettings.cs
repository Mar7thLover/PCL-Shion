namespace PCL.Shion.Core.Instances;

/// <summary>
/// 单个实例的启动器设置，由 <see cref="InstanceStore"/> 保存。
/// </summary>
public sealed class InstanceSettings
{
    /// <summary>指定的 Java 可执行文件，null 表示自动选择。</summary>
    public string? JavaExecutable { get; set; }

    /// <summary>最大内存（MB），null 表示使用全局设置。</summary>
    public int? MaxMemoryMb { get; set; }

    /// <summary>是否把实例目录用作游戏目录，null 表示沿用 <see cref="Minecraft.GameInstance.IsIsolated"/>。</summary>
    public bool? Isolated { get; set; }

    /// <summary>追加在 JVM 参数之后的额外参数。</summary>
    public string? ExtraJvmArguments { get; set; }

    /// <summary>追加在游戏参数之后的额外参数。</summary>
    public string? ExtraGameArguments { get; set; }

    /// <summary>启动游戏前执行的命令。</summary>
    public string? PreLaunchCommand { get; set; }

    /// <summary>游戏退出后执行的命令。</summary>
    public string? PostExitCommand { get; set; }

    /// <summary>放在 java 前面的包装命令，例如 <c>prime-run</c> 或 <c>taskset -c 0-3</c>。</summary>
    public string? WrapperCommand { get; set; }

    /// <summary>启动游戏时额外注入的环境变量。</summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>窗口宽度，null 表示使用默认值。</summary>
    public int? WindowWidth { get; set; }

    /// <summary>窗口高度，null 表示使用默认值。</summary>
    public int? WindowHeight { get; set; }

    /// <summary>启动后直接进入的存档或服务器，null 表示停留在主菜单。</summary>
    public QuickPlayTarget? QuickPlay { get; set; }

    /// <summary>累计游玩时长（秒）。</summary>
    public long TotalPlaySeconds { get; set; }

    /// <summary>最近一次游玩的时间，从未游玩时为 null。</summary>
    public DateTimeOffset? LastPlayed { get; set; }
}
