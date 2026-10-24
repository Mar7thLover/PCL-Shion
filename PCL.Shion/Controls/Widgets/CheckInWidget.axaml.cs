using System;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.Threading;
using PCL.Shion.Core.Widgets;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;

namespace PCL.Shion.Controls;

/// <summary>
///     首页侧栏的日期打卡卡片。
/// </summary>
public partial class CheckInWidget : MyCard
{
    public CheckInWidget()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh(); // 启动器跨天开着时，重新显示后日期要跟着变
        WeakLanguageChanged.Add(this, OnLanguageChanged);
        Refresh();
    }

    private static void OnLanguageChanged(CheckInWidget widget) => Dispatcher.UIThread.Post(() => widget.Refresh());

    /// <summary>
    ///     重新读取打卡记录并刷新显示。
    /// </summary>
    public void Refresh()
    {
        try
        {
            Apply(CheckInService.Get());
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "刷新日期打卡卡片失败：{Exception}", ex);
        }
    }

    private void Apply(CheckInStats stats)
    {
        BtnCheckIn.IsVisible = !stats.CheckedToday;
        PanDone.IsVisible = stats.CheckedToday;
        LabStreak.Text = Lang.Text("Widget.CheckIn.Streak", stats.CurrentStreak);
        LabStats.Text = Lang.Text("Widget.CheckIn.Stats", stats.TotalDays, stats.LongestStreak);
    }

    private void BtnCheckIn_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            var stats = CheckInService.CheckIn();
            Apply(stats);
            // 写入失败时服务只记录日志，此时今天仍未打卡，不提示成功
            if (stats.CheckedToday)
                Hint.Show(Lang.Text("Widget.CheckIn.Hint.Success", stats.CurrentStreak), HintType.Success);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "日期打卡失败：{Exception}", ex);
        }
    }

    private void BtnMore_Click(object? sender, EventArgs? e)
    {
        try
        {
            Navigator.Go(PageType.Tools, PageSubType.ToolsCheckIn);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "打开日期打卡页面失败：{Exception}", ex);
        }
    }
}
