using System;
using Avalonia.Controls;

namespace PCL.Shion.Controls;

public class MyVirtualizingElement<T> : Control where T : Control
{
    private readonly Func<T> _initializer;

    public MyVirtualizingElement(Func<T> initializer)
    {
        _initializer = initializer;
        this.EnableLazyLoad(() => Init());
    }

    /// <summary>
    ///     实例化此控件。
    /// </summary>
    public T Init()
    {
        var element = _initializer();
        if (Parent is not null)
        {
            if (Parent is not Panel)
                throw new Exception("MyVirtualizingElement 的父级必须是一个 Panel");
            var parentPanel = (Panel)Parent;
            var currentIndex = parentPanel.Children.IndexOf(this);
            parentPanel.Children.RemoveAt(currentIndex);
            parentPanel.Children.Insert(currentIndex, element);
        }

        return element;
    }

    public static implicit operator T(MyVirtualizingElement<T> virtualized)
    {
        return virtualized.Init();
    }
}

// 非泛型形式
public class MyVirtualizingElement : Control
{
    private readonly Func<Control> _initializer;

    public MyVirtualizingElement(Func<Control> initializer)
    {
        _initializer = initializer;
        this.EnableLazyLoad(() => Init());
    }

    /// <summary>
    ///     实例化此控件。
    /// </summary>
    public Control Init()
    {
        var element = _initializer();
        if (Parent is not null)
        {
            if (Parent is not Panel)
                throw new Exception("MyVirtualizingElement 的父级必须是一个 Panel");
            var parentPanel = (Panel)Parent;
            var currentIndex = parentPanel.Children.IndexOf(this);
            parentPanel.Children.RemoveAt(currentIndex);
            parentPanel.Children.Insert(currentIndex, element);
        }

        return element;
    }

    /// <summary>
    ///     获取实例化后的控件。
    ///     如果该控件没有实例化，则会立即实例化。
    ///     如果类型错误，则返回原值。
    /// </summary>
    public static Control TryInit(Control element)
    {
        if (IsInstanceOfGenericType(typeof(MyVirtualizingElement<>), element))
        {
            var method = element.GetType().GetMethod("Init", Type.EmptyTypes);
            return (Control)method!.Invoke(element, null)!;
        }
        return element is MyVirtualizingElement ? ((MyVirtualizingElement)element).Init() : element;
    }

    private static bool IsInstanceOfGenericType(Type genericType, object? obj)
    {
        if (obj is null)
            return false;
        var t = obj.GetType();
        while (t is not null)
        {
            if (t.IsGenericType && ReferenceEquals(t.GetGenericTypeDefinition(), genericType))
                return true;
            t = t.BaseType;
        }

        return false;
    }
}
