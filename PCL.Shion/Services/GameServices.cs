using System;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Services;

/// <summary>
/// 全应用共享的服务：设置、下载引擎，以及实例列表变化的通知。
/// </summary>
public sealed class GameServices(AppSettings settings) : IDisposable
{
    public AppSettings Settings { get; } = settings;

    public DownloadEngine Engine { get; } = new(new DownloadOptions { MaxConcurrency = settings.MaxDownloadConcurrency });

    public DownloadSource Source => Settings.DownloadSource;

    /// <summary>实例被安装、删除或修改后触发，订阅者应重新扫描实例。</summary>
    public event Action? InstancesChanged;

    /// <summary>按当前下载源创建安装器；下载源在设置里改动后，需要重新创建。</summary>
    public GameInstaller CreateInstaller() => new(Engine, Source);

    public void NotifyInstancesChanged() => InstancesChanged?.Invoke();

    public void Dispose() => Engine.Dispose();
}
