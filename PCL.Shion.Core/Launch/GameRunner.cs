using System.Diagnostics;
using System.Text.Json;
using PCL.Shion.Core.Instances;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 启动并跟踪游戏：执行实例的前置与后置命令，并在游戏退出后记录游玩时长。可以在多个线程上同时使用。
/// </summary>
public sealed class GameRunner
{
    private readonly List<RunningGame> _running = [];
    private readonly Lock _gate = new();
    private readonly Lock _storeGate = new();

    /// <summary>正在运行的游戏，按启动先后排列；返回调用时的快照。</summary>
    public IReadOnlyList<RunningGame> Running
    {
        get
        {
            lock (_gate)
                return [.. _running];
        }
    }

    /// <summary>有游戏启动或退出。在调用 <see cref="Start"/> 的线程或线程池线程上触发。</summary>
    public event Action? RunningChanged;

    /// <summary>
    /// 执行前置命令并等待其结束，然后按 <paramref name="plan"/> 启动游戏。前置命令运行期间此方法一直阻塞，应在后台线程调用。
    /// 游戏退出后依次：从 <see cref="Running"/> 中移除、累加游玩时长并写入最近游玩时间、触发 <see cref="RunningChanged"/>、
    /// 调用 <paramref name="onExited"/>、执行后置命令（不等待）、释放 <see cref="RunningGame.Session"/>。
    /// 前置与后置命令用系统 shell 执行，工作目录是游戏目录，并带有环境变量
    /// INST_NAME（实例名）、INST_DIR（实例目录）、INST_MC_DIR（游戏目录）与 INST_JAVA（Java 可执行文件）。
    /// </summary>
    /// <param name="minecraftRoot">实例所在的 .minecraft 目录。</param>
    /// <param name="instanceName">实例名称。</param>
    /// <param name="plan">由 <see cref="LaunchPlanner.Build"/> 生成的启动计划。</param>
    /// <param name="settings">实例设置，这里只用到前置与后置命令。游玩时长按退出时 <see cref="InstanceStore"/> 中的最新设置累加。</param>
    /// <param name="onOutput">游戏的每行输出，在进程启动前就已挂接。</param>
    /// <param name="onExited">游戏退出，参数为退出码；此时游玩时长已经保存。</param>
    /// <exception cref="PreLaunchCommandException">前置命令以非零退出码结束，游戏没有启动。</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">无法启动 Java 或包装命令。</exception>
    public RunningGame Start(
        string minecraftRoot,
        string instanceName,
        LaunchPlan plan,
        InstanceSettings settings,
        Action<string>? onOutput,
        Action<int>? onExited)
    {
        var commandEnvironment = new Dictionary<string, string>
        {
            ["INST_NAME"] = instanceName,
            ["INST_DIR"] = Path.Combine(Path.GetFullPath(minecraftRoot), "versions", instanceName),
            ["INST_MC_DIR"] = plan.WorkingDirectory,
            ["INST_JAVA"] = plan.JavaExecutable
        };
        if (!string.IsNullOrWhiteSpace(settings.PreLaunchCommand))
            RunPreLaunchCommand(settings.PreLaunchCommand, plan.WorkingDirectory, commandEnvironment);

        var postExitCommand = settings.PostExitCommand;
        var startTimestamp = 0L;
        RunningGame? game = null;

        void OnExited(int exitCode)
        {
            // Start 持有 _gate 直到 game 加入列表，所以即使进程立即退出，这里取得锁时 game 也已赋值
            lock (_gate)
                _running.Remove(game!);
            game!.MarkExited();

            RecordPlayTime(minecraftRoot, instanceName, Stopwatch.GetElapsedTime(startTimestamp));
            RunningChanged?.Invoke();
            onExited?.Invoke(exitCode);
            if (!string.IsNullOrWhiteSpace(postExitCommand))
                Process.Start(ShellCommand.CreateStartInfo(postExitCommand, plan.WorkingDirectory, commandEnvironment))?.Dispose();
            game.Session.Dispose();
        }

        lock (_gate)
        {
            var startedAt = DateTimeOffset.Now;
            startTimestamp = Stopwatch.GetTimestamp();
            var session = GameSession.Start(plan, onOutput, OnExited);
            game = new RunningGame(minecraftRoot, instanceName, session, startedAt);
            _running.Add(game);
        }
        RunningChanged?.Invoke();
        return game;
    }

    private static void RunPreLaunchCommand(string command, string workingDirectory, IReadOnlyDictionary<string, string> environment)
    {
        // 不重定向输出：命令若留下仍在运行的子进程，重定向的管道会一直不结束，等待也就不会返回
        using var process = Process.Start(ShellCommand.CreateStartInfo(command, workingDirectory, environment))!;
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new PreLaunchCommandException(process.ExitCode);
    }

    /// <summary>
    /// 在退出时重新读取实例设置再累加，不覆盖游戏运行期间对设置的修改。
    /// </summary>
    private void RecordPlayTime(string minecraftRoot, string instanceName, TimeSpan elapsed)
    {
        try
        {
            lock (_storeGate)
            {
                var settings = InstanceStore.Load(minecraftRoot, instanceName);
                settings.TotalPlaySeconds += (long)elapsed.TotalSeconds;
                settings.LastPlayed = DateTimeOffset.Now;
                InstanceStore.Save(minecraftRoot, instanceName, settings);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // 退出处理运行在线程池线程上，异常无处传递；时长写不进去不应影响移除记录与后置命令
        }
    }
}
