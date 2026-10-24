using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PCL.Shion.Navigation;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public class MyScrollViewer : ScrollViewer
{
    private const double WheelDeltaPerNotch = 120d;

    private double realOffset;

    public MyScrollBar? scrollBar;

    public MyScrollViewer()
    {
        AddHandler(PointerWheelChangedEvent, MyScrollViewer_PreviewMouseWheel, RoutingStrategies.Tunnel);
        ScrollChanged += MyScrollViewer_ScrollChanged;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty)
                RefreshBackButton();
        };
        AddHandler(GotFocusEvent, (_, e) =>
        {
            if (e.Source is MySlider)
                e.Handled = true; // #3854，阻止获得焦点时自动滚动
        }, RoutingStrategies.Tunnel);
    }

    public double DeltaMult { get; set; } = 1d;

    public double VerticalOffset => Offset.Y;

    public double ExtentHeight => Extent.Height;

    public double ViewportHeight => Viewport.Height;

    public double ActualHeight => Bounds.Height;

    public double ScrollableHeight => Math.Max(0d, ExtentHeight - ViewportHeight);

    public ScrollBarVisibility ComputedVerticalScrollBarVisibility => VerticalScrollBarVisibility switch
    {
        ScrollBarVisibility.Visible => ScrollBarVisibility.Visible,
        ScrollBarVisibility.Auto when ScrollableHeight > 0d => ScrollBarVisibility.Visible,
        _ => ScrollBarVisibility.Hidden
    };

    public void ScrollToVerticalOffset(double offset)
    {
        Offset = new Vector(Offset.X, offset);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        scrollBar = e.NameScope.Find("PART_VerticalScrollBar") as MyScrollBar;
    }

    private void MyScrollViewer_PreviewMouseWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0d || ScrollableHeight <= 0d)
            return;

        var src = e.Source;
        if (Content is Control element && element.TemplatedParent is null)
        {
            switch (src)
            {
                case ComboBox { IsDropDownOpen: true }:
                case TextBox { AcceptsReturn: true }:
                case ComboBoxItem:
                case CheckBox:
                    return;
            }
        }

        e.Handled = true;
        PerformVerticalOffsetDelta(-e.Delta.Y * WheelDeltaPerNotch);

        DismissTooltips();
    }

    public void PerformVerticalOffsetDelta(double delta)
    {
        ModAnimation.AniStart(ModAnimation.AaDouble(animDelta =>
        {
            realOffset = MathClamp(realOffset + (double)animDelta, 0d, ExtentHeight - ActualHeight);
            ScrollToVerticalOffset(realOffset);
        }, delta * DeltaMult, 300, 0, new ModAnimation.AniEaseOutFluent((ModAnimation.AniEasePower)6), false));
    }

    private void MyScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        realOffset = VerticalOffset;
        if (e.OffsetDelta.Y != 0d || e.ViewportDelta.Y != 0d)
            RefreshBackButton();
    }

    private static void RefreshBackButton()
    {
        if (MainWindow.Current is IPageHost host)
            host.RefreshBackButton();
    }

    private void DismissTooltips()
    {
        DismissTooltips(this);
    }

    private static void DismissTooltips(Visual root)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is Control control && ToolTip.GetIsOpen(control))
                ToolTip.SetIsOpen(control, false);
            DismissTooltips(child);
        }
    }

    private static double MathClamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}
