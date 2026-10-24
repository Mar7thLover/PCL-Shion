using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PCL.Shion.Localization;

/// <summary>
/// 将 XAML 绑定值按照当前展示区域性格式化为字符串。
/// 该转换器用于界面绑定中的数字、日期、时间等 <see cref="IFormattable"/> 值展示，
/// 不负责翻译文本，也不应参与配置、日志、协议、缓存键等需要稳定格式的内容生成。
/// 使用时需要通过 <c>ConverterParameter</c> 提供标准 .NET 格式字符串；如果未提供，则返回原始绑定值。
/// </summary>
public sealed class LocalizationFormatConverter : IValueConverter
{
    /// <summary>
    /// 将绑定值按照当前展示区域性格式化。
    /// </summary>
    /// <param name="value">需要格式化的绑定值。</param>
    /// <param name="targetType">绑定目标属性的类型。本转换器不依赖该参数。</param>
    /// <param name="parameter">标准 .NET 格式字符串，例如 <c>N2</c>、<c>G</c>、<c>d</c>；为空时返回原始值。</param>
    /// <param name="culture">绑定传入的区域性。本转换器不直接使用该参数，而是统一使用当前展示格式区域性。</param>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return null;
        if (parameter is not string format || string.IsNullOrWhiteSpace(format)) return value;
        return value switch
        {
            IFormattable formattable => formattable.ToString(format, Lang.Culture),
            _ => string.Format(Lang.Culture, "{0}", value)
        };
    }

    /// <summary>
    /// 不支持从格式化后的显示文本反向转换回原始值。
    /// </summary>
    /// <exception cref="NotSupportedException">始终抛出。该转换器仅用于单向显示格式化。</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
