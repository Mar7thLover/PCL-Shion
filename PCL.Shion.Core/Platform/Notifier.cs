using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PCL.Shion.Core.Platform;

/// <summary>
/// 显示系统桌面通知。直接调用各系统自带的命令行工具，不引入额外的包。
/// </summary>
public static class Notifier
{
    private const string AppName = "PCL Shion";

    private const string TitleVariable = "PCL_SHION_NOTIFY_TITLE";
    private const string MessageVariable = "PCL_SHION_NOTIFY_MESSAGE";

    // 标题与内容通过环境变量交给脚本，避免命令行转义；
    // 使用 Windows PowerShell 已注册的 AppUserModelID，没有注册过的标识符发出的 Toast 不会显示
    private const string ToastScript = """
        [void][Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
        $xml = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
        $text = $xml.GetElementsByTagName('text')
        [void]$text.Item(0).AppendChild($xml.CreateTextNode([string]$env:PCL_SHION_NOTIFY_TITLE))
        [void]$text.Item(1).AppendChild($xml.CreateTextNode([string]$env:PCL_SHION_NOTIFY_MESSAGE))
        $toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
        $appId = '{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe'
        [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($appId).Show($toast)
        """;

    /// <summary>
    /// 显示一条通知并立即返回。通知工具不存在或执行失败时静默放弃，不抛出异常。
    /// </summary>
    public static void Notify(string title, string message)
    {
        try
        {
            Process.Start(CreateStartInfo(title, message))?.Dispose();
        }
        catch (Win32Exception)
        {
            // 系统没有可用的通知工具
        }
    }

    private static ProcessStartInfo CreateStartInfo(string title, string message)
    {
        return PlatformInfo.Os switch
        {
            OsKind.Windows => CreateToastStartInfo(title, message),
            OsKind.MacOs => new ProcessStartInfo("osascript")
            {
                ArgumentList = { "-e", $"display notification \"{EscapeAppleScript(message)}\" with title \"{EscapeAppleScript(title)}\"" }
            },
            _ => new ProcessStartInfo("notify-send") { ArgumentList = { "-a", AppName, "--", title, message } }
        };
    }

    private static ProcessStartInfo CreateToastStartInfo(string title, string message)
    {
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            CreateNoWindow = true,
            ArgumentList = { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(ToastScript)) }
        };
        startInfo.Environment[TitleVariable] = title;
        startInfo.Environment[MessageVariable] = message;
        return startInfo;
    }

    private static string EscapeAppleScript(string text)
    {
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
