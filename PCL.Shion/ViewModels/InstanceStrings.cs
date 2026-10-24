using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

/// <summary>
/// 实例详情页文字的强类型入口：XAML 里用 <c>{x:Static}</c>，代码里直接调用。
/// 字符串表是 <c>Assets\Strings\Instance.*.json</c>，键为 <c>Instance.</c> 加属性名。
/// </summary>
public static class InstanceStrings
{
    public static string Back => Text(nameof(Back));
    public static string SectionOverview => Text(nameof(SectionOverview));
    public static string SectionSettings => Text(nameof(SectionSettings));
    public static string SectionMods => Text(nameof(SectionMods));
    public static string Loading => Text(nameof(Loading));
    public static string NotFound(string name) => Text(nameof(NotFound), name);
    public static string LoadFailed(string message) => Text(nameof(LoadFailed), message);

    public static string InfoTitle => Text(nameof(InfoTitle));
    public static string FactVersion => Text(nameof(FactVersion));
    public static string FactLoader => Text(nameof(FactLoader));
    public static string FactMode => Text(nameof(FactMode));
    public static string FactInstanceDirectory => Text(nameof(FactInstanceDirectory));
    public static string FactGameDirectory => Text(nameof(FactGameDirectory));
    public static string FactPlayTime => Text(nameof(FactPlayTime));
    public static string FactLastPlayed => Text(nameof(FactLastPlayed));
    public static string UnknownVersion => Text(nameof(UnknownVersion));
    public static string LoaderNone => Text(nameof(LoaderNone));
    public static string ModeIsolated => Text(nameof(ModeIsolated));
    public static string ModeShared => Text(nameof(ModeShared));
    public static string NeverPlayed => Text(nameof(NeverPlayed));
    public static string LessThanMinute => Text(nameof(LessThanMinute));
    public static string Minutes(long minutes) => Text(nameof(Minutes), minutes);
    public static string HoursMinutes(long hours, long minutes) => Text(nameof(HoursMinutes), hours, minutes);
    public static string OpenInstanceFolder => Text(nameof(OpenInstanceFolder));
    public static string OpenGameFolder => Text(nameof(OpenGameFolder));
    public static string OpenFailed(string message) => Text(nameof(OpenFailed), message);

    public static string RenameTitle => Text(nameof(RenameTitle));
    public static string RenameButton => Text(nameof(RenameButton));
    public static string NameEmpty => Text(nameof(NameEmpty));
    public static string NameInvalid => Text(nameof(NameInvalid));
    public static string NameExists => Text(nameof(NameExists));
    public static string Renamed(string name) => Text(nameof(Renamed), name);
    public static string RenameFailed(string message) => Text(nameof(RenameFailed), message);
    public static string RunningBlocked => Text(nameof(RunningBlocked));

    public static string DeleteTitle => Text(nameof(DeleteTitle));
    public static string DeleteDescription => Text(nameof(DeleteDescription));
    public static string DeleteButton => Text(nameof(DeleteButton));
    public static string DeleteConfirm(string name, string directory) => Text(nameof(DeleteConfirm), name, directory);
    public static string DeleteConfirmButton => Text(nameof(DeleteConfirmButton));
    public static string DeleteCancel => Text(nameof(DeleteCancel));
    public static string DeleteFailed(string message) => Text(nameof(DeleteFailed), message);

    public static string GroupRuntime => Text(nameof(GroupRuntime));
    public static string GroupGame => Text(nameof(GroupGame));
    public static string GroupQuickPlay => Text(nameof(GroupQuickPlay));
    public static string GroupAdvanced => Text(nameof(GroupAdvanced));
    public static string JavaLabel => Text(nameof(JavaLabel));
    public static string JavaAuto => Text(nameof(JavaAuto));
    public static string JavaOption(string version, int bits, string path) => Text(nameof(JavaOption), version, bits, path);
    public static string JavaUnlisted(string path) => Text(nameof(JavaUnlisted), path);
    public static string MemoryLabel => Text(nameof(MemoryLabel));
    public static string MemoryPlaceholder(int globalMb) => Text(nameof(MemoryPlaceholder), globalMb);
    public static string MemoryRange(int min, int max) => Text(nameof(MemoryRange), min, max);
    public static string IsolatedLabel => Text(nameof(IsolatedLabel));
    public static string IsolatedHint => Text(nameof(IsolatedHint));
    public static string WindowSizeLabel => Text(nameof(WindowSizeLabel));
    public static string WidthPlaceholder => Text(nameof(WidthPlaceholder));
    public static string HeightPlaceholder => Text(nameof(HeightPlaceholder));
    public static string WidthRange(int min, int max) => Text(nameof(WidthRange), min, max);
    public static string HeightRange(int min, int max) => Text(nameof(HeightRange), min, max);
    public static string ExtraJvmLabel => Text(nameof(ExtraJvmLabel));
    public static string ExtraGameLabel => Text(nameof(ExtraGameLabel));

    public static string QuickPlayHint => Text(nameof(QuickPlayHint));
    public static string QuickPlayModeLabel => Text(nameof(QuickPlayModeLabel));
    public static string QuickPlayOff => Text(nameof(QuickPlayOff));
    public static string QuickPlayWorld => Text(nameof(QuickPlayWorld));
    public static string QuickPlayServer => Text(nameof(QuickPlayServer));
    public static string WorldLabel => Text(nameof(WorldLabel));
    public static string WorldPlaceholder => Text(nameof(WorldPlaceholder));
    public static string WorldsEmpty => Text(nameof(WorldsEmpty));
    public static string WorldMissing(string folder) => Text(nameof(WorldMissing), folder);
    public static string WorldRequired => Text(nameof(WorldRequired));
    public static string ServerLabel => Text(nameof(ServerLabel));
    public static string ServerAddressPlaceholder => Text(nameof(ServerAddressPlaceholder));
    public static string ServerListPlaceholder => Text(nameof(ServerListPlaceholder));
    public static string ServersEmpty => Text(nameof(ServersEmpty));
    public static string ServerInvalid => Text(nameof(ServerInvalid));

    public static string WrapperLabel => Text(nameof(WrapperLabel));
    public static string WrapperHint => Text(nameof(WrapperHint));
    public static string PreLaunchLabel => Text(nameof(PreLaunchLabel));
    public static string PreLaunchHint => Text(nameof(PreLaunchHint));
    public static string PostExitLabel => Text(nameof(PostExitLabel));
    public static string PostExitHint => Text(nameof(PostExitHint));
    public static string EnvironmentLabel => Text(nameof(EnvironmentLabel));
    public static string EnvironmentHint => Text(nameof(EnvironmentHint));
    public static string EnvironmentInvalid(int line) => Text(nameof(EnvironmentInvalid), line);

    public static string Save => Text(nameof(Save));
    public static string Saved => Text(nameof(Saved));
    public static string SaveFailed(string message) => Text(nameof(SaveFailed), message);

    private static string Text(string name) => Localizer.Get($"Instance.{name}");

    private static string Text(string name, params object[] args) => Localizer.Format($"Instance.{name}", args);
}
