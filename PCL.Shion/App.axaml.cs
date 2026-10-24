using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PCL.Shion.Services;

namespace PCL.Shion;

public partial class App : Application
{
    /// <summary>
    ///     全应用共享的服务（设置、下载引擎、正在运行的游戏、更新检查）。在创建主窗口之前初始化。
    /// </summary>
    public static GameServices Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services = new GameServices(AppSettings.Load());
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, _) => Services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
