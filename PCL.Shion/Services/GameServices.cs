using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Logging;
using Avalonia.Threading;
using PCL.Shion.Core.Accounts;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Launch;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Platform;
using PCL.Shion.Core.Sources;
using PCL.Shion.Core.Versions;
using PCL.Shion.Localization;
using PCL.Shion.Pages.Launch;
using PCL.Shion.UI;

namespace PCL.Shion.Services;

/// <summary>
/// 全应用共享的服务：设置、下载引擎、正在运行的游戏、通知、更新检查，以及实例列表变化与页面跳转的通知。
/// </summary>
public sealed class GameServices : IDisposable
{
    private const string UnsupportedHint = "该功能暂未在 Shion 中提供";

    private readonly Lock _engineLock = new();
    private readonly List<DownloadEngine> _retiredEngines = [];
    private readonly Lock _taskLock = new();
    private readonly HashSet<string> _runningTasks = [];
    private DownloadEngine _engine;
    private int _engineConcurrency;

    public GameServices(AppSettings settings)
    {
        Settings = settings;
        _engine = CreateEngine();
        Updates = new UpdateService(settings, () => Engine.Http);
    }

    public AppSettings Settings { get; }

    /// <summary>
    /// 下载引擎。并发数在设置里改动后，下一次取用时换成新的引擎；正在使用旧引擎的下载照常完成。
    /// </summary>
    public DownloadEngine Engine
    {
        get
        {
            lock (_engineLock)
            {
                if (_engineConcurrency != Settings.MaxDownloadConcurrency)
                {
                    _retiredEngines.Add(_engine);
                    _engine = CreateEngine();
                }

                return _engine;
            }
        }
    }

    public DownloadSource Source => Settings.DownloadSource;

    /// <summary>正在运行的游戏。</summary>
    public GameRunner Runner { get; } = new();

    public UpdateService Updates { get; }

    /// <summary>实例被安装、删除或修改后触发，订阅者应重新扫描实例。</summary>
    public event Action? InstancesChanged;

    /// <summary>请求打开某个实例的详情页，参数为实例名。</summary>
    public event Action<string>? OpenInstanceRequested;

    /// <summary>按当前下载源创建安装器；下载源在设置里改动后，需要重新创建。</summary>
    public GameInstaller CreateInstaller() => new(Engine, Source);

    public void NotifyInstancesChanged() => InstancesChanged?.Invoke();

    public void RequestOpenInstance(string name) => OpenInstanceRequested?.Invoke(name);

    /// <summary>
    /// 按设置显示系统通知、播放完成提示音。两项设置各自独立，可在任意线程调用。
    /// </summary>
    public void Notify(string title, string message)
    {
        if (Settings.NotificationsEnabled)
            _ = Task.Run(() => Notifier.Notify(title, message));

        if (Settings.CompletionSound)
            SystemBeep.Play();
    }

    /// <summary>
    /// 是否可以新建其它档案，与 WPF 的 <c>ProfileUi.CanCreateOtherProfile</c> 相同。
    /// </summary>
    public bool CanCreateOtherProfile() =>
        ProfileService.HasMicrosoftProfile || ProfileService.Profiles.Count > 0 ||
        !NetworkInterface.GetIsNetworkAvailable();

    /// <summary>
    /// 补全实例文件（对应 WPF 的 <c>ModDownload.DlClientFix</c>），在后台执行并显示结果。同名任务正在进行时只给出提示。
    /// </summary>
    public void StartRepair(GameInstance instance, string taskName)
    {
        if (!TryBeginTask(taskName))
        {
            PostHint(Lang.Text("Instance.Overall.Repair.Processing"), HintType.Error);
            return;
        }

        var root = Settings.MinecraftRoot;
        _ = Task.Run(async () =>
        {
            try
            {
                await CreateInstaller().CompleteAsync(root, instance.Name, null, CancellationToken.None);
                PostHint("实例文件补全完成", HintType.Success);
            }
            catch (Exception ex)
            {
                LogError(ex, "[Instance] 补全实例文件失败");
                PostHint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
            }
            finally
            {
                EndTask(taskName);
            }
        });
    }

    /// <summary>
    /// 重装实例（对应 WPF 的重置流程）：先把版本 JSON 与客户端 jar 备份到 <c>PCLInstallBackups</c>，再按原版本重新安装。
    /// </summary>
    public void RequestReinstall(GameInstance instance)
    {
        if (instance.MinecraftVersion is not { } vanillaVersion)
        {
            PostHint(UnsupportedHint, HintType.Error);
            return;
        }

        // TODO(shion): 模组加载器（Forge、NeoForge、Fabric 等）的安装步骤尚未在 PCL.Shion.Core 中提供，有加载器的实例暂不重装，以免丢失加载器。
        if (instance.Loader is not null)
        {
            PostHint(UnsupportedHint, HintType.Error);
            return;
        }

        var root = Settings.MinecraftRoot;
        _ = Task.Run(async () =>
        {
            try
            {
                BackupInstanceFiles(instance);
                var manifest = await VersionManifestClient.FetchAsync(Engine.Http, Source, CancellationToken.None);
                var remote = manifest.Versions.FirstOrDefault(version => version.Id == vanillaVersion)
                             ?? throw new InvalidOperationException("版本清单中找不到 " + vanillaVersion);
                await CreateInstaller().InstallVanillaAsync(root, remote, instance.Name, null, CancellationToken.None);
                NotifyInstancesChanged();
                PostHint("实例重装完成", HintType.Success);
            }
            catch (Exception ex)
            {
                LogError(ex, "[Instance] 重装实例失败");
                PostHint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
            }
        });
    }

    /// <summary>
    /// 以测试模式启动实例（对应 WPF 的 <c>McLaunchStart</c> 的 IsTest 选项）。
    /// </summary>
    public void StartTestGame(GameInstance instance)
    {
        // TODO(shion): 启动需要的账号、Java 选择与启动计划组装仍在 PageLaunchLeft 中，尚未抽到 Services，测试游戏暂不可用。
        PostHint(UnsupportedHint, HintType.Error);
    }

    /// <summary>
    /// 把 <paramref name="filePath"/> 中的文件合并进实例的客户端 jar（对应 WPF 的 <c>GameCore.AddToCore</c>），在后台执行。
    /// </summary>
    public void PatchCore(GameInstance instance, string filePath)
    {
        var corePath = Path.Combine(instance.Directory, instance.Name + ".jar");
        _ = Task.Run(() =>
        {
            try
            {
                AddToCore(corePath, filePath);
                PostHint(Lang.Text("Instance.Overall.Patch.Success"), HintType.Success);
            }
            catch (Exception ex)
            {
                LogError(ex, "[Instance] 修补核心失败");
                PostHint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
            }
        });
    }

    /// <summary>
    /// 请求在启动页新建第三方验证档案，并把服务器地址预先填入登录页（对应 WPF 的 <c>PageLoginAuth.draggedAuthServer</c>）。
    /// 调用方需要先切换到启动页。
    /// </summary>
    public void RequestNewAuthProfile(string server)
    {
        PageLoginAuth.draggedAuthServerOAuthSupported = null;
        PageLoginAuth.draggedAuthServer = server;
        Dispatcher.UIThread.Post(() => ProfileUi.RefreshLaunchPage(LoginType.Authlib));
    }

    public void Dispose()
    {
        lock (_engineLock)
        {
            _engine.Dispose();
            foreach (var engine in _retiredEngines)
                engine.Dispose();
        }
    }

    private DownloadEngine CreateEngine()
    {
        _engineConcurrency = Settings.MaxDownloadConcurrency;
        return new DownloadEngine(new DownloadOptions { MaxConcurrency = _engineConcurrency });
    }

    private bool TryBeginTask(string name)
    {
        lock (_taskLock)
            return _runningTasks.Add(name);
    }

    private void EndTask(string name)
    {
        lock (_taskLock)
            _runningTasks.Remove(name);
    }

    private void BackupInstanceFiles(GameInstance instance)
    {
        var backupDirectory = Path.Combine(instance.Directory, "PCLInstallBackups");
        Directory.CreateDirectory(backupDirectory);
        foreach (var extension in new[] { ".json", ".jar" })
        {
            var source = Path.Combine(instance.Directory, instance.Name + extension);
            if (File.Exists(source))
                File.Copy(source, Path.Combine(backupDirectory, instance.Name + extension), true);
        }
    }

    /// <summary>
    /// 与 PCL.Core 的 <c>GameCore.AddToCore</c> 相同：把 <paramref name="jarPath"/> 中的条目复制进核心 jar，并删除签名目录。
    /// 对于不以 .jar 结尾的文件（如 Better Than Wolves 的 .zip 模组），只复制名称包含 MINECRAFT-JAR 的条目。
    /// </summary>
    private static void AddToCore(string corePath, string jarPath)
    {
        if (!File.Exists(corePath))
            throw new FileNotFoundException($"未找到指定文件：{corePath}");
        if (!File.Exists(jarPath))
            throw new FileNotFoundException($"未找到指定文件：{jarPath}");

        using var coreStream = new FileStream(corePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 16384, true);
        using var jarStream = new FileStream(jarPath, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, true);
        using var coreArchive = new ZipArchive(coreStream, ZipArchiveMode.Update);
        using var jarArchive = new ZipArchive(jarStream);

        var filter = jarPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ? "" : "MINECRAFT-JAR";
        foreach (var entry in jarArchive.Entries)
        {
            if (!entry.FullName.Contains(filter, StringComparison.Ordinal))
                continue;
            // 核心 jar 中已有同名条目时先删除，避免写出重复条目
            coreArchive.GetEntry(entry.FullName)?.Delete();
            using var coreEntryStream = coreArchive.CreateEntry(entry.FullName).Open();
            using var jarEntryStream = jarArchive.GetEntry(entry.FullName)?.Open();
            jarEntryStream?.CopyTo(coreEntryStream);
        }

        // 删除签名文件所在的目录，避免 Oracle JDK 加载时校验签名失败导致无法启动
        coreArchive.GetEntry("META-INF")?.Delete();
    }

    private static void PostHint(string text, HintType type) =>
        Dispatcher.UIThread.Post(() => HintService.Hint(text, type));

    private static void LogError(Exception ex, string message) =>
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);
}
