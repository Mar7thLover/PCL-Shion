using System.Diagnostics;
using System.Text;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 一个正在运行的游戏进程。事件在线程池线程上触发，调用方需要在 <see cref="Start"/> 返回后立即订阅。
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
    /// 按 <paramref name="plan"/> 启动 Java 进程。
    /// </summary>
    /// <exception cref="System.ComponentModel.Win32Exception">无法启动 Java 可执行文件。</exception>
    public static GameSession Start(LaunchPlan plan)
    {
        var startInfo = new ProcessStartInfo(plan.JavaExecutable)
        {
            WorkingDirectory = plan.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in plan.Arguments)
            startInfo.ArgumentList.Add(argument);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var session = new GameSession(process);
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
