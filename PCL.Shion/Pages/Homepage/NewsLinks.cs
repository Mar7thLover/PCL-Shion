using System;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Homepage;

/// <summary>
/// 在浏览器中打开新闻与正文里的链接。
/// </summary>
internal static class NewsLinks
{
    /// <summary>
    /// 在默认浏览器中打开网址。打不开时把网址复制到剪贴板，并弹窗说明。
    /// </summary>
    public static void Open(string url)
    {
        try
        {
            SystemShell.OpenUrl(url);
        }
        catch (Exception)
        {
            _ = ShowOpenFailedAsync(url);
        }
    }

    private static async Task ShowOpenFailedAsync(string url)
    {
        try
        {
            if (MainWindow.Current?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(url);
        }
        catch (Exception)
        {
            // 剪贴板可能被其他程序占用；网址仍会显示在弹窗里
        }

        await MsgBox.ShowAsync(
            Lang.Text("SystemDialog.Browser.OpenFailed.Message", url),
            Lang.Text("SystemDialog.Browser.OpenFailed.Title"));
    }
}
