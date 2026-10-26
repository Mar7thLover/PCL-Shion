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
    private readonly CancellationTokenSource _shutdown = new();
    private readonly CancellationToken _shutdownToken;
    private int _disposed;

    public GameServices(AppSettings settings)
    {
        Settings = settings;
        _shutdownToken = _shutdown.Token;
        _engine = CreateEngine();
        Updates = new UpdateService(settings, () => Engine.Http, _shutdownToken);
    }

    public AppSettings Settings { get; }

    /// <summary>启动器退出时取消仍在准备、联网或安装的任务，不终止已启动的游戏。</summary>
    public CancellationToken ShutdownToken => _shutdownToken;

    /// <summary>
    /// 下载引擎。并发数在设置里改动后，下一次取用时换成新的引擎；正在使用旧引擎的下载照常完成。
    /// </summary>
    public DownloadEngine Engine
    {
        get
        {
            lock (_engineLock)
            {
                ObjectDisposedException.ThrowIf(_disposed != 0, this);
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

    public void NotifyInstancesChanged()
    {
        if (InstancesChanged is not { } handlers) return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception ex) { LogError(ex, "[Instance] 实例列表订阅者失败"); }
        }
    }

    public void RequestOpenInstance(string name) => OpenInstanceRequested?.Invoke(name);

    /// <summary>
    /// 按设置显示系统通知、播放完成提示音。两项设置各自独立，可在任意线程调用。
    /// </summary>
    public void Notify(string title, string message)
    {
        if (_shutdownToken.IsCancellationRequested) return;
        if (Settings.NotificationsEnabled)
            _ = Task.Run(() =>
            {
                try { Notifier.Notify(title, message); }
                catch (Exception ex) { LogError(ex, "[Notify] 发送通知失败"); }
            });

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
                var result = await CreateInstaller().CompleteAsync(root, instance.Name, null, _shutdownToken);
                if (!result.Succeeded) throw new InstallException(result);
                PostHint("实例文件补全完成", HintType.Success);
            }
            catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested) { }
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

        var taskName = "Reinstall:" + instance.Directory;
        if (!TryBeginTask(taskName)) return;
        var root = Settings.MinecraftRoot;
        _ = Task.Run(async () =>
        {
            try
            {
                BackupInstanceFiles(instance);
                _shutdownToken.ThrowIfCancellationRequested();
                var manifest = await VersionManifestClient.FetchAsync(Engine.Http, Source, _shutdownToken);
                var remote = manifest.Versions.FirstOrDefault(version => version.Id == vanillaVersion)
                             ?? throw new InvalidOperationException("版本清单中找不到 " + vanillaVersion);
                await CreateInstaller().InstallVanillaAsync(root, remote, instance.Name, null, _shutdownToken);
                NotifyInstancesChanged();
                PostHint("实例重装完成", HintType.Success);
            }
            catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                LogError(ex, "[Instance] 重装实例失败");
                PostHint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
            }
            finally { EndTask(taskName); }
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
    public Task<bool> PatchCore(GameInstance instance, string filePath)
    {
        var corePath = Path.Combine(instance.Directory, instance.Name + ".jar");
        var taskName = "Patch:" + corePath;
        if (!TryBeginTask(taskName)) return Task.FromResult(false);
        return Task.Run(() =>
        {
            try
            {
                AddToCore(corePath, filePath, _shutdownToken);
                PostHint(Lang.Text("Instance.Overall.Patch.Success"), HintType.Success);
                return true;
            }
            catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested) { return false; }
            catch (Exception ex)
            {
                LogError(ex, "[Instance] 修补核心失败");
                PostHint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
                return false;
            }
            finally { EndTask(taskName); }
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
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _shutdown.Cancel(); }
        catch (AggregateException ex) { LogError(ex, "[System] 任务取消回调失败"); }
        lock (_engineLock)
        {
            _engine.Dispose();
            foreach (var engine in _retiredEngines)
                engine.Dispose();
            _retiredEngines.Clear();
        }
        _shutdown.Dispose();
    }

    private DownloadEngine CreateEngine()
    {
        _engineConcurrency = Settings.MaxDownloadConcurrency;
        return new DownloadEngine(new DownloadOptions { MaxConcurrency = _engineConcurrency });
    }

    private bool TryBeginTask(string name)
    {
        lock (_taskLock)
            return !_shutdownToken.IsCancellationRequested && _runningTasks.Add(name);
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
    internal static void AddToCore(string corePath, string jarPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(corePath))
            throw new FileNotFoundException($"未找到指定文件：{corePath}");
        if (!File.Exists(jarPath))
            throw new FileNotFoundException($"未找到指定文件：{jarPath}");

        var temporaryPath = corePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(corePath, temporaryPath);
            using (var coreStream = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                using (var coreArchive = new ZipArchive(coreStream, ZipArchiveMode.Update, leaveOpen: true))
                using (var jarStream = File.OpenRead(jarPath))
                using (var jarArchive = new ZipArchive(jarStream))
                {
                    var isJar = jarPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
                    var buffer = new byte[81920];
                    var patchedEntries = 0;
                    foreach (var entry in jarArchive.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (entry.Name.Length == 0) continue;
                        var entryName = entry.FullName;
                        if (!isJar)
                        {
                            const string marker = "MINECRAFT-JAR/";
                            var index = entryName.IndexOf(marker, StringComparison.Ordinal);
                            if (index < 0) continue;
                            entryName = entryName[(index + marker.Length)..];
                        }
                        if (IsJarSignature(entryName)) continue;
                        coreArchive.GetEntry(entryName)?.Delete();
                        using var target = coreArchive.CreateEntry(entryName).Open();
                        using var source = entry.Open();
                        int count;
                        while ((count = source.Read(buffer)) != 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            target.Write(buffer, 0, count);
                        }
                        patchedEntries++;
                    }

                    if (patchedEntries == 0)
                        throw new InvalidDataException("补丁中没有可应用的游戏文件。");

                    // 移除失效的签名，保留 META-INF/services 等真正需要的资源。
                    foreach (var entry in coreArchive.Entries.Where(entry => IsJarSignature(entry.FullName)).ToArray())
                        entry.Delete();
                }
                coreStream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, corePath, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool IsJarSignature(string name) => name.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)
        && new[] { ".SF", ".RSA", ".DSA", ".EC" }.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

    private static void PostHint(string text, HintType type) =>
        Dispatcher.UIThread.Post(() => HintService.Hint(text, type));

    private static void LogError(Exception ex, string message) =>
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);
}
