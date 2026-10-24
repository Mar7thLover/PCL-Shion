using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace PCL.Shion.UI.Animation;

/// <summary>
/// 补齐 WPF 的资源引用语义。
/// </summary>
/// <remarks>
/// WPF 中 SetValue 会替换属性上的资源引用；Avalonia 的 SetValue 则不会移除本地绑定（包括 DynamicResource），
/// 绑定会在资源变化或控件重新挂上逻辑树时把写入的值改回资源值。
/// </remarks>
public static class ResourceReferenceExtensions
{
    private static readonly ConditionalWeakTable<AvaloniaObject, Dictionary<AvaloniaProperty, LocalValueSource>>
        localValueSources = new();

    /// <summary>
    /// 与 WPF 的 FrameworkElement.SetResourceReference 相同：属性引用该资源并随资源变化，替换属性上原有的本地值与本地绑定。
    /// </summary>
    /// <summary>
    /// 查找画刷资源。控件尚未挂到界面树上（或正在切换页面而被移出）时只能从 Application 查找；仍找不到时返回透明画刷，而不是抛出异常。
    /// </summary>
    public static IBrush FindBrushOrTransparent(this StyledElement element, string key)
    {
        var theme = (element as IThemeVariantHost)?.ActualThemeVariant;
        if (element.TryFindResource(key, theme, out var resource) && resource is IBrush brush)
            return brush;
        if (Application.Current is { } app &&
            app.TryFindResource(key, app.ActualThemeVariant, out resource) && resource is IBrush appBrush)
            return appBrush;
        return Brushes.Transparent;
    }

    public static void SetResourceReference(this StyledElement element, AvaloniaProperty property, object key)
    {
        element.Bind(property, element.GetResourceObservable(key));
    }

    /// <summary>
    /// 与 WPF 的 SetValue 相同：写入本地值，并替换属性上原有的本地绑定（包括资源引用）。
    /// </summary>
    internal static void SetValueReplacingBinding(AvaloniaObject obj, AvaloniaProperty property, object? value)
    {
        var sources = localValueSources.GetOrCreateValue(obj);
        if (sources.TryGetValue(property, out var source) && source.IsBound)
        {
            source.Push(value);
            return;
        }

        // 新的本地绑定会释放属性上原有的本地绑定，此后由它承载写入的值
        source = new LocalValueSource(value);
        sources[property] = source;
        obj.Bind(property, source);
    }

    /// <summary>
    /// 只在写入时推送值的绑定源。被其他本地绑定替换后，Avalonia 会释放订阅，之后需要重新绑定。
    /// </summary>
    private sealed class LocalValueSource : IObservable<object?>, IDisposable
    {
        private object? value;
        private IObserver<object?>? observer;

        public LocalValueSource(object? value)
        {
            this.value = value;
        }

        public bool IsBound => observer is not null;

        public IDisposable Subscribe(IObserver<object?> newObserver)
        {
            observer = newObserver;
            newObserver.OnNext(value);
            return this;
        }

        public void Push(object? newValue)
        {
            value = newValue;
            observer?.OnNext(newValue);
        }

        public void Dispose()
        {
            observer = null;
        }
    }
}
