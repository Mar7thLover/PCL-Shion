using System;
using System.Runtime.InteropServices;

namespace PCL.Shion.Services;

/// <summary>
/// 系统提示音。目前只有 Windows 支持，其他系统不播放。
/// </summary>
internal static class SystemBeep
{
    /// <summary>MB_ICONASTERISK：与 <c>SystemSounds.Asterisk</c> 相同的“信息”提示音。</summary>
    private const uint IconAsterisk = 0x40;

    /// <summary>播放提示音，立即返回。</summary>
    public static void Play()
    {
        if (OperatingSystem.IsWindows())
            MessageBeep(IconAsterisk);
    }

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint type);
}
