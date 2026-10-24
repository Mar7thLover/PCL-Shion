using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Java;
using PCL.Shion.Core.Java.Runtime;
using PCL.Shion.Core.Loaders;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Versions;

namespace PCL.Shion.ViewModels;

// 下载页中与加载器有关的部分：选择加载器及其版本，以及先装原版再装加载器的流程
public sealed partial class DownloadViewModel
{
    private static readonly DownloadLoaderOption[] Loaders =
    [
        new(null, DownloadStrings.LoaderNone),
        new(LoaderKind.Fabric, nameof(LoaderKind.Fabric)),
        new(LoaderKind.Quilt, nameof(LoaderKind.Quilt)),
        new(LoaderKind.Forge, nameof(LoaderKind.Forge)),
        new(LoaderKind.NeoForge, nameof(LoaderKind.NeoForge)),
    ];

    private CancellationTokenSource? _loaderCts;

    public IReadOnlyList<DownloadLoaderOption> LoaderOptions => Loaders;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoader))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial DownloadLoaderOption SelectedLoader { get; set; } = Loaders[0];

    /// <summary>所选加载器适用于当前原版的版本，较新的在前。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoaderVersions))]
    public partial IReadOnlyList<DownloadLoaderVersionItem> LoaderVersions { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial DownloadLoaderVersionItem? SelectedLoaderVersion { get; set; }

    /// <summary>加载器版本框下方的提示：读取中、读取失败或没有可用版本；列表正常时为空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoaderHint))]
    public partial string? LoaderHint { get; set; }

    [ObservableProperty]
    public partial bool LoaderLoadFailed { get; set; }

    public bool HasLoader => SelectedLoader.Kind is not null;

    public bool HasLoaderVersions => LoaderVersions.Count > 0;

    public bool HasLoaderHint => LoaderHint is not null;

    partial void OnSelectedLoaderChanged(DownloadLoaderOption value) => _ = LoadLoaderVersionsAsync();

    partial void OnSelectedLoaderVersionChanged(DownloadLoaderVersionItem? value)
    {
        UpdateDefaultName();
        NameError = ValidateName();
    }

    [RelayCommand]
    private async Task LoadLoaderVersionsAsync()
    {
        _loaderCts?.Cancel();
        _loaderCts?.Dispose();
        _loaderCts = null;
        LoaderVersions = [];
        SelectedLoaderVersion = null;
        LoaderLoadFailed = false;

        if (SelectedLoader.Kind is not { } kind || SelectedVersion is not { } selected)
        {
            LoaderHint = null;
            return;
        }

        var cts = _loaderCts = new CancellationTokenSource();
        var loader = SelectedLoader.Label;
        LoaderHint = DownloadStrings.LoaderVersionsLoading(loader);
        try
        {
            var versions = await LoaderCatalog.GetVersionsAsync(kind, selected.Id, _services.Engine.Http, _services.Source, cts.Token);
            if (cts.IsCancellationRequested)
                return;

            LoaderVersions = versions.Select(version => new DownloadLoaderVersionItem(version)).ToList();
            LoaderHint = LoaderVersions.Count == 0 ? DownloadStrings.LoaderVersionsEmpty(loader, selected.Id) : null;
            SelectedLoaderVersion = LoaderVersions.FirstOrDefault(item => item.Version.Stable) ?? LoaderVersions.FirstOrDefault();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // 已被更新的请求取代，界面由它负责
        }
        catch (Exception e)
        {
            LoaderHint = DownloadStrings.LoaderVersionsLoadFailed(loader, e.Message);
            LoaderLoadFailed = true;
        }
    }

    /// <summary>默认实例名随所选加载器版本变化；用户改过的名称保持不变。</summary>
    private void UpdateDefaultName()
    {
        if (SelectedVersion is not { } version)
            return;

        var name = SelectedLoaderVersion?.DefaultInstanceName ?? version.Id;
        if (InstanceName == _defaultName)
            InstanceName = name;
        _defaultName = name;
    }

    /// <summary>先装原版再装加载器。</summary>
    private async Task InstallWithLoaderAsync(
        RemoteVersion vanilla,
        LoaderVersion loader,
        string name,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        await EnsureVanillaAsync(vanilla, progress, cancellationToken);

        // 只有 Forge 与 NeoForge 的安装处理器需要运行 Java
        var java = loader.Kind is LoaderKind.Forge or LoaderKind.NeoForge
            ? await ResolveJavaAsync(vanilla.Id, progress, cancellationToken)
            : "";

        var stage = DownloadStrings.StageLoader($"{loader.Kind} {loader.Version}");
        BeginStage(stage);
        await new LoaderInstaller(_services.Engine, _services.Source).InstallAsync(
            _services.Settings.MinecraftRoot, loader, name, java,
            new Progress<InstallProgress>(report => ShowProgress(report, stage)), cancellationToken);
    }

    /// <summary>
    /// 以版本号为名装好原版：本机已有就只补全文件。原版没装成时清掉这次新建的目录；
    /// 装成之后加载器才失败的，原版保留，重试时不必再下载。
    /// </summary>
    private async Task EnsureVanillaAsync(RemoteVersion vanilla, IProgress<InstallProgress> progress, CancellationToken cancellationToken)
    {
        var root = _services.Settings.MinecraftRoot;
        var installer = _services.CreateInstaller();
        var directory = InstanceDirectory(vanilla.Id);
        if (File.Exists(Path.Combine(directory, $"{vanilla.Id}.json")))
        {
            var result = await installer.CompleteAsync(root, vanilla.Id, progress, cancellationToken);
            if (!result.Succeeded)
                throw new InstallException(result);
            return;
        }

        var isNew = !Directory.Exists(directory);
        try
        {
            await installer.InstallVanillaAsync(root, vanilla, vanilla.Id, progress, cancellationToken);
        }
        catch
        {
            if (isNew)
                RemoveInstanceDirectory(vanilla.Id);
            throw;
        }
    }

    /// <summary>按原版要求的主版本挑选本机 Java，没有合适的就下载 Mojang 官方运行时。</summary>
    private async Task<string> ResolveJavaAsync(string vanillaName, IProgress<InstallProgress> progress, CancellationToken cancellationToken)
    {
        var root = _services.Settings.MinecraftRoot;
        BeginStage(DownloadStrings.StageJavaSearch);
        var resolved = await Task.Run(() => VersionResolver.Resolve(root, vanillaName), cancellationToken);
        var installed = await Task.Run(() => JavaLocator.FindAll(), cancellationToken);
        if (JavaLocator.Select(installed, resolved.JavaMajorVersion) is { } java)
            return java.Executable;

        if (resolved.JavaComponent is not { } component || JavaRuntimeInstaller.GetPlatformKey() is null)
            throw new InvalidOperationException(DownloadStrings.JavaUnavailable);

        var runtime = await JavaRuntimeInstaller.InstallAsync(root, component, _services.Engine, _services.Source, progress, cancellationToken);
        return runtime.Executable;
    }
}
