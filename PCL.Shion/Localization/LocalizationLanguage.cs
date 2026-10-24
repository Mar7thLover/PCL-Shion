namespace PCL.Shion.Localization;

/// <summary>
/// 一个受支持的界面语言。
/// </summary>
/// <param name="Code">语言代码，同时也是语言文件的名称，如 <c>zh-CN</c>。</param>
/// <param name="NativeName">语言的本地名称，用于语言选择列表。</param>
/// <param name="CultureName">对应的 <see cref="System.Globalization.CultureInfo"/> 名称。</param>
/// <param name="FontProfile">语言对应的 UI 字体策略。</param>
public sealed record LocalizationLanguage(
    string Code,
    string NativeName,
    string CultureName,
    LocalizationFontProfile FontProfile);
