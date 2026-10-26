using System;
using System.Threading.Tasks;
using Avalonia;
using PCL.Shion.Services;

namespace PCL.Shion;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        using var log = DiagnosticLog.Initialize();
        UnhandledExceptionEventHandler fatal = (_, e) =>
            LogWrapper.Error(e.ExceptionObject as Exception, "[System] 未处理的进程异常");
        EventHandler<UnobservedTaskExceptionEventArgs> unobserved = (_, e) =>
        {
            LogWrapper.Error(e.Exception, "[System] 未观察的后台任务异常");
            e.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += fatal;
        TaskScheduler.UnobservedTaskException += unobserved;
        try
        {
            LogWrapper.Info($"[System] PCL Shion {AppInfo.Version}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
            return BuildAvaloniaApp(AppSettings.Load().SystemRenderBackend).StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "[System] 启动器无法继续运行");
            Console.Error.WriteLine("PCL Shion failed to start. Diagnostic log: " + DiagnosticLog.CurrentPath);
            return 1;
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= fatal;
            TaskScheduler.UnobservedTaskException -= unobserved;
        }
    }

    // 设计器通过无参数的重载创建预览，使用自动选择的渲染后端
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(RenderBackend.Auto);

    // 渲染后端要在平台初始化之前确定，所以在这里读取设置，而不是等 App 创建之后
    private static AppBuilder BuildAvaloniaApp(RenderBackend backend) =>
        AppBuilder.Configure<App>().UsePlatformDetect().UseRenderBackend(backend);
}
