using System.ComponentModel;
using System.Diagnostics;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Java;

/// <summary>
/// 在本机搜索 Java 运行时，并按游戏需要的主版本号挑选。
/// </summary>
public static class JavaLocator
{
    private const int VersionCommandTimeoutMs = 15_000;

    private static readonly StringComparer PathComparer =
        PlatformInfo.Os == OsKind.Linux ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// 搜索本机所有可用的 Java。结果按来源优先级排列，同一可执行文件只保留最先发现的一份。
    /// </summary>
    public static IReadOnlyList<JavaRuntime> FindAll() =>
        JavaSearchPaths.Enumerate()
            .Where(candidate => File.Exists(candidate.Executable))
            .DistinctBy(candidate => candidate.Executable, PathComparer)
            .ToList()
            .AsParallel()
            .AsOrdered()
            .Select(Probe)
            .OfType<JavaRuntime>()
            .ToList();

    /// <summary>
    /// 挑选适合游戏的 Java。有版本要求时先选主版本相等的，没有则选大于要求的最小主版本，找不到返回 null；
    /// 无要求时选主版本最高的。主版本确定后 64 位优先，仍有多个时取列表中靠前的。
    /// </summary>
    public static JavaRuntime? Select(IReadOnlyList<JavaRuntime> all, int? requiredMajor)
    {
        if (PickMajorVersion(all, requiredMajor) is not { } major) return null;

        var candidates = all.Where(runtime => runtime.MajorVersion == major).ToList();
        return candidates.FirstOrDefault(runtime => runtime.Is64Bit) ?? candidates.FirstOrDefault();
    }

    private static int? PickMajorVersion(IReadOnlyList<JavaRuntime> all, int? requiredMajor)
    {
        var majors = all.Select(runtime => (int?)runtime.MajorVersion).ToList();
        if (requiredMajor is not { } required) return majors.Max();
        if (majors.Contains(required)) return required;
        return majors.Where(major => major > required).Min();
    }

    private static JavaRuntime? Probe(JavaCandidate candidate)
    {
        var info = ReadReleaseFile(candidate.Executable) ?? RunVersionCommand(candidate.Executable);
        if (info is null) return null;
        return new JavaRuntime(GetLaunchExecutable(candidate.Executable), info.MajorVersion, info.Version, info.Is64Bit, candidate.Source);
    }

    private static JavaVersionInfo? ReadReleaseFile(string executable)
    {
        if (Path.GetDirectoryName(Path.GetDirectoryName(executable)) is not { } javaHome) return null;
        try
        {
            return JavaVersionParser.ParseReleaseFile(File.ReadAllText(Path.Combine(javaHome, "release")));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static JavaVersionInfo? RunVersionCommand(string executable)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            ArgumentList = { "-version" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var output = Task.WhenAll(process.StandardError.ReadToEndAsync(), process.StandardOutput.ReadToEndAsync());
            if (!process.WaitForExit(VersionCommandTimeoutMs))
            {
                process.Kill(true);
                return null;
            }
            return JavaVersionParser.ParseVersionOutput(string.Join('\n', output.Result));
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    // Windows 上启动游戏使用不带控制台窗口的 javaw.exe；探测版本仍须用 java.exe 才有输出
    private static string GetLaunchExecutable(string executable)
    {
        if (PlatformInfo.Os != OsKind.Windows) return executable;
        var javaw = Path.Combine(Path.GetDirectoryName(executable)!, "javaw.exe");
        return File.Exists(javaw) ? javaw : executable;
    }
}
