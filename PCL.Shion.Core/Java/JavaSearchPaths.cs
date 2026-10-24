using System.Runtime.Versioning;
using Microsoft.Win32;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Java;

/// <summary>
/// 一个待检测的 Java 可执行文件（java 或 java.exe）及其发现来源。
/// </summary>
internal readonly record struct JavaCandidate(string Executable, string Source);

/// <summary>
/// 枚举各个来源下可能存在 Java 的位置。路径已展开符号链接，但不保证文件存在。
/// </summary>
internal static class JavaSearchPaths
{
    private const string JavaHomeSource = "JAVA_HOME";
    private const string PathSource = "PATH";
    private const string LauncherSource = "Launcher";
    private const string StoreLauncherSource = "StoreLauncher";
    private const string InstalledSource = "Installed";
    private const string RegistrySource = "Registry";

    private const string StoreLauncherPackage = "Microsoft.4297127D64EC6_8wekyb3d8bbwe";

    // macOS 的 /usr/bin/java 只是占位程序，未安装 JDK 时运行它会弹出安装提示
    private const string MacOsJavaStub = "/usr/bin/java";

    private static readonly string[] WindowsVendorFolders =
    [
        "Java", "Eclipse Adoptium", "Eclipse Foundation", "AdoptOpenJDK", "Microsoft",
        "Zulu", "Amazon Corretto", "BellSoft", "Semeru", "RedHat"
    ];

    public static string ExecutableName => PlatformInfo.Os == OsKind.Windows ? "java.exe" : "java";

    public static IEnumerable<JavaCandidate> Enumerate() =>
        FromJavaHome()
            .Concat(FromPath())
            .Concat(FromLauncherRuntimes())
            .Concat(FromInstalledDirectories())
            .Concat(FromRegistry());

    private static IEnumerable<JavaCandidate> FromJavaHome()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME")?.Trim().Trim('"');
        if (!string.IsNullOrEmpty(javaHome))
        {
            yield return FromHome(javaHome, JavaHomeSource);
        }
    }

    private static IEnumerable<JavaCandidate> FromPath()
    {
        var entries = Environment.GetEnvironmentVariable("PATH")
            ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        foreach (var entry in entries)
        {
            var executable = Path.Combine(entry.Trim('"'), ExecutableName);
            if (PlatformInfo.Os == OsKind.MacOs && executable == MacOsJavaStub) continue;
            yield return FromExecutable(executable, PathSource);
        }
    }

    private static IEnumerable<JavaCandidate> FromLauncherRuntimes()
    {
        var runtimeRoot = Path.Combine(MinecraftFolder.GetDefault(), "runtime");
        foreach (var home in EnumerateRuntimeHomes(runtimeRoot))
        {
            yield return FromHome(home, LauncherSource);
        }

        if (PlatformInfo.Os != OsKind.Windows) yield break;
        var storeRuntimeRoot = Path.Combine(
            SpecialFolders.Get(Environment.SpecialFolder.LocalApplicationData),
            "Packages", StoreLauncherPackage, "LocalCache", "Local", "runtime");
        foreach (var home in EnumerateRuntimeHomes(storeRuntimeRoot))
        {
            yield return FromHome(home, StoreLauncherSource);
        }
    }

    private static IEnumerable<JavaCandidate> FromInstalledDirectories() =>
        EnumerateInstalledHomes().Select(home => FromHome(home, InstalledSource));

    private static IEnumerable<JavaCandidate> FromRegistry() =>
        OperatingSystem.IsWindows() ? ReadRegistryHomes() : [];

    [SupportedOSPlatform("windows")]
    private static IEnumerable<JavaCandidate> ReadRegistryHomes()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var javaSoft = localMachine.OpenSubKey(@"SOFTWARE\JavaSoft");
            if (javaSoft is null) continue;
            foreach (var product in javaSoft.GetSubKeyNames())
            {
                using var productKey = javaSoft.OpenSubKey(product);
                if (productKey is null) continue;
                foreach (var version in productKey.GetSubKeyNames())
                {
                    using var versionKey = productKey.OpenSubKey(version);
                    if (versionKey?.GetValue("JavaHome") is string { Length: > 0 } javaHome)
                    {
                        yield return FromHome(javaHome, RegistrySource);
                    }
                }
            }
        }
    }

    // 官方启动器的运行时布局为 <组件>/<平台>/<组件>，macOS 上 Java 位于其中的 jre.bundle
    private static IEnumerable<string> EnumerateRuntimeHomes(string runtimeRoot) =>
        EnumerateDirectories(runtimeRoot).SelectMany(component =>
            EnumerateDirectories(component).Select(platform =>
            {
                var home = Path.Combine(platform, Path.GetFileName(component));
                return PlatformInfo.Os == OsKind.MacOs ? Path.Combine(home, "jre.bundle", "Contents", "Home") : home;
            }));

    private static IEnumerable<string> EnumerateInstalledHomes() => PlatformInfo.Os switch
    {
        OsKind.Windows => EnumerateWindowsInstalledHomes(),
        OsKind.MacOs => EnumerateDirectories("/Library/Java/JavaVirtualMachines")
            .Select(directory => Path.Combine(directory, "Contents", "Home")),
        _ => EnumerateDirectories("/usr/lib/jvm")
    };

    private static IEnumerable<string> EnumerateWindowsInstalledHomes() =>
        new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
            .Select(folder => Environment.GetFolderPath(folder))
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(programFiles => WindowsVendorFolders
                .SelectMany(vendor => EnumerateDirectories(Path.Combine(programFiles, vendor))));

    private static IEnumerable<string> EnumerateDirectories(string path)
    {
        if (!Directory.Exists(path)) return [];
        return Directory.EnumerateDirectories(path).Order(StringComparer.OrdinalIgnoreCase);
    }

    private static JavaCandidate FromHome(string home, string source) =>
        FromExecutable(Path.Combine(ResolveLink(new DirectoryInfo(home)), "bin", ExecutableName), source);

    private static JavaCandidate FromExecutable(string executable, string source) =>
        new(ResolveLink(new FileInfo(executable)), source);

    // 展开符号链接，让 /usr/bin/java 之类的入口与真实路径合并为同一项
    private static string ResolveLink(FileSystemInfo entry)
    {
        try
        {
            return entry.ResolveLinkTarget(true)?.FullName ?? entry.FullName;
        }
        catch (IOException)
        {
            return entry.FullName;
        }
    }
}
