using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Launch;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class GameSessionTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [TestMethod]
    public void ForwardsOutputFromBothStreamsBeforeReportingTheExitCode()
    {
        var plan = ShellPlan.Create(
            "echo out& echo err 1>&2& exit /b 3",
            "echo out; echo err 1>&2; exit 3");
        var lines = new List<string>();
        var exited = new ManualResetEventSlim();
        var exitCode = int.MinValue;
        var linesBeforeExit = -1;

        using var session = GameSession.Start(plan);
        session.OutputReceived += line =>
        {
            lock (lines)
                lines.Add(line.Trim());
        };
        session.Exited += code =>
        {
            lock (lines)
                linesBeforeExit = lines.Count;
            exitCode = code;
            exited.Set();
        };

        Assert.IsTrue(exited.Wait(Timeout), "the process did not exit in time");
        Assert.IsGreaterThan(0, session.ProcessId);
        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(2, linesBeforeExit);
        lock (lines)
            CollectionAssert.AreEquivalent(new[] { "out", "err" }, lines.ToArray());
    }

    [TestMethod]
    public void HandlersPassedToStartReceiveTheOutputAndTheExitCode()
    {
        var plan = ShellPlan.Create("echo out& exit /b 5", "echo out; exit 5");

        var (lines, exitCode) = Run(plan);

        Assert.AreEqual(5, exitCode);
        CollectionAssert.AreEqual(new[] { "out" }, lines);
    }

    [TestMethod]
    public void RunsInTheWorkingDirectoryOfThePlan()
    {
        using var directory = new FakeMinecraftRoot();
        directory.WriteFile("marker.txt");
        var plan = ShellPlan.Create("dir /b", "ls", directory.RootPath);

        var (lines, _) = Run(plan);

        CollectionAssert.AreEqual(new[] { "marker.txt" }, lines);
    }

    [TestMethod]
    public void SetsTheEnvironmentVariablesOfThePlan()
    {
        var plan = ShellPlan.Create(
            "echo %SHION_TEST_VALUE%",
            "echo \"$SHION_TEST_VALUE\"",
            environment: new Dictionary<string, string> { ["SHION_TEST_VALUE"] = "shion" });

        var (lines, _) = Run(plan);

        CollectionAssert.AreEqual(new[] { "shion" }, lines);
    }

    [TestMethod]
    public void StartsTheWrapperCommandWithJavaAndItsArgumentsAfterIt()
    {
        // 包装命令收到的参数依次是 Java 可执行文件与 Java 的参数
        var plan = ShellPlan.Create(string.Empty, string.Empty) with
        {
            JavaExecutable = "hello",
            Arguments = ["world"],
            WrapperCommand = ShellPlan.IsWindows ? "cmd.exe /c echo" : "/bin/sh -c 'echo \"$0 $1\"'"
        };

        var (lines, exitCode) = Run(plan);

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { "hello world" }, lines);
    }

    [TestMethod]
    public void KillEndsTheProcessTree()
    {
        var plan = ShellPlan.Create("ping -n 120 127.0.0.1 >nul", "sleep 120");
        var exited = new ManualResetEventSlim();

        using var session = GameSession.Start(plan, onExited: _ => exited.Set());
        session.Kill();

        Assert.IsTrue(exited.Wait(Timeout), "the process did not exit after being killed");
    }

    [TestMethod]
    public void KillAfterExitDoesNothing()
    {
        var plan = ShellPlan.Create("exit /b 0", "exit 0");
        var exited = new ManualResetEventSlim();

        using var session = GameSession.Start(plan, onExited: _ => exited.Set());
        Assert.IsTrue(exited.Wait(Timeout), "the process did not exit in time");

        session.Kill();
    }

    [TestMethod]
    public void StartingAMissingExecutableThrows()
    {
        var plan = ShellPlan.Create(string.Empty, string.Empty) with
        {
            JavaExecutable = Path.Combine(Path.GetTempPath(), $"no-such-java-{Guid.NewGuid():N}"),
            Arguments = []
        };

        Assert.Throws<Win32Exception>(() => GameSession.Start(plan));
    }

    /// <summary>
    /// 把处理程序交给 <see cref="GameSession.Start"/> 运行到进程退出，返回去掉首尾空白的输出行与退出码。
    /// </summary>
    private static (string[] Lines, int ExitCode) Run(LaunchPlan plan)
    {
        var lines = new List<string>();
        var exited = new ManualResetEventSlim();
        var exitCode = int.MinValue;

        using var session = GameSession.Start(
            plan,
            line =>
            {
                lock (lines)
                    lines.Add(line.Trim());
            },
            code =>
            {
                exitCode = code;
                exited.Set();
            });

        Assert.IsTrue(exited.Wait(Timeout), "the process did not exit in time");
        lock (lines)
            return (lines.ToArray(), exitCode);
    }
}
