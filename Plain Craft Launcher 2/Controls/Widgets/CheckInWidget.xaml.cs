using System.Windows;
using System.Windows.Input;
using PCL.Core.App.Localization;
using PCL.Core.App.Widgets;

namespace PCL;

/// <summary>
///     首页侧栏的日期打卡卡片。
/// </summary>
public partial class CheckInWidget
{
    public CheckInWidget()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh(); // 启动器跨天开着时，重新显示后日期要跟着变
        WeakLanguageChanged.Add(this, OnLanguageChanged);
        Refresh();
    }

    private static void OnLanguageChanged(CheckInWidget widget) => ModBase.RunInUi(widget.Refresh);

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
            ModBase.Log(ex, "刷新日期打卡卡片失败");
        }
    }

    private void Apply(CheckInStats stats)
    {
        BtnCheckIn.Visibility = stats.CheckedToday ? Visibility.Collapsed : Visibility.Visible;
        PanDone.Visibility = stats.CheckedToday ? Visibility.Visible : Visibility.Collapsed;
        LabStreak.Text = Lang.Text("Widget.CheckIn.Streak", stats.CurrentStreak);
        LabStats.Text = Lang.Text("Widget.CheckIn.Stats", stats.TotalDays, stats.LongestStreak);
    }

    private void BtnCheckIn_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var stats = CheckInService.CheckIn();
            Apply(stats);
            // 写入失败时服务只记录日志，此时今天仍未打卡，不提示成功
            if (stats.CheckedToday)
                HintService.Hint(Lang.Text("Widget.CheckIn.Hint.Success", stats.CurrentStreak), HintType.Success);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "日期打卡失败");
        }
    }

    private void BtnMore_Click(object sender, EventArgs e)
    {
        try
        {
            ModMain.frmMain?.PageChange(FormMain.PageType.Tools);
            ModMain.frmToolsLeft?.OpenSubPage(FormMain.PageSubType.ToolsCheckIn);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "打开日期打卡页面失败");
        }
    }
}
