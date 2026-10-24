namespace PCL.Shion.Core.Loaders.Extra;

/// <summary>
/// Fabric 或 LegacyFabric 的安装器版本。
/// </summary>
/// <param name="Version">安装器版本号，如 <c>1.0.1</c>。</param>
/// <param name="Url">安装器 jar 的下载地址。</param>
/// <param name="Stable">是否为稳定版。</param>
public sealed record ToolVersion(string Version, string Url, bool Stable);

/// <summary>
/// OptiFine 的一个版本。
/// </summary>
/// <param name="DisplayName">显示名称，已去除 HD_U 字样，如 <c>1.12.2 C8</c>。</param>
/// <param name="MinecraftVersion">对应的 Minecraft 版本，如 <c>1.12.2</c>。</param>
/// <param name="FileName">原始文件名，如 <c>OptiFine_1.12.2_HD_U_C8.jar</c>。</param>
/// <param name="VersionName">对应的版本名称，如 <c>1.12.2-OptiFine_HD_U_C8</c>。</param>
/// <param name="IsPreview">是否为测试版。</param>
/// <param name="ReleaseTime">发布时间；OptiFine 的 BMCLAPI 源没有该数据，为空字符串。</param>
/// <param name="RequiredForge">
/// 需要的最低 Forge 版本。空字符串表示无限制，<see langword="null"/> 表示与 Forge 不兼容。
/// </param>
/// <param name="DownloadUrl">安装器 jar 的下载地址。</param>
public sealed record OptiFineVersion(
    string DisplayName,
    string MinecraftVersion,
    string FileName,
    string VersionName,
    bool IsPreview,
    string ReleaseTime,
    string? RequiredForge,
    string DownloadUrl);

/// <summary>
/// LiteLoader 的一个版本。
/// </summary>
/// <param name="MinecraftVersion">对应的 Minecraft 版本，如 <c>1.12.2</c>。</param>
/// <param name="FileName">安装器的文件名，如 <c>liteloader-installer-1.12.2-00-SNAPSHOT.jar</c>。</param>
/// <param name="IsLegacy">是否为 1.7 及更早的远古版。</param>
/// <param name="IsPreview">是否为快照版。</param>
/// <param name="ReleaseTime">发布时间，格式为 <c>yyyy/MM/dd HH:mm</c>。</param>
public sealed record LiteLoaderVersion(
    string MinecraftVersion,
    string FileName,
    bool IsLegacy,
    bool IsPreview,
    string ReleaseTime);

/// <summary>
/// LabyMod 某个渠道的最新版本。
/// </summary>
/// <param name="Channel">渠道，<c>production</c> 或 <c>snapshot</c>。</param>
/// <param name="LabyModVersion">LabyMod 版本号。</param>
public sealed record LabyModChannel(string Channel, string LabyModVersion);
