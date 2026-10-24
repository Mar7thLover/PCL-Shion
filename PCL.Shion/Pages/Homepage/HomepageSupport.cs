using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Shion.Controls;
using PCL.Shion.Localization;
using PCL.Shion.Services;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Homepage;

/// <summary>
///     首页自定义内容的辅助方法：文件读写、文本宏替换、XAML 加载、错误提示。
/// </summary>
internal static class HomepageSupport
{
    /// <summary>
    ///     对应 WPF 的 ModBase.modeDebug，由启动参数 <c>--debug</c> 开启。
    /// </summary>
    internal static bool ModeDebug { get; } =
        Environment.GetCommandLineArgs().Any(arg => arg.Equals("--debug", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     刷新按钮的标记，自定义主页中 Tag 为此值的 <see cref="MyIconButton" /> 点击后会强制刷新主页。
    /// </summary>
    internal const string RefreshTag = "PCL.Shion.HomepageRefresh";

    internal static string ExeDirectory => AppContext.BaseDirectory;

    internal static string TempDirectory => Path.Combine(AppContext.BaseDirectory, "PCL", "Temp");

    internal static string ReadFile(string path) => File.Exists(path) ? File.ReadAllText(path) : "";

    internal static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    internal static string FetchString(string url) =>
        NewsHttp.Client.GetStringAsync(url).GetAwaiter().GetResult();

    /// <summary>
    ///     替换文本中的宏，与 WPF 的 ModMain.ArgumentReplace 对应（不做转义）。
    /// </summary>
    internal static string ArgumentReplace(string text)
    {
        var now = DateTime.Now;
        return text
            .Replace("{pcl_version}", AppInfo.VersionText)
            .Replace("{path}", ExeDirectory.TrimEnd(Path.DirectorySeparatorChar))
            .Replace("{path_temp}", TempDirectory.TrimEnd(Path.DirectorySeparatorChar))
            .Replace("{date}", now.ToString("d", CultureInfo.CurrentCulture))
            .Replace("{time}", now.ToString("T", CultureInfo.CurrentCulture))
            .Replace("{minecraft}", App.Services.Settings.MinecraftRoot);
    }

    /// <summary>
    ///     去掉自定义内容中所有的 xmlns 声明，命名空间统一由 <see cref="WrapPanel" /> 提供。
    /// </summary>
    internal static string StripXmlns(string content)
    {
        while (content.Contains("xmlns"))
            content = Regex.Replace(content, "xmlns[^\"']*(\"|')[^\"']*(\"|')", "").Replace("xmlns", "");
        return content;
    }

    /// <summary>
    ///     把自定义内容包进一个带有统一命名空间的 StackPanel，以便解析为单一控件。
    /// </summary>
    internal static string WrapPanel(string content) =>
        "<StackPanel xmlns=\"https://github.com/avaloniaui\" " +
        "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
        "xmlns:sys=\"using:System\" " +
        "xmlns:local=\"using:PCL.Shion.Controls\" " +
        "xmlns:c=\"using:PCL.Shion.Controls\" " +
        "xmlns:loc=\"using:PCL.Shion.Localization\">" +
        content +
        "</StackPanel>";

    /// <summary>
    ///     运行时解析 XAML。WPF 版通过 XamlReader 实现，Avalonia 版暂未引入运行时 XAML 加载器（AvaloniaRuntimeXamlLoader）。
    ///     返回 null 时调用方会按 “加载失败” 处理，不会崩溃。
    /// </summary>
    // TODO(shion): 需要引入 Avalonia.Markup.Xaml.Loader 后才能解析自定义主页 / 帮助 XAML，目前只返回 null
    internal static object? LoadXaml(string xaml)
    {
        Log("[Page] 当前版本暂不支持运行时加载自定义 XAML，已跳过 " + xaml.Length + " 个字符的内容");
        return null;
    }

    /// <summary>
    ///     为解析出的自定义内容中的刷新按钮绑定点击事件。
    /// </summary>
    internal static void BindRefreshButtons(Control root, Action onRefresh)
    {
        foreach (var button in root.GetSelfAndVisualDescendants().OfType<MyIconButton>()
                     .Where(b => string.Equals(b.Tag?.ToString(), RefreshTag, StringComparison.Ordinal))
                     .ToList())
            button.Click += (_, _) => onRefresh();
    }

    /// <summary>
    ///     读取内置的 “你知道吗” 文本，按语言链查找，找不到返回 null。
    /// </summary>
    internal static string[]? LoadEmbeddedHints(string langCode)
    {
        foreach (var code in LocalizationService.LanguageChain(langCode))
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri($"avares://PCL.Shion/Assets/Hints/{code}.txt"));
                using var reader = new StreamReader(stream);
                var lines = reader.ReadToEnd()
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .ToArray();
                if (lines.Length > 0)
                    return lines;
            }
            catch (IOException)
            {
                // 该语言没有文本，继续尝试回退语言
            }
        }

        return null;
    }

    internal static void Log(string message) =>
        Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(null, "{Message}", message);

    internal static void Log(Exception ex, string message, LogEventLevel level = LogEventLevel.Error) =>
        Logger.TryGet(level, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);

    /// <summary>
    ///     记录错误并向用户提示：调试模式下弹出消息框，否则显示提示。
    /// </summary>
    internal static void ReportError(Exception ex, string message, string userSummary)
    {
        Log(ex, message);
        RunInUi(() =>
        {
            if (ModeDebug)
                _ = MsgBox.ShowAsync(userSummary);
            else
                Hint.Show(userSummary, HintType.Error);
        });
    }

    internal static void RunInUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    internal static void RunInUiWait(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Invoke(action);
    }
}
