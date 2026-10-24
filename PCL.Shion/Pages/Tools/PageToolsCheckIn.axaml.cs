using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.Widgets;
using PCL.Shion.Localization;
using PCL.Shion.Pages;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Tools;

/// <summary>
///     工具页：日期打卡（打卡按钮、统计与本月日历）。
/// </summary>
public partial class PageToolsCheckIn : MyPageRight
{
    private const int CalendarCellHeight = 40;

    public PageToolsCheckIn()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
        WeakLanguageChanged.Add(this, OnLanguageChanged);
    }

    private static void OnLanguageChanged(PageToolsCheckIn page) => Dispatcher.UIThread.Post(page.Reload);

    private static void LogError(Exception ex, string message) =>
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);

    /// <summary>
    ///     重新读取打卡记录并刷新统计与日历。
    /// </summary>
    public void Reload()
    {
        try
        {
            var today = CheckInService.Today;
            var dates = CheckInService.GetDates();
            Apply(CheckInCalculator.Compute(dates, today));
            BuildCalendar(dates, today);
        }
        catch (Exception ex)
        {
            LogError(ex, "刷新日期打卡页面失败");
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
            Reload();
            // 写入失败时服务只记录日志，此时今天仍未打卡，不提示成功
            if (stats.CheckedToday)
                Hint.Show(Lang.Text("Widget.CheckIn.Hint.Success", stats.CurrentStreak), HintType.Success);
        }
        catch (Exception ex)
        {
            LogError(ex, "日期打卡失败");
        }
    }

    #region 日历

    /// <summary>
    ///     按本月的日期重建日历网格（从周一开始）。
    /// </summary>
    private void BuildCalendar(System.Collections.Generic.IReadOnlySet<DateOnly> dates, DateOnly today)
    {
        LabMonth.Text = Lang.Date(new DateTime(today.Year, today.Month, 1), "Y");

        // 星期标题
        PanWeekdays.Children.Clear();
        foreach (var name in GetWeekdayNames())
            PanWeekdays.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 12,
                Opacity = 0.6,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            });

        // 日期：第一天之前补空位，使其落在正确的星期列
        PanDays.Children.Clear();
        var first = new DateOnly(today.Year, today.Month, 1);
        var leading = ((int)first.DayOfWeek + 6) % 7; // 周一为 0，周日为 6
        for (var i = 0; i < leading; i++)
            PanDays.Children.Add(new Border { Height = CalendarCellHeight });
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(today.Year, today.Month, day);
            PanDays.Children.Add(BuildDayCell(day, dates.Contains(date), date == today, date > today));
        }
    }

    /// <summary>
    ///     获取从周一开始的 7 个星期标题。语言键缺失或数量不对时，退回到当前区域性的缩写。
    /// </summary>
    private static string[] GetWeekdayNames()
    {
        var names = Lang.Text("Widget.CheckIn.Weekdays").Split(',', StringSplitOptions.TrimEntries);
        if (names.Length == 7)
            return names;
        var fallback = Lang.Culture.DateTimeFormat.AbbreviatedDayNames; // 周日在前
        return Enumerable.Range(1, 7).Select(i => fallback[i % 7]).ToArray();
    }

    /// <summary>
    ///     单个日期格：已打卡用主题色圆点，今天额外加一圈边框。
    /// </summary>
    private static Control BuildDayCell(int day, bool isChecked, bool isToday, bool isFuture)
    {
        var cell = new Grid { Height = CalendarCellHeight, IsHitTestVisible = false };
        if (isFuture)
            cell.Opacity = 0.4;
        if (isToday)
        {
            var ring = new Ellipse
            {
                Width = 34,
                Height = 34,
                StrokeThickness = 2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ring.SetResourceReference(Shape.StrokeProperty, "ColorBrush3");
            cell.Children.Add(ring);
        }

        if (isChecked)
        {
            var dot = new Ellipse
            {
                Width = 26,
                Height = 26,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            dot.SetResourceReference(Shape.FillProperty, "ColorBrush3");
            cell.Children.Add(dot);
        }

        var label = new TextBlock
        {
            Text = day.ToString(),
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (isChecked)
            label.Foreground = Brushes.White;
        cell.Children.Add(label);
        return cell;
    }

    #endregion
}
