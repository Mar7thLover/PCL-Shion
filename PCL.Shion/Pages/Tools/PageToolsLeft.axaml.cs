using System;
using Avalonia.Input;
using Avalonia.Logging;
using PCL.Shion.Controls;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Homepage;
using PCL.Shion.Pages.Setup;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Tools;

public partial class PageToolsLeft : MyPageLeft
{
    // 各右侧页面的缓存实例，与 WPF 中 ModMain 下的 frmTools* 字段对应
    private PageToolsGameLink? frmToolsGameLink;
    private PageToolsTest? frmToolsTest;
    private PageToolsCheckIn? frmToolsCheckIn;
    private PageToolsRecentPlay? frmToolsRecentPlay;
    private PageHomepageNewsView? frmToolsNews;

    /// <summary>
    ///     当前页面的编号。
    /// </summary>
    public PageSubType pageID = PageSubType.ToolsGameLink;

    public PageToolsLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;

        ItemGameLink.Check += PageCheck;
        ItemTest.Check += PageCheck;
        ItemCheckIn.Check += PageCheck;
        ItemRecentPlay.Check += PageCheck;
        ItemNews.Check += PageCheck;

        foreach (var button in ItemGameLink.Buttons)
            button.Click += Refresh;
    }

    private static void LogError(Exception ex, string message) =>
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);

    public override void OnNavigated(PageSubType sub)
    {
        var hide = App.Services.Settings;
        var forceShow = PageSetupUI.HiddenForceShow;
        // 刷新子页面隐藏情况
        PageSetupUI.HiddenRefresh();

        // 未指定子页面时，选择第一个未被隐藏的子页面
        var target = sub;
        if (target == PageSubType.Default)
        {
            if (!forceShow && !hide.UiHiddenToolsGameLink)
                target = PageSubType.ToolsGameLink;
            else if (!forceShow && !hide.UiHiddenToolsTest)
                target = PageSubType.ToolsTest;
            else
                target = PageSubType.ToolsCheckIn; // 联机与百宝箱都被隐藏时，回退到不可隐藏的小工具页
        }

        try
        {
            var item = GetItem(target) ?? throw new ArgumentException("未知的工具子页面种类：" + (int)target);
            // 先记下目标子页面，勾选左侧项时 PageCheck 会因编号相同而直接返回，只发生一次切换
            pageID = target;
            item.SetChecked(true, false, false);
            Navigator.ShowRight(PageGet(target));
        }
        catch (Exception ex)
        {
            LogError(ex, "打开工具子页面失败（ID " + (int)target + "）");
            Hint.Show(Lang.Text("Tools.Error.OperationFailed"), HintType.Error);
        }
    }

    public void Refresh(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not MyIconButton { Tag: not null } button)
            return;
        var id = Convert.ToInt32(button.Tag);
        switch ((PageSubType)id)
        {
            case PageSubType.ToolsGameLink:
            {
                frmToolsGameLink ??= new PageToolsGameLink();
                frmToolsGameLink.Reload();
                ItemGameLink.Checked = true;
                break;
            }
        }
    }

    #region 页面切换

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object sender, RouteEventArgs e)
    {
        // 尚未初始化控件属性时，Tag 为空，会导致切换到页面 0
        if (sender is MyListItem { Tag: not null } item)
            PageChange((PageSubType)Convert.ToInt32(item.Tag));
    }

    public MyPageRight PageGet(PageSubType id = PageSubType.Default)
    {
        var targetID = id == PageSubType.Default ? pageID : id;
        switch (targetID)
        {
            case PageSubType.ToolsGameLink:
            {
                frmToolsGameLink ??= new PageToolsGameLink();
                return frmToolsGameLink;
            }
            case PageSubType.ToolsTest:
            {
                frmToolsTest ??= new PageToolsTest();
                return frmToolsTest;
            }
            case PageSubType.ToolsCheckIn:
            {
                frmToolsCheckIn ??= new PageToolsCheckIn();
                return frmToolsCheckIn;
            }
            case PageSubType.ToolsRecentPlay:
            {
                frmToolsRecentPlay ??= new PageToolsRecentPlay();
                return frmToolsRecentPlay;
            }
            case PageSubType.ToolsNews:
            {
                // 与首页预设 “Minecraft 信息流” 使用的实例是两个独立的实例，不能共用同一个元素
                frmToolsNews ??= new PageHomepageNewsView();
                return frmToolsNews;
            }
            default:
            {
                throw new Exception("未知的更多子页面种类：" + (int)targetID);
            }
        }
    }

    /// <summary>
    ///     获取子页面编号对应的左侧项。若没有对应的左侧项则返回 null。
    /// </summary>
    private MyListItem? GetItem(PageSubType id)
    {
        return id switch
        {
            PageSubType.ToolsGameLink => ItemGameLink,
            PageSubType.ToolsTest => ItemTest,
            PageSubType.ToolsCheckIn => ItemCheckIn,
            PageSubType.ToolsRecentPlay => ItemRecentPlay,
            PageSubType.ToolsNews => ItemNews,
            _ => null
        };
    }

    /// <summary>
    ///     切换现有页面。
    /// </summary>
    public void PageChange(PageSubType id)
    {
        if (pageID == id)
            return;
        ModAnimation.AniControlEnabled += 1;
        try
        {
            Navigator.ShowRight(PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            LogError(ex, "切换分页面失败（ID " + (int)id + "）");
            Hint.Show(Lang.Text("Tools.Error.OperationFailed"), HintType.Error);
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    #endregion
}
