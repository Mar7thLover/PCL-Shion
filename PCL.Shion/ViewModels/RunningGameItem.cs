using System;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Core.Launch;

namespace PCL.Shion.ViewModels;

/// <summary>“正在运行”列表中的一局游戏。</summary>
/// <param name="game">运行中的游戏。</param>
/// <param name="log">这局游戏的日志。</param>
/// <param name="showLog">把日志切换到界面上显示。</param>
public sealed partial class RunningGameItem(RunningGame game, GameLog log, Action<GameLog> showLog)
{
    public string Name => game.InstanceName;

    public GameLog Log => log;

    /// <summary>列表第二行：进程号与启动时间。</summary>
    public string Description => LaunchStrings.RunningInfo(game.ProcessId, game.StartedAt);

    [RelayCommand]
    private void ShowLog() => showLog(log);

    [RelayCommand]
    private void EndGame() => game.Kill();
}
