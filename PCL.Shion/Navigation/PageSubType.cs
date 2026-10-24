namespace PCL.Shion.Navigation;

/// <summary>
///     次要页面种类。其数值必须与 StackPanel 中的下标一致。
/// </summary>
public enum PageSubType
{
    Default = 0,
    DownloadInstall = 1,
    DownloadMod = 2,
    DownloadPack = 3,
    DownloadDataPack = 4,
    DownloadResourcePack = 5,
    DownloadShader = 6,
    DownloadWorld = 7,
    DownloadCompFavorites = 8,
    DownloadClient = 9,
    DownloadOptiFine = 10,
    DownloadForge = 11,
    DownloadNeoForge = 12,
    DownloadCleanroom = 13,
    DownloadFabric = 14,
    DownloadLiteLoader = 16,
    DownloadLabyMod = 17,
    DownloadLegacyFabric = 18,

    SetupLaunch = 0,
    SetupUI = 1,
    SetupGameManage = 2,
    SetupLink = 3,
    SetupAbout = 4,
    SetupLog = 5,
    SetupFeedback = 6,
    SetupGameLink = 7,
    SetupUpdate = 8,
    SetupJava = 9,
    SetupLauncherMisc = 10,
    SetupLauncherLanguage = 11,

    ToolsGameLink = 1,
    ToolsTest = 3,
    ToolsCheckIn = 4,
    ToolsRecentPlay = 5,
    ToolsNews = 6,

    VersionOverall = 0,
    VersionSetup = 1,
    VersionExport = 2,
    VersionWorld = 3,
    VersionScreenshot = 4,
    VersionMod = 5,
    VersionModDisabled = 6,
    VersionResourcePack = 7,
    VersionShader = 8,
    VersionSchematic = 9,
    VersionInstall = 10,
    VersionServer = 11,
    VersionModJarInJar = 12,
    VersionSavesInfo = 0,
    VersionSavesDatapack = 1
}
