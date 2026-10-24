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

    /// <summary>“前往下载页面”按钮要打开的地址，检查到新版本时指向该版本的发布页。</summary>
    private string _releaseUrl = ShionReleasesUrl;

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

        ComboSystemUpdateChannel.SelectedIndex = settings.SystemUpdateChannel;
        // 不再提供后台下载或安装，旧设置中的前两项按“提示更新”显示
        ComboSystemUpdateMode.SelectedIndex = settings.SystemUpdateMode == 3 ? 3 : 2;

        TextCurrentVersion.Text = "PCL Shion " + VersionNameFormat(AppInfo.VersionText);
        // 这一版本的界面不能替换自身，只检查 GitHub Releases 上的新版本，并提供下载页面的入口
        TextShionNotice.Text = Lang.Text("Setup.Update.Shion.NoticeManual", ShionReleasesUrl);
        ModAnimation.AniControlEnabled -= 1;
        CheckUpdate();
    }

    public async void CheckUpdate()
    {
        var updates = App.Services.Updates;
        CardUpdate.IsVisible = false;
        CardCheck.IsVisible = true;
        BtnCheckAgain.IsEnabled = false;
        TextCurrentDesc.Text = Lang.Text("Setup.Update.Shion.Checking");
        await updates.CheckAsync();
        BtnCheckAgain.IsEnabled = true;
        switch (updates.State)
        {
            case UpdateState.Available when updates.Release is { } release:
                _releaseUrl = release.HtmlUrl.AbsoluteUri;
                TextCurrentDesc.Text = Lang.Text("Setup.Update.Shion.NewVersion", release.Version.ToString());
                BtnShionReleases.Text = Lang.Text("Setup.Update.Shion.Download");
                break;
            case UpdateState.UpToDate:
                _releaseUrl = ShionReleasesUrl;
                TextCurrentDesc.Text = Lang.Text("Setup.Update.Latest");
                BtnShionReleases.Text = Lang.Text("Setup.Update.Shion.OpenReleases");
                break;
            case UpdateState.Failed:
                TextCurrentDesc.Text = Lang.Text("Setup.Update.CheckFailed");
                break;
        }
    }

    private void BtnUpdate_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl(_releaseUrl);
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

    private void BtnChangelog_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl($"{ShionReleasesUrl}/tag/v{AppInfo.VersionText}");
    }

    private void BtnShionReleases_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl(_releaseUrl);
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
