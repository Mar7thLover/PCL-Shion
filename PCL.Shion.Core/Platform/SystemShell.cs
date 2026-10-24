using System.Diagnostics;

namespace PCL.Shion.Core.Platform;

/// <summary>
/// 请系统用默认程序打开文件夹或网址。
/// </summary>
public static class SystemShell
{
    /// <summary>
    /// 在系统文件管理器中打开文件夹。
    /// </summary>
    /// <exception cref="System.ComponentModel.Win32Exception">无法启动系统的打开命令。</exception>
    public static void OpenFolder(string path)
    {
        Open(path);
    }

    /// <summary>
    /// 在默认浏览器中打开网址。只接受 http 与 https，防止被用来启动本地文件或其他协议的处理程序。
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="url"/> 不是 http 或 https 网址。</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">无法启动系统的打开命令。</exception>
    public static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Only absolute http and https URLs can be opened.", nameof(url));

        Open(uri.AbsoluteUri);
    }

    private static void Open(string target)
    {
        var startInfo = PlatformInfo.Os switch
        {
            OsKind.Windows => new ProcessStartInfo(target) { UseShellExecute = true },
            OsKind.MacOs => new ProcessStartInfo("open") { ArgumentList = { target } },
            _ => new ProcessStartInfo("xdg-open") { ArgumentList = { target } }
        };
        Process.Start(startInfo)?.Dispose();
    }
}
