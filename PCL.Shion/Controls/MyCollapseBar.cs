using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

/// <summary>
/// 轻量折叠栏：一行可点击标题 + 三角，点击切换其下内容区的显示。
/// 无卡片外观（无阴影/边框/背景），带高度折叠动画。
/// </summary>
public class MyCollapseBar : StackPanel
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MyCollapseBar, string>(nameof(Title), string.Empty);

    private const double HeaderHeight = 30d;

    private readonly int _uuid = GetUuid();
    private readonly TextBlock _titleBlock;
    private readonly Path _triangle;
    private readonly StackPanel _contentPanel;
    private (MyCard card, bool useAnimation)? _parentCardState;

    static MyCollapseBar()
    {
        TitleProperty.Changed.AddClassHandler<MyCollapseBar>((bar, e) => bar._titleBlock.Text = e.NewValue as string);
    }

    public MyCollapseBar()
    {
        Orientation = Orientation.Vertical;
        ClipToBounds = true;

        _titleBlock = new TextBlock
        {
            FontSize = 14d, FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(6d, 0d, 0d, 0d), IsHitTestVisible = false
        };
        _titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");

        _triangle = new Path
        {
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform, Height = 6d, Width = 10d, Margin = new Thickness(0d, 0d, 12d, 0d),
            IsHitTestVisible = false,
            Data = Geometry.Parse("M2,4 l-2,2 10,10 10,-10 -2,-2 -8,8 -8,-8 z"),
            RenderTransform = new RotateTransform(180d),
            RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative)
        };
        _triangle.SetResourceReference(Shape.FillProperty, "ColorBrush1");

        var header = new Grid
        {
            Height = HeaderHeight, Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand)
        };
        header.Children.Add(_titleBlock);
        header.Children.Add(_triangle);
        header.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
                IsCollapsed = !IsCollapsed;
        };

        _contentPanel = new StackPanel { Margin = new Thickness(6d, 2d, 0d, 0d) };

        Children.Add(header);
        Children.Add(_contentPanel);
    }

    /// <summary>标题文本。</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>内容区，调用方向其 Children 添加要折叠的内容。</summary>
    public StackPanel ContentPanel => _contentPanel;

    /// <summary>开合状态实际改变时触发。</summary>
    public event EventHandler? Toggled;

    /// <summary>是否收起。</summary>
    public bool IsCollapsed
    {
        get;
        set
        {
            if (field == value) return;
            field = value;

            var target = value ? 0d : 180d;
            if (IsLoaded)
                ModAnimation.AniStart(
                    ModAnimation.AaRotateTransform(_triangle,
                        target - ((RotateTransform)_triangle.RenderTransform!).Angle, 250,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                    "MyCollapseBar " + _uuid, true);
            else
                ((RotateTransform)_triangle.RenderTransform!).Angle = target;

            if (IsLoaded && Bounds.Height > 0)
            {
                if (value)
                    CollapseWithAnimation();
                else
                    ExpandWithAnimation();
            }
            else
            {
                _contentPanel.IsVisible = !value;
            }

            Toggled?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CollapseWithAnimation()
    {
        ModAnimation.AniStop("MyCollapseBar Height " + _uuid);
        RestoreParentCardOnInterrupt();
        SilenceParentCard();

        var fullHeight = Bounds.Height;
        Height = fullHeight;

        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaHeight(this, HeaderHeight - fullHeight, 200,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
            ModAnimation.AaCode(() =>
            {
                _contentPanel.IsVisible = false;
                Height = double.NaN;
                RestoreParentCard();
            }, after: true)
        }, "MyCollapseBar Height " + _uuid);
    }

    private void ExpandWithAnimation()
    {
        ModAnimation.AniStop("MyCollapseBar Height " + _uuid);
        RestoreParentCardOnInterrupt();
        SilenceParentCard();

        _contentPanel.IsVisible = true;
        Height = double.NaN;
        Measure(new Size(Bounds.Width, double.PositiveInfinity));
        var fullHeight = DesiredSize.Height;
        Height = HeaderHeight;

        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaHeight(this, fullHeight - HeaderHeight, 200,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
            ModAnimation.AaCode(() =>
            {
                Height = double.NaN;
                RestoreParentCard();
            }, after: true)
        }, "MyCollapseBar Height " + _uuid);
    }

    /// <summary>若上一个动画被中断且它的 RestoreParentCard 未执行，则先恢复。</summary>
    private void RestoreParentCardOnInterrupt()
    {
        if (_parentCardState is { } s)
        {
            s.card.UseAnimation = s.useAnimation;
            _parentCardState = null;
        }
    }

    private void SilenceParentCard()
    {
        if (_parentCardState is not null)
            return;

        var current = Parent as Control;
        while (current is not null)
        {
            if (current is MyCard card)
            {
                _parentCardState = (card, card.UseAnimation);
                card.UseAnimation = false;
                return;
            }

            current = current.Parent as Control;
        }
    }

    private void RestoreParentCard()
    {
        if (_parentCardState is { } s)
        {
            s.card.UseAnimation = s.useAnimation;
            _parentCardState = null;
        }
    }

    private static int uuidSeed;

    private static int GetUuid() => Interlocked.Increment(ref uuidSeed);
}
