using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Core.Java;
using PCL.Shion.Core.Launch;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private const int StatusFileCount = 3;
    private const int LogFileCount = 20;

    private readonly AppSettings _settings;
    private readonly Task<IReadOnlyList<JavaRuntime>> _javaScan = Task.Run(() => JavaLocator.FindAll());

    public HomeViewModel(AppSettings settings)
    {
        _settings = settings;
        PlayerName = settings.PlayerName;
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

    [ObservableProperty]
    public partial string StatusText { get; set; } = Strings.StatusReady;

    public bool ShowPlayerNameError => PlayerName.Length > 0 && !OfflineAccount.IsValidName(PlayerName);

    public bool HasInstancesHint => InstancesHint is not null;

    public bool HasJavaPath => JavaPath is not null;

    public async Task InitializeAsync()
    {
        await LoadInstancesAsync();
        if (SelectedInstance is null)
            ShowJava(null, Strings.JavaSelectInstance);
    }

    partial void OnSelectedInstanceChanged(InstanceItem? value)
    {
        if (value is not null)
            _ = UpdateJavaAsync(value);
    }

    private async Task LoadInstancesAsync()
    {
        var root = _settings.MinecraftRoot;
        string? hint;
        try
        {
            var scanned = await Task.Run(() => InstanceScanner.Scan(root));
            foreach (var instance in scanned)
                Instances.Add(new InstanceItem(instance));
            hint = Instances.Count == 0 ? Strings.InstancesEmpty(root) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            hint = Strings.InstancesReadFailed(e.Message);
        }

        InstancesHint = hint;
        SelectedInstance = Instances.FirstOrDefault(i => i.IsSelectable && i.Name == _settings.SelectedInstance)
                           ?? Instances.FirstOrDefault(i => i.IsSelectable);
    }

    /// <summary>按实例要求的 Java 主版本自动选择 Java。</summary>
    private async Task UpdateJavaAsync(InstanceItem instance)
    {
        ShowJava(null, Strings.JavaSearching);

        var root = _settings.MinecraftRoot;
        int? requiredMajor;
        try
        {
            requiredMajor = await Task.Run(() => VersionResolver.Resolve(root, instance.Name).JavaMajorVersion);
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

        var java = JavaLocator.Select(runtimes, requiredMajor);
        if (java is not null)
            ShowJava(java, Strings.JavaDetected(java.Version, java.Is64Bit ? 64 : 32));
        else
            ShowJava(null, requiredMajor is { } major ? Strings.JavaNotFoundRequired(major) : Strings.JavaNotFound, hasProblem: true);
    }

    private void ShowJava(JavaRuntime? java, string summary, bool hasProblem = false)
    {
        SelectedJava = java;
        JavaSummary = summary;
        JavaPath = java?.Executable;
        HasJavaProblem = hasProblem;
    }

    private bool CanLaunch() =>
        SelectedInstance is { IsSelectable: true } && SelectedJava is not null && OfflineAccount.IsValidName(PlayerName);

    /// <summary>
    /// 准备并启动游戏，直到游戏退出才结束；命令运行期间“启动游戏”按钮自动禁用。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchAsync()
    {
        var instance = SelectedInstance!;
        var java = SelectedJava!;
        var request = new LaunchRequest(
            _settings.MinecraftRoot, instance.Name, OfflineAccount.Create(PlayerName), java.Executable, _settings.MaxMemoryMb);

        Log.Clear();
        StatusText = Strings.StatusPreparing;
        try
        {
            var plan = await Task.Run(() => LaunchPlanner.Build(request));
            if (plan.MissingFiles.Count > 0)
            {
                ReportMissingFiles(plan.MissingFiles);
                return;
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
    }

    /// <summary>在后台线程启动游戏进程，并立即挂接输出与退出事件，避免错过最早的输出。</summary>
    private (GameSession Session, Task<int> Exited) StartSession(LaunchPlan plan)
    {
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = GameSession.Start(plan);
        session.OutputReceived += Log.Append;
        session.Exited += code => exited.TrySetResult(code);
        return (session, exited.Task);
    }

    private void ReportMissingFiles(IReadOnlyList<string> missing)
    {
        LogLauncher(Strings.LogMissingFiles(missing.Count));
        foreach (var path in missing.Take(LogFileCount))
            Log.Append($"  {path}");
        if (missing.Count > LogFileCount)
            Log.Append($"  {Strings.LogMissingFilesMore(missing.Count - LogFileCount)}");

        var names = string.Join(", ", missing.Take(StatusFileCount).Select(path => Path.GetFileName(path)));
        StatusText = Strings.StatusMissingFiles(missing.Count, missing.Count > StatusFileCount ? $"{names} …" : names);
    }

    private void LogLauncher(string message) => Log.Append($"[{Strings.AppName}] {message}");
}
