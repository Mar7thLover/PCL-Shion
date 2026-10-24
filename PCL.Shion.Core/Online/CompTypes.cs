namespace PCL.Shion.Core.Online;

/// <summary>
/// 资源的 Mod 加载器类别。数值与 CurseForge 的 ModLoaderType 一致。
/// </summary>
public enum CompLoaderType
{
    /// <summary>任意加载器</summary>
    Any = 0,

    /// <summary>Forge</summary>
    Forge = 1,

    /// <summary>LiteLoader</summary>
    LiteLoader = 3,

    /// <summary>Fabric</summary>
    Fabric = 4,

    /// <summary>Quilt</summary>
    Quilt = 5,

    /// <summary>NeoForge</summary>
    NeoForge = 6,

    /// <summary>材质包</summary>
    Minecraft = 7,

    /// <summary>光影包 Canvas</summary>
    Canvas = 8,

    /// <summary>光影包 Iris</summary>
    Iris = 9,

    /// <summary>光影包 OptiFine</summary>
    OptiFine = 10,

    /// <summary>光影包 Vanilla</summary>
    Vanilla = 11,

    /// <summary>LabyMod 客户端</summary>
    LabyMod = 12
}

/// <summary>
/// 搜索结果排序方式。
/// </summary>
public enum CompSortType
{
    /// <summary>默认</summary>
    Default = 1,

    /// <summary>相关性 (CurseForge Name (4) / Modrinth relevance)</summary>
    Relevance = 2,

    /// <summary>下载量 (CurseForge TotalDownloads (6) / Modrinth downloads)</summary>
    Downloads = 3,

    /// <summary>关注量 (CurseForge Popularity (2) / Modrinth follows)</summary>
    Follows = 4,

    /// <summary>最新发布 (CurseForge ReleasedDate (11) / Modrinth newest)</summary>
    Newest = 5,

    /// <summary>最近更新 (CurseForge LastUpdated (3) / Modrinth updated)</summary>
    Updated = 6
}

/// <summary>
/// 允许的搜索来源。
/// </summary>
[Flags]
public enum CompSourceType
{
    CurseForge = 1,
    Modrinth = 2,
    Any = CurseForge | Modrinth
}

/// <summary>
/// 资源种类。
/// </summary>
public enum CompType
{
    /// <summary>允许任意种类，或种类未知。</summary>
    Any = -1,

    /// <summary>Mod。</summary>
    Mod = 0,

    /// <summary>整合包。</summary>
    ModPack = 1,

    /// <summary>资源包。</summary>
    ResourcePack = 2,

    /// <summary>光影包。</summary>
    Shader = 3,

    /// <summary>CurseForge：数据包。Modrinth：数据包，或数据包与 Mod 的混合。</summary>
    DataPack = 4,

    /// <summary>服务端插件。</summary>
    Plugin = 5,

    /// <summary>投影原理图。</summary>
    Schematic = 6,

    /// <summary>世界。</summary>
    World = 7
}

/// <summary>
/// 依赖的安装方式。
/// </summary>
public enum CompDepsInstallTypes
{
    /// <summary>无法解析依赖</summary>
    Unresolved = 0,

    /// <summary>用户选择安装前置</summary>
    WithDeps = 1,

    /// <summary>用户选择只安装本体，不安装前置</summary>
    WithoutDeps = 2,

    /// <summary>用户取消安装</summary>
    Cancel = 3,
}

/// <summary>
/// 文件下载的原因，Modrinth 下载链接会附带此参数用于统计。
/// </summary>
public enum DownloadReason
{
    Standalone,
    Dependency,
    ModPack,
    Update
}

/// <summary>
/// CurseForge 文件的发布状态。
/// </summary>
public enum CompFileStatus
{
    /// <summary>正式版</summary>
    Release = 1,

    /// <summary>测试版</summary>
    Beta = 2,

    /// <summary>预览版</summary>
    Alpha = 3
}

/// <summary>
/// 镜像源策略，决定 Modrinth / CurseForge 接口与下载地址的尝试顺序。
/// </summary>
public enum CompSourceStrategy
{
    /// <summary>优先镜像源</summary>
    MirrorFirst = 0,

    /// <summary>官方与镜像源轮流尝试</summary>
    Balanced = 1,

    /// <summary>仅官方源</summary>
    OfficialOnly = 2
}
