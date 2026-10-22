using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace PCL.Shion.Services;

/// <summary>
/// 界面文字查询。字符串表是嵌入资源 <c>Assets\Strings\*.json</c>，按当前界面语言选择：中文用 zh-CN，其余用 en-US。
/// </summary>
public static class Localizer
{
    private static readonly Dictionary<string, string> Table = Load();

    public static string Get(string key) => Table.GetValueOrDefault(key, key);

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    private static Dictionary<string, string> Load()
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en-US";
        using var stream = typeof(Localizer).Assembly.GetManifestResourceStream($"PCL.Shion.Strings.{language}.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
