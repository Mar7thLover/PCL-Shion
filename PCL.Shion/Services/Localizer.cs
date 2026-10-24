using System.Collections.Generic;
using System.Text.Json;
using PCL.Shion.Localization;

namespace PCL.Shion.Services;

/// <summary>
/// 供 ViewModel 使用的界面文字查询。先查嵌入资源 <c>Assets\Strings\**\*.json</c> 里的字符串表，查不到再交给 <see cref="Lang"/>。
/// 语言跟随 <see cref="LocalizationService"/>：按“简体中文 → 英文 → 所选语言”的顺序叠加。
/// 每个页面可以有自己的文件，命名为 <c>&lt;名称&gt;.&lt;语言&gt;.json</c>；全局文件 <c>&lt;语言&gt;.json</c> 最先加载，
/// 其余按名称顺序加载，同名的键后加载的覆盖先加载的。
/// </summary>
public static class Localizer
{
    private const string ResourcePrefix = "PCL.Shion.Strings.";

    private static volatile Dictionary<string, string> _table = Load();

    static Localizer() => LocalizationService.LanguageChanged += () => _table = Load();

    public static string Get(string key) => _table.TryGetValue(key, out var text) ? text : Lang.Text(key);

    public static string Format(string key, params object[] args) => string.Format(Lang.Culture, Get(key), args);

    private static Dictionary<string, string> Load()
    {
        var assembly = typeof(Localizer).Assembly;
        var table = new Dictionary<string, string>();
        foreach (var code in LocalizationService.LanguageChain(LocalizationService.CurrentLanguage.Code))
        {
            foreach (var name in LangResources.Find(assembly, ResourcePrefix, ".json", code, $"{ResourcePrefix}{code}.json"))
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                foreach (var (key, value) in JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!)
                    table[key] = value;
            }
        }

        return table;
    }
}
