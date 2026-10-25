using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Setup;

public partial class PageSetupLauncherMisc : MyPageRight
{
    private bool isFirstLoad = true;

    private bool isLoaded;

    public PageSetupLauncherMisc()
    {
        InitializeComponent();
        PanScroll = PanBack; // 不知道为啥不能在 XAML 设置
        Loaded += PageSetupLink_Loaded;
        Loaded += (_, _) => Reload();
        RadioHttpProxyType2.PropertyChanged += (_, e) =>
        {
            if (e.Property == MyRadioBox.CheckedProperty)
                HttpProxyCustom.IsVisible = RadioHttpProxyType2.Checked;
        };
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
        SliderLoad();
        Reload();
        ModAnimation.AniControlEnabled -= 1;
    }

    public void Reload()
    {
        var settings = SetupConfig.Settings;
        var extra = SetupExtraSettings.Current;

        // 系统设置
        ComboSystemActivity.SelectedIndex = extra.SystemSystemActivity;
        // 只有 Windows 可以自行选择渲染后端，其他平台锁定为自动
        ComboSystemRenderBackend.IsEnabled = RenderBackendExtensions.IsSelectable;
        ComboSystemRenderBackend.SelectedIndex = RenderBackendExtensions.IsSelectable ? (int)settings.SystemRenderBackend : 0;
        if (!RenderBackendExtensions.IsSelectable)
            ToolTip.SetTip(ComboSystemRenderBackend, Lang.Text("Setup.Misc.System.RenderBackend.Locked"));
        SliderAniFPS.Value = settings.UiAniFPS;
        SliderMaxLog.Value = settings.SystemMaxLog;
        CheckSystemTelemetry.Checked = extra.SystemTelemetry;
        CheckNotifications.Checked = settings.NotificationsEnabled;
        CheckCompletionSound.Checked = settings.CompletionSound;

        // 网络
        TextSystemHttpProxy.Text = settings.SystemHttpProxy;
        TextSystemHttpProxyCustomUsername.Text = settings.SystemHttpProxyCustomUsername;
        TextSystemHttpProxyCustomPassword.Text = settings.SystemHttpProxyCustomPassword;
        FindRadio($"RadioHttpProxyType{settings.SystemHttpProxyType}").SetChecked(true, false);
        HttpProxyCustom.IsVisible = RadioHttpProxyType2.Checked;
        CheckNetDohEnable.Checked = extra.SystemNetEnableDoH;

        // 调试选项
        SliderDebugAnim.Value = extra.SystemDebugAnim;
        CheckDebugSkipCopy.Checked = extra.SystemDebugSkipCopy;
        CheckDebugMode.Checked = extra.SystemDebugMode;
        CheckDebugDelay.Checked = settings.SystemDebugDelay;
    }

    private MyRadioBox FindRadio(string name) => this.FindControl<MyRadioBox>(name)!;

    // 初始化
    public void Reset()
    {
        try
        {
            SetupConfig.ResetProperties(name => name.StartsWith("System", StringComparison.Ordinal) || name == nameof(AppSettings.UiAniFPS));
            SetupConfig.Info("[Setup] 已初始化启动器-杂项页设置");
            HintService.Hint(Lang.Text("Setup.Misc.Initialized"), HintType.Success, false);
            Reload();
        }
        catch (Exception ex)
        {
            SetupConfig.Log(
                ex,
                Lang.Text("Setup.Misc.Error.InitFailed"),
                SetupLogLevel.Msgbox,
                Lang.Text("Setup.Misc.Error.InitFailed"));
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void ComboChange(object? senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw!;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private void RadioBoxChange(object? senderRaw, RouteEventArgs e)
    {
        var sender = (MyRadioBox)senderRaw!;
        var gotCfg = sender.Tag?.ToString()?.Split("/") ?? Array.Empty<string>();
        if (ModAnimation.AniControlEnabled == 0 && gotCfg.Length >= 2)
            SetByTag(gotCfg[0], int.Parse(gotCfg[1]));
    }

    private void CheckBoxChange(object? senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw!;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Checked == true);
    }

    private void SliderChange(object? senderRaw, bool user)
    {
        var sender = (MySlider)senderRaw!;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Value);
    }

    private static void SetByTag(string? tag, object value)
    {
        if (tag is not null)
            SetupConfig.TrySetValue(tag, value);
    }

    // 网络
    private void ApplyHttpProxyBtn_OnClicked(object? sender, PointerReleasedEventArgs e)
    {
        SetByTag(nameof(AppSettings.SystemHttpProxy), TextSystemHttpProxy.Text ?? "");
        SetByTag(nameof(AppSettings.SystemHttpProxyCustomUsername), TextSystemHttpProxyCustomUsername.Text ?? "");
        SetByTag(nameof(AppSettings.SystemHttpProxyCustomPassword), TextSystemHttpProxyCustomPassword.Text ?? "");
    }

    // 滑动条
    private void SliderLoad()
    {
        SliderDebugAnim.getHintText = new Func<object, object>(v =>
            (int)v > 29
                ? Lang.Text("Common.Action.Close")
                : Lang.Number(Math.Round(Convert.ToDouble(v) / 10 + 0.1d, 1), "N1") + "x");
        SliderAniFPS.getHintText = new Func<object, string>(v => Lang.Number(Convert.ToInt32(v) + 1, "N0") + " FPS");
        // y = 10x + 50 (0 <= x <= 5, 50 <= y <= 100)
        // y = 50x - 150 (5 < x <= 13, 100 < y <= 500)
        // y = 100x - 800 (13 < x <= 28, 500 < y <= 2000)
        SliderMaxLog.getHintText = new Func<object, object>(v =>
        {
            var val = Convert.ToInt32(v);
            return val switch
            {
                <= 5 => val * 10 + 50,
                <= 13 => val * 50 - 150,
                <= 28 => val * 100 - 800,
                _ => Lang.Text("Setup.Misc.System.MaxLogLines.Unlimited")
            };
        });
    }

    // 渲染后端
    private void ComboSystemRenderBackend_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0 || !RenderBackendExtensions.IsSelectable)
            return;
        ComboChange(sender, e);
        HintService.Hint(Lang.Text("Setup.Misc.HardwareAcceleration.RestartNotice"));
    }

    // 调试模式
    private void CheckDebugMode_Change(object? _, bool __)
    {
        if (ModAnimation.AniControlEnabled == 0)
            HintService.Hint(Lang.Text("Setup.Misc.Debug.Mode.Hint"), HintType.Info, false);
    }

    // 自动更新
    private async void ComboSystemActivity_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (ComboSystemActivity.SelectedIndex != 2)
            return;
        if (await MsgBox.ShowAsync(
                Lang.Text("Setup.Misc.System.Announcement.Disabled.Warning.Message"),
                Lang.Text("Common.Dialog.Warning"),
                Lang.Text("Setup.Misc.System.Announcement.Disabled.Warning.Confirm"),
                Lang.Text("Common.Action.Cancel"), isWarn: true) ==
            2)
            ComboSystemActivity.SelectedItem = e.RemovedItems[0];
    }

    private void CheckDebugMode_OnChange(object? sender, bool user)
    {
        CheckBoxChange(sender, user);
        CheckDebugMode_Change(sender, user);
    }

    private void ComboSystemActivity_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ComboChange(sender, e);
        ComboSystemActivity_SelectionChanged(sender, e);
    }

    #region 导出 / 导入设置

    private async void BtnSystemSettingExp_Click(object? sender, PointerReleasedEventArgs e)
    {
        var savePath = await SetupDialogs.SelectSaveFileAsync(this,
            Lang.Text("Setup.Misc.System.ExportSettings.SaveTitle"), "PCL 全局配置.json",
            Lang.Text("Setup.Misc.System.ExportSettings.Filter"), ["*.json"]);
        if (string.IsNullOrWhiteSpace(savePath))
            return;
        SetupConfig.Settings.Save();
        File.Copy(SetupConfig.SettingsFilePath, savePath, true);
        HintService.Hint(Lang.Text("Setup.Misc.System.ExportSettings.Success"), HintType.Success);
        SystemShell.OpenFolder(savePath);
    }

    private async void BtnSystemSettingImp_Click(object? sender, PointerReleasedEventArgs e)
    {
        var sourcePath = await SetupDialogs.SelectFileAsync(this,
            Lang.Text("Setup.Misc.System.ImportSettings.SelectTitle"),
            Lang.Text("Setup.Misc.System.ExportSettings.Filter"), ["*.json"]);
        if (string.IsNullOrWhiteSpace(sourcePath))
            return;
        File.Copy(sourcePath, SetupConfig.SettingsFilePath, true);
        await MsgBox.ShowAsync(Lang.Text("Setup.Misc.System.ImportSettings.Success.Message"),
            button1: Lang.Text("Setup.Misc.System.ImportSettings.Success.Restart"));
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true });
        MainWindow.Current?.EndProgramForce();
    }

    #endregion

    #region 停止使用 PCL CE

    private async void BtnSystemStopUsingPclCe_Click(object? sender, PointerReleasedEventArgs e)
    {
        var result = await MsgBox.ShowAsync(
            Lang.Text("Setup.Misc.System.StopUsingPclCe.Message"),
            Lang.Text("Setup.Misc.System.StopUsingPclCe.Title"),
            Lang.Text("Common.Action.Continue"),
            Lang.Text("Setup.Misc.System.StopUsingPclCe.ContinueAndRemove"),
            Lang.Text("Common.Action.Cancel"),
            isWarn: true);

        if (result < 3)
        {
            if (await MsgBox.ShowAsync(
                    Lang.Text("Setup.Misc.System.StopUsingPclCe.Message.Final"),
                    Lang.Text("Common.Dialog.Warning"),
                    Lang.Text("Common.Action.Continue"),
                    Lang.Text("Common.Action.Cancel"),
                    isWarn: true) == 1)
            {
                StopUsingPClCeCore(result == 2);
            }
        }
    }

    private static void StopUsingPClCeCore(bool removeMcResources)
    {
        // 删除 MC 文件夹内的 PCL CE 配置
        if (removeMcResources)
        {
            foreach (var realPath in SetupConfig.Settings.MinecraftFolders)
            {
                Delete([Path.Combine(realPath, "PCL.ini")]);

                var versionsPath = Path.Combine(realPath, "versions");
                if (!Directory.Exists(versionsPath)) continue;

                Delete(
                    Directory.EnumerateDirectories(versionsPath)
                        .Select(p => Path.Combine(p, "PCL", "config.v1.yml"))
                );
            }
        }

        // 由于 CE 文件夹正在使用，使用延迟调用 CMD 的方法删除
        var foldersToDelete = new[]
        {
            SetupPaths.DataRoot,
            Path.Combine(Path.GetTempPath(), "PCL Shion")
        };

        if (OperatingSystem.IsWindows())
        {
            var sb = new StringBuilder();
            sb.Append("/c timeout /t 5 /nobreak >nul & ");
            foreach (var folder in foldersToDelete)
            {
                sb.Append($"rmdir /s /q \"{folder}\" & ");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = sb.ToString(),
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            });
        }
        else
        {
            var sb = new StringBuilder("sleep 5");
            foreach (var folder in foldersToDelete)
            {
                sb.Append($"; rm -rf '{folder}'");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/sh",
                ArgumentList = { "-c", sb.ToString() },
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        // 强制退出
        Environment.Exit(0);

        void Delete(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    File.Delete(path);
                }
                catch (Exception)
                {
                    //
                }
            }
        }
    }

    #endregion
}
