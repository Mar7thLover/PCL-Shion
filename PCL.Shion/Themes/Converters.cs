using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace PCL.Shion.Themes;

/// <summary>
///     对数据绑定进行加法运算，使用参数决定加数。
/// </summary>
public class AdditionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return 0;
        if (!double.TryParse(value.ToString(), out var before))
            return 0;
        var scale = 1d;
        if (parameter is not null)
            double.TryParse(parameter.ToString(), out scale);
        return before + scale;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return BindingOperations.DoNothing;
        if (!double.TryParse(value.ToString(), out var before))
            return BindingOperations.DoNothing;
        var scale = 1d;
        if (parameter is not null)
            double.TryParse(parameter.ToString(), out scale);
        if (scale == 0d)
            return BindingOperations.DoNothing;
        return before - scale;
    }
}

/// <summary>
///     对数据绑定进行乘法运算，使用参数决定乘数。
/// </summary>
public class MultiplicationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return 0;
        if (!double.TryParse(value.ToString(), out var before))
            return 0;
        var scale = 1d;
        if (parameter is not null)
            double.TryParse(parameter.ToString(), out scale);
        return before * scale;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return BindingOperations.DoNothing;
        if (!double.TryParse(value.ToString(), out var before))
            return BindingOperations.DoNothing;
        var scale = 1d;
        if (parameter is not null)
            double.TryParse(parameter.ToString(), out scale);
        if (scale == 0d)
            return BindingOperations.DoNothing;
        return before / scale;
    }
}

/// <summary>
///     将布尔值转换为可见性（Avalonia 中以 IsVisible 表示）。
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true;
    }
}

/// <summary>
///     将取反的 Boolean 绑定到可见性（Avalonia 中以 IsVisible 表示）。
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return true;
        return bool.TryParse(value.ToString(), out var boolValue) ? !boolValue : true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return false;
        return value is bool visible ? !visible : false;
    }
}

/// <summary>
///     将 Boolean 取反。
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return false;
        return bool.TryParse(value.ToString(), out var boolValue) ? !boolValue : false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return false;
        if (bool.TryParse(value.ToString(), out var result))
            return !result;
        return false;
    }
}

/// <summary>
///     将可 null 的值转换为可见性：若为 null 则隐藏，否则可见（Avalonia 中以 IsVisible 表示）。
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
