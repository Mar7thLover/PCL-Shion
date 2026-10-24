using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Worlds;

namespace PCL.Shion.ViewModels;

// 实例设置表单：Java、内存、窗口、额外参数、命令、环境变量与快速进入目标
public sealed partial class InstanceViewModel
{
    private const int MinMemoryMb = 512;
    private const int MaxMemoryMb = 131072;
    private const int MinWindowSize = 200;
    private const int MaxWindowSize = 16384;
    private const int MaxPort = 65535;

    private static readonly InstanceQuickPlayMode[] QuickPlayModeList =
    [
        new(null, InstanceStrings.QuickPlayOff),
        new(QuickPlayKind.Singleplayer, InstanceStrings.QuickPlayWorld),
        new(QuickPlayKind.Multiplayer, InstanceStrings.QuickPlayServer),
    ];

    public IReadOnlyList<InstanceQuickPlayMode> QuickPlayModes => QuickPlayModeList;

    /// <summary>内存输入框为空时显示的提示：沿用全局设置的值。</summary>
    public string MemoryPlaceholder { get; }

    [ObservableProperty]
    public partial IReadOnlyList<InstanceJavaOption> JavaOptions { get; set; } = [];

    [ObservableProperty]
    public partial InstanceJavaOption? SelectedJava { get; set; }

    /// <summary>最大内存（MB）；为空表示沿用全局设置。</summary>
    [ObservableProperty]
    public partial string MemoryText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsIsolated { get; set; }

    [ObservableProperty]
    public partial string WidthText { get; set; } = "";

    [ObservableProperty]
    public partial string HeightText { get; set; } = "";

    [ObservableProperty]
    public partial string ExtraJvmArguments { get; set; } = "";

    [ObservableProperty]
    public partial string ExtraGameArguments { get; set; } = "";

    [ObservableProperty]
    public partial string WrapperText { get; set; } = "";

    [ObservableProperty]
    public partial string PreLaunchText { get; set; } = "";

    [ObservableProperty]
    public partial string PostExitText { get; set; } = "";

    /// <summary>环境变量，每行一个 <c>名称=值</c>。</summary>
    [ObservableProperty]
    public partial string EnvironmentText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSingleplayerQuickPlay), nameof(IsMultiplayerQuickPlay))]
    public partial InstanceQuickPlayMode SelectedQuickPlayMode { get; set; } = QuickPlayModeList[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWorlds))]
    public partial IReadOnlyList<InstanceQuickPlayChoice> Worlds { get; set; } = [];

    [ObservableProperty]
    public partial InstanceQuickPlayChoice? SelectedWorld { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasServers))]
    public partial IReadOnlyList<InstanceQuickPlayChoice> Servers { get; set; } = [];

    [ObservableProperty]
    public partial InstanceQuickPlayChoice? SelectedServer { get; set; }

    /// <summary>要加入的服务器地址，可从服务器列表选取，也可手动输入。</summary>
    [ObservableProperty]
    public partial string ServerAddress { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSettingsMessage))]
    public partial string? SettingsMessage { get; set; }

    [ObservableProperty]
    public partial bool SettingsMessageIsError { get; set; }

    public bool IsSingleplayerQuickPlay => SelectedQuickPlayMode.Kind == QuickPlayKind.Singleplayer;

    public bool IsMultiplayerQuickPlay => SelectedQuickPlayMode.Kind == QuickPlayKind.Multiplayer;

    public bool HasWorlds => Worlds.Count > 0;

    public bool HasServers => Servers.Count > 0;

    public bool HasSettingsMessage => SettingsMessage is not null;

    // 从列表选了服务器就填入地址；地址恰好与列表中的某一项相同时，选择框也跟着选中它
    partial void OnSelectedServerChanged(InstanceQuickPlayChoice? value)
    {
        if (value is not null)
            ServerAddress = value.Value;
    }

    partial void OnServerAddressChanged(string value) => SelectedServer = FindServer(value);

    private InstanceQuickPlayChoice? FindServer(string address) =>
        Servers.FirstOrDefault(server => server.Value == address);

    private void ApplySettings(GameInstance instance, InstanceSettings settings)
    {
        BuildJavaOptions(settings.JavaExecutable);
        MemoryText = FormatNumber(settings.MaxMemoryMb);
        IsIsolated = settings.Isolated ?? instance.IsIsolated;
        WidthText = FormatNumber(settings.WindowWidth);
        HeightText = FormatNumber(settings.WindowHeight);
        ExtraJvmArguments = settings.ExtraJvmArguments ?? "";
        ExtraGameArguments = settings.ExtraGameArguments ?? "";
        WrapperText = settings.WrapperCommand ?? "";
        PreLaunchText = settings.PreLaunchCommand ?? "";
        PostExitText = settings.PostExitCommand ?? "";
        EnvironmentText = string.Join(Environment.NewLine, settings.EnvironmentVariables.Select(pair => $"{pair.Key}={pair.Value}"));
        SettingsMessage = null;
    }

    private void ApplyQuickPlay(QuickPlayTarget? target, IReadOnlyList<WorldInfo> worlds, IReadOnlyList<ServerEntry> servers)
    {
        var worldChoices = worlds.Select(world => new InstanceQuickPlayChoice(world.FolderName, FormatWorld(world))).ToList();
        // 保存过的存档已被删除时仍保留这一项，免得不经意间丢掉设置
        if (target is { Kind: QuickPlayKind.Singleplayer } && worldChoices.All(choice => choice.Value != target.Value))
            worldChoices.Add(new InstanceQuickPlayChoice(target.Value, InstanceStrings.WorldMissing(target.Value)));

        Worlds = worldChoices;
        SelectedWorld = target is { Kind: QuickPlayKind.Singleplayer }
            ? worldChoices.First(choice => choice.Value == target.Value)
            : null;

        Servers = servers.Select(server => new InstanceQuickPlayChoice(server.Address, FormatServer(server))).ToList();
        ServerAddress = target is { Kind: QuickPlayKind.Multiplayer } ? target.Value : "";
        SelectedServer = FindServer(ServerAddress);

        SelectedQuickPlayMode = QuickPlayModeList.First(mode => mode.Kind == target?.Kind);
    }

    private static string FormatWorld(WorldInfo world) =>
        world.DisplayName == world.FolderName ? world.DisplayName : $"{world.DisplayName} ({world.FolderName})";

    private static string FormatServer(ServerEntry server) =>
        server.Name == server.Address ? server.Address : $"{server.Name} ({server.Address})";

    private static string FormatNumber(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";

    private bool CanSave() => IsLoaded && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var instance = _instance!;
        var root = MinecraftRoot;
        var name = Name;

        IsBusy = true;
        try
        {
            // 重新读取一遍再改，免得覆盖游戏运行期间更新的累计时长与最近游玩时间
            var settings = await Task.Run(() => InstanceStore.Load(root, name));
            if (ApplyForm(settings, instance) is { } error)
            {
                ShowSettingsMessage(error, isError: true);
                return;
            }

            await Task.Run(() => InstanceStore.Save(root, name, settings));

            // 切换隔离会改变游戏目录，存档、服务器与 Mod 都要换成新目录里的
            var gameDirectory = InstanceManager.GetGameDirectory(root, instance, settings);
            if (gameDirectory != _gameDirectory)
            {
                var (worlds, servers) = await Task.Run(() => ScanQuickPlayTargets(gameDirectory));
                _gameDirectory = gameDirectory;
                ApplyQuickPlay(settings.QuickPlay, worlds, servers);
                Mods.Load(_gameDirectory);
            }

            ShowFacts(instance, settings);
            ShowSettingsMessage(InstanceStrings.Saved, isError: false);
        }
        catch (Exception e)
        {
            ShowSettingsMessage(InstanceStrings.SaveFailed(e.Message), isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowSettingsMessage(string text, bool isError)
    {
        SettingsMessageIsError = isError;
        SettingsMessage = text;
    }

    /// <summary>把表单内容写进 <paramref name="settings"/>；有输入不合法时返回第一条错误信息，此时 <paramref name="settings"/> 不应保存。</summary>
    private string? ApplyForm(InstanceSettings settings, GameInstance instance)
    {
        if (!TryParseNumber(MemoryText, MinMemoryMb, MaxMemoryMb, out var memory))
            return InstanceStrings.MemoryRange(MinMemoryMb, MaxMemoryMb);
        if (!TryParseNumber(WidthText, MinWindowSize, MaxWindowSize, out var width))
            return InstanceStrings.WidthRange(MinWindowSize, MaxWindowSize);
        if (!TryParseNumber(HeightText, MinWindowSize, MaxWindowSize, out var height))
            return InstanceStrings.HeightRange(MinWindowSize, MaxWindowSize);
        if (!TryParseEnvironment(EnvironmentText, out var environment, out var invalidLine))
            return InstanceStrings.EnvironmentInvalid(invalidLine);
        if (BuildQuickPlay(out var quickPlay) is { } quickPlayError)
            return quickPlayError;

        settings.JavaExecutable = SelectedJava?.Executable;
        settings.MaxMemoryMb = memory;
        // 与实例自身的隔离状态一致时记为“沿用”，这样之后改动实例的隔离状态仍会生效
        settings.Isolated = IsIsolated == instance.IsIsolated ? null : IsIsolated;
        settings.WindowWidth = width;
        settings.WindowHeight = height;
        settings.ExtraJvmArguments = NullIfBlank(ExtraJvmArguments);
        settings.ExtraGameArguments = NullIfBlank(ExtraGameArguments);
        settings.WrapperCommand = NullIfBlank(WrapperText);
        settings.PreLaunchCommand = NullIfBlank(PreLaunchText);
        settings.PostExitCommand = NullIfBlank(PostExitText);
        settings.EnvironmentVariables = environment;
        settings.QuickPlay = quickPlay;
        return null;
    }

    /// <summary>按当前选择生成快速进入目标；选择不完整或地址不合法时返回错误信息。</summary>
    private string? BuildQuickPlay(out QuickPlayTarget? target)
    {
        target = null;
        switch (SelectedQuickPlayMode.Kind)
        {
            case QuickPlayKind.Singleplayer:
                if (SelectedWorld is not { } world)
                    return InstanceStrings.WorldRequired;
                target = new QuickPlayTarget(QuickPlayKind.Singleplayer, world.Value);
                break;
            case QuickPlayKind.Multiplayer:
                var address = ServerAddress.Trim();
                if (!IsValidServerAddress(address))
                    return InstanceStrings.ServerInvalid;
                target = new QuickPlayTarget(QuickPlayKind.Multiplayer, address);
                break;
        }

        return null;
    }

    /// <summary>空串表示不设置；否则必须是 <paramref name="min"/> 到 <paramref name="max"/> 之间的整数。</summary>
    private static bool TryParseNumber(string text, int min, int max, out int? value)
    {
        value = null;
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return true;

        if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < min || parsed > max)
            return false;

        value = parsed;
        return true;
    }

    /// <summary>逐行解析 <c>名称=值</c>，空行忽略；<paramref name="invalidLine"/> 是第一处格式错误的行号（从 1 开始）。</summary>
    private static bool TryParseEnvironment(string text, out Dictionary<string, string> variables, out int invalidLine)
    {
        variables = new Dictionary<string, string>();
        invalidLine = 0;
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var separator = line.IndexOf('=');
            var key = separator < 0 ? "" : line[..separator].Trim();
            if (key.Length == 0)
            {
                invalidLine = i + 1;
                return false;
            }

            variables[key] = line[(separator + 1)..];
        }

        return true;
    }

    private static bool IsValidServerAddress(string address)
    {
        var match = ServerAddressPattern().Match(address);
        return match.Success
               && (!match.Groups["port"].Success
                   || int.Parse(match.Groups["port"].Value, CultureInfo.InvariantCulture) is >= 1 and <= MaxPort);
    }

    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>主机名、IPv4 或带方括号的 IPv6，后面可跟 <c>:端口</c>。</summary>
    [GeneratedRegex(@"^(?:[^\s:\[\]]+|\[[0-9A-Fa-f:.]+\])(?::(?<port>\d{1,5}))?$")]
    private static partial Regex ServerAddressPattern();
}
