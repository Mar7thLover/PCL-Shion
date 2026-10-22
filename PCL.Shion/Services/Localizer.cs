using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace PCL.Shion.Services;

/// <summary>
/// 界面文字查询。字符串表是嵌入资源 <c>Assets\Strings\**\*.json</c>，按当前界面语言选择：中文用 zh-CN，其余用 en-US。
/// 每个页面可以有自己的文件，命名为 <c>&lt;名称&gt;.&lt;语言&gt;.json</c>；全局文件 <c>&lt;语言&gt;.json</c> 最先加载，
/// 其余按名称顺序加载，同名的键后加载的覆盖先加载的。
/// </summary>
public static class Localizer
{
    private const string ResourcePrefix = "PCL.Shion.Strings.";

    private static readonly Dictionary<string, string> Table = Load();

    public static string Get(string key) => Table.GetValueOrDefault(key, key);

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    private static Dictionary<string, string> Load()
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en-US";
        var globalName = $"{ResourcePrefix}{language}.json";
        var assembly = typeof(Localizer).Assembly;

        var names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                           && name.EndsWith($".{language}.json", StringComparison.Ordinal))
            .OrderBy(name => name != globalName)
            .ThenBy(name => name, StringComparer.Ordinal);

        var table = new Dictionary<string, string>();
        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            foreach (var (key, value) in JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!)
                table[key] = value;
        }

        return table;
    }
}
