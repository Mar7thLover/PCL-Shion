using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public class MyCard : AnimatedBackgroundGrid
{
    // 动画
    private const double dropShadowIdleOpacity = 0.07d;
    private const double dropShadowHoverOpacity = 0.4d;

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MyCard, string>(nameof(Title), "");

    private readonly Border mainBorder;

    // 控件
    private readonly Grid mainGrid;
    private TextBlock? _MainTextBlock;
    private bool isLoad;

    // UI 建立
    public MyCard() : base(Border.BackgroundProperty)
    {
        MainChrome = new MyDropShadow
        {
            Margin = new Thickness(-3, -3, -3, -3 - GetPixelSize(this)), ShadowRadius = 3d,
            Opacity = dropShadowIdleOpacity, CornerRadius = new CornerRadius(5d)
        };
        MainChrome.SetResourceReference(MyDropShadow.ColorProperty, "ColorObject1");
        Children.Insert(0, MainChrome);
        mainBorder = new Border { CornerRadius = new CornerRadius(5d), IsHitTestVisible = false };
        Children.Insert(1, mainBorder);
        mainGrid = new Grid();
        Children.Add(mainGrid);
        // 设置背景色
        this.SetResourceReference(BackgroundBrushProperty, "ColorBrushTransparentBackground");
        Loaded += (_, _) => Init();
        PointerEntered += MyCard_MouseEnter;
        PointerExited += MyCard_MouseLeave;
        SizeChanged += MySizeChanged;
        PointerPressed += MyCard_MouseLeftButtonDown;
        PointerReleased += MyCard_MouseLeftButtonUp;
        PointerExited += MyCard_MouseLeave_Swap;
    }

    public MyDropShadow MainChrome { get; }

    public Control? BorderChild
    {
        get => mainBorder.Child;
        set => mainBorder.Child = value;
    }

    public TextBlock MainTextBlock
    {
        get
        {
            Init(); // 当父级触发 Loaded 时，本卡片可能尚未触发 Loaded（该事件从父级向子级调用），因此这会是 null。手动触发以确保控件已加载。
            return _MainTextBlock!;
        }
        set => _MainTextBlock = value;
    }

    public Path? MainSwap
    {
        get
        {
            Init();
            return field;
        }
        set => field = value;
    }

    // 属性
    public InlineCollection Inlines => MainTextBlock.Inlines;

    public CornerRadius CornerRadius
    {
        get => MainChrome.CornerRadius;
        set
        {
            MainChrome.CornerRadius = value;
            mainBorder.CornerRadius = value;
        }
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set
        {
            SetValue(TitleProperty, value);
            if (_MainTextBlock is not null)
                MainTextBlock.Text = value;
        }
    }

    protected override SolidColorBrush AnimatableBrush
    {
        get => (SolidColorBrush)mainBorder.Background!;
        set => mainBorder.Background = value;
    }

    protected override Control AnimatableElement => mainBorder;
    public bool HasMouseAnimation { get; set; } = true;

    private void Init()
    {
        if (isLoad)
            return;
        isLoad = true;
        MainChrome.Margin = new Thickness(-3, -3, -3, -3 - GetPixelSize(this));
        // AddHandler ThemeChanged, AddressOf _BackgroundBrushChanged '已在依赖属性中实现
        // 初次加载限定
        if (MainTextBlock is null)
        {
            MainTextBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(15d, 12d, 0d, 0d), FontWeight = FontWeight.Bold, FontSize = 13d,
                IsHitTestVisible = false
            };
            MainTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
            MainTextBlock.Bind(TextBlock.TextProperty, this.GetObservable(TitleProperty));
            mainGrid.Children.Add(MainTextBlock);
        }

        if (CanSwap || SwapControl is not null)
        {
            if (SwapControl is null && Children.Count > 3)
                SwapControl = Children[3];
            var swap = new Path
            {
                HorizontalAlignment = HorizontalAlignment.Right, Stretch = Stretch.Uniform, Height = 6d, Width = 10d,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0d, 17d, 16d, 0d),
                Data = Geometry.Parse("M2,4 l-2,2 10,10 10,-10 -2,-2 -8,8 -8,-8 z"),
                RenderTransform = new RotateTransform(180d),
                RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative)
            };
            MainSwap = swap;
            swap.SetResourceReference(Shape.FillProperty, "ColorBrush1");
            mainGrid.Children.Add(swap);
        }

        // 改变默认的折叠
        if (IsSwapped && SwapControl is not null)
        {
            MainSwap!.RenderTransform = new RotateTransform(SwapLogoRight ? 270 : 0);
            SwapControl.IsVisible = false;
            // 取消由于高度变化被迫触发的高度动画
            var rawUseAnimation = UseAnimation;
            UseAnimation = false;
            Height = SwapedHeight;
            ModAnimation.AniStop("MyCard Height " + uuid);
            isHeightAnimating = false;
            RunInUi(() => UseAnimation = rawUseAnimation, true);
        }
    }

    private static double GetPixelSize(Visual visual)
    {
        return 1d / (TopLevel.GetTopLevel(visual)?.RenderScaling ?? 1d);
    }

    private static void RunInUi(Action action, bool forceWaitUntilLoaded = false)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action, forceWaitUntilLoaded ? DispatcherPriority.Loaded : DispatcherPriority.Normal);
    }

    public void StackInstall()
    {
        var argstack = (StackPanel)SwapControl!;
        StackInstall(ref argstack, InstallMethod);
        SwapControl = argstack;
        TriggerForceResize();
    }

    public static void StackInstall(ref StackPanel stack, Action<StackPanel>? installMethod)
    {
        if (stack.Tag is null)
            return;
        try
        {
            installMethod?.Invoke(stack);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "[MyCard] InstallMethod 调用失败：{Exception}", ex);
        }

        stack.Children.Add(new Control { Height = 18d }); // 下边距，同时适应折叠
        stack.Tag = null;
    }

    private void MyCard_MouseEnter(object? sender, PointerEventArgs e)
    {
        if (!HasMouseAnimation)
            return;
        var aniList = new List<ModAnimation.AniData>();
        if (MainTextBlock is not null)
            aniList.Add(ModAnimation.AaColor(MainTextBlock, TextBlock.ForegroundProperty, "ColorBrush2", 90));
        if (MainSwap is not null)
            aniList.Add(ModAnimation.AaColor(MainSwap, Shape.FillProperty, "ColorBrush2", 90));
        aniList.AddRange(new[]
        {
            ModAnimation.AaColor(MainChrome, MyDropShadow.ColorProperty, "ColorObject4", 90),
            ModAnimation.AaOpacity(MainChrome, dropShadowHoverOpacity - MainChrome.Opacity, 90)
        });
        if (!IsAnimating)
            ModAnimation.AniStart(aniList, "MyCard Mouse " + uuid);
    }

    private void MyCard_MouseLeave(object? sender, PointerEventArgs e)
    {
        if (!HasMouseAnimation)
            return;
        var aniList = new List<ModAnimation.AniData>();
        if (MainTextBlock is not null)
            aniList.Add(ModAnimation.AaColor(MainTextBlock, TextBlock.ForegroundProperty, "ColorBrush1", 90));
        if (MainSwap is not null)
            aniList.Add(ModAnimation.AaColor(MainSwap, Shape.FillProperty, "ColorBrush1", 90));
        aniList.AddRange(new[]
        {
            ModAnimation.AaColor(MainChrome, MyDropShadow.ColorProperty, "ColorObject1", 90),
            ModAnimation.AaOpacity(MainChrome, dropShadowIdleOpacity - MainChrome.Opacity, 90)
        });
        if (!IsAnimating)
            ModAnimation.AniStart(aniList, "MyCard Mouse " + uuid);
    }

    #region 高度改变动画

    /// <summary>
    ///     是否启用高度改变动画。
    /// </summary>
    public bool UseAnimation { get; set; } = true;

    private bool isHeightAnimating;
    private double actualUsedHeight; // 回滚实际高度（例如 NaN）

    private void MySizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!UseAnimation)
            return;
        var deltaHeight = (IsSwapped ? SwapedHeight : e.NewSize.Height) - e.PreviousSize.Height;
        // 卡片的进入时动画已被页面通用切换动画替代
        if (e.PreviousSize.Height == 0d || isHeightAnimating || Math.Abs(deltaHeight) < 1d || Bounds.Height == 0d)
            return;
        StartHeightAnimation(deltaHeight, e.PreviousSize.Height, false);
    }

    /// <summary>
    ///     启动卡片高度变化的动画效果
    ///     根据变化距离的大小采用不同的动画策略：短距离使用简单缓动，长距离使用分段动画
    /// </summary>
    /// <param name="delta">高度变化量</param>
    /// <param name="previousHeight">之前的高度</param>
    /// <param name="isLoadAnimation">是否为加载动画</param>
    private void StartHeightAnimation(double delta, double previousHeight, bool isLoadAnimation)
    {
        if (isHeightAnimating || MainWindow.Current is null)
            return; // 避免 XAML 设计器出错

        var animList = new List<ModAnimation.AniData>();
        var absDelta = Math.Abs(delta);

        if (absDelta <= 800d)
        {
            // 短距离，直接使用 150ms 的缓动动画
            animList.Add(ModAnimation.AaHeight(this, delta, 150,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)));
        }
        else
        {
            var easeLength = default(int);
            int easeTime;
            int initSpeed; // 到达缓动区前的初速度
            if (delta < 0d && absDelta - easeLength > 5000d * 0.1d)
            {
                // 收回距离过长 (>0.1s)，强制以 100ms 完成匀速段，然后让减速段更长
                easeLength = 200;
                easeTime = 150;
                initSpeed = (int)Math.Round((absDelta - easeLength) / 0.1d);
            }
            else if (delta > 0d && absDelta - easeLength > 5000d * 0.6d)
            {
                // 展开距离过长 (>0.6s)，以 5000 速度展示 300ms 匀速段，剩下的距离全部归入减速段
                initSpeed = 5000;
                easeLength = (int)Math.Round(absDelta - initSpeed * 0.3d);
                easeTime = 400;
            }
            else
            {
                // 中程，匀速地快速展开（或收回）
                easeLength = 150;
                easeTime = 200;
                initSpeed = 4000;
            }

            // 匀速段
            animList.Add(ModAnimation.AaHeight(this, (absDelta - easeLength) * Math.Sign(delta),
                (int)Math.Round((absDelta - easeLength) / initSpeed * 1000d)));
            // 减速段
            animList.Add(ModAnimation.AaHeight(this, easeLength * Math.Sign(delta), easeTime,
                ease: new ModAnimation.AniEaseOutFluentWithInitial(initSpeed, easeTime / 1000d, easeLength),
                after: true));
        }

        animList.Add(ModAnimation.AaCode(() =>
        {
            isHeightAnimating = false;
            Height = actualUsedHeight;
            if (IsSwapped && SwapControl is not null)
                SwapControl.IsVisible = false;
        }, after: true));
        ModAnimation.AniStart(animList, "MyCard Height " + uuid);
        isHeightAnimating = true;
        actualUsedHeight = IsSwapped ? SwapedHeight : Height;
        Height = previousHeight;
    }

    /// <summary>
    ///     通知 MyCard，控件内容已改变，需要中断动画并瞬间更新高度。
    /// </summary>
    public void TriggerForceResize()
    {
        Height = IsSwapped ? SwapedHeight : double.NaN;
        ModAnimation.AniStop("MyCard Height " + uuid);
        isHeightAnimating = false;
    }

    #endregion

    #region 折叠

    // 若设置了 CanSwap，或 SwapControl 不为空，则判定为会进行折叠
    // 这是因为不能直接在 XAML 中设置 SwapControl
    public Control? SwapControl;
    public bool CanSwap { get; set; } = false;

    /// <summary>
    ///     数据转为列表项的转换方法
    /// </summary>
    /// <returns></returns>
    public Action<StackPanel>? InstallMethod { get; set; }

    /// <summary>
    ///     是否已被折叠。
    /// </summary>
    public bool IsSwapped
    {
        get => field;
        set
        {
            if (field == value)
                return;
            field = value;
            if (SwapControl is null)
                return;

            // 当卡片展开时，如果SwapControl是StackPanel类型，则执行安装方法
            // 这通常用于动态添加内容到折叠卡片中
            if (!IsSwapped && SwapControl is StackPanel)
            {
                var argstack = (StackPanel)SwapControl;
                StackInstall(ref argstack, InstallMethod);
                SwapControl = argstack;
            }

            // 若尚未加载，会在 Loaded 事件中触发无动画的折叠，不需要在这里进行
            if (!IsLoaded)
                return;

            // 更新控件的可见性和高度
            SwapControl.IsVisible = true;
            TriggerForceResize();

            // 根据折叠状态旋转箭头图标
            // 折叠时箭头指向右侧或向上（根据SwapLogoRight设置），展开时指向下方
            ModAnimation.AniStart(
                ModAnimation.AaRotateTransform(MainSwap!,
                    (field ? SwapLogoRight ? 270 : 0 : 180) - ((RotateTransform)MainSwap!.RenderTransform!).Angle,
                    250, ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                "MyCard Swap " + uuid, true);
        }
    }

    /// <summary>
    ///     是否已被折叠。(已过时，请使用 IsSwapped)
    /// </summary>
    [Obsolete("请使用 IsSwapped 属性，IsSwaped 存在拼写错误")]
    public bool IsSwaped
    {
        get => IsSwapped;
        set => IsSwapped = value;
    }

    public bool SwapLogoRight { get; set; } = false;
    private bool isSwapMouseDown = false; //用于触发卡片展开/折叠的 MouseDown
    private bool isCustomMouseDown = false; //用于触发自定义事件的 MouseDown
    public event PreviewSwapEventHandler? PreviewSwap;

    public delegate void PreviewSwapEventHandler(object sender, RouteEventArgs e);

    public event SwapEventHandler? Swap;

    public delegate void SwapEventHandler(object sender, RouteEventArgs e);

    public const int SwapedHeight = 40;

    private void MyCard_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;
        double pos = e.GetPosition(this).Y;
        if (!IsSwapped && (pos > (IsSwapped ? SwapedHeight : SwapedHeight - 6) || (pos == 0 && !IsPointerOver)))
            return;
        isCustomMouseDown = true;
        if (!IsSwapped && (SwapControl is null || pos > (IsSwapped ? SwapedHeight : SwapedHeight - 6) || (pos == 0 && !IsPointerOver)))
            return;
        isSwapMouseDown = true;
    }

    private void MyCard_MouseLeftButtonUp(object? sender, PointerReleasedEventArgs e)
    {
        if (!isCustomMouseDown) return;
        isCustomMouseDown = false;

        if (!isSwapMouseDown) return;
        isSwapMouseDown = false;

        double pos = e.GetPosition(this).Y;
        if (!IsSwapped && (SwapControl is null || pos > (IsSwapped ? SwapedHeight : SwapedHeight - 6) || (pos == 0 && !IsPointerOver)))
            return; // 检测点击位置；或已经不在可视树上的误判

        var e2 = new RouteEventArgs(true);
        PreviewSwap?.Invoke(this, e2);
        if (e2.handled)
        {
            isSwapMouseDown = false;
            return;
        }

        IsSwapped = !IsSwapped;
        Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "[Control] {Message}",
            (IsSwapped ? "折叠卡片" : "展开卡片") + (Title is null ? "" : "：" + Title));
        Swap?.Invoke(this, e2);
    }

    private void MyCard_MouseLeave_Swap(object? sender, PointerEventArgs e)
    {
        isSwapMouseDown = false;
    }

    #endregion
}
