namespace PCL.Shion.Navigation;

// 页面种类与属性
// 注意，这一枚举在 “切换页面” EventType 中调用，应视作公开 API 的一部分
/// <summary>
///     页面种类。
/// </summary>
public enum PageType
{
    /// <summary>
    ///     启动。
    /// </summary>
    Launch = 0,

    /// <summary>
    ///     下载。
    /// </summary>
    Download = 1,

    /// <summary>
    ///     联机。
    /// </summary>
    Tools = 3,

    /// <summary>
    ///     设置。
    /// </summary>
    Setup = 2,

    /// <summary>
    ///     实例选择。这是一个副页面。
    /// </summary>
    InstanceSelect = 5,

    /// <summary>
    ///     任务管理。这是一个副页面。
    /// </summary>
    TaskManager = 6,

    /// <summary>
    ///     实例设置。这是一个副页面。
    /// </summary>
    InstanceSetup = 7,

    /// <summary>
    ///     资源工程详情。这是一个副页面。
    /// </summary>
    CompDetail = 8,

    /// <summary>
    ///     帮助详情。这是一个副页面。
    /// </summary>
    HelpDetail = 9,

    /// <summary>
    ///     游戏实时日志。这是一个副页面。
    /// </summary>
    GameLog = 10,

    /// <summary>
    ///     存档详细管理，这是一个副页面。
    /// </summary>
    VersionSaves = 12,

    /// <summary>
    ///     Minecraft 新闻详情。这是一个副页面。
    /// </summary>
    NewsDetail = 13,
}
