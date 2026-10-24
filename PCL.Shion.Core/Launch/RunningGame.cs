namespace PCL.Shion.Core.Launch;

/// <summary>
/// 由 <see cref="GameRunner"/> 启动、尚未退出的一局游戏。
/// </summary>
public sealed class RunningGame
{
    private readonly Lock _gate = new();
    private bool _exited;

    internal RunningGame(string minecraftRoot, string instanceName, GameSession session, DateTimeOffset startedAt)
    {
        MinecraftRoot = minecraftRoot;
        InstanceName = instanceName;
        Session = session;
        StartedAt = startedAt;
    }

    /// <summary>实例所在的 .minecraft 目录。</summary>
    public string MinecraftRoot { get; }

    public string InstanceName { get; }

    public int ProcessId => Session.ProcessId;

    public DateTimeOffset StartedAt { get; }

    /// <summary>游戏进程。游戏退出、退出处理完成后由 <see cref="GameRunner"/> 释放。</summary>
    public GameSession Session { get; }

    /// <summary>
    /// 结束游戏进程及其子进程；游戏已经退出时什么也不做。之后的退出处理（记录时长、后置命令等）照常进行。
    /// </summary>
    public void Kill()
    {
        lock (_gate)
        {
            if (!_exited)
                Session.Kill();
        }
    }

    /// <summary>
    /// 标记游戏已经退出，此后 <see cref="Kill"/> 不再访问即将释放的进程。
    /// </summary>
    internal void MarkExited()
    {
        lock (_gate)
            _exited = true;
    }
}
