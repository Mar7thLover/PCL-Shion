using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Logging;
using PCL.Shion.Controls;
using PCL.Shion.Localization;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Setup;

public partial class PageSetupGameLink : MyPageRight
{
    private bool isFirstLoad = true;

    private bool isLoaded;

    public PageSetupGameLink()
    {
        InitializeComponent();
        PanScroll = PanBack; // 不知道为啥不能在 XAML 设置
        TextUdpNatType.Text = Lang.Text("Setup.GameLink.NetworkTest.UdpNatType", Lang.Text("Setup.GameLink.NetworkTest.NotTested"));
        TextTcpNatType.Text = Lang.Text("Setup.GameLink.NetworkTest.TcpNatType", Lang.Text("Setup.GameLink.NetworkTest.NotTested"));
        TextIpv6Status.Text = Lang.Text("Setup.GameLink.NetworkTest.Ipv6Status", Lang.Text("Setup.GameLink.NetworkTest.NotTested"));
        Loaded += PageSetupLink_Loaded;
        Loaded += (_, _) => Reload();
    }

    private void PageSetupLink_Loaded(object? sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        ModAnimation.AniControlEnabled += 1;
        Reload();
        ModAnimation.AniControlEnabled -= 1;
    }

    public void Reload()
    {
        var settings = App.Services.Settings;
        TextLinkUsername.Text = settings.LinkUsername;
        CheckLatencyFirstMode.Checked = settings.LinkLatencyFirstMode;
        ComboPreferProtocol.SelectedIndex = (int)settings.LinkProtocolPreference;
        CheckTryPunchSym.Checked = settings.LinkTryPunchSym;
        CheckEnableIPv6.Checked = settings.LinkEnableIPv6;
        CheckEnableCliOutput.Checked = settings.LinkEnableCliOutput;
    }

    // 初始化：恢复联机页的全部设置为默认值（与 WPF 中 Config.Link 的默认值一致）
    public void Reset()
    {
        try
        {
            var settings = App.Services.Settings;
            settings.LinkUsername = "";
            settings.LinkLatencyFirstMode = true;
            settings.LinkProtocolPreference = LinkProtocolPreference.Tcp;
            settings.LinkTryPunchSym = true;
            settings.LinkEnableIPv6 = true;
            settings.LinkEnableCliOutput = false;
            settings.Save();

            Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(null, "{Message}", "[Setup] 已初始化联机页设置");
            HintService.Hint(Lang.Text("Setup.GameLink.Initialized"), HintType.Success, false);
        }
        catch (Exception ex)
        {
            LogAndHint(ex, "[Setup] 初始化联机页设置失败", Lang.Text("Setup.GameLink.Error.InitFailed"));
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void TextBoxChange(object? senderRaw, TextChangedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw!;
        if (ModAnimation.AniControlEnabled == 0)
            SetLinkSetting(sender.Tag?.ToString(), sender.Text ?? "");
    }

    private void CheckBoxChange(object? senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw!;
        if (ModAnimation.AniControlEnabled == 0)
            SetLinkSetting(sender.Tag?.ToString(), sender.Checked == true);
    }

    /// <summary>
    ///     按控件 Tag 中的设置名写入 <see cref="AppSettings"/> 并保存。
    /// </summary>
    private static void SetLinkSetting(string? tag, object value)
    {
        if (tag is null)
            return;

        var settings = App.Services.Settings;
        switch (tag)
        {
            case nameof(AppSettings.LinkUsername):
                settings.LinkUsername = (string)value;
                break;
            case nameof(AppSettings.LinkLatencyFirstMode):
                settings.LinkLatencyFirstMode = (bool)value;
                break;
            case nameof(AppSettings.LinkProtocolPreference):
                settings.LinkProtocolPreference = (LinkProtocolPreference)(int)value;
                break;
            case nameof(AppSettings.LinkTryPunchSym):
                settings.LinkTryPunchSym = (bool)value;
                break;
            case nameof(AppSettings.LinkEnableIPv6):
                settings.LinkEnableIPv6 = (bool)value;
                break;
            case nameof(AppSettings.LinkEnableCliOutput):
                settings.LinkEnableCliOutput = (bool)value;
                break;
            default:
                return;
        }

        settings.Save();
    }

    private void LinkProtocolPerferenceChange(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled == 0)
            try
            {
                SetLinkSetting(ComboPreferProtocol.Tag?.ToString(), ComboPreferProtocol.SelectedIndex);
            }
            catch (Exception ex)
            {
                LogAndHint(ex, "[Setup] 联机协议偏好写入失败", Lang.Text("Setup.GameLink.Error.ConfigChangeFailed"));
            }
    }

    // 网络测试
    // TODO(shion): 网络测试依赖 PCL.Core.Link 中的 EasyTier CLI 与 NAT 探测（仅 Windows/WPF），Shion 中尚未移植。
    //              该卡片在 XAML 中默认隐藏，这里仅给出提示，待移植后再接入 CliNetTest 的结果。
    private void BtnNetTest_Click(object? sender, PointerReleasedEventArgs e)
    {
        HintService.Hint(
            LocalizedOrFallback("Setup.GameLink.NetworkTest.Unavailable", "网络测试暂未在 Shion 中提供"),
            HintType.Info,
            false);
    }

    private static void LogAndHint(Exception ex, string message, string hint)
    {
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);
        HintService.Hint(hint, HintType.Error, false);
    }

    /// <summary>
    ///     取得本地化文本；键缺失时返回给定的中文兜底文本，避免直接显示键名。
    /// </summary>
    private static string LocalizedOrFallback(string key, string fallback) =>
        LocalizationService.FindText(key) ?? fallback;
}
