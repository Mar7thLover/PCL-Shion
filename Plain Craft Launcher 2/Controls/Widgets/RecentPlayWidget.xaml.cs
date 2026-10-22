using System.IO;
using System.Windows;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.App.Widgets;

namespace PCL;

/// <summary>
///     首页侧栏的「最近游玩」卡片：显示最近玩过的 3 个实例，点击即可切换为当前实例。
/// </summary>
public partial class RecentPlayWidget
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

    private static void OnLanguageChanged(RecentPlayWidget widget) => ModBase.RunInUi(widget.Refresh);

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
            LabEmpty.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Widget] 刷新最近游玩卡片失败", ModBase.LogLevel.Normal);
        }
    }

    private void OnRecordsChanged()
    {
        ModBase.RunInUi(Refresh);
    }

    private void BtnMore_Click(object sender, EventArgs e)
    {
        try
        {
            ModMain.frmMain?.PageChange(FormMain.PageType.Tools);
            ModMain.frmToolsLeft?.OpenSubPage(FormMain.PageSubType.ToolsRecentPlay);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Widget] 打开最近游玩页面失败", ModBase.LogLevel.Normal);
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
            Tag = entry,
            ToolTip = entry.Path
        };
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
            // 与实例选择页一致：启动过程中不允许切换实例
            if (ModLaunch.mcLaunchLoader.State == ModBase.LoadState.Loading)
            {
                HintService.Hint(Lang.Text("Minecraft.Launch.Error.AlreadyLaunching"), HintType.Error);
                return true;
            }

            var instance = ResolveInstance(entry.Path);
            if (instance is null)
            {
                HintService.Hint(Lang.Text("Widget.RecentPlay.Missing"), HintType.Error);
                return false;
            }

            // 与启动流程中强制切换实例时的做法一致
            ModInstanceList.McMcInstanceSelected = instance;
            States.Game.SelectedInstance = instance.Name;
            ModMain.frmLaunchLeft?.RefreshButtonsUI();
            ModMain.frmLaunchLeft?.RefreshPage(false);
            HintService.Hint(Lang.Text("Widget.RecentPlay.Selected", instance.Name), HintType.Success);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Widget] 切换到最近游玩的实例失败：" + entry.Path, ModBase.LogLevel.Normal);
        }

        return true;
    }

    /// <summary>
    ///     找到可用于选中的实例对象，不可用时返回 null。
    /// </summary>
    private static McInstance? ResolveInstance(string path)
    {
        if (!IsInstanceAvailable(path))
            return null;
        // 优先使用实例列表里已有的对象
        var listed = ModInstanceList.mcInstanceList.Values.SelectMany(list => list)
            .FirstOrDefault(i => RecentPlayStore.IsSamePath(i.PathInstance, path));
        if (listed is not null)
            return new McInstance(listed.PathInstance).Check() ? listed : null; // 与实例选择页一致，用新对象确认可用
        var fresh = new McInstance(path);
        return fresh.Check() ? fresh : null;
    }

    /// <summary>
    ///     实例文件夹是否仍存在，且属于当前选中的 Minecraft 文件夹。
    ///     PCL 只记录当前文件夹内的实例名，所以其他文件夹的实例在这里无法被选中，按不可用处理。
    /// </summary>
    private static bool IsInstanceAvailable(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        var folder = ModFolder.mcFolderSelected;
        if (!string.IsNullOrEmpty(folder))
        {
            var prefix = RecentPlayStore.NormalizePath(folder) + @"\versions\";
            if (!RecentPlayStore.NormalizePath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return Directory.Exists(path);
    }

    #endregion
}
