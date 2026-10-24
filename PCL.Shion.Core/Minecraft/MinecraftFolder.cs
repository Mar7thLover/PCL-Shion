using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 官方启动器使用的默认 .minecraft 目录。
/// </summary>
public static class MinecraftFolder
{
    public static string GetDefault()
    {
        var home = SpecialFolders.Get(Environment.SpecialFolder.UserProfile);
        return PlatformInfo.Os switch
        {
            OsKind.Windows => Path.Combine(SpecialFolders.Get(Environment.SpecialFolder.ApplicationData), ".minecraft"),
            OsKind.MacOs => Path.Combine(home, "Library", "Application Support", "minecraft"),
            _ => Path.Combine(home, ".minecraft")
        };
    }
}
