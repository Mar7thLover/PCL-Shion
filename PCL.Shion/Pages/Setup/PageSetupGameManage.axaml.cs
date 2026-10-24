using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PCL.Shion.Controls;
using PCL.Shion.Localization;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Setup;

public partial class PageSetupGameManage : MyPageRight
{
    private static AppSettings Settings => App.Services.Settings;

    private bool isLoaded;

    public PageSetupGameManage()
    {
        InitializeComponent();
        Loaded += PageSetupGameManage_Loaded;
    }

    private void PageSetupGameManage_Loaded(object? sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        ModAnimation.AniControlEnabled += 1;
        Reload();
        SliderLoad();

        if (!Lang.IsChineseMainland)
        {
            TextFilenameFormat.IsVisible = false;
            ComboDownloadTranslateV2.IsVisible = false;
            TextModManageStyle.IsVisible = false;
            ComboModLocalNameStyle.IsVisible = false;

            // 行 2 至 5 为中文区专用的文件名格式与模组管理样式
            for (var row = 2; row <= 5; row++)
                GridCommunity.RowDefinitions[row].Height = new GridLength(0);
        }

        AlignNameColumns();
        ModAnimation.AniControlEnabled -= 1;
    }

    /// <summary>
    ///     让两张卡片中的标题列宽度一致，对应 WPF 的 SharedSizeGroup="Name"。
    /// </summary>
    private void AlignNameColumns()
    {
        var grids = new[] { GridSource, GridCommunity };
        var width = 0d;
        foreach (var grid in grids)
            foreach (var child in grid.Children.OfType<Control>())
            {
                if (!child.IsVisible || Grid.GetColumn(child) != 0)
                    continue;
                child.Measure(Size.Infinity);
                width = Math.Max(width, child.DesiredSize.Width);
            }

        foreach (var grid in grids)
            grid.ColumnDefinitions[0].Width = new GridLength(width);
    }

    public void Reload()
    {
        var settings = Settings;

        // 下载
        SliderDownloadThread.Value = settings.ToolDownloadThread;
        SliderDownloadFileConnection.Value = settings.ToolDownloadFileConnection;
        SliderDownloadSpeed.Value = settings.ToolDownloadSpeed;
        ComboDownloadHttpMode.SelectedIndex = settings.ToolDownloadHttpMode;
        ComboDownloadSource.SelectedIndex = settings.ToolDownloadSource;
        ComboDownloadVersion.SelectedIndex = settings.ToolDownloadVersion;
        CheckDownloadAutoSelectVersion.Checked = settings.ToolDownloadAutoSelectVersion;
        CheckFixAuthlib.Checked = settings.ToolFixAuthlib;

        // Mod 与整合包
        ComboDownloadTranslateV2.SelectedIndex = settings.ToolDownloadTranslateV2;
        ComboDownloadMod.SelectedIndex = settings.ToolDownloadMod;
        ComboModLocalNameStyle.SelectedIndex = settings.ToolModLocalNameStyle;
        ComboDownloadQuickBehavior.SelectedIndex = settings.ToolDownloadQuickBehavior;
        CheckDownloadIgnoreQuilt.Checked = settings.ToolDownloadIgnoreQuilt;
        CheckDownloadAutoInstallDependencies.Checked = settings.ToolDownloadAutoInstallDependencies;
        CheckDownloadClipboard.Checked = settings.ToolDownloadClipboard;

        // Minecraft 更新提示
        CheckUpdateRelease.Checked = settings.ToolUpdateRelease;
        CheckUpdateSnapshot.Checked = settings.ToolUpdateSnapshot;

        // 辅助设置
        CheckHelpLauncherLanguage.Checked = settings.ToolHelpChinese;
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Settings.ResetDownload();
            Settings.ResetTool();
            Settings.Save();
            Trace.TraceInformation("[Setup] 已初始化其他页设置");
            Hint.Show(Lang.Text("Setup.GameManage.Initialized"), HintType.Success);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"初始化其他页设置失败：{ex}");
            Hint.Show(Lang.Text("Setup.GameManage.Error.InitFailed"), HintType.Error);
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void CheckBoxChange(object sender, bool user)
    {
        if (ModAnimation.AniControlEnabled != 0 || sender is not MyCheckBox box)
            return;
        var settings = Settings;
        var value = box.Checked == true;
        if (ReferenceEquals(box, CheckDownloadAutoSelectVersion))
            settings.ToolDownloadAutoSelectVersion = value;
        else if (ReferenceEquals(box, CheckFixAuthlib))
            settings.ToolFixAuthlib = value;
        else if (ReferenceEquals(box, CheckDownloadIgnoreQuilt))
            settings.ToolDownloadIgnoreQuilt = value;
        else if (ReferenceEquals(box, CheckDownloadAutoInstallDependencies))
            settings.ToolDownloadAutoInstallDependencies = value;
        else if (ReferenceEquals(box, CheckDownloadClipboard))
            settings.ToolDownloadClipboard = value;
        else if (ReferenceEquals(box, CheckUpdateRelease))
            settings.ToolUpdateRelease = value;
        else if (ReferenceEquals(box, CheckUpdateSnapshot))
            settings.ToolUpdateSnapshot = value;
        else if (ReferenceEquals(box, CheckHelpLauncherLanguage))
            settings.ToolHelpChinese = value;
        else
            return;
        settings.Save();
    }

    private void SliderChange(object sender, bool user)
    {
        if (ModAnimation.AniControlEnabled != 0 || sender is not MySlider slider)
            return;
        var settings = Settings;
        if (ReferenceEquals(slider, SliderDownloadThread))
            settings.ToolDownloadThread = slider.Value;
        else if (ReferenceEquals(slider, SliderDownloadFileConnection))
            settings.ToolDownloadFileConnection = slider.Value;
        else if (ReferenceEquals(slider, SliderDownloadSpeed))
            settings.ToolDownloadSpeed = slider.Value;
        else
            return;
        settings.Save();
    }

    private void ComboChange(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0 || sender is not MyComboBox box)
            return;
        var settings = Settings;
        var index = box.SelectedIndex;
        if (ReferenceEquals(box, ComboDownloadSource))
            settings.ToolDownloadSource = index;
        else if (ReferenceEquals(box, ComboDownloadVersion))
            settings.ToolDownloadVersion = index;
        else if (ReferenceEquals(box, ComboDownloadHttpMode))
            settings.ToolDownloadHttpMode = index;
        else if (ReferenceEquals(box, ComboDownloadMod))
            settings.ToolDownloadMod = index;
        else if (ReferenceEquals(box, ComboDownloadTranslateV2))
            settings.ToolDownloadTranslateV2 = index;
        else if (ReferenceEquals(box, ComboModLocalNameStyle))
            settings.ToolModLocalNameStyle = index;
        else if (ReferenceEquals(box, ComboDownloadQuickBehavior))
            settings.ToolDownloadQuickBehavior = index;
        else
            return;
        settings.Save();
    }

    // 滑动条
    private void SliderLoad()
    {
        SliderDownloadThread.getHintText = new Func<object, object>(v => (int)v + 1);
        SliderDownloadFileConnection.getHintText = new Func<object, object>(v => (int)v + 1);
        SliderDownloadSpeed.getHintText = new Func<object, object>(v =>
        {
            var value = (int)v;
            return value switch
            {
                <= 14 => Lang.Number((value + 1) * 0.1d, "N1") + " MiB/s",
                <= 31 => Lang.Number((value - 11) * 0.5d, "N1") + " MiB/s",
                <= 41 => Lang.Number(value - 21, "N0") + " MiB/s",
                _ => Lang.Text("Setup.GameManage.Download.Unlimited")
            };
        });
    }

    private void SliderDownloadThread_PreviewChange(object sender, RouteEventArgs e)
    {
        if (SliderDownloadThread.Value < 64)
            return;
        if (!Settings.HintLargeDownloadThread)
        {
            Settings.HintLargeDownloadThread = true;
            Settings.Save();
            _ = MsgBox.ShowAsync(
                Lang.Text("Setup.GameManage.Download.Threads.TooManyWarning.Message"),
                Lang.Text("Common.Dialog.Warning"),
                Lang.Text("Setup.GameManage.Download.Threads.TooManyWarning.Confirm"), isWarn: true);
        }
    }
}
