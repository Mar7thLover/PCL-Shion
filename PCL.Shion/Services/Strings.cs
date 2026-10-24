namespace PCL.Shion.Services;

/// <summary>
/// 界面文字的强类型入口：XAML 里用 <c>{x:Static}</c>，代码里直接调用。属性名即字符串表里的键。
/// </summary>
public static class Strings
{
    public static string AppName => Localizer.Get(nameof(AppName));
    public static string PageLaunch => Localizer.Get(nameof(PageLaunch));
    public static string PageDownload => Localizer.Get(nameof(PageDownload));
    public static string PageSettings => Localizer.Get(nameof(PageSettings));
    public static string UpdateAvailable => Localizer.Get(nameof(UpdateAvailable));

    public static string PlayerName => Localizer.Get(nameof(PlayerName));
    public static string PlayerNameHint => Localizer.Get(nameof(PlayerNameHint));

    public static string Instances => Localizer.Get(nameof(Instances));
    public static string InstancesLoading => Localizer.Get(nameof(InstancesLoading));
    public static string InstanceUnknownVersion => Localizer.Get(nameof(InstanceUnknownVersion));
    public static string InstancesEmpty(string root) => Localizer.Format(nameof(InstancesEmpty), root);
    public static string InstancesReadFailed(string message) => Localizer.Format(nameof(InstancesReadFailed), message);

    public static string Java => Localizer.Get(nameof(Java));
    public static string JavaSearching => Localizer.Get(nameof(JavaSearching));
    public static string JavaSelectInstance => Localizer.Get(nameof(JavaSelectInstance));
    public static string JavaNotFound => Localizer.Get(nameof(JavaNotFound));
    public static string JavaDetected(string version, int bits) => Localizer.Format(nameof(JavaDetected), version, bits);
    public static string JavaNotFoundRequired(int major) => Localizer.Format(nameof(JavaNotFoundRequired), major);
    public static string JavaResolveFailed(string message) => Localizer.Format(nameof(JavaResolveFailed), message);

    public static string LaunchGame => Localizer.Get(nameof(LaunchGame));
    public static string StatusReady => Localizer.Get(nameof(StatusReady));
    public static string StatusPreparing => Localizer.Get(nameof(StatusPreparing));
    public static string StatusLaunchFailed(string message) => Localizer.Format(nameof(StatusLaunchFailed), message);
    public static string StatusRunning(int processId) => Localizer.Format(nameof(StatusRunning), processId);
    public static string StatusExited(int exitCode) => Localizer.Format(nameof(StatusExited), exitCode);

    public static string LogTitle => Localizer.Get(nameof(LogTitle));
    public static string LogEmpty => Localizer.Get(nameof(LogEmpty));
    public static string LogStarting(string instance, string java) => Localizer.Format(nameof(LogStarting), instance, java);
    public static string LogLaunchFailed(string message) => Localizer.Format(nameof(LogLaunchFailed), message);
    public static string LogExited(int exitCode) => Localizer.Format(nameof(LogExited), exitCode);
}
