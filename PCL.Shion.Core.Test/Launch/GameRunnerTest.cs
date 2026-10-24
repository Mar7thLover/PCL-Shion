using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Launch;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class GameRunnerTest
{
    private const string Instance = "runner";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private FakeMinecraftRoot _root = null!;
    private string _gameDirectory = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = new FakeMinecraftRoot();
        _root.WriteVersion(Instance, "{}");
        _gameDirectory = _root.GetPath($"versions/{Instance}");
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        // 后置命令不受等待，写完标记文件后可能还要片刻才退出并释放它的工作目录
        Assert.IsTrue(SpinWait.SpinUntil(TryDeleteRoot, Timeout), "the test directory is still in use");
    }

    [TestMethod]
    public void RunsThePreLaunchCommandWithInstanceVariablesBeforeStarting()
    {
        var settings = new InstanceSettings
        {
            PreLaunchCommand = ShellPlan.IsWindows
                ? "(echo %INST_NAME%& echo %INST_DIR%& echo %INST_MC_DIR%& echo %INST_JAVA%)>pre.txt"
                : "printf '%s\\n' \"$INST_NAME\" \"$INST_DIR\" \"$INST_MC_DIR\" \"$INST_JAVA\" > pre.txt"
        };
        var exited = new ManualResetEventSlim();
        var runner = new GameRunner();

        runner.Start(_root.RootPath, Instance, GamePlan(), settings, null, _ => exited.Set());
        var written = File.ReadAllLines(Path.Combine(_gameDirectory, "pre.txt")).Select(line => line.Trim()).ToArray();

        CollectionAssert.AreEqual(new[] { Instance, _gameDirectory, _gameDirectory, ShellPlan.Shell }, written);
        Assert.IsTrue(exited.Wait(Timeout), "the game did not exit in time");
    }

    [TestMethod]
    public void FailingPreLaunchCommandPreventsTheLaunch()
    {
        var settings = new InstanceSettings { PreLaunchCommand = ShellPlan.IsWindows ? "exit /b 2" : "exit 2" };
        var plan = GamePlan("echo started>started.txt", "echo started > started.txt");
        var runner = new GameRunner();
        var changes = 0;
        runner.RunningChanged += () => Interlocked.Increment(ref changes);

        var error = Assert.Throws<PreLaunchCommandException>(
            () => runner.Start(_root.RootPath, Instance, plan, settings, null, null));

        Assert.AreEqual(2, error.ExitCode);
        Assert.HasCount(0, runner.Running);
        Assert.AreEqual(0, changes);
        Assert.IsFalse(File.Exists(Path.Combine(_gameDirectory, "started.txt")));
    }

    [TestMethod]
    public void TracksTheGameUntilItExits()
    {
        var runner = new GameRunner();
        var changes = 0;
        var changesBeforeExit = -1;
        var exitCode = int.MinValue;
        var exited = new ManualResetEventSlim();
        runner.RunningChanged += () => Interlocked.Increment(ref changes);

        var game = runner.Start(_root.RootPath, Instance, GamePlan("exit /b 4", "exit 4"), new InstanceSettings(), null, code =>
        {
            changesBeforeExit = Volatile.Read(ref changes);
            exitCode = code;
            exited.Set();
        });

        Assert.AreEqual(Instance, game.InstanceName);
        Assert.AreEqual(_root.RootPath, game.MinecraftRoot);
        Assert.IsGreaterThan(0, game.ProcessId);
        Assert.IsTrue(exited.Wait(Timeout), "the game did not exit in time");
        Assert.AreEqual(4, exitCode);
        Assert.AreEqual(2, changesBeforeExit);
        Assert.HasCount(0, runner.Running);
    }

    [TestMethod]
    public void ListsEveryRunningGame()
    {
        var runner = new GameRunner();
        var settings = new InstanceSettings();
        using var exited = new CountdownEvent(2);

        var first = runner.Start(_root.RootPath, Instance, LongGamePlan(), settings, null, _ => exited.Signal());
        var second = runner.Start(_root.RootPath, Instance, LongGamePlan(), settings, null, _ => exited.Signal());
        try
        {
            CollectionAssert.AreEqual(new[] { first, second }, runner.Running.ToArray());
        }
        finally
        {
            first.Kill();
            second.Kill();
        }

        Assert.IsTrue(exited.Wait(Timeout), "the games did not exit after being killed");
        Assert.HasCount(0, runner.Running);
    }

    [TestMethod]
    public void AddsThePlayTimeToTheLatestStoredSettings()
    {
        InstanceStore.Save(_root.RootPath, Instance, new InstanceSettings { TotalPlaySeconds = 100 });
        var exited = new ManualResetEventSlim();
        var runner = new GameRunner();
        var startedAt = DateTimeOffset.Now;

        runner.Start(_root.RootPath, Instance, GamePlan("ping -n 2 127.0.0.1 >nul", "sleep 1"), new InstanceSettings(), null, _ => exited.Set());
        // 游戏运行期间别处修改了设置，退出时不应被覆盖
        var changed = InstanceStore.Load(_root.RootPath, Instance);
        changed.MaxMemoryMb = 2048;
        InstanceStore.Save(_root.RootPath, Instance, changed);
        Assert.IsTrue(exited.Wait(Timeout), "the game did not exit in time");

        var saved = InstanceStore.Load(_root.RootPath, Instance);
        Assert.IsGreaterThanOrEqualTo(101L, saved.TotalPlaySeconds);
        Assert.IsNotNull(saved.LastPlayed);
        Assert.IsGreaterThanOrEqualTo(startedAt, saved.LastPlayed.Value);
        Assert.AreEqual(2048, saved.MaxMemoryMb);
    }

    [TestMethod]
    public void RunsThePostExitCommandAfterTheGameExits()
    {
        var settings = new InstanceSettings
        {
            // 先写临时文件再改名，测试看到文件时内容已经完整
            PostExitCommand = ShellPlan.IsWindows
                ? "echo %INST_NAME%>post.tmp& move /y post.tmp post.txt >nul"
                : "echo \"$INST_NAME\" > post.tmp && mv post.tmp post.txt"
        };
        var marker = Path.Combine(_gameDirectory, "post.txt");
        var markerAtExit = true;
        var runner = new GameRunner();

        runner.Start(_root.RootPath, Instance, GamePlan(), settings, null, _ => markerAtExit = File.Exists(marker));

        Assert.IsTrue(SpinWait.SpinUntil(() => File.Exists(marker), Timeout), "the post-exit command did not run");
        Assert.IsFalse(markerAtExit, "the post-exit command ran before the exit was reported");
        Assert.AreEqual(Instance, File.ReadAllText(marker).Trim());
    }

    [TestMethod]
    public void KilledGameIsStillRecorded()
    {
        var exited = new ManualResetEventSlim();
        var runner = new GameRunner();

        var game = runner.Start(_root.RootPath, Instance, LongGamePlan(), new InstanceSettings(), null, _ => exited.Set());
        game.Kill();

        Assert.IsTrue(exited.Wait(Timeout), "the game did not exit after being killed");
        Assert.HasCount(0, runner.Running);
        Assert.IsNotNull(InstanceStore.Load(_root.RootPath, Instance).LastPlayed);
        game.Kill();
    }

    /// <summary>
    /// 在实例目录中运行的游戏：默认停顿约一秒后正常退出。
    /// </summary>
    private LaunchPlan GamePlan(string windowsScript = "exit /b 0", string unixScript = "exit 0")
    {
        return ShellPlan.Create(windowsScript, unixScript, _gameDirectory);
    }

    private LaunchPlan LongGamePlan()
    {
        return GamePlan("ping -n 120 127.0.0.1 >nul", "sleep 120");
    }

    private bool TryDeleteRoot()
    {
        try
        {
            _root.Dispose();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
