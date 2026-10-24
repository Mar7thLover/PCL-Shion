using PCL.Shion.Core.Java;
using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

/// <summary>
/// 设置页文字的强类型入口：XAML 里用 <c>{x:Static}</c>，代码里直接调用。
/// 字符串表是 <c>Assets\Strings\Settings.*.json</c>，键为 <c>Settings.</c> 加属性名。
/// </summary>
public static class SettingsStrings
{
    public static string GameTitle => Text(nameof(GameTitle));
    public static string FoldersTitle => Text(nameof(FoldersTitle));
    public static string FoldersHint => Text(nameof(FoldersHint));
    public static string CurrentFolder => Text(nameof(CurrentFolder));
    public static string AddFolder => Text(nameof(AddFolder));
    public static string SetCurrentFolder => Text(nameof(SetCurrentFolder));
    public static string MoveFolderUp => Text(nameof(MoveFolderUp));
    public static string RemoveFolder => Text(nameof(RemoveFolder));
    public static string PickFolderTitle => Text(nameof(PickFolderTitle));

    public static string MemoryTitle => Text(nameof(MemoryTitle));
    public static string MemoryHint => Text(nameof(MemoryHint));

    public static string JavaTitle => Text(nameof(JavaTitle));
    public static string JavaHint => Text(nameof(JavaHint));
    public static string JavaAuto => Text(nameof(JavaAuto));
    public static string JavaRescan => Text(nameof(JavaRescan));
    public static string JavaOption(JavaRuntime java) =>
        Text(nameof(JavaOption), java.Version, java.Is64Bit ? 64 : 32, java.Executable);

    public static string DownloadTitle => Text(nameof(DownloadTitle));
    public static string SourceTitle => Text(nameof(SourceTitle));
    public static string SourceHint => Text(nameof(SourceHint));
    public static string SourceOfficial => Text(nameof(SourceOfficial));
    public static string SourceBmclapi => Text(nameof(SourceBmclapi));
    public static string ConcurrencyTitle => Text(nameof(ConcurrencyTitle));
    public static string ConcurrencyHint => Text(nameof(ConcurrencyHint));

    public static string NotificationsTitle => Text(nameof(NotificationsTitle));
    public static string NotificationsEnabled => Text(nameof(NotificationsEnabled));
    public static string NotificationsHint => Text(nameof(NotificationsHint));
    public static string CompletionSound => Text(nameof(CompletionSound));
    public static string CompletionSoundHint => Text(nameof(CompletionSoundHint));

    public static string UpdatesTitle => Text(nameof(UpdatesTitle));
    public static string CheckUpdates => Text(nameof(CheckUpdates));
    public static string IncludePrerelease => Text(nameof(IncludePrerelease));
    public static string CurrentVersion(string version) => Text(nameof(CurrentVersion), version);
    public static string UpdateIdle => Text(nameof(UpdateIdle));
    public static string UpdateChecking => Text(nameof(UpdateChecking));
    public static string UpdateUpToDate => Text(nameof(UpdateUpToDate));
    public static string UpdateAvailable(string version) => Text(nameof(UpdateAvailable), version);
    public static string UpdateFailed(string message) => Text(nameof(UpdateFailed), message);
    public static string CheckNow => Text(nameof(CheckNow));
    public static string OpenRelease => Text(nameof(OpenRelease));
    public static string OpenFailed(string message) => Text(nameof(OpenFailed), message);

    private static string Text(string name) => Localizer.Get($"Settings.{name}");

    private static string Text(string name, params object[] args) => Localizer.Format($"Settings.{name}", args);
}
