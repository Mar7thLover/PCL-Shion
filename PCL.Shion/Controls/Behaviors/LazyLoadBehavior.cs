using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

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
        // Avalonia 中判断控件是否进入滚动区域的标准方式是 EffectiveViewportChanged，
        // 按 WPF 的做法在 LayoutUpdated 中手动计算交集时，占位元素始终不会被实例化
        element.EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    internal void Detach()
    {
        if (_associatedObject is not null)
            _associatedObject.EffectiveViewportChanged -= OnEffectiveViewportChanged;
        _associatedObject = null;
    }

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var element = _associatedObject;
        if (element is null || element.Bounds.Width < double.Epsilon || !element.IsVisible)
            return;

        // EffectiveViewport 以元素自身为坐标系，与元素区域相交即表示已进入可视区域
        if (!e.EffectiveViewport.Intersects(new Rect(element.Bounds.Size)))
            return;

        // 仅执行一次；实例化会把本元素从父级中移除，因此先解除订阅
        Detach();
        Dispatcher.UIThread.Post(() => Action?.Invoke());
    }
}
