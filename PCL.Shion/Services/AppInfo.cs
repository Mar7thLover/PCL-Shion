using System;

namespace PCL.Shion.Services;

/// <summary>
/// PCL Shion 自身的信息。
/// </summary>
public static class AppInfo
{
    /// <summary>当前版本，只含主、次、修订三段，与发布标签（如 <c>v0.3.0</c>）可直接比较。</summary>
    public static Version Version { get; } = ReadVersion();

    /// <summary>当前版本的文字，如 <c>0.3.0</c>。</summary>
    public static string VersionText => Version.ToString();

    private static Version ReadVersion()
    {
        var assemblyVersion = typeof(AppInfo).Assembly.GetName().Version!;
        return new Version(assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build);
    }
}
