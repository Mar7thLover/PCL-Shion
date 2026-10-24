using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace PCL.Shion.Localization;

/// <summary>
/// XAML 里的本地化文字：<c>Text="{loc:L Main.Tab.Launch}"</c>。键名与 WPF 版 <c>{DynamicResource 键名}</c> 相同，
/// 语言切换后自动刷新。
/// </summary>
public sealed class LExtension : MarkupExtension
{
    public LExtension(string key) => Key = key;

    /// <summary>语言文件里的键名。</summary>
    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new ReflectionBindingExtension($"[{Key}]")
        {
            Mode = BindingMode.OneWay,
            Source = LangSource.Instance
        };
        return binding.ProvideValue(serviceProvider);
    }
}
