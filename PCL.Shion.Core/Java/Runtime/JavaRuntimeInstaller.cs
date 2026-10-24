using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Platform;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Java.Runtime;

/// <summary>
/// 下载并安装 Mojang 官方发布的 Java 运行时，目录布局与官方启动器一致。
/// </summary>
public static partial class JavaRuntimeInstaller
{
    /// <summary>
    /// Java 运行时索引的官方地址，镜像地址由 <see cref="MirrorUrls"/> 推导。
    /// </summary>
    public const string RuntimeIndexUrl = "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    private const string ProgressStage = "java";

    // 与 JavaLocator 扫描 <.minecraft>/runtime 时标注的来源一致
    private const string RuntimeSource = "Launcher";

    private const UnixFileMode ExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    [GeneratedRegex(@"^JAVA_VERSION=""?([^""\r\n]+)", RegexOptions.Multiline)]
    private static partial Regex JavaVersionPattern();

    /// <summary>
    /// 当前平台在运行时索引中的键：windows-x64、windows-arm64、windows-x86、mac-os、mac-os-arm64、linux、linux-i386。
    /// Mojang 没有为当前平台发布运行时（例如 Linux ARM64）时返回 null。
    /// </summary>
    public static string? GetPlatformKey() => GetPlatformKey(PlatformInfo.Os, RuntimeInformation.OSArchitecture);

    internal static string? GetPlatformKey(OsKind os, Architecture architecture) => (os, architecture) switch
    {
        (OsKind.Windows, Architecture.X64) => "windows-x64",
        (OsKind.Windows, Architecture.Arm64) => "windows-arm64",
        (OsKind.Windows, Architecture.X86) => "windows-x86",
        (OsKind.MacOs, Architecture.X64) => "mac-os",
        (OsKind.MacOs, Architecture.Arm64) => "mac-os-arm64",
        (OsKind.Linux, Architecture.X64) => "linux",
        (OsKind.Linux, Architecture.X86) => "linux-i386",
        _ => null
    };

    /// <summary>
    /// 安装指定组件（例如 java-runtime-gamma）到 <c>&lt;minecraftRoot&gt;/runtime/&lt;组件&gt;/&lt;平台&gt;/&lt;组件&gt;</c>，
    /// 返回其中的 Java。已存在且校验通过的文件不会重新下载。
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Mojang 没有为当前平台发布运行时。</exception>
    /// <exception cref="InvalidOperationException">当前平台没有这个组件。</exception>
    /// <exception cref="IOException">有文件下载失败。</exception>
    public static async Task<JavaRuntime> InstallAsync(
        string minecraftRoot,
        string component,
        DownloadEngine engine,
        DownloadSource source,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var platformKey = GetPlatformKey()
            ?? throw new PlatformNotSupportedException("Mojang does not publish a Java runtime for this platform.");
        progress?.Report(new InstallProgress(ProgressStage, null));

        var index = await FetchAsync(engine.Http, MirrorUrls.Resolve(RuntimeIndexUrl, source), null, cancellationToken);
        var manifestRef = RuntimeIndex.FindManifest(index, platformKey, component);
        var manifest = RuntimeManifest.Parse(
            await FetchAsync(engine.Http, MirrorUrls.Resolve(manifestRef.Url, source), manifestRef.Sha1, cancellationToken));

        var directory = Path.Combine(minecraftRoot, "runtime", component, platformKey, component);
        foreach (var relativePath in manifest.Directories)
        {
            Directory.CreateDirectory(RuntimeManifest.ResolvePath(directory, relativePath));
        }

        var result = await engine.DownloadAsync(
            manifest.ToDownloadItems(directory, source),
            progress is null ? null : new StageProgress(progress),
            cancellationToken);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Take(3).Select(error => $"{error.Key}: {string.Join(", ", error.Value)}"));
            throw new IOException($"Failed to download {result.Failed.Count} file(s) of the Java runtime '{component}'. {errors}");
        }

        ApplyUnixEntries(directory, manifest);
        return Locate(directory, platformKey);
    }

    /// <summary>
    /// 在已安装的组件目录中找到 Java。Mojang 的 release 文件只有 JAVA_VERSION 而没有 OS_ARCH，
    /// 因此位数取自平台键，不能交给 <see cref="JavaVersionParser.ParseReleaseFile"/>。
    /// </summary>
    internal static JavaRuntime Locate(string componentDirectory, string platformKey)
    {
        // 官方启动器的 macOS 运行时是 jre.bundle 的结构，Java 位于其中的 Contents/Home
        var home = PlatformInfo.Os == OsKind.MacOs
            ? Path.Combine(componentDirectory, "jre.bundle", "Contents", "Home")
            : componentDirectory;
        var match = JavaVersionPattern().Match(File.ReadAllText(Path.Combine(home, "release")));
        if (!match.Success || JavaVersionParser.ParseMajorVersion(match.Groups[1].Value) is not { } majorVersion)
            throw new InvalidDataException($"Cannot read the Java version from the release file in {home}.");

        var is64Bit = platformKey is not ("windows-x86" or "linux-i386");
        return new JavaRuntime(GetLaunchExecutable(home), majorVersion, match.Groups[1].Value, is64Bit, RuntimeSource);
    }

    // 与 JavaLocator 一致：Windows 上启动游戏优先使用不带控制台窗口的 javaw.exe
    private static string GetLaunchExecutable(string home)
    {
        var bin = Path.Combine(home, "bin");
        if (PlatformInfo.Os == OsKind.Windows)
        {
            var javaw = Path.Combine(bin, "javaw.exe");
            if (File.Exists(javaw)) return javaw;
        }
        return Path.Combine(bin, JavaSearchPaths.ExecutableName);
    }

    // Windows 没有可执行位，创建符号链接也需要特殊权限，而 Windows 的运行时清单本身不含链接
    private static void ApplyUnixEntries(string directory, RuntimeManifest manifest)
    {
        if (OperatingSystem.IsWindows()) return;

        foreach (var file in manifest.Files.Where(file => file.Executable))
        {
            File.SetUnixFileMode(RuntimeManifest.ResolvePath(directory, file.RelativePath), ExecutableMode);
        }
        foreach (var link in manifest.Links)
        {
            var path = RuntimeManifest.ResolvePath(directory, link.RelativePath);
            File.Delete(path);
            File.CreateSymbolicLink(path, link.Target);
        }
    }

    // 按顺序尝试各个地址，最后一个地址的异常直接抛出；取消时立即抛出
    private static async Task<byte[]> FetchAsync(HttpClient http, IReadOnlyList<Uri> urls, string? sha1, CancellationToken cancellationToken)
    {
        foreach (var url in urls.SkipLast(1))
        {
            try
            {
                return await FetchFromAsync(http, url, sha1, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // 这个地址不可用，换下一个
            }
        }
        return await FetchFromAsync(http, urls[^1], sha1, cancellationToken);
    }

    private static async Task<byte[]> FetchFromAsync(HttpClient http, Uri url, string? sha1, CancellationToken cancellationToken)
    {
        var data = await http.GetByteArrayAsync(url, cancellationToken);
        if (sha1 is not null && !string.Equals(Convert.ToHexStringLower(SHA1.HashData(data)), sha1, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The SHA-1 of {url} does not match.");
        return data;
    }

    // 直接转发而不用 Progress<T>，避免经线程池回调后进度乱序
    private sealed class StageProgress(IProgress<InstallProgress> target) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => target.Report(new InstallProgress(ProgressStage, value));
    }
}
