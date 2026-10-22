using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
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

    public HomeViewModel(GameServices services)
    {
        _services = services;
        _settings = services.Settings;
        PlayerName = _settings.PlayerName;
        services.InstancesChanged += () => Dispatcher.UIThread.Post(() => _ = LoadInstancesAsync());
    }

    public GameLog Log { get; } = new();

    public ObservableCollection<InstanceItem> Instances { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlayerNameError))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    public partial string PlayerName { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    public partial InstanceItem? SelectedInstance { get; set; }

    /// <summary>实例列表上方的提示：读取中、没有实例或读取失败；列表正常时为空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstancesHint))]
    public partial string? InstancesHint { get; set; } = Strings.InstancesLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    public partial JavaRuntime? SelectedJava { get; set; }

    [ObservableProperty]
    public partial string JavaSummary { get; set; } = Strings.JavaSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasJavaPath))]
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

    /// <summary>补全文件、下载 Java 或运行游戏期间为 true，此时不能再开始另一项任务。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadJavaCommand))]
    public partial bool IsBusy { get; set; }

    public bool ShowPlayerNameError => PlayerName.Length > 0 && !OfflineAccount.IsValidName(PlayerName);

    public bool HasInstancesHint => InstancesHint is not null;

    public bool HasJavaPath => JavaPath is not null;

    public bool HasJavaOffer => JavaOfferText is not null;

    public Task InitializeAsync() => LoadInstancesAsync();

    partial void OnSelectedInstanceChanged(InstanceItem? value)
    {
        if (value is not null)
            _ = UpdateJavaAsync(value);
    }

    private static Task<IReadOnlyList<JavaRuntime>> ScanJava() => Task.Run(() => JavaLocator.FindAll());

    /// <summary>重新扫描实例列表，并尽量保持当前选中的实例。</summary>
    private async Task LoadInstancesAsync()
    {
        var root = _settings.MinecraftRoot;
        var preferred = SelectedInstance?.Name ?? _settings.SelectedInstance;
        IReadOnlyList<GameInstance> scanned = [];
        string? hint;
        try
        {
            scanned = await Task.Run(() => InstanceScanner.Scan(root));
            hint = scanned.Count == 0 ? Strings.InstancesEmpty(root) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            hint = Strings.InstancesReadFailed(e.Message);
        }

        Instances.Clear();
        foreach (var instance in scanned)
            Instances.Add(new InstanceItem(instance));

        InstancesHint = hint;
        SelectedInstance = Instances.FirstOrDefault(i => i.IsSelectable && i.Name == preferred)
                           ?? Instances.FirstOrDefault(i => i.IsSelectable);
        if (SelectedInstance is null)
            ShowJava(null, Strings.JavaSelectInstance);
    }

    /// <summary>按实例要求的 Java 主版本自动选择 Java。</summary>
    private async Task UpdateJavaAsync(InstanceItem instance)
    {
        ShowJava(null, Strings.JavaSearching);

        var root = _settings.MinecraftRoot;
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
            ShowJava(java, Strings.JavaDetected(java.Version, java.Is64Bit ? 64 : 32));
            return;
        }

        var summary = requiredMajor is { } major ? Strings.JavaNotFoundRequired(major) : Strings.JavaNotFound;
        ShowJava(null, summary, hasProblem: true, offer: CreateJavaOffer(version));
    }

    /// <summary>版本声明了运行时组件，且当前平台有官方运行时可下载时，提供下载。</summary>
    private static JavaOffer? CreateJavaOffer(ResolvedVersion version) =>
        version is { JavaComponent: { } component, JavaMajorVersion: { } major } && JavaRuntimeInstaller.GetPlatformKey() is not null
            ? new JavaOffer(component, major)
            : null;

    private void ShowJava(JavaRuntime? java, string summary, bool hasProblem = false, JavaOffer? offer = null)
    {
        SelectedJava = java;
        JavaSummary = summary;
        JavaPath = java?.Executable;
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
        IsBusy = true;
        Log.Clear();
        LogLauncher(LaunchStrings.LogJavaDownloading(offer.Major));
        StatusText = LaunchStrings.Pending(LaunchStrings.Stage("java"));
        ProgressValue = 0;
        IsProgressVisible = true;
        try
        {
            var runtime = await JavaRuntimeInstaller.InstallAsync(
                _settings.MinecraftRoot, offer.Component, _services.Engine, _services.Source,
                new Progress<InstallProgress>(ShowInstallProgress), CancellationToken.None);

            LogLauncher(LaunchStrings.LogJavaInstalled(runtime.Executable));
            StatusText = LaunchStrings.StatusJavaInstalled(runtime.Version);
            _javaScan = ScanJava();
            if (SelectedInstance is { } current)
                await UpdateJavaAsync(current);
        }
        catch (Exception e)
        {
            LogLauncher(LaunchStrings.JavaDownloadFailed(e.Message));
            StatusText = LaunchStrings.JavaDownloadFailed(e.Message);
        }
        finally
        {
            IsProgressVisible = false;
            IsBusy = false;
        }
    }

    private bool CanLaunch() =>
        SelectedInstance is { IsSelectable: true } && SelectedJava is not null && OfflineAccount.IsValidName(PlayerName)
        && !IsBusy;

    /// <summary>
    /// 准备并启动游戏，直到游戏退出才结束；缺少文件时先自动补全。命令运行期间“启动游戏”按钮自动禁用。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchAsync()
    {
        var instance = SelectedInstance!;
        var java = SelectedJava!;
        var request = new LaunchRequest(
            _settings.MinecraftRoot, instance.Name, OfflineAccount.Create(PlayerName), java.Executable, _settings.MaxMemoryMb);

        IsBusy = true;
        Log.Clear();
        StatusText = Strings.StatusPreparing;
        try
        {
            var plan = await Task.Run(() => LaunchPlanner.Build(request));
            if (plan.MissingFiles.Count > 0)
            {
                if (!await CompleteFilesAsync(instance.Name, plan.MissingFiles.Count))
                    return;

                StatusText = Strings.StatusPreparing;
                plan = await Task.Run(() => LaunchPlanner.Build(request));
                if (plan.MissingFiles.Count > 0)
                {
                    ReportMissingFiles(plan.MissingFiles);
                    return;
                }
            }

            LogLauncher(Strings.LogStarting(instance.Name, java.Executable));
            var (session, exited) = await Task.Run(() => StartSession(plan));
            using (session)
            {
                _settings.PlayerName = PlayerName;
                _settings.SelectedInstance = instance.Name;
                _settings.Save();

                StatusText = Strings.StatusRunning(session.ProcessId);
                var exitCode = await exited;
                LogLauncher(Strings.LogExited(exitCode));
                StatusText = Strings.StatusExited(exitCode);
            }
        }
        catch (Exception e)
        {
            LogLauncher(Strings.LogLaunchFailed(e.Message));
            StatusText = Strings.StatusLaunchFailed(e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>补全实例缺失或损坏的文件，全部成功时返回 true；失败的文件已写入日志与状态。</summary>
    private async Task<bool> CompleteFilesAsync(string instanceName, int missingCount)
    {
        LogLauncher(LaunchStrings.LogCompleting(missingCount));
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
            ReportDownloadFailures(result);
            return false;
        }

        LogLauncher(LaunchStrings.Completed);
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

    /// <summary>
    /// 在后台线程启动游戏进程。输出与退出的处理程序交给 <see cref="GameSession.Start"/>，在进程启动前就挂接好，
    /// 因此不会错过最早的输出，也不会错过过早的退出。
    /// </summary>
    private (GameSession Session, Task<int> Exited) StartSession(LaunchPlan plan)
    {
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = GameSession.Start(plan, Log.Append, code => exited.TrySetResult(code));
        return (session, exited.Task);
    }

    private void ReportMissingFiles(IReadOnlyList<string> missing)
    {
        LogLauncher(LaunchStrings.LogStillMissing(missing.Count));
        LogFileList(missing);
        StatusText = LaunchStrings.StatusStillMissing(missing.Count, SummarizeFiles(missing));
    }

    private void ReportDownloadFailures(DownloadResult result)
    {
        var failed = result.Failed;
        LogLauncher(LaunchStrings.LogDownloadFailed(failed.Count));
        LogFileList(failed.Select(item => $"{item.TargetPath}: {LaunchStrings.DownloadErrors(result.Errors.GetValueOrDefault(item.TargetPath))}").ToList());
        StatusText = LaunchStrings.StatusDownloadFailed(
            failed.Count, SummarizeFiles(failed.Select(item => item.TargetPath).ToList()));
    }

    private void LogFileList(IReadOnlyList<string> lines)
    {
        foreach (var line in lines.Take(LogFileCount))
            Log.Append($"  {line}");
        if (lines.Count > LogFileCount)
            Log.Append($"  {LaunchStrings.LogMore(lines.Count - LogFileCount)}");
    }

    private static string SummarizeFiles(IReadOnlyList<string> paths)
    {
        var names = string.Join(", ", paths.Take(StatusFileCount).Select(path => Path.GetFileName(path)));
        return paths.Count > StatusFileCount ? $"{names} …" : names;
    }

    private void LogLauncher(string message) => Log.Append($"[{Strings.AppName}] {message}");

    /// <summary>可下载的官方 Java 运行时：组件名与对应的主版本号。</summary>
    private sealed record JavaOffer(string Component, int Major);
}

/// <summary>主页新增文字的强类型入口。键统一带 Launch 前缀，避免与其他页面的字符串表合并后重名。</summary>
file static class LaunchStrings
{
    public static string Stage(string stage) => stage switch
    {
        "version" => Localizer.Get("LaunchStageVersion"),
        "files" => Localizer.Get("LaunchStageFiles"),
        "assets" => Localizer.Get("LaunchStageAssets"),
        "java" => Localizer.Get("LaunchStageJava"),
        _ => stage,
    };

    public static string Completed => Localizer.Get("LaunchCompleted");

    public static string Pending(string stage) => Localizer.Format("LaunchProgressPending", stage);

    public static string Progress(string stage, int completed, int total, string speed) =>
        Localizer.Format("LaunchProgress", stage, completed, total, speed);

    public static string DownloadJava(int major) => Localizer.Format("LaunchDownloadJava", major);

    public static string LogCompleting(int count) => Localizer.Format("LaunchLogCompleting", count);

    public static string LogDownloadFailed(int count) => Localizer.Format("LaunchLogDownloadFailed", count);

    public static string LogStillMissing(int count) => Localizer.Format("LaunchLogStillMissing", count);

    public static string LogMore(int count) => Localizer.Format("LaunchLogMore", count);

    public static string LogJavaDownloading(int major) => Localizer.Format("LaunchLogJavaDownloading", major);

    public static string LogJavaInstalled(string path) => Localizer.Format("LaunchLogJavaInstalled", path);

    public static string StatusDownloadFailed(int count, string files) =>
        Localizer.Format("LaunchStatusDownloadFailed", count, files);

    public static string StatusStillMissing(int count, string files) =>
        Localizer.Format("LaunchStatusStillMissing", count, files);

    public static string StatusJavaInstalled(string version) => Localizer.Format("LaunchStatusJavaInstalled", version);

    public static string JavaDownloadFailed(string message) => Localizer.Format("LaunchJavaDownloadFailed", message);

    /// <summary>把一个文件各地址的失败原因转成当前界面语言的文字，没有原因时为空。</summary>
    public static string DownloadErrors(IReadOnlyList<DownloadError>? errors) =>
        errors is null ? "" : string.Join("; ", errors.Select(DescribeError));

    private static string DescribeError(DownloadError error)
    {
        var text = error.Kind switch
        {
            DownloadErrorKind.NoUrl => Localizer.Get("LaunchErrorNoUrl"),
            DownloadErrorKind.SizeMismatch => Localizer.Format("LaunchErrorSizeMismatch", error.Expected ?? "", error.Actual ?? ""),
            DownloadErrorKind.HashMismatch => Localizer.Format("LaunchErrorHashMismatch", error.Expected ?? "", error.Actual ?? ""),
            DownloadErrorKind.Timeout => Localizer.Format("LaunchErrorTimeout", error.Expected ?? ""),
            // 其余失败的说明取自系统（HTTP 状态、网络或磁盘错误），不在字符串表里
            _ => error.Actual ?? "",
        };
        return error.Url is null ? text : $"{error.Url}: {text}";
    }
}
