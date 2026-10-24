using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Setup;

public partial class PageSetupLeft : MyPageLeft
{
    private const string ShionIssuesUrl = "https://github.com/Mar7thLover/PCL-Shion/issues";

    private static AppSettings Settings => App.Services.Settings;

    private bool isLoad;
    private bool isPageSwitched; // 如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次

    // 右侧子页面的缓存，与 WPF ModMain 中的同名字段对应
    public static PageSetupLaunch? frmSetupLaunch;
    public static PageSetupJava? frmSetupJava;
    public static PageSetupGameManage? frmSetupGameManage;
    public static PageSetupGameLink? frmSetupGameLink;
    public static PageSetupUI? frmSetupUI;
    public static PageSetupLauncherLanguage? frmSetupLauncherLanguage;
    public static PageSetupLauncherMisc? frmSetupLauncherMisc;
    public static PageSetupAbout? frmSetupAbout;
    public static PageSetupUpdate? frmSetupUpdate;
    public static PageSetupFeedback? frmSetupFeedback;
    public static PageSetupLog? frmSetupLog;

    /// <summary>
    ///     当前页面的编号。从左往右从 0 开始计算。
    /// </summary>
    public PageSubType pageID;

    public PageSetupLeft()
    {
        InitializeComponent();
        // 选择第一个未被禁用的子页面
        pageID = FirstVisiblePage();
        AnimatedControl = PanItem;
        Loaded += PageSetupLeft_Loaded;
        Unloaded += PageOtherLeft_Unloaded;
        ItemFeedback.Click += (_, _) => _ = TryFeedbackAsync();
    }

    /// <summary>
    ///     获取第一个未被隐藏的子页面。
    /// </summary>
    private static PageSubType FirstVisiblePage()
    {
        var hide = Settings;
        if (!hide.UiHiddenSetupLaunch)
            return PageSubType.SetupLaunch;
        if (!hide.UiHiddenSetupJava)
            return PageSubType.SetupJava;
        if (!hide.UiHiddenSetupGameManage)
            return PageSubType.SetupGameManage;
        if (!hide.UiHiddenSetupGameLink)
            return PageSubType.SetupGameLink;
        if (!hide.UiHiddenSetupUi)
            return PageSubType.SetupUI;
        if (!hide.UiHiddenSetupLauncherLanguage)
            return PageSubType.SetupLauncherLanguage;
        if (!hide.UiHiddenSetupLauncherMisc)
            return PageSubType.SetupLauncherMisc;
        if (!hide.UiHiddenSetupAbout)
            return PageSubType.SetupAbout;
        if (!hide.UiHiddenSetupUpdate)
            return PageSubType.SetupUpdate;
        if (!hide.UiHiddenSetupFeedback)
            return PageSubType.SetupFeedback;
        if (!hide.UiHiddenSetupLog)
            return PageSubType.SetupLog;
        return PageSubType.SetupLaunch;
    }

    private void PageSetupLeft_Loaded(object? sender, RoutedEventArgs e)
    {
        // 是否处于隐藏的子页面
        var isHiddenPage = false;
        var hide = Settings;

        if (ItemLaunch.Checked && hide.UiHiddenSetupLaunch) isHiddenPage = true;
        if (ItemJava.Checked && hide.UiHiddenSetupJava) isHiddenPage = true;
        if (ItemGameManage.Checked && hide.UiHiddenSetupGameManage) isHiddenPage = true;
        if (ItemGameLink.Checked && hide.UiHiddenSetupGameLink) isHiddenPage = true;
        if (ItemUI.Checked && hide.UiHiddenSetupUi) isHiddenPage = true;
        if (ItemLauncherLanguage.Checked && hide.UiHiddenSetupLauncherLanguage) isHiddenPage = true;
        if (ItemLauncherMisc.Checked && hide.UiHiddenSetupLauncherMisc) isHiddenPage = true;
        if (ItemAbout.Checked && hide.UiHiddenSetupAbout) isHiddenPage = true;
        if (ItemUpdate.Checked && hide.UiHiddenSetupUpdate) isHiddenPage = true;
        if (ItemFeedback.Checked && hide.UiHiddenSetupFeedback) isHiddenPage = true;
        if (ItemLog.Checked && hide.UiHiddenSetupLog) isHiddenPage = true;
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
        ItemOf(FirstVisiblePage())?.SetChecked(true, false, false);
    }

    private void PageOtherLeft_Unloaded(object? sender, RoutedEventArgs e)
    {
        isPageSwitched = false;
    }

    private MyListItem? ItemOf(PageSubType id) => id switch
    {
        PageSubType.SetupLaunch => ItemLaunch,
        PageSubType.SetupJava => ItemJava,
        PageSubType.SetupGameManage => ItemGameManage,
        PageSubType.SetupGameLink => ItemGameLink,
        PageSubType.SetupUI => ItemUI,
        PageSubType.SetupLauncherLanguage => ItemLauncherLanguage,
        PageSubType.SetupLauncherMisc => ItemLauncherMisc,
        PageSubType.SetupAbout => ItemAbout,
        PageSubType.SetupUpdate => ItemUpdate,
        PageSubType.SetupFeedback => ItemFeedback,
        PageSubType.SetupLog => ItemLog,
        _ => null
    };

    private async void Reset(object? sender, PointerReleasedEventArgs e)
    {
        switch ((PageSubType)Convert.ToInt32(((MyIconButton)sender!).Tag))
        {
            case PageSubType.SetupLaunch:
            {
                if (await ConfirmReset("Setup.Left.Reset.Launch.Message"))
                {
                    frmSetupLaunch ??= new PageSetupLaunch();
                    frmSetupLaunch.Reset();
                    ItemLaunch.Checked = true;
                }

                break;
            }
            case PageSubType.SetupUI:
            {
                if (await ConfirmReset("Setup.Left.Reset.Ui.Message"))
                {
                    frmSetupUI ??= new PageSetupUI();
                    frmSetupUI.Reset();
                    ItemUI.Checked = true;
                }

                break;
            }
            case PageSubType.SetupGameManage:
            {
                if (await ConfirmReset("Setup.Left.Reset.GameManage.Message"))
                {
                    frmSetupGameManage ??= new PageSetupGameManage();
                    frmSetupGameManage.Reset();
                    ItemGameManage.Checked = true;
                }

                break;
            }
            case PageSubType.SetupGameLink:
            {
                if (await ConfirmReset("Setup.Left.Reset.GameLink.Message"))
                {
                    frmSetupGameLink ??= new PageSetupGameLink();
                    frmSetupGameLink.Reset();
                    ItemGameLink.Checked = true;
                }

                break;
            }
            case PageSubType.SetupLauncherLanguage:
            {
                if (await ConfirmReset("Setup.Left.Reset.Language.Message"))
                {
                    frmSetupLauncherLanguage ??= new PageSetupLauncherLanguage();
                    frmSetupLauncherLanguage.Reset();
                    ItemLauncherLanguage.Checked = true;
                }

                break;
            }
            case PageSubType.SetupLauncherMisc:
            {
                if (await ConfirmReset("Setup.Left.Reset.Misc.Message"))
                {
                    frmSetupLauncherMisc ??= new PageSetupLauncherMisc();
                    frmSetupLauncherMisc.Reset();
                    ItemLauncherMisc.Checked = true;
                }

                break;
            }
        }
    }

    private static async Task<bool> ConfirmReset(string messageKey)
    {
        return await MsgBox.ShowAsync(Lang.Text(messageKey), Lang.Text("Setup.Left.Reset.Title"),
            button2: Lang.Text("Common.Action.Cancel"), isWarn: true) == 1;
    }

    /// <summary>
    ///     处理“问题反馈”边栏按钮的点击。
    /// </summary>
    internal static async Task TryFeedbackAsync()
    {
        if (!await CanFeedbackAsync(true))
            return;
        var choice = await MsgBox.ShowAsync(Lang.Text("Setup.Left.Feedback.Message"),
            Lang.Text("Setup.Left.Feedback.Title"),
            Lang.Text("Setup.Left.Feedback.SubmitNew"), Lang.Text("Setup.Left.Feedback.ViewList"),
            Lang.Text("Common.Action.Cancel"));
        switch (choice)
        {
            case 1:
            {
                await FeedbackAsync();
                break;
            }
            case 2:
            {
                SystemShell.OpenUrl(ShionIssuesUrl);
                break;
            }
        }
    }

    private static async Task<bool> CanFeedbackAsync(bool showHint)
    {
        var updates = App.Services.Updates;
        if (updates.State == UpdateState.Idle)
            await updates.CheckAsync();
        if (updates.State == UpdateState.UpToDate)
            return true;

        if (!showHint)
            return false;

        var notLatest = updates.State == UpdateState.Available;
        var choice = await MsgBox.ShowAsync(
            Lang.Text(notLatest
                ? "Setup.Feedback.Unavailable.NotLatest.Message"
                : "Setup.Feedback.Unavailable.CheckFailed.Message"),
            Lang.Text("Setup.Feedback.Unavailable.Title"),
            Lang.Text(notLatest
                ? "Setup.Feedback.Unavailable.NotLatest.Action"
                : "Setup.Feedback.Unavailable.CheckFailed.Action"),
            Lang.Text("Common.Action.Cancel"));
        if (choice == 1)
            Navigator.Go(PageType.Setup, PageSubType.SetupUpdate);

        return false;
    }

    private static async Task FeedbackAsync()
    {
        var currentDate = DateTime.Now.ToString("yyyy-M-dd", CultureInfo.InvariantCulture);
        var openLog = await MsgBox.ShowAsync(
            Lang.Text("Setup.Feedback.Reminder.Message", currentDate),
            Lang.Text("Setup.Feedback.Reminder.Title"),
            Lang.Text("Common.Action.OpenFolder"),
            Lang.Text("Setup.Feedback.Reminder.NotNeeded")) == 1;
        if (openLog)
            SystemShell.OpenFolder(Path.Combine(AppContext.BaseDirectory, "PCL", "Log"));
        SystemShell.OpenUrl(ShionIssuesUrl);
    }

    private void Refresh(object? sender, PointerReleasedEventArgs e) // 由边栏按钮匿名调用
    {
        switch ((PageSubType)Convert.ToInt32(((MyIconButton)sender!).Tag))
        {
            case PageSubType.SetupFeedback:
            {
                frmSetupFeedback?.Loader.Start(isForceRestart: true);
                ItemFeedback.Checked = true;
                break;
            }
            case PageSubType.SetupJava:
            {
                frmSetupJava?.loader.Start(isForceRestart: true);
                ItemJava.Checked = true;
                break;
            }
        }

        HintService.Hint(Lang.Text("Setup.Left.Refreshing"), HintType.Info, false);
    }

    #region 页面切换

    /// <summary>
    ///     导航到设置页面时调用，<paramref name="sub" /> 为要显示的子页面；默认值表示保持当前子页面。
    /// </summary>
    public override void OnNavigated(PageSubType sub)
    {
        var target = sub == PageSubType.Default ? pageID : sub;
        pageID = target;
        Navigator.ShowRight(PageGet(target));
        // 勾选对应的边栏项，其 Check 事件会调用 PageCheck，此时页面已一致，不会重复切换
        ItemOf(target)?.SetChecked(true, false, false);
    }

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object sender, RouteEventArgs e)
    {
        var item = (MyListItem)sender;
        // 尚未初始化控件属性时，Tag 为空，会跳过切换
        if (item.Tag is not null)
            PageChange((PageSubType)Convert.ToInt32(item.Tag));
    }

    /// <summary>
    ///     获取指定的右侧页面，不存在时创建。
    /// </summary>
    public MyPageRight PageGet(PageSubType id)
    {
        switch (id)
        {
            case PageSubType.SetupLaunch:
                return frmSetupLaunch ??= new PageSetupLaunch();
            case PageSubType.SetupUI:
                return frmSetupUI ??= new PageSetupUI();
            case PageSubType.SetupGameManage:
                return frmSetupGameManage ??= new PageSetupGameManage();
            case PageSubType.SetupUpdate:
                return frmSetupUpdate ??= new PageSetupUpdate();
            case PageSubType.SetupAbout:
                return frmSetupAbout ??= new PageSetupAbout();
            case PageSubType.SetupLog:
                return frmSetupLog ??= new PageSetupLog();
            case PageSubType.SetupFeedback:
                return frmSetupFeedback ??= new PageSetupFeedback();
            case PageSubType.SetupGameLink:
                return frmSetupGameLink ??= new PageSetupGameLink();
            case PageSubType.SetupLauncherLanguage:
                return frmSetupLauncherLanguage ??= new PageSetupLauncherLanguage();
            case PageSubType.SetupLauncherMisc:
                return frmSetupLauncherMisc ??= new PageSetupLauncherMisc();
            case PageSubType.SetupJava:
                return frmSetupJava ??= new PageSetupJava();
            default:
                throw new Exception("未知的设置子页面种类：" + (int)id);
        }
    }

    /// <summary>
    ///     切换现有页面。
    /// </summary>
    public void PageChange(PageSubType id)
    {
        if (pageID == id)
            return;
        ModAnimation.AniControlEnabled += 1;
        isPageSwitched = true;
        try
        {
            Navigator.ShowRight(PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"切换分页面失败（ID {(int)id}）：{ex}");
            Hint.Show(Lang.Text("Setup.Error.OperationFailed"), HintType.Error);
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    #endregion
}
