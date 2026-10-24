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

    private GameSession(Process process)
    {
        _process = process;
        process.OutputDataReceived += OnDataReceived;
        process.ErrorDataReceived += OnDataReceived;
        process.Exited += OnExited;
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

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var session = new GameSession(process);
        session.OutputReceived += onOutput;
        session.Exited += onExited;
        try
        {
            process.Start();
        }
        catch
        {
            session.Dispose();
            throw;
        }

        session.ProcessId = process.Id;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return session;
    }

    /// <summary>
    /// 结束游戏进程及其全部子进程（经包装命令启动时，Java 是包装命令的子进程）。进程已经退出时什么也不做。
    /// </summary>
    public void Kill()
    {
        _process.Kill(entireProcessTree: true);
    }

    /// <summary>
    /// 释放进程句柄并停止事件；不会结束游戏进程。
    /// </summary>
    public void Dispose()
    {
        _process.Dispose();
    }

    private void OnDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
            OutputReceived?.Invoke(e.Data);
    }

    private void OnExited(object? sender, EventArgs e)
    {
        // 无参的 WaitForExit 会等到重定向的输出读完，保证退出事件在全部日志行之后
        _process.WaitForExit();
        Exited?.Invoke(_process.ExitCode);
    }
}
