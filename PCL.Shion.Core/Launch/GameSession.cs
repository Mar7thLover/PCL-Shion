using System.Diagnostics;
using System.Text;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 一个正在运行的游戏进程。事件在线程池线程上触发。进程启动后就开始读取输出，要收到全部输出与退出通知，
/// 处理程序须作为 <see cref="Start"/> 的参数传入；在 <see cref="Start"/> 返回后再订阅，可能错过已经发出的事件。
/// </summary>
public sealed class GameSession : IDisposable
{
    private readonly Process _process;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _exitCancellation = new();
    private int _disposed;

    private GameSession(Process process)
    {
        _process = process;
        process.OutputDataReceived += OnDataReceived;
        process.ErrorDataReceived += OnDataReceived;
    }

    /// <summary>标准输出与标准错误合流后的一行文本。</summary>
    public event Action<string>? OutputReceived;

    /// <summary>进程退出，参数为退出码。此事件晚于全部 <see cref="OutputReceived"/>。</summary>
    public event Action<int>? Exited;

    public int ProcessId { get; private set; }

    /// <summary>
    /// 按 <paramref name="plan"/> 启动 Java 进程；有包装命令时启动包装命令，Java 及其参数接在包装命令之后。
    /// </summary>
    /// <param name="plan">启动计划。</param>
    /// <param name="onOutput">在进程启动前就订阅 <see cref="OutputReceived"/> 的处理程序，因此不会错过任何一行输出。</param>
    /// <param name="onExited">在进程启动前就订阅 <see cref="Exited"/> 的处理程序，因此不会错过过早的退出。</param>
    /// <exception cref="System.ComponentModel.Win32Exception">无法启动 Java 可执行文件或包装命令。</exception>
    public static GameSession Start(LaunchPlan plan, Action<string>? onOutput = null, Action<int>? onExited = null)
    {
        List<string> command = plan.WrapperCommand is null ? [] : [.. ShellWords.Split(plan.WrapperCommand)];
        command.Add(plan.JavaExecutable);
        var startInfo = new ProcessStartInfo(command[0])
        {
            WorkingDirectory = plan.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in command.Skip(1).Concat(plan.Arguments))
            startInfo.ArgumentList.Add(argument);
        foreach (var (name, value) in plan.EnvironmentVariables)
            startInfo.Environment[name] = value;

        var process = new Process { StartInfo = startInfo };
        var session = new GameSession(process);
        session.OutputReceived += onOutput;
        session.Exited += onExited;
        try
        {
            process.Start();
            session.ProcessId = process.Id;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // 先开始读两条管道再等待退出，避免瞬间退出的进程在读日志前触发清理并释放句柄。
            // 始终在线程池回调，不能在 Start 返回前同步调用 GameRunner 的退出处理。
            var exitToken = session._exitCancellation.Token;
            _ = Task.Run(() => session.WaitForExitAsync(exitToken));
        }
        catch
        {
            session.Dispose();
            throw;
        }
        return session;
    }

    /// <summary>
    /// 结束游戏进程及其全部子进程（经包装命令启动时，Java 是包装命令的子进程）。进程已经退出时什么也不做。
    /// </summary>
    public void Kill()
    {
        lock (_gate)
        {
            if (_disposed != 0) return;
            try
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (_process.HasExited)
            {
                // 进程可能在 HasExited 与 Kill 之间退出。
            }
        }
    }

    /// <summary>
    /// 释放进程句柄并停止事件；不会结束游戏进程。
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _exitCancellation.Cancel();
            _process.OutputDataReceived -= OnDataReceived;
            _process.ErrorDataReceived -= OnDataReceived;
            _process.Dispose();
            _exitCancellation.Dispose();
        }
    }

    private void OnDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null && Volatile.Read(ref _disposed) == 0)
            InvokeSafely(OutputReceived, e.Data);
    }

    private async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        try
        {
            // WaitForExitAsync 同时等待重定向输出结束，退出通知一定晚于最后一行日志。
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (Volatile.Read(ref _disposed) == 0)
                InvokeSafely(Exited, _process.ExitCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (InvalidOperationException) when (Volatile.Read(ref _disposed) != 0)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceError("Unable to observe game process exit: {0}", exception);
        }
    }

    private static void InvokeSafely<T>(Action<T>? handlers, T value)
    {
        if (handlers is null) return;
        foreach (Action<T> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(value);
            }
            catch (Exception exception)
            {
                // 输出与退出来自后台线程，界面或日志订阅者异常不能终止整个启动器或阻止其他订阅者。
                Trace.TraceError("Game process callback failed: {0}", exception);
            }
        }
    }
}
