using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Sources;
using PCL.Shion.Core.Versions;
using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

public sealed partial class DownloadViewModel : ObservableObject
{
    /// <summary>安装失败时最多列出多少个失败文件。</summary>
    private const int FailedFileLimit = 5;

    /// <summary>实例名会成为目录名，沿用 Windows 的限制，保证同一个 .minecraft 在各系统间通用。</summary>
    private static readonly SearchValues<char> InvalidNameChars = SearchValues.Create("\\/:*?\"<>|");

    private static readonly DownloadSourceOption[] Sources =
    [
        new(DownloadSource.Official, DownloadStrings.SourceOfficial),
        new(DownloadSource.Bmclapi, DownloadStrings.SourceBmclapi),
    ];

    private static readonly DownloadTypeOption[] Types =
    [
        new(DownloadStrings.FilterRelease, ["release"]),
        new(DownloadStrings.FilterSnapshot, ["snapshot"]),
        new(DownloadStrings.FilterOld, ["old_beta", "old_alpha"]),
    ];

    private readonly GameServices _services;

    private IReadOnlyList<DownloadVersionItem> _allVersions = [];
    private DownloadVersionItem? _namedVersion;
    private bool _loadingVersions;
    private CancellationTokenSource? _installCts;

    public DownloadViewModel(GameServices services)
    {
        _services = services;
        SelectedSource = Sources.First(option => option.Source == services.Settings.DownloadSource);
    }

    public IReadOnlyList<DownloadSourceOption> SourceOptions => Sources;

    public IReadOnlyList<DownloadTypeOption> TypeOptions => Types;

    [ObservableProperty]
    public partial DownloadSourceOption SelectedSource { get; set; } = Sources[0];

    [ObservableProperty]
    public partial DownloadTypeOption SelectedType { get; set; } = Types[0];

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>经类型与搜索词筛选后的版本。</summary>
    [ObservableProperty]
    public partial IReadOnlyList<DownloadVersionItem> VisibleVersions { get; set; } = [];

    /// <summary>版本列表上方的提示：读取中、读取失败或没有匹配项；列表正常时为空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVersionsHint))]
    public partial string? VersionsHint { get; set; }

    [ObservableProperty]
    public partial bool LoadFailed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial DownloadVersionItem? SelectedVersion { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial string InstanceName { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNameError))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial string? NameError { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsCancelling { get; set; }

    [ObservableProperty]
    public partial string StageText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsProgressIndeterminate { get; set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial string ProgressDetail { get; set; } = "";

    [ObservableProperty]
    public partial string SpeedText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    public partial string? ResultText { get; set; }

    [ObservableProperty]
    public partial bool ResultIsError { get; set; }

    public bool HasVersionsHint => VersionsHint is not null;

    public bool HasSelection => SelectedVersion is not null;

    public bool HasNameError => NameError is not null;

    public bool HasResult => ResultText is not null;

    /// <summary>页面第一次显示时调用：版本清单还没有取到、也不在读取时才去获取。</summary>
    public Task EnsureLoadedAsync() =>
        _allVersions.Count == 0 && !_loadingVersions ? LoadVersionsAsync() : Task.CompletedTask;

    partial void OnSelectedSourceChanged(DownloadSourceOption value)
    {
        if (value.Source == _services.Settings.DownloadSource)
            return;

        _services.Settings.DownloadSource = value.Source;
        _services.Settings.Save();
    }

    partial void OnSelectedTypeChanged(DownloadTypeOption value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedVersionChanged(DownloadVersionItem? value)
    {
        // 重建列表时选中项会短暂变空再还原，只有真正换了版本才重置实例名
        if (value is not null && !ReferenceEquals(value, _namedVersion))
        {
            _namedVersion = value;
            InstanceName = value.Id;
            ResultText = null;
        }

        NameError = ValidateName();
    }

    partial void OnInstanceNameChanged(string value) => NameError = ValidateName();

    [RelayCommand]
    private async Task LoadVersionsAsync()
    {
        _loadingVersions = true;
        LoadFailed = false;
        VersionsHint = DownloadStrings.VersionsLoading;
        try
        {
            var manifest = await VersionManifestClient.FetchAsync(_services.Engine.Http, _services.Source, CancellationToken.None);
            _allVersions = manifest.Versions.Select(version => new DownloadVersionItem(version)).ToList();
            ApplyFilter();
            SelectedVersion = VisibleVersions.FirstOrDefault(item => item.Id == manifest.LatestRelease);
        }
        catch (Exception e)
        {
            VersionsHint = DownloadStrings.VersionsLoadFailed(e.Message);
            LoadFailed = true;
        }
        finally
        {
            _loadingVersions = false;
        }
    }

    private void ApplyFilter()
    {
        if (_allVersions.Count == 0)
            return;

        var previous = SelectedVersion;
        var types = SelectedType.ManifestTypes;
        var keyword = SearchText.Trim();
        VisibleVersions = _allVersions
            .Where(item => types.Contains(item.Version.Type) && item.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();
        VersionsHint = VisibleVersions.Count == 0 ? DownloadStrings.VersionsEmpty : null;

        // 列表换了数据源，ListBox 会清掉选中项；仍在结果里的要还原
        SelectedVersion = previous is not null && VisibleVersions.Contains(previous) ? previous : null;
    }

    /// <summary>检查实例名，没有问题时返回 null。</summary>
    private string? ValidateName()
    {
        if (SelectedVersion is null)
            return null;

        var name = InstanceName.Trim();
        if (name.Length == 0)
            return DownloadStrings.NameEmpty;
        if (name.EndsWith('.') || name.AsSpan().ContainsAny(InvalidNameChars))
            return DownloadStrings.NameInvalid;
        return Directory.Exists(InstanceDirectory(name)) ? DownloadStrings.NameExists : null;
    }

    private string InstanceDirectory(string name) => Path.Combine(_services.Settings.MinecraftRoot, "versions", name);

    private bool CanInstall() => SelectedVersion is not null && NameError is null;

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        NameError = ValidateName();
        if (NameError is not null)
            return;

        var version = SelectedVersion!.Version;
        var name = InstanceName.Trim();

        var cts = _installCts = new CancellationTokenSource();
        var token = cts.Token;
        ResultText = null;
        ResetProgress(DownloadStrings.StageVersion);
        IsInstalling = true;
        try
        {
            var progress = new Progress<InstallProgress>(ShowProgress);
            await _services.CreateInstaller().InstallVanillaAsync(_services.Settings.MinecraftRoot, version, name, progress, token);

            _services.NotifyInstancesChanged();
            ShowResult(DownloadStrings.InstallSucceeded(name), isError: false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            RemoveInstanceDirectory(name);
            ShowResult(DownloadStrings.InstallCancelled, isError: false);
        }
        catch (InstallException e)
        {
            RemoveInstanceDirectory(name);
            ShowResult(DescribeFailure(e), isError: true);
        }
        catch (Exception e)
        {
            RemoveInstanceDirectory(name);
            ShowResult(DownloadStrings.InstallFailed(e.Message), isError: true);
        }
        finally
        {
            cts.Dispose();
            _installCts = null;
            IsCancelling = false;
            IsInstalling = false;
            NameError = ValidateName();
        }
    }

    private bool CanCancel() => IsInstalling && !IsCancelling;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        IsCancelling = true;
        ResetProgress(DownloadStrings.Cancelling);
        _installCts?.Cancel();
    }

    private void ResetProgress(string stage)
    {
        StageText = stage;
        IsProgressIndeterminate = true;
        ProgressPercent = 0;
        ProgressDetail = "";
        SpeedText = "";
    }

    private void ShowProgress(InstallProgress progress)
    {
        if (IsCancelling)
            return;

        var stage = progress.Stage switch
        {
            "version" => DownloadStrings.StageVersion,
            "files" => DownloadStrings.StageFiles,
            "assets" => DownloadStrings.StageAssets,
            _ => progress.Stage,
        };

        if (progress.Download is not { } download)
        {
            ResetProgress(stage);
            return;
        }

        StageText = stage;
        IsProgressIndeterminate = download.TotalBytes <= 0 && download.TotalFiles <= 0;
        ProgressPercent = Fraction(download) * 100;
        ProgressDetail = download.TotalBytes > 0
            ? $"{DownloadStrings.ProgressFiles(download.CompletedFiles, download.TotalFiles)} · {ByteFormatter.Format(download.DownloadedBytes)} / {ByteFormatter.Format(download.TotalBytes)}"
            : DownloadStrings.ProgressFiles(download.CompletedFiles, download.TotalFiles);
        SpeedText = DownloadStrings.ProgressSpeed(ByteFormatter.Format(download.BytesPerSecond));
    }

    /// <summary>优先按字节算进度；没有文件大小时按文件数。</summary>
    private static double Fraction(DownloadProgress progress)
    {
        if (progress.TotalBytes > 0)
            return (double)progress.DownloadedBytes / progress.TotalBytes;
        return progress.TotalFiles > 0 ? (double)progress.CompletedFiles / progress.TotalFiles : 0;
    }

    private void ShowResult(string text, bool isError)
    {
        ResultText = text;
        ResultIsError = isError;
    }

    private string DescribeFailure(InstallException e)
    {
        var root = _services.Settings.MinecraftRoot;
        var lines = new List<string> { DownloadStrings.InstallFailedFiles(e.FailedFiles.Count) };
        lines.AddRange(e.FailedFiles.Take(FailedFileLimit).Select(item => $"  {Path.GetRelativePath(root, item.TargetPath)}"));
        if (e.FailedFiles.Count > FailedFileLimit)
            lines.Add($"  {DownloadStrings.InstallFailedMore(e.FailedFiles.Count - FailedFileLimit)}");
        return string.Join('\n', lines);
    }

    /// <summary>取消或失败后清掉这次新建的实例目录，避免留下装到一半的实例并占着名称。</summary>
    private void RemoveInstanceDirectory(string name)
    {
        try
        {
            Directory.Delete(InstanceDirectory(name), recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 清理只是善后，失败不应盖住真正的错误信息
        }
    }
}
