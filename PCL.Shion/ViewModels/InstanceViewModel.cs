using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Java;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Worlds;
using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

/// <summary>
/// 实例详情页：基本信息、实例设置、快速进入目标、重命名与删除，以及内嵌的 Mod 管理。
/// 构造后自动读取实例；离开本页（返回，或实例已被删除）时触发 <see cref="CloseRequested"/>，由外壳切换回上一页。
/// </summary>
public sealed partial class InstanceViewModel : ObservableObject
{
    private readonly GameServices _services;
    private GameInstance? _instance;
    private string _gameDirectory = "";
    private IReadOnlyList<JavaRuntime> _javaRuntimes = [];

    public InstanceViewModel(GameServices services, string instanceName)
    {
        _services = services;
        Name = instanceName;
        NewName = instanceName;
        MemoryPlaceholder = InstanceStrings.MemoryPlaceholder(services.Settings.MaxMemoryMb);
        Mods = new ModsViewModel(services);
        _ = InitializeAsync();
    }

    /// <summary>用户要离开本页时触发。</summary>
    public event Action? CloseRequested;

    /// <summary>实例名；重命名后更新。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial string Name { get; set; } = "";

    public ModsViewModel Mods { get; }

    /// <summary>读取中或读取失败时的提示；实例读取完成后为空，此时才显示页面内容。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadHint), nameof(IsLoaded))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RenameCommand), nameof(RequestDeleteCommand), nameof(ConfirmDeleteCommand))]
    public partial string? LoadHint { get; set; } = InstanceStrings.Loading;

    [ObservableProperty]
    public partial bool LoadFailed { get; set; }

    /// <summary>保存、重命名或删除进行期间为 true，此时不能再开始另一项操作。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RenameCommand), nameof(RequestDeleteCommand), nameof(ConfirmDeleteCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverviewSection), nameof(IsSettingsSection), nameof(IsModsSection))]
    public partial InstanceSection Section { get; set; }

    /// <summary>实例本身无法解析时的原因；解析正常时为空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstanceError))]
    public partial string? InstanceError { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<InstanceFact> Facts { get; set; } = [];

    /// <summary>游戏目录与实例目录不同（实例未隔离）时，才有必要单独提供“打开游戏目录”。</summary>
    [ObservableProperty]
    public partial bool ShowOpenGameFolder { get; set; }

    public bool HasLoadHint => LoadHint is not null;

    public bool IsLoaded => LoadHint is null;

    public bool HasInstanceError => InstanceError is not null;

    public bool IsOverviewSection => Section == InstanceSection.Overview;

    public bool IsSettingsSection => Section == InstanceSection.Settings;

    public bool IsModsSection => Section == InstanceSection.Mods;

    private string MinecraftRoot => _services.Settings.MinecraftRoot;

    [RelayCommand]
    private void Back() => CloseRequested?.Invoke();

    [RelayCommand]
    private void ShowSection(InstanceSection section) => Section = section;

    private async Task InitializeAsync()
    {
        await LoadAsync();
        if (IsLoaded)
            await ScanJavaAsync();
    }

    /// <summary>读取实例、实例设置与存档列表并刷新整个页面；重命名后也用它重新载入。</summary>
    private async Task LoadAsync()
    {
        var root = MinecraftRoot;
        var name = Name;
        InstanceData? data;
        try
        {
            data = await Task.Run(() => ReadInstance(root, name));
        }
        catch (Exception e)
        {
            ShowLoadFailure(InstanceStrings.LoadFailed(e.Message));
            return;
        }

        if (data is null)
            ShowLoadFailure(InstanceStrings.NotFound(name));
        else
            Apply(data);
    }

    private void ShowLoadFailure(string message)
    {
        LoadFailed = true;
        LoadHint = message;
    }

    private static InstanceData? ReadInstance(string root, string name)
    {
        var instance = InstanceScanner.Scan(root).FirstOrDefault(i => i.Name == name);
        if (instance is null)
            return null;

        var settings = InstanceStore.Load(root, name);
        var gameDirectory = InstanceManager.GetGameDirectory(root, instance, settings);
        var (worlds, servers) = ScanQuickPlayTargets(gameDirectory);
        return new InstanceData(instance, settings, gameDirectory, worlds, servers);
    }

    private static (IReadOnlyList<WorldInfo> Worlds, IReadOnlyList<ServerEntry> Servers) ScanQuickPlayTargets(string gameDirectory) =>
        (WorldScanner.ScanWorlds(gameDirectory), WorldScanner.ScanServers(gameDirectory));

    private void Apply(InstanceData data)
    {
        _instance = data.Instance;
        _gameDirectory = data.GameDirectory;
        InstanceError = data.Instance.Error;
        ShowFacts(data.Instance, data.Settings);
        ApplySettings(data.Instance, data.Settings);
        ApplyQuickPlay(data.Settings.QuickPlay, data.Worlds, data.Servers);
        LoadFailed = false;
        LoadHint = null;
        Mods.Load(_gameDirectory);
    }

    private void ShowFacts(GameInstance instance, InstanceSettings settings)
    {
        var isolated = settings.Isolated ?? instance.IsIsolated;
        ShowOpenGameFolder = !isolated;
        Facts =
        [
            new(InstanceStrings.FactVersion, instance.MinecraftVersion ?? InstanceStrings.UnknownVersion),
            new(InstanceStrings.FactLoader, instance.Loader is { } loader ? FormatLoader(loader) : InstanceStrings.LoaderNone),
            new(InstanceStrings.FactMode, isolated ? InstanceStrings.ModeIsolated : InstanceStrings.ModeShared),
            new(InstanceStrings.FactInstanceDirectory, instance.Directory),
            new(InstanceStrings.FactGameDirectory, _gameDirectory),
            new(InstanceStrings.FactPlayTime, FormatPlayTime(settings.TotalPlaySeconds)),
            new(InstanceStrings.FactLastPlayed, settings.LastPlayed is { } played
                ? played.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                : InstanceStrings.NeverPlayed),
        ];
    }

    private static string FormatLoader(string loader) => loader.ToLowerInvariant() switch
    {
        "forge" => "Forge",
        "neoforge" => "NeoForge",
        "fabric" => "Fabric",
        "quilt" => "Quilt",
        _ => loader,
    };

    private static string FormatPlayTime(long seconds)
    {
        if (seconds <= 0)
            return InstanceStrings.NeverPlayed;

        var minutes = seconds / 60;
        if (minutes < 1)
            return InstanceStrings.LessThanMinute;
        return minutes < 60 ? InstanceStrings.Minutes(minutes) : InstanceStrings.HoursMinutes(minutes / 60, minutes % 60);
    }

    /// <summary>搜索系统里的 Java，供“Java”选择框列出；扫描期间选择框里只有“自动选择”与已保存的那一项。</summary>
    private async Task ScanJavaAsync()
    {
        _javaRuntimes = await Task.Run(() => JavaLocator.FindAll());
        BuildJavaOptions(SelectedJava?.Executable);
    }

    /// <summary>重建 Java 选项并选中 <paramref name="selected"/>；它不在检测到的列表里时（如已被卸载的 Java）仍保留这一项。</summary>
    private void BuildJavaOptions(string? selected)
    {
        var options = new List<InstanceJavaOption> { new(null, InstanceStrings.JavaAuto) };
        options.AddRange(_javaRuntimes.Select(java => new InstanceJavaOption(
            java.Executable, InstanceStrings.JavaOption(java.Version, java.Is64Bit ? 64 : 32, java.Executable))));
        if (selected is not null && options.All(option => option.Executable != selected))
            options.Add(new InstanceJavaOption(selected, InstanceStrings.JavaUnlisted(selected)));

        JavaOptions = options;
        SelectedJava = options.First(option => option.Executable == selected);
    }

    /// <summary>一次读取到的实例信息。</summary>
    private sealed record InstanceData(
        GameInstance Instance,
        InstanceSettings Settings,
        string GameDirectory,
        IReadOnlyList<WorldInfo> Worlds,
        IReadOnlyList<ServerEntry> Servers);
}
