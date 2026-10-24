using System;
using System.Linq;
using System.Threading.Tasks;
using PCL.Shion.Pages.Setup;
using PCL.Shion.UI;

namespace PCL.Shion;

/// <summary>
///     与 WPF 版 PCL.Core.UI.SystemDialogs 对应的文件对话框。
///     Avalonia 的文件选择器（<c>TopLevel.StorageProvider</c>）只有异步接口，因此实际的选择逻辑放在 <see cref="SelectFileAsync" />
///     和 <see cref="SelectSaveFileAsync" />；同步签名只为保持与 WPF 调用处的形状一致，无法在 UI 线程上同步等待，目前只提示暂未提供。
///     文件过滤器沿用 WPF 的写法，例如 <c>常用图片文件|*.png;*.jpg</c>。取消时返回空字符串，与 WPF 相同。
/// </summary>
public static class SystemDialogs
{
    private const string DefaultSelectTitle = "选择文件";
    private const string DefaultSaveTitle = "选择保存位置";
    private const string DefaultFilterName = "所有文件";

    /// <summary>
    ///     异步选择一个已有文件，取消时返回空字符串。
    /// </summary>
    public static async Task<string> SelectFileAsync(string? fileFilter = null, string? title = null)
    {
        if (MainWindow.Current is not { } anchor)
            return "";
        var (filterName, patterns) = ParseFilter(fileFilter);
        return await SetupDialogs.SelectFileAsync(anchor, title ?? DefaultSelectTitle, filterName, patterns) ?? "";
    }

    /// <summary>
    ///     异步选择一个保存位置，取消时返回空字符串。
    /// </summary>
    public static async Task<string> SelectSaveFileAsync(string? title, string fileName, string? fileFilter = null)
    {
        if (MainWindow.Current is not { } anchor)
            return "";
        var (filterName, patterns) = ParseFilter(fileFilter);
        return await SetupDialogs.SelectSaveFileAsync(anchor, title ?? DefaultSaveTitle, fileName, filterName,
            patterns) ?? "";
    }

    /// <summary>
    ///     同步形状的选择文件。调用方应改用 <see cref="SelectFileAsync" />。
    /// </summary>
    public static string SelectFile(string? fileFilter = null, string? title = null, string? initialDirectory = null)
    {
        // TODO(shion): 调用方（实例概览页的图标、补丁文件选择）需要改成 await SelectFileAsync，这里无法同步等待 Avalonia 的文件选择器。
        HintService.Hint("该功能暂未在 Shion 中提供", HintType.Error);
        return "";
    }

    /// <summary>
    ///     同步形状的选择保存位置。调用方应改用 <see cref="SelectSaveFileAsync" />。
    /// </summary>
    public static string SelectSaveFile(string? title, string fileName, string? fileFilter = null,
        string? initialDirectory = null)
    {
        // TODO(shion): 调用方（导出启动脚本）需要改成 await SelectSaveFileAsync，这里无法同步等待 Avalonia 的文件选择器。
        HintService.Hint("该功能暂未在 Shion 中提供", HintType.Error);
        return "";
    }

    /// <summary>
    ///     解析 WPF 风格的过滤器字符串（<c>名称|*.a;*.b|名称2|*.c</c>）。第一组的名称作为显示名，所有扩展名合并为匹配模式。
    /// </summary>
    private static (string Name, string[] Patterns) ParseFilter(string? fileFilter)
    {
        var fallback = (DefaultFilterName, new[] { "*" });
        if (string.IsNullOrWhiteSpace(fileFilter))
            return fallback;

        var parts = fileFilter.Split('|');
        if (parts.Length < 2)
            return fallback;

        var patterns = parts.Where((_, index) => index % 2 == 1)
            .SelectMany(part => part.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct()
            .ToArray();
        return (parts[0], patterns.Length == 0 ? new[] { "*" } : patterns);
    }
}
