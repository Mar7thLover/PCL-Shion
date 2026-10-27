using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Core.Mods;
using PCL.Shion.Core.Platform;
using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

/// <summary>
/// 某个实例的 Mod 列表：搜索、启用或禁用、删除到回收站，并提示重复的 Mod 与缺少的前置。
/// </summary>
public sealed partial class ModsViewModel : ObservableObject
{
    private IReadOnlyList<ModItem> _all = [];
    private ModItem? _pendingDelete;
    private string? _gameDirectory;
    private string? _loadError;
    private bool _loading;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _scanCancellation;

    /// <summary>每次扫描加一，用来丢弃已被新一轮扫描取代的结果。</summary>
    private int _generation;

    /// <summary>通过共享服务读取并保存模组解析偏好。</summary>
    public ModsViewModel(GameServices services)
    {
        _settings = services.Settings;
        ParseMetadata = _settings.EnableModMetadataParsing;
    }

    [ObservableProperty]
    public partial bool ParseMetadata { get; set; } = true;

    partial void OnParseMetadataChanged(bool value)
    {
        if (_settings.EnableModMetadataParsing == value)
            return;
        _settings.EnableModMetadataParsing = value;
        _settings.Save();
        _ = RefreshAsync();
    }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>经搜索词筛选后的 Mod。</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ModItem> VisibleMods { get; set; } = [];

    /// <summary>列表上的提示：读取中、读取失败、没有 Mod 或没有匹配项；列表正常时为空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasListHint))]
    public partial string? ListHint { get; set; }

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIssues))]
    public partial IReadOnlyList<string> Issues { get; set; } = [];

    [ObservableProperty]
    public partial string IssuesTitle { get; set; } = "";

    /// <summary>启用、禁用或删除失败时的原因；下一次操作开始时清除。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorText { get; set; }

    public bool HasListHint => ListHint is not null;

    public bool HasIssues => Issues.Count > 0;

    public bool HasError => ErrorText is not null;

    /// <summary>指定实例的游戏目录并开始读取 Mod；扫描在后台线程进行，完成后更新列表。</summary>
    public void Load(string gameDirectory)
    {
        _gameDirectory = gameDirectory;
        ParseMetadata = _settings.EnableModMetadataParsing;
        _ = RefreshAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_gameDirectory is not { } directory)
            return;

        var generation = ++_generation;
        _scanCancellation?.Cancel();
        var cancellation = _scanCancellation = new CancellationTokenSource();
        var options = new ModScanOptions { ParseMetadata = ParseMetadata };
        _loading = true;
        _loadError = null;
        ErrorText = null;
        ApplyFilter();

        IReadOnlyList<LocalMod> scanned = [];
        string? failure = null;
        try
        {
            scanned = await Task.Run(() => ModScanner.Scan(directory, options, cancellation.Token), cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (generation == _generation)
            {
                _loading = false;
                ApplyFilter();
            }
            return;
        }
        catch (Exception e)
        {
            failure = e.Message;
        }
        finally
        {
            if (ReferenceEquals(_scanCancellation, cancellation))
                _scanCancellation = null;
            cancellation.Dispose();
        }

        if (generation != _generation)
            return;

        _all = scanned.Select(mod => new ModItem(mod, this)).ToList();
        _pendingDelete = null;
        _loading = false;
        _loadError = failure;
        var warningCount = scanned.Count(mod => mod.ReadWarning is not null);
        if (warningCount > 0)
            ErrorText = ModsStrings.ReadWarnings(warningCount);
        ApplyFilter();
        UpdateOverview();
    }

    /// <summary>离开页面时停止哈希和嵌套解析，并丢弃迟到的结果。</summary>
    public void CancelScan()
    {
        ++_generation;
        _scanCancellation?.Cancel();
        _loading = false;
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (_gameDirectory is not { } directory)
            return;

        var modsDirectory = ModScanner.GetModsDirectory(directory);
        Directory.CreateDirectory(modsDirectory);
        SystemShell.OpenFolder(modsDirectory);
    }

    /// <summary>按 <paramref name="item"/> 上开关的新状态改文件名；失败时把开关还原。</summary>
    internal async Task ApplyEnabledAsync(ModItem item)
    {
        var enabled = item.Enabled;
        ErrorText = null;
        item.IsBusy = true;
        try
        {
            item.Replace(await Task.Run(() => ModManager.SetEnabled(item.Mod, enabled)));
            UpdateOverview();
        }
        catch (Exception e)
        {
            item.RestoreEnabled();
            ErrorText = enabled
                ? ModsStrings.EnableFailed(item.DisplayName, e.Message)
                : ModsStrings.DisableFailed(item.DisplayName, e.Message);
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    /// <summary>让 <paramref name="item"/> 进入待确认删除的状态，同一时间只有一行在等待确认。</summary>
    internal void RequestDelete(ModItem item)
    {
        if (_pendingDelete is not null)
            _pendingDelete.IsDeletePending = false;

        _pendingDelete = item;
        item.IsDeletePending = true;
    }

    internal async Task DeleteAsync(ModItem item)
    {
        ErrorText = null;
        item.IsBusy = true;
        try
        {
            await Task.Run(() => ModManager.Delete(item.Mod));
            _all = _all.Where(other => other != item).ToList();
            ApplyFilter();
            UpdateOverview();
        }
        catch (Exception e)
        {
            item.IsDeletePending = false;
            ErrorText = ModsStrings.DeleteFailed(item.DisplayName, e.Message);
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var keyword = SearchText.Trim();
        VisibleMods = keyword.Length == 0 ? _all : _all.Where(item => item.Matches(keyword)).ToList();

        if (_loadError is not null)
            ListHint = ModsStrings.LoadFailed(_loadError);
        else if (_all.Count == 0)
            ListHint = _loading ? ModsStrings.Loading : ModsStrings.Empty;
        else
            ListHint = VisibleMods.Count == 0 ? ModsStrings.NoMatch : null;
    }

    /// <summary>按当前的 Mod 重新统计数量，并重新检查重复与缺少的前置。</summary>
    private void UpdateOverview()
    {
        var mods = _all.Select(item => item.Mod).ToList();
        SummaryText = ModsStrings.Summary(mods.Count, mods.Count(mod => mod.Enabled));
        Issues = ModAnalyzer.Analyze(mods).Select(Describe).ToList();
        IssuesTitle = ModsStrings.IssuesTitle(Issues.Count);
    }

    private static string Describe(ModIssue issue)
    {
        var name = issue.Mods.FirstOrDefault(mod => mod.Id == issue.ModId)?.Name ?? issue.ModId;
        return issue.Kind == ModIssueKind.Duplicate
            ? ModsStrings.IssueDuplicate(name, string.Join(ModsStrings.FileSeparator, issue.Mods.Select(mod => mod.FileName)))
            : ModsStrings.IssueMissingDependency(name, issue.MissingDependencyId ?? "");
    }
}
