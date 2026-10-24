using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace PCL.Shion.Localization;

/// <summary>
/// 界面语言与展示格式。语言默认跟随系统，不受支持的语言回退为简体中文。
/// 文字以简体中文为完整底本，非中文语言再叠一层英文，最后才是所选语言，缺少的键因此总有文字可显示。
/// </summary>
public static class LocalizationService
{
    /// <summary>跟随系统。</summary>
    public const string Auto = "auto";

    /// <summary>展示格式与界面语言保持一致。</summary>
    public const string FormatCultureFollowLanguage = "ui-language";

    /// <summary>默认语言，也是语言资源的完整底本。</summary>
    public const string DefaultLanguageCode = "zh-CN";

    private const string EnglishLanguageCode = "en-US";

    private static readonly Lock SyncRoot = new();
    private static readonly LocalizationLanguage DefaultLanguage = new(
        DefaultLanguageCode, "简体中文（中国大陆）", "zh-CN", LocalizationFontProfile.SimplifiedChinese);
    private static readonly CultureInfo SystemFormatCulture = CultureInfo.CurrentCulture;
    private static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;

    private static volatile Dictionary<string, string> _table = new();
    private static bool _initialized;

    static LocalizationService() => Apply(Auto);

    /// <summary>受支持的界面语言。</summary>
    public static IReadOnlyList<LocalizationLanguage> SupportedLanguages { get; } =
    [
        DefaultLanguage,
        new("zh-TW", "繁體中文（台灣）", "zh-TW", LocalizationFontProfile.TraditionalChinese),
        new(EnglishLanguageCode, "English (US)", "en-US", LocalizationFontProfile.English),
        new("en-GB", "English (United Kingdom)", "en-GB", LocalizationFontProfile.English),
        new("ja-JP", "日本語（日本）", "ja-JP", LocalizationFontProfile.Japanese),
        new("fr-FR", "Français (France)", "fr-FR", LocalizationFontProfile.Other),
        new("es-ES", "Español (España)", "es-ES", LocalizationFontProfile.Other)
    ];

    /// <summary>当前界面语言。</summary>
    public static LocalizationLanguage CurrentLanguage { get; private set; } = DefaultLanguage;

    /// <summary>当前日期、数字等的展示格式所用的区域性。</summary>
    public static CultureInfo CurrentFormatCulture { get; private set; } = SystemFormatCulture;

    /// <summary>界面语言或展示格式改变后触发。</summary>
    public static event Action? LanguageChanged;

    /// <summary>
    /// 应用界面语言与展示格式。
    /// </summary>
    /// <param name="languageCode">语言代码，<see cref="Auto"/> 表示跟随系统。</param>
    /// <param name="formatCultureCode">展示格式：<see cref="Auto"/> 跟随系统区域格式，<see cref="FormatCultureFollowLanguage"/> 跟随界面语言，或具体的区域性名称。</param>
    public static void Apply(string languageCode, string formatCultureCode = Auto)
    {
        bool changed;
        lock (SyncRoot)
        {
            var language = ResolveLanguage(languageCode);
            var uiCulture = CultureInfo.GetCultureInfo(language.CultureName);
            var formatCulture = ResolveFormatCulture(formatCultureCode, uiCulture);

            var languageChanged = !_initialized || language.Code != CurrentLanguage.Code;
            changed = languageChanged || formatCulture.Name != CurrentFormatCulture.Name;
            if (!changed) return;

            CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
            CultureInfo.DefaultThreadCurrentCulture = formatCulture;
            CultureInfo.CurrentUICulture = uiCulture;
            CultureInfo.CurrentCulture = formatCulture;

            if (languageChanged) _table = LangResources.Load(LanguageChain(language.Code));
            CurrentLanguage = language;
            CurrentFormatCulture = formatCulture;
            _initialized = true;
        }

        LanguageChanged?.Invoke();
    }

    /// <summary>语言代码是否受支持。空值与 <see cref="Auto"/> 视为受支持。</summary>
    public static bool IsLanguageSupported(string? languageCode)
    {
        if (IsAuto(languageCode)) return true;
        return FindLanguage(Normalize(languageCode)) is not null;
    }

    /// <summary>按配置值解析语言：<see cref="Auto"/> 取系统语言，不受支持的取简体中文。</summary>
    public static LocalizationLanguage ResolveLanguage(string? languageCode)
    {
        if (IsAuto(languageCode)) return ResolveSystemLanguage();
        return FindLanguage(Normalize(languageCode)) ?? DefaultLanguage;
    }

    /// <summary>
    /// 某个语言的读取顺序，后者覆盖前者：简体中文 → 英文（非中文语言才有）→ 该语言本身。
    /// </summary>
    internal static IReadOnlyList<string> LanguageChain(string languageCode)
    {
        var chain = new List<string> { DefaultLanguageCode };
        if (!languageCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) chain.Add(EnglishLanguageCode);
        if (!chain.Contains(languageCode, StringComparer.OrdinalIgnoreCase)) chain.Add(languageCode);
        return chain;
    }

    internal static string? FindText(string key) => _table.GetValueOrDefault(key);

    private static LocalizationLanguage ResolveSystemLanguage()
    {
        var name = Normalize(SystemUiCulture.Name);
        var exact = FindLanguage(name);
        if (exact is not null) return exact;

        var neutral = SystemUiCulture.TwoLetterISOLanguageName;
        if (neutral == "zh" && IsTraditionalChinese(name)) return FindLanguage("zh-TW")!;
        return SupportedLanguages.FirstOrDefault(language =>
                   language.Code.StartsWith(neutral + "-", StringComparison.OrdinalIgnoreCase))
               ?? DefaultLanguage;
    }

    private static CultureInfo ResolveFormatCulture(string? formatCultureCode, CultureInfo uiCulture)
    {
        if (IsAuto(formatCultureCode)) return SystemFormatCulture;

        var code = Normalize(formatCultureCode);
        if (string.Equals(code, FormatCultureFollowLanguage, StringComparison.OrdinalIgnoreCase)) return uiCulture;

        try
        {
            return CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            return SystemFormatCulture;
        }
    }

    private static LocalizationLanguage? FindLanguage(string code) =>
        SupportedLanguages.FirstOrDefault(language => string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));

    private static bool IsTraditionalChinese(string cultureName) =>
        cultureName.Contains("Hant", StringComparison.OrdinalIgnoreCase) || cultureName is "zh-HK" or "zh-MO";

    private static bool IsAuto(string? code) =>
        string.IsNullOrWhiteSpace(code) || string.Equals(code.Trim(), Auto, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? code) => (code ?? string.Empty).Replace('_', '-').Trim();
}
