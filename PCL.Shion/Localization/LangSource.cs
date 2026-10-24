using System.ComponentModel;

namespace PCL.Shion.Localization;

/// <summary>
/// <c>{loc:L 键名}</c> 绑定的数据源：按键取文本，语言切换时通知所有绑定刷新。
/// </summary>
internal sealed class LangSource : INotifyPropertyChanged
{
    // 索引器的属性名；绑定引擎按它判断是否需要刷新
    private const string IndexerName = "Item";

    private LangSource() => LocalizationService.LanguageChanged += OnLanguageChanged;

    public static LangSource Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => Lang.Text(key);

    private void OnLanguageChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerName));
}
