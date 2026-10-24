using System;
using Avalonia;
using PCL.Shion.Services;

namespace PCL.Shion;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp(AppSettings.Load().SystemRenderBackend)
        .StartWithClassicDesktopLifetime(args);

    // 设计器通过无参数的重载创建预览，使用自动选择的渲染后端
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(RenderBackend.Auto);

    // 渲染后端要在平台初始化之前确定，所以在这里读取设置，而不是等 App 创建之后
    private static AppBuilder BuildAvaloniaApp(RenderBackend backend) =>
        AppBuilder.Configure<App>().UsePlatformDetect().UseRenderBackend(backend);
}
