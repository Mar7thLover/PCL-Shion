using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

/// <summary>
/// 下载页文字的强类型入口：XAML 里用 <c>{x:Static}</c>，代码里直接调用。
/// 字符串表是 <c>Assets\Strings\Download.*.json</c>，键为 <c>Download.</c> 加属性名。
/// </summary>
public static class DownloadStrings
{
    public static string SearchPlaceholder => Text(nameof(SearchPlaceholder));
    public static string SourceLabel => Text(nameof(SourceLabel));
    public static string SourceOfficial => Text(nameof(SourceOfficial));
    public static string SourceBmclapi => Text(nameof(SourceBmclapi));

    public static string FilterRelease => Text(nameof(FilterRelease));
    public static string FilterSnapshot => Text(nameof(FilterSnapshot));
    public static string FilterOld => Text(nameof(FilterOld));

    public static string ColumnVersion => Text(nameof(ColumnVersion));
    public static string ColumnType => Text(nameof(ColumnType));
    public static string ColumnDate => Text(nameof(ColumnDate));
    public static string TypeRelease => Text(nameof(TypeRelease));
    public static string TypeSnapshot => Text(nameof(TypeSnapshot));
    public static string TypeOldBeta => Text(nameof(TypeOldBeta));
    public static string TypeOldAlpha => Text(nameof(TypeOldAlpha));

    public static string VersionsLoading => Text(nameof(VersionsLoading));
    public static string VersionsLoadFailed(string message) => Text(nameof(VersionsLoadFailed), message);
    public static string VersionsEmpty => Text(nameof(VersionsEmpty));
    public static string Retry => Text(nameof(Retry));

    public static string SelectedVersionTitle => Text(nameof(SelectedVersionTitle));
    public static string SelectVersionHint => Text(nameof(SelectVersionHint));
    public static string InstanceNameTitle => Text(nameof(InstanceNameTitle));
    public static string NameEmpty => Text(nameof(NameEmpty));
    public static string NameInvalid => Text(nameof(NameInvalid));
    public static string NameExists => Text(nameof(NameExists));

    public static string Install => Text(nameof(Install));
    public static string Cancel => Text(nameof(Cancel));
    public static string StageVersion => Text(nameof(StageVersion));
    public static string StageFiles => Text(nameof(StageFiles));
    public static string StageAssets => Text(nameof(StageAssets));
    public static string Cancelling => Text(nameof(Cancelling));
    public static string ProgressFiles(int completed, int total) => Text(nameof(ProgressFiles), completed, total);
    public static string ProgressSpeed(string speed) => Text(nameof(ProgressSpeed), speed);

    public static string InstallSucceeded(string name) => Text(nameof(InstallSucceeded), name);
    public static string InstallCancelled => Text(nameof(InstallCancelled));
    public static string InstallFailed(string message) => Text(nameof(InstallFailed), message);
    public static string InstallFailedFiles(int count) => Text(nameof(InstallFailedFiles), count);
    public static string InstallFailedMore(int count) => Text(nameof(InstallFailedMore), count);

    private static string Text(string name) => Localizer.Get($"Download.{name}");

    private static string Text(string name, params object[] args) => Localizer.Format($"Download.{name}", args);
}
