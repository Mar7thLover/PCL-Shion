using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace PCL.Shion.Controls;

internal static class LazyLoader
{
    public static void EnableLazyLoad(this Layoutable element, Action action)
    {
        var behavior = new LazyLoadBehavior();
        behavior.Action = action;
        behavior.Attach(element);
    }
}

public class LazyLoadBehavior : AvaloniaObject
{
    public static readonly StyledProperty<Action?> ActionProperty = AvaloniaProperty.Register<LazyLoadBehavior, Action?>(
        nameof(Action));

    public Action? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    private Layoutable? _associatedObject;

    internal void Attach(Layoutable element)
    {
        Detach();
        _associatedObject = element;
        element.LayoutUpdated += OnLayoutUpdated;
    }

    internal void Detach()
    {
        if (_associatedObject is not null)
            _associatedObject.LayoutUpdated -= OnLayoutUpdated;
        _associatedObject = null;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var element = _associatedObject;
        if (element is null || element.Bounds.Width < double.Epsilon || !element.IsVisible)
            return;

        var scrollViewer = FindParentScrollViewer(element);
        if (scrollViewer is null)
            return;

        var position = element.TranslatePoint(new Point(0d, 0d), scrollViewer);
        if (position is null)
            return;
        var elementBounds = new Rect(position.Value, element.Bounds.Size);
        var viewport = new Rect(0d, 0d, scrollViewer.Viewport.Width, scrollViewer.Viewport.Height);

        if (!viewport.Intersects(elementBounds))
            return;

        Action?.Invoke();
        // 仅执行一次
        element.LayoutUpdated -= OnLayoutUpdated;
    }

    private static ScrollViewer? FindParentScrollViewer(Visual element)
    {
        for (var current = element; current is not null; current = current.GetVisualParent())
            if (current is ScrollViewer scrollViewer)
                return scrollViewer;

        return null;
    }
}
