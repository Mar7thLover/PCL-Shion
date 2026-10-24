using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Logging;
using PCL.Shion.Core.Instances;
using PCL.Shion.Navigation;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Instance.Saves;

/// <summary>
///     存档相关页面共用的辅助方法。
/// </summary>
internal static class SavesSupport
{
    /// <summary>
    ///     记录异常；若给出提示文本，同时以错误提示告知用户。
    /// </summary>
    public static void LogFailure(Exception ex, string message, string? hint = null)
    {
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);
        if (hint is not null)
            HintService.Hint(hint, HintType.Error);
    }

    /// <summary>
    ///     递归复制目录，目标已存在的文件会被覆盖。
    /// </summary>
    public static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    /// <summary>
    ///     字符串的 MD5（小写十六进制），用于生成缓存文件名。
    /// </summary>
    public static string GetStringMd5(string text) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>
    ///     以指定存档启动游戏：把快捷启动目标写入实例设置，并跳转到启动页。
    /// </summary>
    public static void QuickPlay(string worldName)
    {
        var instance = PageInstanceLeft.McInstance;
        if (instance is null)
            return;

        var services = App.Services;
        var root = services.Settings.MinecraftRoot;
        var settings = InstanceStore.Load(root, instance.Name);
        settings.QuickPlay = new QuickPlayTarget(QuickPlayKind.Singleplayer, worldName);
        InstanceStore.Save(root, instance.Name, settings);

        services.Settings.SelectedInstance = instance.Name;
        services.Settings.Save();
        Navigator.Go(PageType.Launch);
    }
}
