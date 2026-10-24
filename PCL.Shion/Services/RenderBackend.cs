using System;
using Avalonia;

namespace PCL.Shion.Services;

/// <summary>
/// 可选的渲染后端。只在 Windows 上可选，其他平台锁定为 <see cref="Auto"/>。
/// </summary>
public enum RenderBackend
{
    /// <summary>由 Avalonia 按平台自动选择。</summary>
    Auto = 0,

    /// <summary>ANGLE，把 OpenGL ES 转换为 DirectX 调用。Windows 上的默认后端。</summary>
    Angle = 1,

    /// <summary>原生 OpenGL（WGL）。</summary>
    OpenGl = 2,

    /// <summary>Vulkan。</summary>
    Vulkan = 3,

    /// <summary>软件渲染，不使用显卡加速。</summary>
    Software = 4
}

public static class RenderBackendExtensions
{
    /// <summary>当前平台能否自行选择渲染后端。</summary>
    public static bool IsSelectable => OperatingSystem.IsWindows();

    /// <summary>
    /// 按设置配置 Win32 平台的渲染方式。选中的后端初始化失败时退回软件渲染，避免窗口无法显示。
    /// 非 Windows 平台不做任何修改。
    /// </summary>
    public static AppBuilder UseRenderBackend(this AppBuilder builder, RenderBackend backend)
    {
        if (!IsSelectable || backend == RenderBackend.Auto)
            return builder;

        Win32RenderingMode[] modes = backend switch
        {
            RenderBackend.Angle => [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
            RenderBackend.OpenGl => [Win32RenderingMode.Wgl, Win32RenderingMode.Software],
            RenderBackend.Vulkan => [Win32RenderingMode.Vulkan, Win32RenderingMode.Software],
            _ => [Win32RenderingMode.Software]
        };
        return builder.With(new Win32PlatformOptions { RenderingMode = modes });
    }
}
