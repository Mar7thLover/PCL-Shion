using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Shion.Localization;
using PCL.Shion.Pages;
using PCL.Shion.Pages.Download;
using PCL.Shion.Pages.Homepage;
using PCL.Shion.Pages.Instance;
using PCL.Shion.Pages.Instance.Saves;
using PCL.Shion.Pages.Launch;
using PCL.Shion.Pages.Log;
using PCL.Shion.Pages.Select;
using PCL.Shion.Pages.Setup;
using PCL.Shion.Pages.Speed;
using PCL.Shion.Pages.Tools;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Navigation;

/// <summary>
///     主页面导航。对应 WPF FormMain 中切换页面的部分。
/// </summary>
public static class Navigator
{
    // 页面状态存储

    /// <summary>
    ///     当前的主页面。
    /// </summary>
    private static PageStackData pageCurrent = PageType.Launch;

    /// <summary>
    ///     上一个主页面。
    /// </summary>
    private static PageStackData pageLast = PageType.Launch;

    /// <summary>
    ///     当前的子页面。
    /// </summary>
    private static PageSubType pageCurrentSub = PageSubType.Default;

    /// <summary>
    ///     上层页面的编号堆栈，用于返回。
    /// </summary>
    private static readonly List<PageStackData> pageStack = new();

    /// <summary>
    ///     左侧页面实例缓存。与 WPF 相同，每个主页面只创建一次。
    /// </summary>
    private static readonly Dictionary<PageType, MyPageLeft> pageLeftCache = new();

    private static MyPageLeft? pageLeft;
    private static MyPageRight? pageRight;

    // 引发实际页面切换的入口
    private static bool isChangingPage;

    // 正在由 Go 触发的切换中，此时 ShowRight 只记录目标页面，由 PageChangeActual 统一处理
    private static bool isInPageChange;

    private static MyPageRight? pendingRight;

    // 首次导航直接显示页面，不播放切换动画（对应 WPF FormMain 构造函数中的初始化）
    private static bool isStarted;

    private static IPageHost? pageHost;

    /// <summary>
    ///     当前的主页面。
    /// </summary>
    public static PageType Current => pageCurrent.page;

    /// <summary>
    ///     上一个主页面。
    /// </summary>
    public static PageType Last => pageLast.page;

    /// <summary>
    ///     当前的子页面。
    /// </summary>
    public static PageSubType CurrentSub => pageCurrentSub;

    /// <summary>
    ///     左侧页面容器中当前的页面。
    /// </summary>
    public static MyPageLeft? PageLeft => pageLeft;

    /// <summary>
    ///     右侧页面容器中当前的页面。
    /// </summary>
    public static MyPageRight? PageRight => pageRight;

    /// <summary>
    ///     主页面改变后触发。
    /// </summary>
    public static event Action<PageType>? Changed;

    /// <summary>
    ///     设置页面宿主。主窗口创建后调用；不调用时会从 <see cref="MainWindow.Current" /> 获取。
    /// </summary>
    internal static void Attach(IPageHost host)
    {
        pageHost = host;
    }

    private static IPageHost? Host => pageHost ?? MainWindow.Current as IPageHost;

    /// <summary>
    ///     切换页面，并引起对应选择 UI 的改变。
    /// </summary>
    public static void Go(PageType type, PageSubType sub = PageSubType.Default)
    {
        Go((PageStackData)type, sub);
    }

    /// <summary>
    ///     切换页面，并引起对应选择 UI 的改变。
    /// </summary>
    public static void Go(PageStackData stack, PageSubType sub = PageSubType.Default)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Go(stack, sub));
            return;
        }

        if (isChangingPage)
            return;

        var left = GetPageLeft(stack.page);

        if (!isStarted)
        {
            StartPage(stack, sub, left);
            return;
        }

        if (IsMainPage(stack.page))
        {
            // 切换到主页面
            PageChangeExit();
            isChangingPage = true; // 防止下面的勾选直接触发了 PageChangeActual
            try
            {
                Host?.SetTitleTabChecked(stack.page, true, IsMainPage(pageCurrent.page));
            }
            finally
            {
                isChangingPage = false;
            }
        }

        PageChangeActual(stack, sub, left);
    }

    /// <summary>
    ///     切换右侧页面。由左侧页面在 <see cref="MyPageLeft.OnNavigated" /> 中调用；做法与 WPF 左侧页面的 PageChange 相同。
    ///     若在导航之外调用（例如左侧页面没有右侧内容，导航完成后再指定右侧页面），则立即切换右侧页面。
    /// </summary>
    public static void ShowRight(MyPageRight page)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowRight(page));
            return;
        }

        if (isInPageChange)
        {
            // 正在切换主页面，由 PageChangeActual 一并处理左右两侧
            pendingRight = page;
            return;
        }

        if (ReferenceEquals(page, pageRight))
            return;

        PageChangeRun(page);
        Changed?.Invoke(pageCurrent.page);
    }

    /// <summary>
    ///     通过点击返回按钮或手动触发返回来改变页面。
    /// </summary>
    public static void Back()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Back);
            return;
        }

        if (pageStack.Any())
        {
            var target = pageStack[0];
            PageChangeActual(target, PageSubType.Default, GetPageLeft(target.page));
        }
        else
        {
            Go(PageType.Launch);
        }
    }

    /// <summary>
    ///     刷新子页面的名称。页面内容改变后可调用。
    /// </summary>
    public static void RefreshTitle()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshTitle);
            return;
        }

        var name = PageNameGet(pageCurrent, pageLeft, pageRight);
        if (!string.IsNullOrEmpty(name) && Host?.LabTitleInner is { } lab)
            lab.Text = name;
    }

    // 首次导航
    private static void StartPage(PageStackData stack, PageSubType sub, MyPageLeft left)
    {
        isStarted = true;
        var previous = pageCurrent;
        pageLast = previous;
        pageCurrent = stack;
        pageCurrentSub = sub;

        pendingRight = null;
        isInPageChange = true;
        try
        {
            left.OnNavigated(sub);
        }
        finally
        {
            isInPageChange = false;
        }

        var right = pendingRight;
        pendingRight = null;
        pageLeft = left;
        pageRight = right;

        var host = Host;
        if (host is not null)
        {
            host.PageLeft = left;
            host.PageRight = right;
            if (right is not null)
                right.PageState = MyPageRight.PageStates.ContentStay;
            host.SetTitleTabChecked(stack.page, false, false);
            host.RefreshExtraButtons();
        }

        Changed?.Invoke(stack.page);
    }

    // 实际处理页面切换
    /// <summary>
    ///     切换现有页面的实际方法。与 WPF 的 PageChangeActual 对应。
    /// </summary>
    private static void PageChangeActual(PageStackData stack, PageSubType subType, MyPageLeft left)
    {
        if (pageCurrent.page == stack.page && pageCurrentSub == subType)
            return;

        ModAnimation.AniControlEnabled += 1;
        try
        {
            #region 实际更改页面框架 UI

            var previous = pageCurrent;
            pageLast = pageCurrent;
            pageCurrent = stack;
            pageCurrentSub = subType;

            pendingRight = null;
            isInPageChange = true;
            try
            {
                // 左侧页面自行决定右侧页面，与 WPF 的 PageGet 相同
                left.OnNavigated(subType);
            }
            finally
            {
                isInPageChange = false;
            }

            var right = pendingRight;
            pendingRight = null;

            #endregion

            #region 子页面处理

            var pageName = PageNameGet(stack, left, right);
            if (string.IsNullOrEmpty(pageName))
            {
                // 即将切换到一个顶级页面
                PageChangeExit();
            }
            else if (pageStack.Any())
            {
                // 子页面 → 另一个子页面，更新
                if (Host?.LabTitleInner is { } lab)
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(lab, -lab.Opacity, 130),
                            ModAnimation.AaCode(() => lab.Text = pageName, after: true),
                            ModAnimation.AaOpacity(lab, 1d, 150, 30)
                        }, "FrmMain Titlebar SubLayer");
                if (pageStack.Contains(stack))
                    // 返回到更上层的子页面
                    while (pageStack.Contains(stack))
                        pageStack.RemoveAt(0);
                else
                    // 进入更深层的子页面
                    pageStack.Insert(0, previous);
            }
            else
            {
                // 主页面 → 子页面，进入
                var panTitleMain = Host?.PanTitleMain;
                var panTitleInner = Host?.PanTitleInner;
                if (panTitleMain is not null && panTitleInner is not null)
                {
                    panTitleInner.IsVisible = true;
                    panTitleMain.IsHitTestVisible = false;
                    panTitleInner.IsHitTestVisible = true;
                    if (Host?.LabTitleInner is { } lab)
                        lab.Text = pageName;
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(panTitleMain, -panTitleMain.Opacity, 150),
                            ModAnimation.AaX(panTitleMain, 12d - panTitleMain.Margin.Left, 150,
                                ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                            ModAnimation.AaOpacity(panTitleInner, 1d - panTitleInner.Opacity, 150, 200),
                            ModAnimation.AaX(panTitleInner, -panTitleInner.Margin.Left, 350, 200,
                                new ModAnimation.AniEaseOutBack()),
                            ModAnimation.AaCode(() => panTitleMain.IsVisible = false, after: true)
                        }, "FrmMain Titlebar FirstLayer");
                }

                pageStack.Insert(0, previous);
            }

            #endregion

            PageChangeAnim(left, right);

            #region 设置为最新状态

            Host?.RefreshExtraButtons();

            #endregion

            Changed?.Invoke(stack.page);
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private static void PageChangeAnim(MyPageLeft targetLeft, MyPageRight? targetRight)
    {
        ModAnimation.AniStop("FrmMain LeftChange");
        ModAnimation.AniStop("PageLeft PageChange"); // 停止左边栏变更导致的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        ModAnimation.AniControlEnabled += 1;
        // 清除新页面关联性
        DetachFromParent(targetLeft);
        if (targetRight is not null)
            DetachFromParent(targetRight);
        var oldLeft = pageLeft;
        var oldRight = pageRight;
        pageLeft = targetLeft;
        pageRight = targetRight;
        // 触发页面通用动画
        oldLeft?.TriggerHideAnimation();
        oldRight?.PageOnExit();
        ModAnimation.AniControlEnabled -= 1;
        // 执行动画
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaCode(() =>
                {
                    ModAnimation.AniControlEnabled += 1;
                    // 把新页面添加进容器
                    var host = Host;
                    if (host is not null)
                    {
                        host.PageLeft = pageLeft;
                        pageLeft!.Opacity = 0d;
                    }

                    ModAnimation.AniControlEnabled -= 1;
                    Dispatcher.UIThread.Post(() =>
                    {
                        var pageHostNow = Host;
                        if (pageHostNow is not null)
                            pageHostNow.ResizePageLeft(pageHostNow.PageLeftWidth);
                    }, DispatcherPriority.Loaded);
                }, 110),
                ModAnimation.AaCode(() =>
                {
                    // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                    pageLeft!.Opacity = 1d;
                    pageLeft.TriggerShowAnimation();
                }, 30, true)
            }, "FrmMain PageChangeLeft");
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaCode(() =>
                {
                    ModAnimation.AniControlEnabled += 1;
                    // 把新页面添加进容器
                    oldRight?.PageOnForceExit();
                    var host = Host;
                    if (host is not null)
                    {
                        host.PageRight = pageRight;
                        if (pageRight is not null)
                            pageRight.Opacity = 0d;
                    }

                    ModAnimation.AniControlEnabled -= 1;
                    Dispatcher.UIThread.Post(() => Host?.RefreshBackButton(), DispatcherPriority.Loaded);
                }, 110),
                ModAnimation.AaCode(() =>
                {
                    // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                    if (pageRight is not null)
                    {
                        pageRight.Opacity = 1d;
                        pageRight.PageOnEnter();
                    }
                }, 30, true)
            }, "FrmMain PageChangeRight");
    }

    // 切换右侧页面，对应 WPF 左侧页面的 PageChangeRun
    private static void PageChangeRun(MyPageRight target)
    {
        ModAnimation.AniStop("FrmMain PageChangeRight"); // 停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        DetachFromParent(target);
        var oldRight = pageRight;
        pageRight = target;
        oldRight?.PageOnExit();
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaCode(() =>
                {
                    oldRight?.PageOnForceExit();
                    var host = Host;
                    if (host is not null)
                        host.PageRight = pageRight;
                    if (pageRight is not null)
                        pageRight.Opacity = 0d;
                }, 130),
                ModAnimation.AaCode(() =>
                {
                    // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                    if (pageRight is not null)
                    {
                        pageRight.Opacity = 1d;
                        pageRight.PageOnEnter();
                    }
                }, 30, true)
            }, "PageLeft PageChange");
    }

    /// <summary>
    ///     退出子界面。
    /// </summary>
    private static void PageChangeExit()
    {
        if (pageStack.Any())
        {
            // 子页面 → 主页面，退出
            var panTitleMain = Host?.PanTitleMain;
            var panTitleInner = Host?.PanTitleInner;
            if (panTitleMain is not null && panTitleInner is not null)
            {
                panTitleMain.IsVisible = true;
                panTitleMain.IsHitTestVisible = true;
                panTitleInner.IsHitTestVisible = false;
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(panTitleInner, -panTitleInner.Opacity, 150),
                        ModAnimation.AaX(panTitleInner, -18 - panTitleInner.Margin.Left, 150,
                            ease: new ModAnimation.AniEaseInFluent()),
                        ModAnimation.AaOpacity(panTitleMain, 1d - panTitleMain.Opacity, 150, 200),
                        ModAnimation.AaX(panTitleMain, -panTitleMain.Margin.Left, 350, 200,
                            new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaCode(() => panTitleInner.IsVisible = false, after: true)
                    }, "FrmMain Titlebar FirstLayer");
            }

            pageStack.Clear();
        }
        // 主页面 → 主页面，无事发生
    }

    /// <summary>
    ///     获取次级页面的名称。若并非次级页面则返回空字符串，故可以以此判断是否为次级页面。
    /// </summary>
    private static string PageNameGet(PageStackData stack, MyPageLeft? left, MyPageRight? right)
    {
        switch (stack.page)
        {
            case PageType.InstanceSelect:
            {
                return left?.Title ?? Lang.Text("Main.Title.InstanceSelect");
            }
            case PageType.TaskManager:
            {
                return left?.Title ?? Lang.Text("Main.Title.TaskManager");
            }
            case PageType.GameLog:
            {
                return left?.Title ?? Lang.Text("Main.Title.GameLog");
            }
            case PageType.InstanceSetup:
            {
                return left?.Title ??
                       Lang.Text("Main.Title.InstanceSetup", Lang.Text("Common.State.Unknown"));
            }
            case PageType.CompDetail:
            {
                return right?.Title ?? left?.Title ??
                    Lang.Text("Main.Title.ResourceDownload", Lang.Text("Common.State.Unknown"));
            }
            case PageType.HelpDetail:
            {
                return right?.Title ?? left?.Title ?? "";
            }
            case PageType.NewsDetail:
            {
                return right?.Title ?? left?.Title ?? Lang.Text("Main.Title.NewsDetail");
            }
            case PageType.VersionSaves:
            {
                return right?.Title ?? left?.Title ??
                    Lang.Text("Main.Title.SaveManagement", Lang.Text("Common.State.Unknown"));
            }

            default:
            {
                return "";
            }
        }
    }

    private static bool IsMainPage(PageType type)
    {
        return type is PageType.Launch or PageType.Download or PageType.Setup or PageType.Tools;
    }

    /// <summary>
    ///     获取主页面对应的左侧页面实例，不存在时创建。
    /// </summary>
    private static MyPageLeft GetPageLeft(PageType type)
    {
        if (!pageLeftCache.TryGetValue(type, out var left))
        {
            left = CreatePageLeft(type);
            pageLeftCache[type] = left;
        }

        return left;
    }

    private static MyPageLeft CreatePageLeft(PageType type)
    {
        switch (type)
        {
            case PageType.Launch:
                return new PageLaunchLeft();
            case PageType.Download:
                return new PageDownloadLeft();
            case PageType.Setup:
                return new PageSetupLeft();
            case PageType.Tools:
                return new PageToolsLeft();
            case PageType.InstanceSelect:
                return new PageSelectLeft();
            case PageType.TaskManager:
                return new PageSpeedLeft();
            case PageType.InstanceSetup:
                return new PageInstanceLeft();
            case PageType.GameLog:
                return new PageLogLeft();
            case PageType.VersionSaves:
                return new PageInstanceSavesLeft();
            case PageType.NewsDetail:
                return new PageNewsDetailLeft();
            default:
                // CompDetail、HelpDetail 与 WPF 相同，左侧为空的 MyPageLeft，右侧由调用方通过 ShowRight 指定
                return new MyPageLeft();
        }
    }

    /// <summary>
    ///     将控件从现有的父容器中摘下，避免重复设置内容时引发视觉树冲突。
    /// </summary>
    internal static void DetachFromParent(Control control)
    {
        switch (control.Parent)
        {
            case ContentControl contentControl:
                contentControl.Content = null;
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case Panel panel:
                panel.Children.Remove(control);
                break;
        }
    }
}
