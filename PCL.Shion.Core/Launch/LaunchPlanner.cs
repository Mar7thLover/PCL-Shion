using System.Collections.ObjectModel;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 把安装好的实例与启动请求组装成可执行的 Java 命令。
/// </summary>
public static class LaunchPlanner
{
    private static readonly string[] EncodingArguments =
    [
        "-Dfile.encoding=UTF-8",
        "-Dstdout.encoding=UTF-8",
        "-Dstderr.encoding=UTF-8"
    ];

    /// <summary>旧格式版本 JSON 不含 JVM 参数，由启动器补上。</summary>
    private static readonly string[] LegacyJvmArguments =
    [
        "-Djava.library.path=${natives_directory}",
        "-cp",
        "${classpath}"
    ];

    /// <summary>
    /// 解析版本、筛选库并拼出参数。存在的旧式 natives 会被解压到 natives 目录，旧版虚拟资源会被复制到虚拟资源目录，
    /// 游戏目录不存在时会被创建。缺失的库、客户端 jar 与资源只列入 <see cref="LaunchPlan.MissingFiles"/>，不会抛出异常。
    /// </summary>
    /// <exception cref="IOException">版本 JSON 不存在或无法读取。</exception>
    /// <exception cref="InvalidDataException">版本 JSON 内容无效。</exception>
    public static LaunchPlan Build(LaunchRequest request)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.MinecraftRoot));
        var version = VersionResolver.Resolve(root, request.InstanceName);
        var layout = Layout.Create(root, request.InstanceName, request.GameDirectoryOverride);
        Directory.CreateDirectory(layout.GameDirectory);
        var quickPlay = QuickPlayArguments.Create(request.QuickPlay, version.GameArguments);

        var features = new HashSet<string>();
        if (request.WindowWidth > 0 && request.WindowHeight > 0)
            features.Add("has_custom_resolution");
        if (quickPlay.Feature is not null)
            features.Add(quickPlay.Feature);
        var environment = RuleEnvironment.ForCurrentPlatform(features);

        var missingFiles = new List<string>();
        var classpath = PrepareLibraries(version, layout, environment, missingFiles);
        var virtualAssets = version.AssetIndexId is "legacy" or "virtual";
        missingFiles.AddRange(GameAssets.Prepare(layout.AssetsDirectory, version.AssetIndexId, virtualAssets));

        var placeholders = CreatePlaceholders(request, version, layout, classpath, virtualAssets);
        if (quickPlay.Placeholder is not null)
            placeholders[quickPlay.Placeholder] = quickPlay.Value;
        var arguments = CreateArguments(request, version, layout, environment, placeholders, quickPlay.FallbackArguments);
        return new LaunchPlan(
            request.JavaExecutable,
            arguments,
            layout.GameDirectory,
            layout.NativesDirectory,
            missingFiles,
            request.EnvironmentVariables ?? ReadOnlyDictionary<string, string>.Empty,
            request.WrapperCommand);
    }

    /// <summary>
    /// 创建 natives 目录，检查库与客户端 jar 是否存在，解压存在的旧式 natives，返回 classpath（客户端 jar 在最后）。
    /// </summary>
    private static List<string> PrepareLibraries(
        ResolvedVersion version,
        Layout layout,
        RuleEnvironment environment,
        List<string> missingFiles)
    {
        var classpath = new List<string>();
        Directory.CreateDirectory(layout.NativesDirectory);
        foreach (var library in LibrarySelector.Select(version.Libraries, environment))
        {
            var path = ManifestPath.Resolve(layout.LibrariesDirectory, library.RelativePath);
            var exists = File.Exists(path);
            if (!exists)
                missingFiles.Add(path);

            if (library.ExtractExclude is { } exclude)
            {
                if (exists)
                    NativesExtractor.Extract(path, layout.NativesDirectory, exclude);
            }
            else
            {
                classpath.Add(path);
            }
        }

        classpath.Add(version.ClientJarPath);
        if (!File.Exists(version.ClientJarPath))
            missingFiles.Add(version.ClientJarPath);
        return classpath;
    }

    private static Dictionary<string, string> CreatePlaceholders(
        LaunchRequest request,
        ResolvedVersion version,
        Layout layout,
        List<string> classpath,
        bool virtualAssets)
    {
        var account = request.Account;
        return new Dictionary<string, string>
        {
            ["auth_player_name"] = account.Name,
            ["version_name"] = request.InstanceName,
            ["game_directory"] = layout.GameDirectory,
            ["assets_root"] = layout.AssetsDirectory,
            ["assets_index_name"] = version.AssetIndexId,
            ["game_assets"] = virtualAssets ? GameAssets.GetVirtualDirectory(layout.AssetsDirectory) : layout.AssetsDirectory,
            ["auth_uuid"] = account.Uuid,
            ["auth_access_token"] = account.AccessToken,
            ["auth_session"] = $"token:{account.AccessToken}:{account.Uuid}",
            ["user_type"] = "legacy",
            ["version_type"] = version.Type ?? "release",
            ["natives_directory"] = layout.NativesDirectory,
            ["launcher_name"] = request.LauncherName,
            ["launcher_version"] = request.LauncherVersion,
            ["classpath"] = string.Join(PlatformInfo.ClasspathSeparator, classpath),
            ["classpath_separator"] = PlatformInfo.ClasspathSeparator,
            ["library_directory"] = layout.LibrariesDirectory,
            ["user_properties"] = "{}",
            ["clientid"] = string.Empty,
            ["auth_xuid"] = string.Empty,
            ["resolution_width"] = request.WindowWidth.ToString(),
            ["resolution_height"] = request.WindowHeight.ToString()
        };
    }

    /// <summary>
    /// 依次为：内存与编码参数、版本的 JVM 参数、日志配置、用户追加的 JVM 参数、主类、版本的游戏参数、
    /// 旧版本直接进入服务器的参数、用户追加的游戏参数。
    /// </summary>
    private static List<string> CreateArguments(
        LaunchRequest request,
        ResolvedVersion version,
        Layout layout,
        RuleEnvironment environment,
        IReadOnlyDictionary<string, string> placeholders,
        IReadOnlyList<string> quickPlayFallback)
    {
        var arguments = new List<string> { $"-Xmx{request.MaxMemoryMb}m" };
        arguments.AddRange(EncodingArguments);
        if (version.LegacyMinecraftArguments is not null)
            arguments.AddRange(LegacyJvmArguments.Select(argument => PlaceholderFormatter.Apply(argument, placeholders)));
        arguments.AddRange(Expand(version.JvmArguments, environment, placeholders));

        if (version.LoggingArgument is not null && version.LoggingFileId is not null)
        {
            // 日志配置文件不在 MissingFiles 之列：没有它游戏照常运行，只是日志格式不同
            var logConfig = ManifestPath.ResolveName(Path.Combine(layout.AssetsDirectory, "log_configs"), version.LoggingFileId);
            if (File.Exists(logConfig))
                arguments.Add(version.LoggingArgument.Replace("${path}", logConfig));
        }

        // 放在版本参数之后，用户可以借此覆盖前面的同名选项（例如 -Xmx）
        if (request.ExtraJvmArguments is not null)
            arguments.AddRange(ShellWords.Split(request.ExtraJvmArguments));

        arguments.Add(version.MainClass);
        if (version.LegacyMinecraftArguments is not null)
        {
            var legacyTokens = version.LegacyMinecraftArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            arguments.AddRange(legacyTokens.Select(token => PlaceholderFormatter.Apply(token, placeholders)));
        }
        arguments.AddRange(Expand(version.GameArguments, environment, placeholders));
        arguments.AddRange(quickPlayFallback);
        if (request.ExtraGameArguments is not null)
            arguments.AddRange(ShellWords.Split(request.ExtraGameArguments));
        return arguments;
    }

    private static IEnumerable<string> Expand(
        IEnumerable<VersionArgument> arguments,
        RuleEnvironment environment,
        IReadOnlyDictionary<string, string> placeholders)
    {
        return arguments
            .Where(argument => RuleEvaluator.IsAllowed(argument.Rules, environment))
            .SelectMany(argument => argument.Values)
            .Select(value => PlaceholderFormatter.Apply(value, placeholders));
    }

    /// <summary>
    /// 一次启动用到的各个目录。未指定游戏目录时，实例隔离则为实例目录，否则为 .minecraft 本身。
    /// </summary>
    private sealed record Layout(
        string GameDirectory,
        string LibrariesDirectory,
        string AssetsDirectory,
        string NativesDirectory)
    {
        public static Layout Create(string root, string instanceName, string? gameDirectory)
        {
            var instanceDirectory = Path.Combine(root, "versions", instanceName);
            return new Layout(
                gameDirectory is not null
                    ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDirectory))
                    : InstanceSettings.IsIsolated(instanceDirectory) ? instanceDirectory : root,
                Path.Combine(root, "libraries"),
                Path.Combine(root, "assets"),
                Path.Combine(instanceDirectory, $"{instanceName}-natives"));
        }
    }
}
