using System;
using Avalonia.Threading;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Launch;
using PCL.Shion.UI;

namespace PCL.Shion;

/// <summary>
///     与 WPF 版 ModLaunch 的启动入口对应。实际的启动流程由启动页 <see cref="PageLaunchLeft" /> 完成，这里负责转发。
/// </summary>
public static class ModLaunch
{
    /// <summary>
    ///     启动选项，与 WPF 版 McLaunchOptions 中目前用到的字段对应。
    /// </summary>
    public sealed class McLaunchOptions
    {
        /// <summary>强行指定启动的 Minecraft 实例，为空时使用当前选中的实例。</summary>
        public GameInstance? instance { get; set; }

        /// <summary>启动后要直接进入的服务器地址。</summary>
        public string? ServerIp { get; set; }
    }

    /// <summary>
    ///     开始启动 Minecraft。应在 UI 线程调用。
    ///     启动页在调用方之后切换（例如 <c>Navigator.Go(PageType.Launch)</c>），所以这里把启动请求放到 UI 队列末尾执行。
    /// </summary>
    public static void McLaunchStart(McLaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.instance is { } instance)
            PageLaunchLeft.SelectInstance(instance);

        if (!string.IsNullOrWhiteSpace(options.ServerIp))
        {
            // TODO(shion): 启动后直接进入服务器需要把地址传给启动流程，PageLaunchLeft 目前只按实例设置里的快速进入项启动，这里暂未支持。
            HintService.Hint("该功能暂未在 Shion 中提供：启动后不会直接进入服务器，将按实例设置启动", HintType.Warning);
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (Navigator.PageLeft is PageLaunchLeft launch)
                launch.LaunchButtonClick();
            else
                HintService.Hint("无法启动游戏：启动页面不可用", HintType.Error);
        });
    }
}
