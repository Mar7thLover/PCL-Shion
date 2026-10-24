using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Core.Java;
using PCL.Shion.Core.Platform;
using PCL.Shion.Core.Sources;
using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    public const double MemoryMinimumMb = 1024;
    public const double MemoryStepMb = 512;
    public const double ConcurrencyMinimum = 1;
    public const double ConcurrencyMaximum = 64;

    private static readonly StringComparer PathComparer =
        PlatformInfo.Os == OsKind.Linux ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private static readonly SettingsSourceOption[] Sources =
    [
        new(DownloadSource.Official, SettingsStrings.SourceOfficial),
        new(DownloadSource.Bmclapi, SettingsStrings.SourceBmclapi),
    ];

    private readonly GameServices _services;
    private readonly AppSettings _settings;
    private bool _syncing;
    private bool _javaScanned;

    public SettingsViewModel(GameServices services)
    {
        _services = services;
        _settings = services.Settings;
        MemoryMaximumMb = Math.Max(GetMemoryCeilingMb(), Math.Max(_settings.MaxMemoryMb, MemoryMinimumMb));

        Sync(() =>
        {
            LoadFolders(null);
            MemoryMb = _settings.MaxMemoryMb;
            ShowJavaOptions([]);
            SelectedSource = FindSource();
            Concurrency = _settings.MaxDownloadConcurrency;
            NotificationsEnabled = _settings.NotificationsEnabled;
            CompletionSound = _settings.CompletionSound;
            CheckUpdates = _settings.CheckUpdates;
            IncludePrereleaseUpdates = _settings.IncludePrereleaseUpdates;
        });
        services.Updates.Changed += OnUpdatesChanged;
    }

    /// <summary>当前系统是否支持完成提示音。</summary>
    public static bool IsSoundSupported => OperatingSystem.IsWindows();

    public ObservableCollection<SettingsFolderItem> Folders { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveFolderUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetCurrentFolderCommand))]
    public partial SettingsFolderItem? SelectedFolder { get; set; }

    public double MemoryMaximumMb { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemoryText))]
    public partial double MemoryMb { get; set; }

    public string MemoryText => ByteFormatter.Format(MemoryMb * 1024 * 1024);

    [ObservableProperty]
    public partial IReadOnlyList<SettingsJavaOption> JavaOptions { get; set; } = [];

    [ObservableProperty]
    public partial SettingsJavaOption? SelectedJava { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanJavaCommand))]
    public partial bool IsScanningJava { get; set; }

    public IReadOnlyList<SettingsSourceOption> SourceOptions => Sources;

    [ObservableProperty]
    public partial SettingsSourceOption SelectedSource { get; set; } = Sources[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConcurrencyText))]
    public partial double Concurrency { get; set; }

    public string ConcurrencyText => ((int)Concurrency).ToString();

    [ObservableProperty]
    public partial bool NotificationsEnabled { get; set; }

    [ObservableProperty]
    public partial bool CompletionSound { get; set; }

    [ObservableProperty]
    public partial bool CheckUpdates { get; set; }

    [ObservableProperty]
    public partial bool IncludePrereleaseUpdates { get; set; }

    public string VersionText => SettingsStrings.CurrentVersion(AppInfo.VersionText);

    /// <summary>无法打开发布页面时的原因；打开成功后为空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenError))]
    public partial string? OpenError { get; set; }

    public bool HasOpenError => OpenError is not null;

    public bool IsChecking => _services.Updates.State == UpdateState.Checking;

    public bool HasUpdate => _services.Updates.State == UpdateState.Available;

    public bool UpdateFailed => _services.Updates.State == UpdateState.Failed;

    public string UpdateText
    {
        get
        {
            var updates = _services.Updates;
            return updates.State switch
            {
                UpdateState.Checking => SettingsStrings.UpdateChecking,
                UpdateState.UpToDate => SettingsStrings.UpdateUpToDate,
                UpdateState.Available => SettingsStrings.UpdateAvailable(updates.Release!.Tag),
                UpdateState.Failed => SettingsStrings.UpdateFailed(updates.Error!),
                _ => SettingsStrings.UpdateIdle,
            };
        }
    }

    /// <summary>
    /// 设置页每次显示时调用。下载页也能改下载源，所以按设置刷新；第一次显示时才扫描 Java。
    /// </summary>
    public Task OnShownAsync()
    {
        Sync(() => SelectedSource = FindSource());
        return _javaScanned ? Task.CompletedTask : ScanJavaAsync();
    }

    /// <summary>添加一个 Minecraft 目录；已在列表中时只选中它。</summary>
    public void AddFolder(string path)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (_settings.MinecraftFolders.Contains(folder, PathComparer))
            SelectedFolder = Folders.First(item => PathComparer.Equals(item.Path, folder));
        else
            ChangeFolders(folders => folders.Add(folder), folder);
    }

    partial void OnMemoryMbChanged(double value) => Persist(() => _settings.MaxMemoryMb = (int)value);

    partial void OnSelectedJavaChanged(SettingsJavaOption? value)
    {
        // 重建选项列表时选择框会短暂清空选中项，这不是用户的选择
        if (value is not null)
            Persist(() => _settings.DefaultJavaExecutable = value.Executable);
    }

    partial void OnSelectedSourceChanged(SettingsSourceOption value) =>
        Persist(() => _settings.DownloadSource = value.Source);

    partial void OnConcurrencyChanged(double value) =>
        Persist(() => _settings.MaxDownloadConcurrency = (int)value);

    partial void OnNotificationsEnabledChanged(bool value) =>
        Persist(() => _settings.NotificationsEnabled = value);

    partial void OnCompletionSoundChanged(bool value) =>
        Persist(() => _settings.CompletionSound = value);

    partial void OnCheckUpdatesChanged(bool value) =>
        Persist(() => _settings.CheckUpdates = value);

    partial void OnIncludePrereleaseUpdatesChanged(bool value)
    {
        // 新版本的判断取决于是否包含预发布版，改动后需要重新检查
        if (Persist(() => _settings.IncludePrereleaseUpdates = value))
            _ = _services.Updates.CheckAsync();
    }

    private bool CanRemoveFolder() => SelectedFolder is not null && Folders.Count > 1;

    [RelayCommand(CanExecute = nameof(CanRemoveFolder))]
    private void RemoveFolder()
    {
        var index = Folders.IndexOf(SelectedFolder!);
        var neighbor = Folders[index == Folders.Count - 1 ? index - 1 : index + 1].Path;
        ChangeFolders(folders => folders.RemoveAt(index), neighbor);
    }

    private bool CanMoveFolder() => SelectedFolder is { IsCurrent: false };

    [RelayCommand(CanExecute = nameof(CanMoveFolder))]
    private void MoveFolderUp()
    {
        var folder = SelectedFolder!;
        var index = Folders.IndexOf(folder);
        ChangeFolders(folders => (folders[index - 1], folders[index]) = (folders[index], folders[index - 1]), folder.Path);
    }

    [RelayCommand(CanExecute = nameof(CanMoveFolder))]
    private void SetCurrentFolder()
    {
        var folder = SelectedFolder!;
        var index = Folders.IndexOf(folder);
        ChangeFolders(folders =>
        {
            folders.RemoveAt(index);
            folders.Insert(0, folder.Path);
        }, folder.Path);
    }

    private bool CanScanJava() => !IsScanningJava;

    [RelayCommand(CanExecute = nameof(CanScanJava))]
    private async Task ScanJavaAsync()
    {
        _javaScanned = true;
        IsScanningJava = true;
        try
        {
            ShowJavaOptions(await Task.Run(() => JavaLocator.FindAll()));
        }
        finally
        {
            IsScanningJava = false;
        }
    }

    private bool CanCheckUpdate() => !IsChecking;

    [RelayCommand(CanExecute = nameof(CanCheckUpdate))]
    private Task CheckUpdateAsync() => _services.Updates.CheckAsync();

    private bool CanOpenRelease() => HasUpdate;

    [RelayCommand(CanExecute = nameof(CanOpenRelease))]
    private void OpenRelease()
    {
        try
        {
            SystemShell.OpenUrl(_services.Updates.Release!.HtmlUrl.AbsoluteUri);
            OpenError = null;
        }
        catch (Win32Exception e)
        {
            OpenError = SettingsStrings.OpenFailed(e.Message);
        }
    }

    private void OnUpdatesChanged()
    {
        OnPropertyChanged(nameof(IsChecking));
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(UpdateFailed));
        OnPropertyChanged(nameof(UpdateText));
        CheckUpdateCommand.NotifyCanExecuteChanged();
        OpenReleaseCommand.NotifyCanExecuteChanged();
    }

    /// <summary>修改目录列表并保存；当前目录因此变化时，通知各页面重新读取实例。</summary>
    private void ChangeFolders(Action<List<string>> change, string selectedPath)
    {
        var previousRoot = _settings.MinecraftRoot;
        change(_settings.MinecraftFolders);
        _settings.Save();
        LoadFolders(selectedPath);
        if (!PathComparer.Equals(_settings.MinecraftRoot, previousRoot))
            _services.NotifyInstancesChanged();
    }

    private void LoadFolders(string? selectedPath)
    {
        Folders.Clear();
        for (var i = 0; i < _settings.MinecraftFolders.Count; i++)
            Folders.Add(new SettingsFolderItem(_settings.MinecraftFolders[i], i == 0));

        SelectedFolder = selectedPath is null ? null : Folders.First(item => PathComparer.Equals(item.Path, selectedPath));
        RemoveFolderCommand.NotifyCanExecuteChanged();
    }

    private void ShowJavaOptions(IReadOnlyList<JavaRuntime> runtimes)
    {
        var saved = _settings.DefaultJavaExecutable;
        var options = new List<SettingsJavaOption> { new(null, SettingsStrings.JavaAuto) };
        options.AddRange(runtimes
            .OrderByDescending(java => java.MajorVersion)
            .Select(java => new SettingsJavaOption(java.Executable, SettingsStrings.JavaOption(java))));

        // 之前选定的 Java 可能不在扫描结果里（例如已卸载），仍保留这一项，设置不会因扫描而丢失
        if (saved is not null && !options.Any(option => IsExecutable(option, saved)))
            options.Add(new SettingsJavaOption(saved, saved));

        Sync(() =>
        {
            JavaOptions = options;
            SelectedJava = saved is null ? options[0] : options.First(option => IsExecutable(option, saved));
        });
    }

    private static bool IsExecutable(SettingsJavaOption option, string path) =>
        option.Executable is { } executable && PathComparer.Equals(executable, path);

    private SettingsSourceOption FindSource() => Sources.First(option => option.Source == _settings.DownloadSource);

    /// <summary>以物理内存为上限，按步长向下取整。</summary>
    private static double GetMemoryCeilingMb()
    {
        var physicalMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024);
        return Math.Floor(physicalMb / MemoryStepMb) * MemoryStepMb;
    }

    /// <summary>把界面上的改动写回设置并保存。界面按设置刷新（<see cref="Sync"/>）时不会反过来保存，返回 false。</summary>
    private bool Persist(Action update)
    {
        if (_syncing)
            return false;

        update();
        _settings.Save();
        return true;
    }

    /// <summary>执行按设置刷新界面的操作，期间界面属性的变化不会写回设置。</summary>
    private void Sync(Action update)
    {
        var previous = _syncing;
        _syncing = true;
        try
        {
            update();
        }
        finally
        {
            _syncing = previous;
        }
    }
}
