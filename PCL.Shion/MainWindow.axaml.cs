using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages;
using PCL.Shion.Pages.Download;
using PCL.Shion.Pages.Instance;
using PCL.Shion.Pages.Launch;
using PCL.Shion.Pages.Setup;
using PCL.Shion.Themes;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;
using PCL.Shion.ViewModels;

namespace PCL.Shion;

public partial class MainWindow : Window, IPageHost
{
    /// <summary>
    ///     标题栏帮助按钮打开的教程地址。
    /// </summary>
    private const string HelpVideoUrl = "https://www.bilibili.com/video/BV1uT4y1P7CX";

    /// <summary>
    ///     首次启动时“查看协议”打开的地址。
    /// </summary>
    private const string EulaUrl = "https://shimo.im/docs/rGrd8pY8xWkt6ryW";

    /// <summary>
    ///     是否可以通过窗口边缘拖动改变大小。
    /// </summary>
    private bool canResize = true;

    /// <summary>
    ///     是否可以向设置中储存窗口尺寸。以此避免初始化时误储存。
    /// </summary>
    private bool isSizeSaveable;

    /// <summary>
    ///     是否已经开始退出，避免重复执行退出动画。
    /// </summary>
    private bool isEnding;

    /// <summary>
    ///     退出动画结束后，真正关闭窗口时设为 true，使 <see cref="OnClosing" /> 不再拦截。
    /// </summary>
    private bool isCloseAllowed;

    /// <summary>
    ///     愚人节模式下是否已经投降。
    /// </summary>
    private bool isAprilGiveup;

    /// <summary>
    ///     定时刷新右上角附加按钮的状态。
    /// </summary>
    private readonly DispatcherTimer extraRefreshTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    /// <summary>
    ///     当前主窗口，供弹窗、提示与导航访问 PanMsg、PanHint 等宿主。
    /// </summary>
    public static MainWindow? Current { get; private set; }

    private HomeViewModel? home;

    /// <summary>
    ///     主页视图模型。WPF 版通过 DataContext 持有，Shion 中没有 MainWindowViewModel，
    ///     这里按需创建一份，供日志页读取正在运行的游戏。
    /// </summary>
    public HomeViewModel Home => home ??= new HomeViewModel(App.Services);

    /// <summary>
    ///     愚人节当天启用的彩蛋。
    /// </summary>
    public static bool IsAprilEnabled => DateTime.Now.Month == 4 && DateTime.Now.Day == 1;

    /// <summary>
    ///     开场动画使用的旋转与平移变换，对应 WPF 版 XAML 中 RootGrid 的 TransformGroup。
    /// </summary>
    private readonly RotateTransform TransformRotate = new(-4d);
    private readonly TranslateTransform TransformPos = new(0d, 60d);

    public MainWindow()
    {
        Current = this;
        Navigator.Attach(this);
        // 与 WPF FormMain 相同：配色模式或主题色变化时刷新主窗口
        ThemeService.ColorModeChanged += (_, _) => ThemeManager.ThemeRefresh();
        ThemeService.ColorThemeChanged += theme => ThemeManager.ThemeRefresh((int)theme);
        // 对应 WPF 中的 ModAnimation.AniControlEnabled 启动时的占用，由开场动画结束时释放
        ModAnimation.AniControlEnabled += 1;
        InitializeComponent();
        RootGrid.RenderTransform = new TransformGroup { Children = { TransformRotate, TransformPos } };
        Opacity = 0d;

        var settings = App.Services.Settings;
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;
        if (settings.LockWindowSize)
            RemoveResizer();
        else
            AddResizer();

        SizeChanged += (_, _) => FormMainSizeChanged();
        PanBack.SizeChanged += (_, e) => PanBackSizeChanged(e.NewSize);
        extraRefreshTimer.Tick += (_, _) => RefreshExtraButtonsTick();
    }

    #region 窗口生命周期

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        FormMainLoaded();
    }

    /// <summary>
    ///     对应 WPF 的 FormMain_Loaded：初始化附加按钮、进入首个页面、播放开场动画并检查协议。
    /// </summary>
    private void FormMainLoaded()
    {
        FormMainSizeChanged();

        BtnExtraDownload.showCheck = BtnExtraDownload_ShowCheck;
        BtnExtraBack.showCheck = BtnExtraBack_ShowCheck;
        BtnExtraApril.showCheck = BtnExtraApril_ShowCheck;
        BtnExtraShutdown.showCheck = BtnExtraShutdown_ShowCheck;
        BtnExtraLog.showCheck = BtnExtraLog_ShowCheck;
        BtnExtraApril.ShowRefresh();

        Navigator.Go(PageType.Launch);

        // 对应 WPF 的 ThemeManager.ThemeRefresh 与 ModSetup.ApplyAll：把设置应用到界面上（背景、标题栏、透明度等）
        ModSetup.ApplyAll();
        // ApplyAll 已按设置写入不透明度，开场动画是从 0 开始的增量动画，这里先归零
        Opacity = 0d;

        Topmost = false;
        isSizeSaveable = true;
        ShowWindowToTop();
        PlayShowAnimation();
        extraRefreshTimer.Start();
        _ = CheckLauncherEulaAsync();
    }

    /// <summary>
    ///     对应 WPF 的 Form Show 动画：窗口淡入并上移、旋转归位。
    /// </summary>
    private void PlayShowAnimation()
    {
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() => ModAnimation.AniControlEnabled -= 1, 50),
            // 与 WPF 相同，淡入到设置中的启动器不透明度
            ModAnimation.AaOpacity(this, SetupExtraSettings.Current.UiLauncherTransparent / 1000d + 0.4d, 250, 100),
            ModAnimation.AaDouble(i => TransformPos.Y += (double)i, -TransformPos.Y, 600,
                100, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                -TransformRotate.Angle, 500, 100, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() =>
            {
                RenderTransform = null;
                Trace.TraceInformation("[System] 窗口加载完成，尺寸：" + Width + " x " + Height);
            }, after: true)
        }, "Form Show");
        ModAnimation.AniStart();
    }

    /// <summary>
    ///     首次启动时弹出用户协议，对应 WPF 中的 EULA 提示。
    /// </summary>
    private async Task CheckLauncherEulaAsync()
    {
        var settings = App.Services.Settings;
        if (settings.LauncherEula)
            return;

        while (true)
        {
            var choice = await MsgBox.ShowAsync(
                Lang.Text("Main.Eula.Message"),
                Lang.Text("Main.Eula.Title"),
                Lang.Text("Common.Action.Agree"),
                Lang.Text("Common.Action.Decline"),
                Lang.Text("Main.Eula.View"));
            if (choice == 3)
            {
                OpenWebsite(EulaUrl);
                continue;
            }

            if (choice == 1)
            {
                settings.LauncherEula = true;
                settings.Save();
            }
            else
            {
                EndProgram();
            }

            return;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (isCloseAllowed)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        EndProgram();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (ReferenceEquals(Current, this))
            Current = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // 修复 #1938：窗口不允许最大化
        if (change.Property == WindowStateProperty && WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
    }

    /// <summary>
    ///     正常关闭程序，播放退出动画后退出。对应 WPF 的 EndProgram。
    /// </summary>
    public void EndProgram()
    {
        if (isEnding)
            return;
        isEnding = true;

        IsHitTestVisible = false;
        if (RenderTransform is null)
        {
            var transformPos = new TranslateTransform(0d, 0d);
            var transformRotate = new RotateTransform(0d);
            var transformScale = new ScaleTransform(1d, 1d);
            RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
            var group = new TransformGroup();
            group.Children.Add(transformRotate);
            group.Children.Add(transformPos);
            group.Children.Add(transformScale);
            RenderTransform = group;
            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaOpacity(this, -Opacity, 140, 40,
                    new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaDouble(i =>
                {
                    transformScale.ScaleX += (double)i;
                    transformScale.ScaleY += (double)i;
                }, 0.88d - transformScale.ScaleX, 180),
                ModAnimation.AaDouble(i => transformPos.Y += (double)i,
                    20d - transformPos.Y, 180, 0,
                    new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaDouble(i => transformRotate.Angle += (double)i,
                    0.6d - transformRotate.Angle, 180, 0,
                    new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaCode(() =>
                {
                    IsVisible = false;
                    ShowInTaskbar = false;
                }, 210),
                ModAnimation.AaCode(EndProgramForce, 230)
            }, "Form Close");
        }
        else
        {
            EndProgramForce();
        }

        Trace.TraceInformation("[System] 收到关闭指令");
    }

    /// <summary>
    ///     立即关闭窗口并退出程序。
    /// </summary>
    public void EndProgramForce()
    {
        ModAnimation.AniControlEnabled += 1;
        try
        {
            if (isSizeSaveable)
                App.Services.Settings.Save();
        }
        catch (Exception ex)
        {
            Trace.TraceError("[System] 保存设置失败：" + ex);
        }

        isCloseAllowed = true;
        Close();
    }

    /// <summary>
    ///     把窗口拖到最前面并激活。
    /// </summary>
    public void ShowWindowToTop()
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsVisible = true;
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Topmost = true; // 偶尔 Activate 失效，先置顶再取消
            Topmost = false;
            Activate();
            Trace.TraceInformation($"[System] 窗口已置顶，位置：({Position.X}, {Position.Y}), {Width} x {Height}");
        });
    }

    #endregion

    #region 窗口尺寸与边缘拖动

    /// <summary>
    ///     允许通过窗口边缘拖动改变大小。对应 WPF 的 AddResizer。
    /// </summary>
    public void AddResizer()
    {
        canResize = true;
    }

    /// <summary>
    ///     禁止通过窗口边缘拖动改变大小。对应 WPF 的 RemoveResizer。
    /// </summary>
    public void RemoveResizer()
    {
        canResize = false;
    }

    private void FormMainSizeChanged()
    {
        if (!isSizeSaveable)
            return;
        var settings = App.Services.Settings;
        settings.WindowWidth = Width;
        settings.WindowHeight = Height;
    }

    /// <summary>
    ///     对应 WPF FormMain_SizeChanged 中设置 PanMain 尺寸的部分。PanMain 左对齐，不显式设置宽度时右侧页面只会按内容收缩。
    /// </summary>
    private void PanBackSizeChanged(Size size)
    {
        PanMain.Width = size.Width;
        PanMain.Height = Math.Max(0d, size.Height - PanTitle.Height);
    }

    // 窗口边缘的热区：与 WPF 版相同，距边缘 6 像素起、宽 5 像素。
    private const double EdgeOffset = 6d;
    private const double EdgeHitWidth = 5d;

    private WindowEdge? HitTestEdge(Point point)
    {
        if (!canResize || WindowState != WindowState.Normal)
            return null;

        var size = ClientSize;
        var inLeft = point.X >= EdgeOffset && point.X <= EdgeOffset + EdgeHitWidth;
        var inRight = point.X <= size.Width - EdgeOffset && point.X >= size.Width - EdgeOffset - EdgeHitWidth;
        var inTop = point.Y >= EdgeOffset && point.Y <= EdgeOffset + EdgeHitWidth;
        var inBottom = point.Y <= size.Height - EdgeOffset && point.Y >= size.Height - EdgeOffset - EdgeHitWidth;

        if (inTop && inLeft) return WindowEdge.NorthWest;
        if (inTop && inRight) return WindowEdge.NorthEast;
        if (inBottom && inLeft) return WindowEdge.SouthWest;
        if (inBottom && inRight) return WindowEdge.SouthEast;
        if (inLeft) return WindowEdge.West;
        if (inRight) return WindowEdge.East;
        if (inTop) return WindowEdge.North;
        if (inBottom) return WindowEdge.South;
        return null;
    }

    private static Cursor GetEdgeCursor(WindowEdge edge) => edge switch
    {
        WindowEdge.NorthWest => new Cursor(StandardCursorType.TopLeftCorner),
        WindowEdge.NorthEast => new Cursor(StandardCursorType.TopRightCorner),
        WindowEdge.SouthWest => new Cursor(StandardCursorType.BottomLeftCorner),
        WindowEdge.SouthEast => new Cursor(StandardCursorType.BottomRightCorner),
        WindowEdge.West => new Cursor(StandardCursorType.LeftSide),
        WindowEdge.East => new Cursor(StandardCursorType.RightSide),
        WindowEdge.North => new Cursor(StandardCursorType.TopSide),
        _ => new Cursor(StandardCursorType.BottomSide)
    };

    #endregion

    #region 输入事件

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;

        // 调用弹窗：回车选择第一个，Esc 选择最后一个
        if (PanMsg.Children.Count > 0)
        {
            if (e.Key == Key.Enter)
            {
                GetMsgEnterAction(PanMsg.Children[0])?.Invoke();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                GetMsgEscapeAction(PanMsg.Children[0])?.Invoke();
                e.Handled = true;
                return;
            }
        }

        // 按 ESC 返回上一级
        if (e.Key == Key.Escape)
            TriggerPageBack();

        // 更改功能隐藏可见性
        if (e.Key == Key.F12)
        {
            PageSetupUI.HiddenForceShow = !PageSetupUI.HiddenForceShow;
            if (PageSetupUI.HiddenForceShow)
                Hint.Show(Lang.Text("Main.HiddenFeature.Disabled"), HintType.Success);
            else
                Hint.Show(Lang.Text("Main.HiddenFeature.Enabled"), HintType.Success);
            PageSetupUI.HiddenRefresh();
            e.Handled = true;
            return;
        }

        // 按 F5 刷新页面
        if (e.Key == Key.F5)
        {
            if (Navigator.PageLeft is IRefreshable refreshLeft)
                refreshLeft.Refresh();
            if (Navigator.PageRight is IRefreshable refreshRight)
                refreshRight.Refresh();
            e.Handled = true;
            return;
        }

        // 调用启动游戏
        if (e.Key == Key.Enter && Navigator.Current == PageType.Launch)
        {
            if (IsAprilEnabled && !isAprilGiveup)
                Hint.Show(Lang.Text("Main.April.Nope"));
            else
                (Navigator.PageLeft as PageLaunchLeft)?.LaunchButtonClick();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled)
            return;

        // 鼠标侧键返回上一级（弹窗中除外，#5513）
        var updateKind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        if (updateKind is PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton2Pressed)
        {
            if (PanMsg.Children.Count == 0)
                TriggerPageBack();
            e.Handled = true;
            return;
        }

        // 边缘拖动改变窗口大小
        if (ReferenceEquals(e.Source, RootGrid) && HitTestEdge(e.GetPosition(this)) is { } edge)
        {
            BeginResizeDrag(edge, e);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        RootGrid.Cursor = ReferenceEquals(e.Source, RootGrid) && HitTestEdge(e.GetPosition(this)) is { } edge
            ? GetEdgeCursor(edge)
            : null;
    }

    /// <summary>
    ///     标题栏和弹窗背景拖动窗口，对应 WPF 的 FormDragMove。
    /// </summary>
    private void FormDragMove(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Visual visual && ReferenceEquals(e.Source, visual) &&
            e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    /// <summary>
    ///     按 Esc 或侧键时的返回逻辑。对应 WPF 的 TriggerPageBack。
    /// </summary>
    private static void TriggerPageBack()
    {
        if (Navigator.Current == PageType.Download && Navigator.CurrentSub == PageSubType.DownloadInstall &&
            Navigator.PageRight is PageDownloadInstall { isInSelectPage: true } install)
            install.ExitSelectPage();
        else if (Navigator.Current == PageType.InstanceSetup && Navigator.CurrentSub == PageSubType.VersionInstall &&
                 Navigator.PageRight is PageInstanceInstall { isInSelectPage: true } instanceInstall)
            instanceInstall.ExitSelectPage();
        else
            Navigator.Back();
    }

    private static Action? GetMsgEnterAction(Control msg) => msg switch
    {
        MyMsgInput input => () => input.Btn1_Click(null, null),
        MyMsgSelect select => () => select.Btn1_Click(null, null),
        MyMsgText text => () => text.Btn1_Click(null, null),
        MyMsgMarkdown markdown => () => markdown.Btn1_Click(null, null),
        // TODO(shion): MyMsgLogin 尚未公开 Btn1_Click，回车暂不触发其主按钮（打开验证网址）
        MyMsgLogin => null,
        _ => null
    };

    private static Action? GetMsgEscapeAction(Control msg) => msg switch
    {
        MyMsgInput input => input.Btn2.IsVisible
            ? () => input.Btn2_Click(null, null)
            : () => input.Btn1_Click(null, null),
        MyMsgSelect select => select.Btn2.IsVisible
            ? () => select.Btn2_Click(null, null)
            : () => select.Btn1_Click(null, null),
        MyMsgText text => text.Btn3.IsVisible
            ? () => text.Btn3_Click(null, null)
            : text.Btn2.IsVisible
                ? () => text.Btn2_Click(null, null)
                : () => text.Btn1_Click(null, null),
        MyMsgMarkdown markdown => markdown.Btn3.IsVisible
            ? () => markdown.Btn3_Click(null, null)
            : markdown.Btn2.IsVisible
                ? () => markdown.Btn2_Click(null, null)
                : () => markdown.Btn1_Click(null, null),
        // TODO(shion): MyMsgLogin 尚未公开 Btn3_Click，Esc 暂不能取消设备码登录，需要在 MyMsgLogin 中公开后接入
        MyMsgLogin => null,
        _ => null
    };

    private void OpenWebsite(string url)
    {
        try
        {
            _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            Trace.TraceError("[System] 打开网页失败：" + ex);
        }
    }

    #endregion

    #region 标题栏

    private void BtnTitleClose_Click(object? sender, PointerReleasedEventArgs e)
    {
        EndProgram();
    }

    private void BtnTitleMin_Click(object? sender, PointerReleasedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnTitleHelp_Click(object? sender, PointerReleasedEventArgs e)
    {
        OpenWebsite(HelpVideoUrl);
    }

    /// <summary>
    ///     通过点击顶部页签改变页面。
    /// </summary>
    private void BtnTitleSelect_Check(MyRadioButton sender, bool raiseByMouse)
    {
        if (sender is not Control { Tag: { } tag })
            return;
        Navigator.Go((PageType)int.Parse(tag.ToString() ?? "0"));
    }

    private void BtnTitleInner_Click(object? sender, PointerReleasedEventArgs e)
    {
        Navigator.Back();
    }

    /// <summary>
    ///     标题栏的页签宽度变化时，限制左侧（图标）区域的最大宽度，防止遮挡页签。
    /// </summary>
    private void PanTitle_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // 与 WPF 相同，取标题栏左侧一列的实际宽度。不能用 PanTitleMain.Bounds 推算：首次布局时它还是 0，
        // 会把这一列的最大宽度设为 0，之后 PanTitleLeft 尺寸不再变化，本事件也不会再触发，标题徽标就一直不显示
        var columnWidth = PanTitleMain.ColumnDefinitions[0].ActualWidth;
        if (columnWidth - 30 <= 0)
            PanTitleLeft.ColumnDefinitions[0].MaxWidth = 0;
        else
            PanTitleLeft.ColumnDefinitions[0].MaxWidth = columnWidth - 30;
    }

    #endregion

    #region 页面容器

    /// <summary>
    ///     左侧页面容器的宽度变化时，同步调整背景宽度。
    /// </summary>
    private void PanMainLeft_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged)
            return;
        PanMainLeftResize(e.NewSize.Width);
    }

    /// <summary>
    ///     按新的宽度调整左侧栏背景与阴影。对应 WPF 的 PanMainLeft_Resize。
    /// </summary>
    private void PanMainLeftResize(double newWidth)
    {
        var delta = newWidth - RectLeftBackground.Width;
        if (Math.Abs(delta) > 0.1d && ModAnimation.AniControlEnabled == 0)
        {
            if (PanMain.Opacity < 0.1d)
                PanMainLeft.IsHitTestVisible = false; // 避免左边栏指向背景未能完美覆盖左边栏
            if (newWidth > 0d)
                // 宽度足够，显示
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaWidth(RectLeftBackground, newWidth - RectLeftBackground.Width, 180,
                            ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                        ModAnimation.AaOpacity(RectLeftShadow, 1d - RectLeftShadow.Opacity, 180),
                        ModAnimation.AaCode(() => PanMainLeft.IsHitTestVisible = true, 150)
                    }, "FrmMain LeftChange", true);
            else
                // 宽度不足，隐藏
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaWidth(RectLeftBackground, -RectLeftBackground.Width, 180,
                            ease: new ModAnimation.AniEaseOutFluent()),
                        ModAnimation.AaOpacity(RectLeftShadow, -RectLeftShadow.Opacity, 180),
                        ModAnimation.AaCode(() => PanMainLeft.IsHitTestVisible = true, 150)
                    }, "FrmMain LeftChange", true);
        }
        else
        {
            RectLeftBackground.Width = newWidth;
            PanMainLeft.IsHitTestVisible = true;
            ModAnimation.AniStop("FrmMain LeftChange");
        }
    }

    #endregion

    #region 附加按钮

    private bool BtnExtraDownload_ShowCheck()
    {
        return ModLoader.HasDownloadingTask() && Navigator.Current != PageType.TaskManager;
    }

    private void BtnExtraDownload_Click(object? sender, PointerReleasedEventArgs e)
    {
        Navigator.Go(PageType.TaskManager);
    }

    private bool BtnExtraBack_ShowCheck()
    {
        var realScroll = BtnExtraBack_GetRealChild();
        return realScroll is not null && realScroll.IsVisible &&
               realScroll.VerticalOffset > Height + (BtnExtraBack.Show ? 0 : 700);
    }

    /// <summary>
    ///     返回顶部。
    /// </summary>
    public void BackToTop()
    {
        var realScroll = BtnExtraBack_GetRealChild();
        if (realScroll is not null)
            realScroll.PerformVerticalOffsetDelta(-realScroll.VerticalOffset);
        else
            Hint.Show(Lang.Text("Main.Error.ScrollToTopFailed"), HintType.Error);
    }

    private void BtnExtraBack_Click(object? sender, PointerReleasedEventArgs e)
    {
        BackToTop();
    }

    private MyScrollViewer? BtnExtraBack_GetRealChild()
    {
        return Navigator.PageRight?.PanScroll;
    }

    private bool BtnExtraApril_ShowCheck()
    {
        return IsAprilEnabled && !isAprilGiveup && Navigator.Current == PageType.Launch;
    }

    /// <summary>
    ///     愚人节投降。对应 WPF 的 AprilGiveup。
    /// </summary>
    public void AprilGiveup()
    {
        if (IsAprilEnabled && !isAprilGiveup)
        {
            Hint.Show("=D", HintType.Success);
            isAprilGiveup = true;
            BtnExtraApril.ShowRefresh();
        }
    }

    private void BtnExtraApril_Click(object? sender, PointerReleasedEventArgs e)
    {
        AprilGiveup();
    }

    private bool BtnExtraShutdown_ShowCheck()
    {
        return App.Services.Runner.Running.Count > 0;
    }

    /// <summary>
    ///     强制关闭所有正在运行的 Minecraft。
    /// </summary>
    private void BtnExtraShutdown_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            foreach (var game in App.Services.Runner.Running.ToList())
                game.Kill();
            Hint.Show(Lang.Text("Main.ShutdownMinecraft.Success"), HintType.Success);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[System] 强制关闭所有 Minecraft 失败：" + ex);
            Hint.Show(Lang.Text("Main.Error.OperationFailed"), HintType.Error);
        }
    }

    private bool BtnExtraLog_ShowCheck()
    {
        return Navigator.Current != PageType.GameLog && App.Services.Runner.Running.Count > 0;
    }

    private void BtnExtraLog_Click(object? sender, PointerReleasedEventArgs e)
    {
        Navigator.Go(PageType.GameLog);
    }

    private void RefreshExtraButtonsTick()
    {
        BtnExtraDownload.ShowRefresh();
        BtnExtraBack.ShowRefresh();
        BtnExtraApril.ShowRefresh();
        BtnExtraShutdown.ShowRefresh();
        BtnExtraLog.ShowRefresh();
    }

    #endregion

    #region IPageHost

    MyPageLeft? IPageHost.PageLeft
    {
        get => PanMainLeft.Child as MyPageLeft;
        set
        {
            PanMainLeft.Child = null;
            if (value is not null)
                Navigator.DetachFromParent(value);
            PanMainLeft.Child = value;
            PanMainLeft.Background = null;
        }
    }

    MyPageRight? IPageHost.PageRight
    {
        get => PanMainRight.Child as MyPageRight;
        set
        {
            PanMainRight.Child = null;
            if (value is not null)
                Navigator.DetachFromParent(value);
            PanMainRight.Child = value;
            PanMainRight.Background = null;
        }
    }

    double IPageHost.PageLeftWidth => PanMainLeft.Bounds.Width;

    void IPageHost.ResizePageLeft(double newWidth) => PanMainLeftResize(newWidth);

    Control IPageHost.PanTitleMain => PanTitleMain;

    Control IPageHost.PanTitleInner => PanTitleInner;

    TextBlock IPageHost.LabTitleInner => LabTitleInner;

    void IPageHost.SetTitleTabChecked(PageType page, bool raiseByMouse, bool anime)
    {
        ((MyRadioButton)PanTitleSelect.Children[(int)page]).SetChecked(true, raiseByMouse, anime);
    }

    void IPageHost.RefreshExtraButtons()
    {
        BtnExtraDownload.ShowRefresh();
        BtnExtraApril.ShowRefresh();
    }

    void IPageHost.RefreshBackButton()
    {
        BtnExtraBack.ShowRefresh();
    }

    #endregion
}
