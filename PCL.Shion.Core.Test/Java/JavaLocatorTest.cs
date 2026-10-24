using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Java;

namespace PCL.Shion.Core.Test.Java;

[TestClass]
public class JavaLocatorTest
{
    private static JavaRuntime CreateRuntime(int major, bool is64Bit = true, string name = "default") =>
        new($"/jvm/{name}/bin/java", major, $"{major}.0.1", is64Bit, "Installed");

    [TestMethod]
    public void Select_ReturnsNullWhenNothingIsInstalled()
    {
        Assert.IsNull(JavaLocator.Select([], null));
        Assert.IsNull(JavaLocator.Select([], 17));
    }

    [TestMethod]
    public void Select_PicksHighestVersionWithoutRequirement()
    {
        JavaRuntime[] all = [CreateRuntime(8), CreateRuntime(21), CreateRuntime(17)];
        Assert.AreEqual(21, JavaLocator.Select(all, null)?.MajorVersion);
    }

    [TestMethod]
    public void Select_PrefersExactMajorVersion()
    {
        JavaRuntime[] all = [CreateRuntime(21), CreateRuntime(17), CreateRuntime(8)];
        Assert.AreEqual(17, JavaLocator.Select(all, 17)?.MajorVersion);
        Assert.AreEqual(8, JavaLocator.Select(all, 8)?.MajorVersion);
    }

    [TestMethod]
    public void Select_FallsBackToSmallestHigherVersion()
    {
        JavaRuntime[] all = [CreateRuntime(21), CreateRuntime(8), CreateRuntime(17)];
        Assert.AreEqual(17, JavaLocator.Select(all, 11)?.MajorVersion);
        Assert.AreEqual(8, JavaLocator.Select(all, 7)?.MajorVersion);
    }

    [TestMethod]
    public void Select_ReturnsNullWhenAllVersionsAreTooLow()
    {
        JavaRuntime[] all = [CreateRuntime(8), CreateRuntime(17)];
        Assert.IsNull(JavaLocator.Select(all, 21));
    }

    [TestMethod]
    public void Select_PrefersExactMajorVersionOver64Bit()
    {
        JavaRuntime[] all = [CreateRuntime(17, is64Bit: false), CreateRuntime(21)];
        Assert.AreEqual(17, JavaLocator.Select(all, 17)?.MajorVersion);
    }

    [TestMethod]
    public void Select_Prefers64BitWithinSameMajorVersion()
    {
        var x86 = CreateRuntime(8, is64Bit: false, name: "x86");
        var x64 = CreateRuntime(8, name: "x64");
        Assert.AreSame(x64, JavaLocator.Select([x86, x64], 8));
        Assert.AreSame(x64, JavaLocator.Select([x86, x64], null));
    }

    [TestMethod]
    public void Select_Prefers64BitWhenFallingBackToHigherVersion()
    {
        var x86 = CreateRuntime(17, is64Bit: false, name: "x86");
        var x64 = CreateRuntime(17, name: "x64");
        Assert.AreSame(x64, JavaLocator.Select([x86, x64, CreateRuntime(21)], 11));
    }

    [TestMethod]
    public void Select_KeepsListOrderAmongEqualCandidates()
    {
        var first = CreateRuntime(17, name: "first");
        var second = CreateRuntime(17, name: "second");
        Assert.AreSame(first, JavaLocator.Select([first, second], 17));
        Assert.AreSame(first, JavaLocator.Select([first, second], null));
    }

    [TestMethod]
    public void Select_Uses32BitWhenNo64BitExists()
    {
        var x86 = CreateRuntime(17, is64Bit: false);
        Assert.AreSame(x86, JavaLocator.Select([x86], 17));
    }

    [TestMethod]
    public void FindAll_ReadsReleaseFileOfJavaHome()
    {
        using var home = new FakeJavaHome("""
            JAVA_VERSION="17.0.9"
            OS_ARCH="x86_64"
            """);
        using var environment = new EnvironmentScope();
        environment.Set("JAVA_HOME", home.Root);

        var runtime = JavaLocator.FindAll().Single(item => IsSamePath(item.Executable, home.LaunchExecutable));

        Assert.AreEqual(17, runtime.MajorVersion);
        Assert.AreEqual("17.0.9", runtime.Version);
        Assert.IsTrue(runtime.Is64Bit);
        Assert.AreEqual("JAVA_HOME", runtime.Source);
    }

    [TestMethod]
    public void FindAll_ListsExecutableOnlyOnceAcrossSources()
    {
        using var home = new FakeJavaHome("""
            JAVA_VERSION="1.8.0_301"
            OS_ARCH="i586"
            """);
        using var environment = new EnvironmentScope();
        environment.Set("JAVA_HOME", home.Root);
        environment.Set("PATH", home.BinDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));

        var matches = JavaLocator.FindAll().Where(item => IsSamePath(item.Executable, home.LaunchExecutable)).ToList();

        Assert.AreEqual(1, matches.Count);
        Assert.AreEqual(8, matches[0].MajorVersion);
        Assert.IsFalse(matches[0].Is64Bit);
        Assert.AreEqual("JAVA_HOME", matches[0].Source);
    }

    [TestMethod]
    public void FindAll_FindsJavaOnPath()
    {
        using var home = new FakeJavaHome("""
            JAVA_VERSION="21.0.10"
            OS_ARCH="aarch64"
            """);
        using var environment = new EnvironmentScope();
        environment.Set("JAVA_HOME", null);
        environment.Set("PATH", home.BinDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));

        var runtime = JavaLocator.FindAll().Single(item => IsSamePath(item.Executable, home.LaunchExecutable));

        Assert.AreEqual(21, runtime.MajorVersion);
        Assert.AreEqual("PATH", runtime.Source);
    }

    [TestMethod]
    public void FindAll_SkipsExecutableThatReportsNoVersion()
    {
        using var home = new FakeJavaHome(null);
        using var environment = new EnvironmentScope();
        environment.Set("JAVA_HOME", home.Root);

        Assert.IsFalse(JavaLocator.FindAll().Any(item => IsSamePath(item.Executable, home.LaunchExecutable)));
    }

    private static bool IsSamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 在测试输出目录下伪造一个 Java 根目录。可执行文件是空文件，因此版本只能来自 release 文件。
    /// </summary>
    private sealed class FakeJavaHome : IDisposable
    {
        public FakeJavaHome(string? release)
        {
            Root = Path.Combine(AppContext.BaseDirectory, "java-test-" + Guid.NewGuid().ToString("N"));
            BinDirectory = Path.Combine(Root, "bin");
            Directory.CreateDirectory(BinDirectory);

            var windows = OperatingSystem.IsWindows();
            File.WriteAllText(Path.Combine(BinDirectory, windows ? "java.exe" : "java"), string.Empty);
            if (windows) File.WriteAllText(Path.Combine(BinDirectory, "javaw.exe"), string.Empty);
            LaunchExecutable = Path.Combine(BinDirectory, windows ? "javaw.exe" : "java");

            if (release is not null) File.WriteAllText(Path.Combine(Root, "release"), release);
        }

        public string Root { get; }

        public string BinDirectory { get; }

        /// <summary>FindAll 返回的可执行文件路径，Windows 上为 javaw.exe。</summary>
        public string LaunchExecutable { get; }

        public void Dispose() => Directory.Delete(Root, true);
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _original = [];

        public void Set(string name, string? value)
        {
            _original.TryAdd(name, Environment.GetEnvironmentVariable(name));
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            foreach (var (name, value) in _original)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
