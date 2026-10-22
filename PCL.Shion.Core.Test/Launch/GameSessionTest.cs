using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Launch;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class GameSessionTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [TestMethod]
    public void ForwardsOutputFromBothStreamsBeforeReportingTheExitCode()
    {
        var plan = CreatePlan(
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
        var plan = CreatePlan("echo out& exit /b 5", "echo out; exit 5");
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
        Assert.AreEqual(5, exitCode);
        lock (lines)
            CollectionAssert.AreEqual(new[] { "out" }, lines.ToArray());
    }

    [TestMethod]
    public void RunsInTheWorkingDirectoryOfThePlan()
    {
        using var directory = new FakeMinecraftRoot();
        directory.WriteFile("marker.txt");
        var plan = CreatePlan("dir /b", "ls", directory.RootPath);
        var lines = new List<string>();
        var exited = new ManualResetEventSlim();

        using var session = GameSession.Start(plan);
        session.OutputReceived += line =>
        {
            lock (lines)
                lines.Add(line.Trim());
        };
        session.Exited += _ => exited.Set();

        Assert.IsTrue(exited.Wait(Timeout), "the process did not exit in time");
        lock (lines)
            CollectionAssert.AreEqual(new[] { "marker.txt" }, lines.ToArray());
    }

    [TestMethod]
    public void StartingAMissingExecutableThrows()
    {
        var plan = new LaunchPlan(
            Path.Combine(Path.GetTempPath(), $"no-such-java-{Guid.NewGuid():N}"),
            [],
            Path.GetTempPath(),
            Path.GetTempPath(),
            []);

        Assert.Throws<Win32Exception>(() => GameSession.Start(plan));
    }

    /// <summary>
    /// 借用系统 shell 充当 Java（Windows 用 cmd，其他系统用 sh）。脚本先停顿约一秒，
    /// 让测试有时间在输出和退出之前订阅事件。
    /// </summary>
    private static LaunchPlan CreatePlan(string windowsScript, string unixScript, string? workingDirectory = null)
    {
        var isWindows = PlatformInfo.Os == OsKind.Windows;
        string[] arguments = isWindows
            ? ["/c", $"ping -n 2 127.0.0.1 >nul& {windowsScript}"]
            : ["-c", $"sleep 1; {unixScript}"];
        return new LaunchPlan(
            isWindows ? "cmd.exe" : "/bin/sh",
            arguments,
            workingDirectory ?? Path.GetTempPath(),
            Path.GetTempPath(),
            []);
    }
}
