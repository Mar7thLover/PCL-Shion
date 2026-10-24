using System;
using Avalonia.Controls;
using Avalonia.Input;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Setup;

public partial class PageSetupUpdate : MyPageRight
{
    private const string ShionReleasesUrl = "https://github.com/Mar7thLover/PCL-Shion/releases";

    // PCL Shion 暂不提供应用内更新：展示前往 GitHub Releases 的提示，并隐藏更新源与更新渠道等设置
    private const bool InAppUpdateEnabled = false;

    public PageSetupUpdate()
    {
        InitializeComponent();
        PanScroll = PanBack; // 不知道为啥不能在 XAML 设置
        Loaded += (_, _) => Init();
    }

    private void Init()
    {
        ModAnimation.AniControlEnabled += 1;
        var settings = SetupConfig.Settings;
        TextMirrorCDK.Text = settings.MirrorChyanKey;

        ComboSystemUpdateChannel.SelectedIndex = settings.SystemUpdateChannel;
        ComboSystemUpdateMode.SelectedIndex = settings.SystemUpdateMode;

        TextCurrentVersion.Text = "PCL Shion " + VersionNameFormat(AppInfo.VersionText);
        CardShionNotice.IsVisible = !InAppUpdateEnabled;
        CardUpdateSettings.IsVisible = InAppUpdateEnabled;
        TextShionNotice.Text = Lang.Text("Setup.Update.Shion.Notice", ShionReleasesUrl);
        ModAnimation.AniControlEnabled -= 1;
        CheckUpdate();
    }

    public void CheckUpdate()
    {
        // PCL Shion 不检查更新，也不访问上游更新源
        CardUpdate.IsVisible = false;
        CardCheck.IsVisible = true;
        BtnCheckAgain.IsVisible = false;
        TextCurrentDesc.Text = Lang.Text("Setup.Update.Shion.Status");
    }

    private void BtnUpdate_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl(ShionReleasesUrl);
    }

    private async void BtnChangelogDetail_Click(object? sender, EventArgs? e)
    {
        await MsgBox.ShowAsync(Lang.Text("Setup.Update.Changelog.Unavailable"), Lang.Text("Setup.Update.Changelog.Title"));
    }

    private void ComboSystemUpdateMode_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(nameof(AppSettings.SystemUpdateMode), ComboSystemUpdateMode.SelectedIndex);
    }

    private async void ComboSystemUpdateBranch_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;

        var isCancelled = false;
        switch (ComboSystemUpdateChannel.SelectedIndex)
        {
            case 0:
            {
                break;
            }
            case 1:
            {
                if (await MsgBox.ShowAsync(Lang.Text("Setup.Update.Channel.Beta.Warning.Message"),
                        Lang.Text("Setup.Update.Channel.Common.Warning.Title"),
                        Lang.Text("Setup.Update.Channel.Common.Warning.Confirm"),
                        Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                    isCancelled = true;
                else
                    CheckUpdate();
                break;
            }
            case 2:
            {
                if (await MsgBox.ShowAsync(Lang.Text("Setup.Update.Channel.Dev.Warning.Message"),
                        Lang.Text("Setup.Update.Channel.Common.Warning.Title"),
                        Lang.Text("Setup.Update.Channel.Common.Warning.Confirm"),
                        Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                {
                    isCancelled = true;
                    break;
                }

                var confirmText = Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.ExpectedInput");
                var ret = await MsgBox.InputAsync(
                    Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Title"),
                    Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Message", confirmText));

                if (ret == confirmText)
                {
                    CheckUpdate();
                }
                else
                {
                    HintService.Hint(Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.WrongInput"));
                    isCancelled = true;
                }

                break;
            }
        }

        if (isCancelled)
        {
            ModAnimation.AniControlEnabled += 1;
            ComboSystemUpdateChannel.SelectedItem = e.RemovedItems[0];
            ModAnimation.AniControlEnabled -= 1;
        }
        else
        {
            SetByTag(nameof(AppSettings.SystemUpdateChannel), ComboSystemUpdateChannel.SelectedIndex);
        }
    }

    private void TextMirrorCDK_PasswordChanged(object? sender, TextChangedEventArgs e)
    {
        SetByTag(nameof(AppSettings.MirrorChyanKey), TextMirrorCDK.Text ?? "");
    }

    private void BtnGetMirrorCDK_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl("https://mirrorchyan.com/");
    }

    private void BtnChangelog_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl(ShionReleasesUrl);
    }

    private void BtnShionReleases_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl(ShionReleasesUrl);
    }

    public string VersionNameFormat(string str)
    {
        str = str.Replace("v", "");
        if (!str.Contains("-"))
            return str;
        var add = str.Substring(str.LastIndexOf('-') + 1);
        str = str.Substring(0, str.LastIndexOf('-'));
        return $"{str} {add.Replace(".", " ").Replace("beta", "Beta").Replace("rc", "RC")}";
    }

    private void BtnCheckAgain_OnClick(object? sender, PointerReleasedEventArgs e)
    {
        CheckUpdate();
    }

    private static void SetByTag(string tag, object value)
    {
        SetupConfig.TrySetValue(tag, value);
    }
}
