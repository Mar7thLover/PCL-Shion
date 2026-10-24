using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Threading;
using PCL.Shion.Core.Widgets;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;
using PCL.Shion.ViewModels;

namespace PCL.Shion.Controls;

/// <summary>
///     首页侧栏的「最近游玩」卡片：显示最近玩过的 3 个实例，点击即可切换为当前实例。
/// </summary>
public partial class RecentPlayWidget : MyCard
{
    /// <summary>
    ///     卡片上显示的记录条数。
    /// </summary>
    private const int DisplayCount = 3;

    public RecentPlayWidget()
    {
        InitializeComponent();
        Refresh(); // 在首次布局前就填好内容，避免卡片先空后涨
        Loaded += (_, _) =>
        {
            // 控件可能被反复移出、加入首页，只在显示期间监听记录变化
            RecentPlayService.Changed -= OnRecordsChanged;
            RecentPlayService.Changed += OnRecordsChanged;
            Refresh();
        };
        Unloaded += (_, _) => RecentPlayService.Changed -= OnRecordsChanged;
        WeakLanguageChanged.Add(this, OnLanguageChanged); // 相对时间、时长等文字是代码生成的，换语言后要重新生成
    }

    /// <summary>
    ///     首页的实例视图模型；主窗口还没建立时为 null。
    /// </summary>
    private static HomeViewModel? Home => MainWindow.Current?.Home;

    private static void OnLanguageChanged(RecentPlayWidget widget) => Dispatcher.UIThread.Post(() => widget.Refresh());

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
                PanList.Children.Add(CreateRow(entry, Refresh));
            LabEmpty.IsVisible = entries.Count == 0;
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "[Widget] 刷新最近游玩卡片失败：{Exception}", ex);
        }
    }

    private void OnRecordsChanged() => Dispatcher.UIThread.Post(() => Refresh());

    private void BtnMore_Click(object? sender, EventArgs? e)
    {
        try
        {
            Navigator.Go(PageType.Tools, PageSubType.ToolsRecentPlay);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "[Widget] 打开最近游玩页面失败：{Exception}", ex);
        }
    }

    #region 列表项（卡片与完整页面共用）

    /// <summary>
    ///     为一条记录创建列表项。实例仍存在时可点击切换；否则显示「实例已不存在」且不可点击。
    /// </summary>
    /// <param name="entry">记录。</param>
    /// <param name="refresh">点击时发现实例已不可用，需要刷新所在列表时调用。</param>
    public static MyListItem CreateRow(RecentPlayEntry entry, Action refresh)
    {
        var available = IsInstanceAvailable(entry.Path);
        var item = new MyListItem
        {
            Title = string.IsNullOrWhiteSpace(entry.Name) ? entry.Path : entry.Name,
            Info = available
                ? Lang.Text("Widget.RecentPlay.Info", WidgetFormat.RelativeTime(entry.LastPlayedUtc),
                    WidgetFormat.Duration(TimeSpan.FromSeconds(entry.TotalSeconds)))
                : Lang.Text("Widget.RecentPlay.Missing"),
            Type = available ? MyListItem.CheckType.Clickable : MyListItem.CheckType.None,
            Tag = entry
        };
        ToolTip.SetTip(item, entry.Path);
        if (available)
            item.Click += (_, _) =>
            {
                if (!SelectInstance(entry))
                    refresh();
            };
        else
            item.isMouseOverAnimationEnabled = false;
        return item;
    }

    /// <summary>
    ///     把记录对应的实例设为当前选中的实例。返回 false 表示实例已不可用（已提示），调用方应刷新列表。
    /// </summary>
    private static bool SelectInstance(RecentPlayEntry entry)
    {
        try
        {
            var home = Home;
            if (home is null)
            {
                Hint.Show(Lang.Text("Widget.RecentPlay.Missing"), HintType.Error);
                return false;
            }

            // 与实例选择页一致：启动过程中不允许切换实例
            if (home.IsBusy)
            {
                Hint.Show(Lang.Text("Minecraft.Launch.Error.AlreadyLaunching"), HintType.Error);
                return true;
            }

            var instance = ResolveInstance(home, entry.Path);
            if (instance is null)
            {
                Hint.Show(Lang.Text("Widget.RecentPlay.Missing"), HintType.Error);
                return false;
            }

            // 与启动流程中强制切换实例时的做法一致
            home.SelectedInstance = instance;
            Hint.Show(Lang.Text("Widget.RecentPlay.Selected", instance.Name), HintType.Success);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "[Widget] 切换到最近游玩的实例失败：{Path}：{Exception}", entry.Path, ex);
        }

        return true;
    }

    /// <summary>
    ///     在首页的实例列表里找到可用于选中的实例，不可用时返回 null。
    /// </summary>
    private static InstanceItem? ResolveInstance(HomeViewModel home, string path)
    {
        if (!IsInstanceAvailable(path))
            return null;
        return home.Instances.FirstOrDefault(i => i.IsSelectable && RecentPlayStore.IsSamePath(i.Instance.Directory, path));
    }

    /// <summary>
    ///     实例文件夹是否仍存在，且属于当前选中的 Minecraft 文件夹。
    ///     首页只列出当前文件夹内的实例，所以其他文件夹的实例在这里无法被选中，按不可用处理。
    ///     实例列表还没读完时，只按文件夹是否存在判断。
    /// </summary>
    private static bool IsInstanceAvailable(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;
        var instances = Home?.Instances;
        return instances is null || instances.Count == 0
               || instances.Any(i => RecentPlayStore.IsSamePath(i.Instance.Directory, path));
    }

    #endregion
}
