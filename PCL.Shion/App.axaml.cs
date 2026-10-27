using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Shion.Localization;
using PCL.Shion.Services;

namespace PCL.Shion;

public partial class App : Application
{
    /// <summary>
    ///     全应用共享的服务（设置、下载引擎、正在运行的游戏、更新检查）。在创建主窗口之前初始化。
    /// </summary>
    public static GameServices Services { get; private set; } = null!;

    internal static Func<AppSettings> SettingsLoader { get; set; } = AppSettings.Load;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 对应 WPF 的 Application_DispatcherUnhandledException：UI 线程上的异常只记录并提示，不让启动器直接退出
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                e.Handled = true;
                try
                {
                    ModBase.Log(e.Exception, Lang.Text("SystemDialog.Error.Unexpected.Message"), ModBase.LogLevel.Hint);
                }
                catch
                {
                    // 全局处理中不能再抛出异常
                }
            };
            Services = new GameServices(SettingsLoader());
            // 联机组件通过宿主接口读取设置、日志与语言
            LinkEnvironment.Initialize(Services.Settings);
            // 主题服务与设置页共用同一份设置，否则设置页的修改不会反映到主题上
            Themes.ThemeService.Settings = Services.Settings;
            // 对应 WPF 中在窗口创建前启动的主题服务：按设置计算浅色 / 深色配色资源
            Themes.ThemeService.Initialize();
            // 配色模式为“跟随系统”时，系统切换深浅色后同步刷新（对应 WPF FormMain 的 WndProc 处理）
            if (PlatformSettings is { } platformSettings)
                platformSettings.ColorValuesChanged += (_, _) =>
                    Dispatcher.UIThread.Post(Themes.ThemeService.RefreshColorMode);
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, _) =>
            {
                // 退出前关闭联机，避免 EasyTier 进程残留
                try
                {
                    Core.Link.Lobby.LobbyService.Stop();
                }
                catch (Exception ex)
                {
                    LogWrapper.Warn(ex, "[Link] 退出时关闭联机失败");
                }

                Services.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
