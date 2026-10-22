using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;

using PCL.Core.App.Localization;
namespace PCL;

public partial class PageToolsLeft
{
    private bool isLoad;
    private bool isPageSwitched; // 如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次

    public PageToolsLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;
        Loaded += PageLinkLeft_Loaded;
        Unloaded += PageOtherLeft_Unloaded;
    }

    private void PageLinkLeft_Loaded(object sender, RoutedEventArgs e)
    {
        var isHiddenPage = false;
        var hide = Config.Preference.Hide;

        if (ItemGameLink.Checked && hide.ToolsGameLink) isHiddenPage = true;
        if (ItemTest.Checked && hide.ToolsTest) isHiddenPage = true;
        if (PageSetupUI.HiddenForceShow)
            isHiddenPage = false;
        // 若页面错误，或尚未加载，则继续
        if (isLoad && !isHiddenPage)
            return;
        isLoad = true;
        // 刷新子页面隐藏情况
        PageSetupUI.HiddenRefresh();
        // 选择第一个未被禁用的子页面
        if (isPageSwitched) 
            return;
        var hideCfg = Config.Preference.Hide;
        if (!hideCfg.ToolsGameLink)
            ItemGameLink.SetChecked(true, false, false);
        else if (!hideCfg.ToolsTest)
            ItemTest.SetChecked(true, false, false);
        else
            ItemCheckIn.SetChecked(true, false, false); // 联机与百宝箱都被隐藏时，回退到不可隐藏的小工具页
    }

    private void PageOtherLeft_Unloaded(object sender, RoutedEventArgs e)
    {
        isPageSwitched = false;
    }

    public void Refresh(object sender, EventArgs e)
    {
        var button = (MyIconButton)sender;
        if (button.Tag is null)
            return;
        double id = ModBase.Val(button.Tag);
        switch (id)
        {
            case (double)FormMain.PageSubType.ToolsGameLink:
            {
                if (ModMain.frmToolsGameLink is null)
                    ModMain.frmToolsGameLink = new PageToolsGameLink();
                ModMain.frmToolsGameLink.Reload();
                ItemGameLink.Checked = true;
                break;
            }
        }
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。
    /// </summary>
    public FormMain.PageSubType pageID = FormMain.PageSubType.ToolsGameLink;

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyListItem)senderRaw;
        // 尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
        // 若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        if (sender.Tag is not null)
            PageChange((FormMain.PageSubType)ModBase.Val(sender.Tag));
    }

    public object PageGet(FormMain.PageSubType? id = null)
    {
        var targetID = id ?? pageID;
        switch (targetID)
        {
            case FormMain.PageSubType.ToolsGameLink:
            {
                if (ModMain.frmToolsGameLink is null)
                    ModMain.frmToolsGameLink = new PageToolsGameLink();
                return ModMain.frmToolsGameLink;
            }
            case FormMain.PageSubType.ToolsTest:
            {
                if (ModMain.frmToolsTest is null)
                    ModMain.frmToolsTest = new PageToolsTest();
                return ModMain.frmToolsTest;
            }
            case FormMain.PageSubType.ToolsCheckIn:
            {
                ModMain.frmToolsCheckIn ??= new PageToolsCheckIn();
                return ModMain.frmToolsCheckIn;
            }
            case FormMain.PageSubType.ToolsRecentPlay:
            {
                ModMain.frmToolsRecentPlay ??= new PageToolsRecentPlay();
                return ModMain.frmToolsRecentPlay;
            }
            case FormMain.PageSubType.ToolsNews:
            {
                // 与首页预设 “Minecraft 信息流” 使用的 frmHomepageNews 是两个独立的实例，不能共用同一个元素
                ModMain.frmToolsNews ??= new PageHomepageNewsView();
                return ModMain.frmToolsNews;
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
    private MyListItem? GetItem(FormMain.PageSubType id)
    {
        return id switch
        {
            FormMain.PageSubType.ToolsGameLink => ItemGameLink,
            FormMain.PageSubType.ToolsTest => ItemTest,
            FormMain.PageSubType.ToolsCheckIn => ItemCheckIn,
            FormMain.PageSubType.ToolsRecentPlay => ItemRecentPlay,
            FormMain.PageSubType.ToolsNews => ItemNews,
            _ => null
        };
    }

    /// <summary>
    ///     从代码中切换到工具页的指定子页面，并勾选对应的左侧项。
    ///     若当前尚未处于工具页，会一并切换主页面，因此既可单独调用，也可在 PageChange(PageType.Tools) 之后调用。
    ///     可在任意线程调用。
    /// </summary>
    public void OpenSubPage(FormMain.PageSubType id)
    {
        ModBase.RunInUi(() =>
        {
            try
            {
                var item = GetItem(id) ?? throw new ArgumentException("未知的工具子页面种类：" + (int)id);
                if (ModMain.frmMain.pageCurrent.page == FormMain.PageType.Tools)
                {
                    // 已在工具页：勾选左侧项，由 PageCheck 负责切换右侧页面
                    item.SetChecked(true, true, true);
                }
                else
                {
                    // 尚未在工具页：先记下目标子页面并勾选左侧项（此时 PageCheck 会因编号相同而直接返回），再切换主页面，这样只会发生一次切换
                    pageID = id;
                    isPageSwitched = true; // 避免首次 Loaded 时又被重置回默认的子页面
                    item.SetChecked(true, false, false);
                    ModMain.frmMain.PageChange(FormMain.PageType.Tools);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    $"打开工具子页面失败（ID {(int)id}）",
                    ModBase.LogLevel.Feedback,
                    userSummary: Lang.Text("Tools.Error.OperationFailed"));
            }
        });
    }

    /// <summary>
    ///     切换现有页面。
    /// </summary>
    public void PageChange(FormMain.PageSubType id)
    {
        if (pageID == id)
            return;
        ModAnimation.AniControlEnabled += 1;
        isPageSwitched = true;
        try
        {
            PageChangeRun((MyPageRight)PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                $"切换分页面失败（ID {(int)id}）",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Error.OperationFailed"));
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private static void PageChangeRun(MyPageRight target)
    {
        ModAnimation.AniStop("FrmMain PageChangeRight"); // 停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        if (target.Parent is not null)
            target.SetValue(ContentPresenter.ContentProperty, null);
        ModMain.frmMain.pageRight = target;
        ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnExit();
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnForceExit();
                ModMain.frmMain.PanMainRight.Child = ModMain.frmMain.pageRight;
                ModMain.frmMain.pageRight.Opacity = 0d;
            }, 130),
            ModAnimation.AaCode(() =>
            {
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                ModMain.frmMain.pageRight.Opacity = 1d;
                ModMain.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }

    #endregion
}
