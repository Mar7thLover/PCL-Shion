namespace PCL.Shion.Core.Loaders.Cleanroom;

/// <summary>
/// 一个 Cleanroom 发布版本，对应 GitHub 上的一个 release。Cleanroom 只适用于 Minecraft 1.12.2。
/// </summary>
/// <param name="Tag">发布的标签，如 0.2.4.1-alpha 或 0.2.4。</param>
public sealed record CleanroomVersion(string Tag)
{
    /// <summary>Cleanroom 适用的原版版本号。</summary>
    public const string MinecraftVersion = "1.12.2";

    /// <summary>是否为测试版，即标签包含 alpha。</summary>
    public bool IsBeta => Tag.Contains("alpha", StringComparison.Ordinal);

    /// <summary>标签中第一个 "-" 之前的部分，如 0.2.4。</summary>
    public string Number => Tag.Split('-')[0];

    /// <summary>发布附件的基础地址，不含后缀。</summary>
    public string AssetBase => $"https://github.com/CleanroomMC/Cleanroom/releases/download/{Tag}/cleanroom-{Tag}";

    /// <summary>安装器的下载地址。GitHub 没有镜像，只有这一个地址。</summary>
    public Uri InstallerUrl => new($"{AssetBase}-installer.jar");

    /// <summary>更新日志的地址。</summary>
    public Uri ChangelogUrl => new($"{AssetBase}-changelog.txt");
}
