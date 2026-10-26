using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

/// <summary>
/// Mod 区域文字的强类型入口：XAML 里用 <c>{x:Static}</c>，代码里直接调用。
/// 字符串表是 <c>Assets\Strings\Mods.*.json</c>，键为 <c>Mods.</c> 加属性名。
/// </summary>
public static class ModsStrings
{
    public static string SearchPlaceholder => Text(nameof(SearchPlaceholder));
    public static string Refresh => Text(nameof(Refresh));
    public static string OpenFolder => Text(nameof(OpenFolder));
    public static string ParseMetadata => Text(nameof(ParseMetadata));
    public static string ParseMetadataHint => Text(nameof(ParseMetadataHint));
    public static string PartialMetadata => Text(nameof(PartialMetadata));
    public static string ReadWarnings(int count) => Text(nameof(ReadWarnings), count);
    public static string Summary(int total, int enabled) => Text(nameof(Summary), total, enabled);

    public static string Loading => Text(nameof(Loading));
    public static string LoadFailed(string message) => Text(nameof(LoadFailed), message);
    public static string Empty => Text(nameof(Empty));
    public static string NoMatch => Text(nameof(NoMatch));

    public static string IssuesTitle(int count) => Text(nameof(IssuesTitle), count);
    public static string IssueDuplicate(string mod, string files) => Text(nameof(IssueDuplicate), mod, files);
    public static string IssueMissingDependency(string mod, string dependency) => Text(nameof(IssueMissingDependency), mod, dependency);
    public static string FileSeparator => Text(nameof(FileSeparator));

    public static string Delete => Text(nameof(Delete));
    public static string DeleteQuestion => Text(nameof(DeleteQuestion));
    public static string DeleteConfirm => Text(nameof(DeleteConfirm));
    public static string Cancel => Text(nameof(Cancel));

    public static string EnableFailed(string mod, string message) => Text(nameof(EnableFailed), mod, message);
    public static string DisableFailed(string mod, string message) => Text(nameof(DisableFailed), mod, message);
    public static string DeleteFailed(string mod, string message) => Text(nameof(DeleteFailed), mod, message);

    private static string Text(string name) => Localizer.Get($"Mods.{name}");

    private static string Text(string name, params object[] args) => Localizer.Format($"Mods.{name}", args);
}
