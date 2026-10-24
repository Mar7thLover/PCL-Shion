namespace PCL.Shion.Core.Launch;

/// <summary>
/// 实例的前置命令以非零退出码结束，游戏没有启动。
/// </summary>
public sealed class PreLaunchCommandException(int exitCode)
    : Exception($"The pre-launch command exited with code {exitCode}.")
{
    /// <summary>前置命令的退出码。</summary>
    public int ExitCode { get; } = exitCode;
}
