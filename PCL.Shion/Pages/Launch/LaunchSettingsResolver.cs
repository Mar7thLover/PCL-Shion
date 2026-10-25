using System.Collections.Generic;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Pages.Instance;
using PCL.Shion.Services;

namespace PCL.Shion.Pages.Launch;

/// <summary>
///     把设置页里的启动设置合并进本次启动使用的实例设置。
///     实例设置页与全局启动设置页按 WPF 的配置键名保存（<see cref="AppSettings.Config" />），
///     启动核心读取的是 <see cref="InstanceStore" />，两边需要在启动前合并。规则与 WPF 的 ModLaunch 相同：
///     实例里填写了的项优先，留空时沿用全局设置。
/// </summary>
internal static class LaunchSettingsResolver
{
    /// <summary>
    ///     合并后的设置只用于本次启动，不会写回 <see cref="InstanceStore" />。
    /// </summary>
    /// <param name="settings">刚从 <see cref="InstanceStore" /> 读出的实例设置，会被直接修改。</param>
    /// <param name="instance">要启动的实例。</param>
    /// <param name="appSettings">启动器设置。</param>
    /// <param name="oneShotQuickPlay">仅本次启动进入的存档或服务器，例如存档页的「直接进入」；优先于实例设置的进入服务器。</param>
    public static void Apply(InstanceSettings settings, GameInstance instance, AppSettings appSettings,
        QuickPlayTarget? oneShotQuickPlay)
    {
        var path = instance.PathInstance;
        var config = appSettings.Config.Instance;

        settings.ExtraJvmArguments = FirstFilled(config.JvmArgs[path], settings.ExtraJvmArguments, appSettings.LaunchAdvanceJvm);
        settings.ExtraGameArguments = FirstFilled(config.GameArgs[path], settings.ExtraGameArguments, appSettings.LaunchAdvanceGame);
        settings.PreLaunchCommand = FirstFilled(config.PreLaunchCommand[path], settings.PreLaunchCommand, appSettings.LaunchAdvanceRun);
        settings.PostExitCommand = FirstFilled(config.PostExitCommand[path], settings.PostExitCommand);
        settings.WrapperCommand = FirstFilled(config.WrapperCommand[path], settings.WrapperCommand);
        foreach (var (name, value) in ParseEnvironment(config.EnvironmentVariables[path]))
            settings.EnvironmentVariables[name] = value;

        // 进入服务器只在本次没有指定目标时生效；之前版本由存档页写入的目标不再沿用
        var server = config.ServerToEnter[path];
        settings.QuickPlay = oneShotQuickPlay ?? (string.IsNullOrWhiteSpace(server)
            ? null
            : new QuickPlayTarget(QuickPlayKind.Multiplayer, server.Trim()));
    }

    /// <summary>
    ///     解析每行一个的 名称=值。空行、以 # 开头的行和没有等号的行会被忽略，名称两侧的空白会被去掉。
    /// </summary>
    internal static IEnumerable<(string Name, string Value)> ParseEnvironment(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.TrimStart().StartsWith('#'))
                continue;
            var equal = line.IndexOf('=');
            if (equal <= 0)
                continue;
            var name = line[..equal].Trim();
            if (name.Length > 0)
                yield return (name, line[(equal + 1)..]);
        }
    }

    private static string? FirstFilled(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
