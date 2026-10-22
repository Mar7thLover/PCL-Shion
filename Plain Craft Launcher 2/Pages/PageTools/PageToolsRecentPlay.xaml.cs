using System.Windows;
using System.Windows.Input;
using PCL.Core.App.Localization;
using PCL.Core.App.Widgets;

namespace PCL;

/// <summary>
///     「工具」里的最近游玩页面：完整的最近游玩列表，以及清空记录。
/// </summary>
public partial class PageToolsRecentPlay
{
    /// <summary>
    ///     页面上显示的记录条数。
    /// </summary>
    private const int DisplayCount = 20;

    public PageToolsRecentPlay()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            // 页面被缓存复用，只在显示期间监听记录变化
            RecentPlayService.Changed -= OnRecordsChanged;
            RecentPlayService.Changed += OnRecordsChanged;
            Refresh();
        };
        Unloaded += (_, _) => RecentPlayService.Changed -= OnRecordsChanged;
        WeakLanguageChanged.Add(this, OnLanguageChanged);
    }

    private static void OnLanguageChanged(PageToolsRecentPlay page) => ModBase.RunInUi(page.Refresh);

    /// <summary>
    ///     重新读取记录并刷新列表。
    /// </summary>
    public void Refresh()
    {
        try
        {
            var entries = RecentPlayService.GetRecent(DisplayCount);
            PanList.Children.Clear();
            foreach (var entry in entries)
                PanList.Children.Add(RecentPlayWidget.CreateRow(entry, Refresh));
            var hasEntries = entries.Count > 0;
            LabEmpty.Visibility = hasEntries ? Visibility.Collapsed : Visibility.Visible;
            BtnClear.Visibility = hasEntries ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Widget] 刷新最近游玩页面失败", ModBase.LogLevel.Normal);
        }
    }

    private void OnRecordsChanged()
    {
        ModBase.RunInUi(Refresh);
    }

    private void BtnClear_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModMain.MyMsgBox(Lang.Text("Widget.RecentPlay.Clear.Confirm"), Lang.Text("Widget.RecentPlay.Clear"),
                button2: Lang.Text("Common.Action.Cancel"), isWarn: true) != 1)
            return;
        RecentPlayService.Clear(); // 清空后会触发 Changed，列表随之刷新
    }
}
