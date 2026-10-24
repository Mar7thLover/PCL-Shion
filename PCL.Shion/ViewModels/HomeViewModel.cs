using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Java;
using PCL.Shion.Core.Java.Runtime;
using PCL.Shion.Core.Launch;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private const int StatusFileCount = 3;
    private const int LogFileCount = 20;

    private readonly GameServices _services;
    private readonly AppSettings _settings;
    private Task<IReadOnlyList<JavaRuntime>> _javaScan = ScanJava();
    private JavaOffer? _javaOffer;
    private IReadOnlyList<GameInstance> _scanned = [];

    public HomeViewModel(GameServices services)
    {
        _services = services;
        _settings = services.Settings;
        PlayerName = _settings.PlayerName;
        SortOptions =
        [
            new(InstanceSortMode.LastPlayed, LaunchStrings.SortLastPlayed),
            new(InstanceSortMode.Name, LaunchStrings.SortName),
            new(InstanceSortMode.Version, LaunchStrings.SortVersion),
        ];
        SelectedSort = SortOptions.First(option => option.Mode == _settings.InstanceSort);
        RunningGames.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRunningGames));
        services.InstancesChanged += () => Dispatcher.UIThread.Post(() => _ = LoadInstancesAsync());
    }

    /// <summary>界面上显示的日志：最近一次启动或下载的，或是在“正在运行”列表里选择查看的那一局。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogHeading))]
    public partial GameLog Log { get; set; } = new();

    public ObservableCollection<InstanceItem> Instances { get; } = [];

    public IReadOnlyList<InstanceSortOption> SortOptions { get; }

    public ObservableCollection<RunningGameItem> RunningGames { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlayerNameError))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    public partial string PlayerName { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenInstanceDetailsCommand))]
    public partial InstanceItem? SelectedInstance { get; set; }

    [ObservableProperty]
    public partial InstanceSortOption? SelectedSort { get; set; }

    /// <summary>实例列表上方的提示：读取中、没有实例或读取失败；列表正常时为空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstancesHint))]
    public partial string? InstancesHint { get; set; } = Strings.InstancesLoading;

    [ObservableProperty]
    public partial string JavaSummary { get; set; } = Strings.JavaSearching;

    /// <summary>启动所用 Java 的路径：实例指定的，或自动选择的；没有可用的 Java 时为空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasJavaPath))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    public partial string? JavaPath { get; set; }

    [ObservableProperty]
    public partial bool HasJavaProblem { get; set; }

    /// <summary>“下载 Java”按钮的文字；没有可下载的运行时时为空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasJavaOffer))]
    public partial string? JavaOfferText { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = Strings.StatusReady;

    [ObservableProperty]
    public partial bool IsProgressVisible { get; set; }

    /// <summary>下载进度，0 到 1。</summary>
    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    /// <summary>补全文件、下载 Java 或正在启动游戏期间为 true，此时不能再开始另一项任务；游戏启动后即恢复。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadJavaCommand))]
    public partial bool IsBusy { get; set; }

    public string LogHeading => Log.Title is { } title ? LaunchStrings.LogHeading(title) : Strings.LogTitle;

    public bool ShowPlayerNameError => PlayerName.Length > 0 && !OfflineAccount.IsValidName(PlayerName);

    public bool HasInstancesHint => InstancesHint is not null;

    public bool HasRunningGames => RunningGames.Count > 0;

    public bool HasJavaPath => JavaPath is not null;

    public bool HasJavaOffer => JavaOfferText is not null;

    public Task InitializeAsync() => LoadInstancesAsync();

    partial void OnSelectedInstanceChanged(InstanceItem? value)
    {
        if (value is null)
            return;

        ShowJava(null, Strings.JavaSearching);
        _ = UpdateJavaAsync(value);
    }

    partial void OnSelectedSortChanged(InstanceSortOption? value)
    {
        if (value is null || value.Mode == _settings.InstanceSort)
            return;

        _settings.InstanceSort = value.Mode;
        _settings.Save();
        ShowInstances();
    }

    private static Task<IReadOnlyList<JavaRuntime>> ScanJava() => Task.Run(() => JavaLocator.FindAll());

    /// <summary>重新扫描实例列表，并尽量保持当前选中的实例。</summary>
    private async Task LoadInstancesAsync()
    {
        var root = _settings.MinecraftRoot;
        string? hint;
        try
        {
            _scanned = await Task.Run(() => InstanceScanner.Scan(root));
            hint = _scanned.Count == 0 ? Strings.InstancesEmpty(root) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _scanned = [];
            hint = Strings.InstancesReadFailed(e.Message);
        }

        InstancesHint = hint;
        ShowInstances();
    }

    /// <summary>按当前排序方式重建列表，并尽量保持当前选中的实例。</summary>
    private void ShowInstances()
    {
        var preferred = SelectedInstance?.Name ?? _settings.SelectedInstance;
        Instances.Clear();
        foreach (var instance in Sort(_scanned, _settings.InstanceSort))
            Instances.Add(new InstanceItem(instance));

        SelectedInstance = Instances.FirstOrDefault(i => i.IsSelectable && i.Name == preferred)
                           ?? Instances.FirstOrDefault(i => i.IsSelectable);
        if (SelectedInstance is null)
            ShowJava(null, Strings.JavaSelectInstance);
    }

    /// <summary>名称按字母顺序；版本和最近游玩较新的在前，无法判断的排在最后。</summary>
    private static IEnumerable<GameInstance> Sort(IEnumerable<GameInstance> instances, InstanceSortMode mode) => mode switch
    {
        InstanceSortMode.Version => instances
            .OrderByDescending(i => ParseVersion(i.MinecraftVersion))
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
        InstanceSortMode.LastPlayed => instances
            .OrderByDescending(i => i.LastPlayed)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
        _ => instances.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>取版本号开头的数字部分（如 1.20.1），快照等无法识别的版本为 null。</summary>
    private static Version? ParseVersion(string? text)
    {
        if (text is null)
            return null;

        var length = 0;
        while (length < text.Length && (char.IsAsciiDigit(text[length]) || text[length] == '.'))
            length++;
        return Version.TryParse(text.AsSpan(0, length).TrimEnd('.'), out var version) ? version : null;
    }

    /// <summary>
    /// 重新匹配当前实例的 Java。设置页与实例详情页里的改动会影响结果，因此每次回到启动页都要调用。
    /// </summary>
    public void RefreshJava()
    {
        if (SelectedInstance is { } instance && !IsBusy)
            _ = UpdateJavaAsync(instance);
    }

    /// <summary>
    /// 依次采用实例设置里指定的 Java、设置里的默认 Java，都没有时按实例要求的 Java 主版本自动选择。
    /// </summary>
    private async Task UpdateJavaAsync(InstanceItem instance)
    {
        var root = _settings.MinecraftRoot;
        string? instanceJava;
        try
        {
            instanceJava = await Task.Run(() => InstanceStore.Load(root, instance.Name).JavaExecutable);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            if (ReferenceEquals(SelectedInstance, instance))
                ShowJava(null, LaunchStrings.SettingsReadFailed(e.Message), hasProblem: true);
            return;
        }

        if (instanceJava is not null)
        {
            ShowConfiguredJava(instance, instanceJava, LaunchStrings.JavaCustom, LaunchStrings.JavaCustomMissing);
            return;
        }

        if (_settings.DefaultJavaExecutable is { } defaultJava)
        {
            ShowConfiguredJava(instance, defaultJava, LaunchStrings.JavaDefault, LaunchStrings.JavaDefaultMissing);
            return;
        }

        ResolvedVersion version;
        try
        {
            version = await Task.Run(() => VersionResolver.Resolve(root, instance.Name));
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            if (ReferenceEquals(SelectedInstance, instance))
                ShowJava(null, Strings.JavaResolveFailed(e.Message), hasProblem: true);
            return;
        }

        var runtimes = await _javaScan;
        if (!ReferenceEquals(SelectedInstance, instance))
            return;

        var requiredMajor = version.JavaMajorVersion;
        var java = JavaLocator.Select(runtimes, requiredMajor);
        if (java is not null)
        {
            ShowJava(java.Executable, Strings.JavaDetected(java.Version, java.Is64Bit ? 64 : 32));
            return;
        }

        var summary = requiredMajor is { } major ? Strings.JavaNotFoundRequired(major) : Strings.JavaNotFound;
        ShowJava(null, summary, hasProblem: true, offer: CreateJavaOffer(version));
    }

    /// <summary>采用用户指定的 Java；文件不存在时不改用别的，而是提示出来，此时无法启动。</summary>
    private void ShowConfiguredJava(InstanceItem instance, string path, string summary, Func<string, string> missingSummary)
    {
        if (!ReferenceEquals(SelectedInstance, instance))
            return;

        if (File.Exists(path))
            ShowJava(path, summary);
        else
            ShowJava(null, missingSummary(path), hasProblem: true);
    }

    /// <summary>版本声明了运行时组件，且当前平台有官方运行时可下载时，提供下载。</summary>
    private static JavaOffer? CreateJavaOffer(ResolvedVersion version) =>
        version is { JavaComponent: { } component, JavaMajorVersion: { } major } && JavaRuntimeInstaller.GetPlatformKey() is not null
            ? new JavaOffer(component, major)
            : null;

    private void ShowJava(string? executable, string summary, bool hasProblem = false, JavaOffer? offer = null)
    {
        JavaSummary = summary;
        JavaPath = executable;
        HasJavaProblem = hasProblem;
        _javaOffer = offer;
        JavaOfferText = offer is null ? null : LaunchStrings.DownloadJava(offer.Major);
        DownloadJavaCommand.NotifyCanExecuteChanged();
    }

    private bool CanDownloadJava() => _javaOffer is not null && !IsBusy;

    /// <summary>下载实例所需的官方 Java 运行时，完成后重新扫描并选用。</summary>
    [RelayCommand(CanExecute = nameof(CanDownloadJava))]
    private async Task DownloadJavaAsync()
    {
        var offer = _javaOffer!;
        var log = StartLog(SelectedInstance?.Name);
        IsBusy = true;
        log.AppendLauncher(LaunchStrings.LogJavaDownloading(offer.Major));
        StatusText = LaunchStrings.Pending(LaunchStrings.Stage("java"));
        ProgressValue = 0;
        IsProgressVisible = true;
        try
        {
            var runtime = await JavaRuntimeInstaller.InstallAsync(
                _settings.MinecraftRoot, offer.Component, _services.Engine, _services.Source,
                new Progress<InstallProgress>(ShowInstallProgress), CancellationToken.None);

            log.AppendLauncher(LaunchStrings.LogJavaInstalled(runtime.Executable));
            StatusText = LaunchStrings.StatusJavaInstalled(runtime.Version);
            _javaScan = ScanJava();
            if (SelectedInstance is { } current)
                await UpdateJavaAsync(current);
        }
        catch (Exception e)
        {
            log.AppendLauncher(LaunchStrings.JavaDownloadFailed(e.Message));
            StatusText = LaunchStrings.JavaDownloadFailed(e.Message);
        }
        finally
        {
            IsProgressVisible = false;
            IsBusy = false;
        }
    }

    private bool CanLaunch() =>
        SelectedInstance is { IsSelectable: true } && JavaPath is not null && OfflineAccount.IsValidName(PlayerName)
        && !IsBusy;

    private bool CanOpenInstanceDetails() => SelectedInstance is not null;

    [RelayCommand(CanExecute = nameof(CanOpenInstanceDetails))]
    private void OpenInstanceDetails() => _services.RequestOpenInstance(SelectedInstance!.Name);

    /// <summary>
    /// 准备并启动游戏，游戏进程启动后即返回；缺少文件时先自动补全。命令运行期间“启动游戏”按钮自动禁用。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchAsync()
    {
        var instance = SelectedInstance!;
        var java = JavaPath!;
        var root = _settings.MinecraftRoot;
        var log = StartLog(instance.Name);

        IsBusy = true;
        StatusText = Strings.StatusPreparing;
        try
        {
            var instanceSettings = await Task.Run(() => InstanceStore.Load(root, instance.Name));
            var request = CreateRequest(instance, instanceSettings, java);
            var plan = await Task.Run(() => LaunchPlanner.Build(request));
            if (plan.MissingFiles.Count > 0)
            {
                if (!await CompleteFilesAsync(instance.Name, plan.MissingFiles.Count, log))
                    return;

                StatusText = Strings.StatusPreparing;
                plan = await Task.Run(() => LaunchPlanner.Build(request));
                if (plan.MissingFiles.Count > 0)
                {
                    ReportMissingFiles(plan.MissingFiles, log);
                    return;
                }
            }

            log.AppendLauncher(Strings.LogStarting(instance.Name, java));
            // 输出与退出的处理程序交给 Start，在进程启动前就挂接好，因此不会错过最早的输出，也不会错过过早的退出
            var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var game = await Task.Run(() => _services.Runner.Start(
                root, instance.Name, plan, instanceSettings, log.Append, code => exited.TrySetResult(code)));

            _settings.PlayerName = PlayerName;
            _settings.SelectedInstance = instance.Name;
            _settings.Save();

            var running = new RunningGameItem(game, log, shown => Log = shown);
            RunningGames.Add(running);
            StatusText = LaunchStrings.StatusStarted(instance.Name, game.ProcessId);
            _services.Notify(instance.Name, LaunchStrings.NotifyStarted);
            _ = WatchExitAsync(running, exited.Task);
        }
        catch (PreLaunchCommandException e)
        {
            ReportLaunchFailure(log, LaunchStrings.PreLaunchFailed(e.ExitCode));
        }
        catch (Exception e)
        {
            ReportLaunchFailure(log, e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReportLaunchFailure(GameLog log, string message)
    {
        log.AppendLauncher(Strings.LogLaunchFailed(message));
        StatusText = Strings.StatusLaunchFailed(message);
    }

    /// <summary>用实例设置与全局设置组装启动请求；实例设置里没有指定的项沿用全局设置。</summary>
    private LaunchRequest CreateRequest(InstanceItem instance, InstanceSettings settings, string java)
    {
        var root = _settings.MinecraftRoot;
        var request = new LaunchRequest(
            root, instance.Name, OfflineAccount.Create(PlayerName), java, settings.MaxMemoryMb ?? _settings.MaxMemoryMb,
            GameDirectoryOverride: InstanceManager.GetGameDirectory(root, instance.Instance, settings),
            ExtraJvmArguments: settings.ExtraJvmArguments,
            ExtraGameArguments: settings.ExtraGameArguments,
            WrapperCommand: settings.WrapperCommand,
            EnvironmentVariables: settings.EnvironmentVariables,
            QuickPlay: settings.QuickPlay);
        return request with
        {
            WindowWidth = settings.WindowWidth ?? request.WindowWidth,
            WindowHeight = settings.WindowHeight ?? request.WindowHeight,
        };
    }

    /// <summary>等游戏退出后移出“正在运行”列表、通知用户，并刷新实例列表里的游玩时长。</summary>
    private async Task WatchExitAsync(RunningGameItem game, Task<int> exited)
    {
        var exitCode = await exited;
        RunningGames.Remove(game);
        game.Log.AppendLauncher(Strings.LogExited(exitCode));
        if (!IsBusy)
            StatusText = LaunchStrings.StatusExited(game.Name, exitCode);
        _services.Notify(game.Name, LaunchStrings.NotifyExited(exitCode));
        await LoadInstancesAsync();
    }

    /// <summary>开始一份新日志并显示出来，之后这次任务的消息都写入它。</summary>
    private GameLog StartLog(string? title)
    {
        var log = new GameLog(title);
        Log = log;
        return log;
    }

    /// <summary>补全实例缺失或损坏的文件，全部成功时返回 true；失败的文件已写入日志与状态。</summary>
    private async Task<bool> CompleteFilesAsync(string instanceName, int missingCount, GameLog log)
    {
        log.AppendLauncher(LaunchStrings.LogCompleting(missingCount));
        ProgressValue = 0;
        IsProgressVisible = true;
        DownloadResult result;
        try
        {
            result = await _services.CreateInstaller().CompleteAsync(
                _settings.MinecraftRoot, instanceName, new Progress<InstallProgress>(ShowInstallProgress), CancellationToken.None);
        }
        finally
        {
            IsProgressVisible = false;
        }

        if (!result.Succeeded)
        {
            ReportDownloadFailures(result, log);
            return false;
        }

        log.AppendLauncher(LaunchStrings.Completed);
        return true;
    }

    private void ShowInstallProgress(InstallProgress progress)
    {
        var stage = LaunchStrings.Stage(progress.Stage);
        if (progress.Download is not { } download)
        {
            StatusText = LaunchStrings.Pending(stage);
            return;
        }

        ProgressValue = download.TotalBytes > 0
            ? (double)download.DownloadedBytes / download.TotalBytes
            : download.TotalFiles > 0 ? (double)download.CompletedFiles / download.TotalFiles : 0;
        StatusText = LaunchStrings.Progress(
            stage, download.CompletedFiles, download.TotalFiles, ByteFormatter.FormatSpeed(download.BytesPerSecond));
    }

    private void ReportMissingFiles(IReadOnlyList<string> missing, GameLog log)
    {
        log.AppendLauncher(LaunchStrings.LogStillMissing(missing.Count));
        AppendFileList(log, missing);
        StatusText = LaunchStrings.StatusStillMissing(missing.Count, SummarizeFiles(missing));
    }

    private void ReportDownloadFailures(DownloadResult result, GameLog log)
    {
        var failed = result.Failed;
        log.AppendLauncher(LaunchStrings.LogDownloadFailed(failed.Count));
        AppendFileList(log, failed.Select(item => $"{item.TargetPath}: {LaunchStrings.DownloadErrors(result.Errors.GetValueOrDefault(item.TargetPath))}").ToList());
        StatusText = LaunchStrings.StatusDownloadFailed(
            failed.Count, SummarizeFiles(failed.Select(item => item.TargetPath).ToList()));
    }

    private static void AppendFileList(GameLog log, IReadOnlyList<string> lines)
    {
        foreach (var line in lines.Take(LogFileCount))
            log.Append($"  {line}");
        if (lines.Count > LogFileCount)
            log.Append($"  {LaunchStrings.LogMore(lines.Count - LogFileCount)}");
    }

    private static string SummarizeFiles(IReadOnlyList<string> paths)
    {
        var names = string.Join(", ", paths.Take(StatusFileCount).Select(path => Path.GetFileName(path)));
        return paths.Count > StatusFileCount ? $"{names} …" : names;
    }

    /// <summary>可下载的官方 Java 运行时：组件名与对应的主版本号。</summary>
    private sealed record JavaOffer(string Component, int Major);
}
