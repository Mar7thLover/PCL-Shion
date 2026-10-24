using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Platform;
using PCL.Shion.Core.Sources;
using PCL.Shion.Themes;

using InstanceValueStore = System.Collections.Generic.Dictionary<string,
    System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>>;

namespace PCL.Shion.Services;

/// <summary>
/// 联机设置中的协议偏好，与 WPF 的 <c>LinkProtocolPreference</c> 一致。
/// </summary>
public enum LinkProtocolPreference
{
    Tcp = 0,
    Udp = 1
}

/// <summary>
/// 启动器设置，保存在 <see cref="Environment.SpecialFolder.ApplicationData"/> 下的 <c>PCL Shion/settings.json</c>
/// （Windows 为 <c>%APPDATA%\PCL Shion</c>，Linux 为 <c>~/.config/PCL Shion</c>，macOS 为 <c>~/Library/Application Support/PCL Shion</c>），不与 PCL CE 共用目录。
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>启动设置里 JVM 参数的默认值。</summary>
    public const string DefaultLaunchJvmArgs =
        "-XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=true -Dfml.ignoreInvalidMinecraftCertificates=True -Dfml.ignorePatchDiscrepancies=True -Dlog4j2.formatMsgNoLookups=true";

    /// <summary>已添加的 .minecraft 目录，第一项是当前使用的目录；列表不会为空。</summary>
    public List<string> MinecraftFolders { get; set; } = [MinecraftFolder.GetDefault()];

    /// <summary>当前使用的 .minecraft 目录，即 <see cref="MinecraftFolders"/> 的第一项。</summary>
    [JsonIgnore]
    public string MinecraftRoot => MinecraftFolders[0];

    public string PlayerName { get; set; } = "";
    public string? SelectedInstance { get; set; }
    public InstanceSortMode InstanceSort { get; set; } = InstanceSortMode.LastPlayed;
    public int MaxMemoryMb { get; set; } = 4096;

    /// <summary>默认使用的 Java 可执行文件；为 null 时按实例需要自动选择。对应 WPF 的 LaunchArgumentJavaSelect。</summary>
    public string? DefaultJavaExecutable { get; set; }

    /// <summary>用户手动添加的 Java 可执行文件路径。</summary>
    public List<string> JavaManualPaths { get; set; } = [];

    /// <summary>被用户停用的 Java 可执行文件路径，停用的 Java 不参与自动选择。</summary>
    public List<string> JavaDisabledPaths { get; set; } = [];

    public DownloadSource DownloadSource { get; set; } = DownloadSource.Official;
    public int MaxDownloadConcurrency { get; set; } = 32;
    public bool NotificationsEnabled { get; set; } = true;
    public bool CompletionSound { get; set; } = true;
    public bool CheckUpdates { get; set; } = true;
    public bool IncludePrereleaseUpdates { get; set; }

    #region 启动设置（对应 WPF 的 Config.Launch）

    /// <summary>内存分配模式：0 为自动，1 为手动。</summary>
    public int LaunchRamType { get; set; } = 0;

    /// <summary>手动分配的最大内存滑块刻度值。</summary>
    public int LaunchRamCustom { get; set; } = 15;

    /// <summary>手动分配的初始内存滑块刻度值，0 表示不主动设置。</summary>
    public int LaunchRamCustomInitial { get; set; } = 0;

    /// <summary>是否固定堆大小（-Xms 与 -Xmx 相等）。</summary>
    public bool LaunchAdvanceLockMemory { get; set; } = false;

    /// <summary>优先 IP 协议栈，与设置界面下拉框的选项序号一致。</summary>
    public int LaunchPreferredIpStack { get; set; } = 0;

    /// <summary>附加 JVM 参数。</summary>
    public string LaunchAdvanceJvm { get; set; } = DefaultLaunchJvmArgs;

    /// <summary>附加游戏参数。</summary>
    public string LaunchAdvanceGame { get; set; } = "";

    /// <summary>预启动指令。</summary>
    public string LaunchAdvanceRun { get; set; } = "";

    /// <summary>是否等待预启动指令完成。</summary>
    public bool LaunchAdvanceRunWait { get; set; } = true;

    /// <summary>禁用 Java Launch Wrapper。</summary>
    public bool LaunchAdvanceDisableJLW { get; set; } = true;

    /// <summary>禁用 LegacyFix。</summary>
    public bool LaunchAdvanceDisableLF { get; set; } = false;

    /// <summary>强制使用高性能显卡。</summary>
    public bool LaunchAdvanceGraphicCard { get; set; } = true;

    /// <summary>使用 java 而不是 javaw。</summary>
    public bool LaunchAdvanceNoJavaw { get; set; } = false;

    /// <summary>禁用 LWJGL Unsafe Agent。</summary>
    public bool LaunchAdvanceDisableLwjglUnsafeAgent { get; set; } = false;

    /// <summary>禁用自动崩溃分析。</summary>
    public bool LaunchAdvanceDisableCrashAnalysis { get; set; } = false;

    /// <summary>渲染器，与设置界面下拉框的选项序号一致。</summary>
    public int LaunchAdvanceRenderer { get; set; } = 0;

    /// <summary>游戏窗口标题。</summary>
    public string LaunchArgumentTitle { get; set; } = "";

    /// <summary>自定义左下角版本信息。</summary>
    public string LaunchArgumentInfo { get; set; } = "PCLCE";

    /// <summary>版本隔离 V2，与设置界面下拉框的选项序号一致。</summary>
    public int LaunchArgumentIndieV2 { get; set; } = 4;

    /// <summary>游戏启动后启动器可见性，与设置界面下拉框的选项序号一致。</summary>
    public int LaunchArgumentVisible { get; set; } = 5;

    /// <summary>游戏进程优先级，与设置界面下拉框项的 Tag 一致。</summary>
    public int LaunchArgumentPriority { get; set; } = 1;

    /// <summary>游戏窗口宽度。</summary>
    public int LaunchArgumentWindowWidth { get; set; } = 854;

    /// <summary>游戏窗口高度。</summary>
    public int LaunchArgumentWindowHeight { get; set; } = 480;

    /// <summary>游戏窗口模式，与设置界面下拉框的选项序号一致。</summary>
    public int LaunchArgumentWindowType { get; set; } = 1;

    /// <summary>正版登录方式。</summary>
    public int LoginMsAuthType { get; set; } = 1;

    /// <summary>已提示过渲染器风险，之后不再提示。</summary>
    public bool HintRenderer { get; set; }

    #endregion

    #region 下载与工具设置（对应 WPF 的 Config.Download 与 Config.Tool）

    /// <summary>下载线程滑块刻度值，实际连接上限为刻度值加 1。</summary>
    public int ToolDownloadThread { get; set; } = 63;

    /// <summary>单文件连接数滑块刻度值。</summary>
    public int ToolDownloadFileConnection { get; set; } = 7;

    /// <summary>下载速度限制滑块刻度值。</summary>
    public int ToolDownloadSpeed { get; set; } = 42;

    /// <summary>HTTP 协议版本，与设置界面下拉框的选项序号一致。</summary>
    public int ToolDownloadHttpMode { get; set; } = 0;

    /// <summary>游戏文件下载源，与设置界面下拉框的选项序号一致。</summary>
    public int ToolDownloadSource { get; set; } = 1;

    /// <summary>版本列表下载源，与设置界面下拉框的选项序号一致。</summary>
    public int ToolDownloadVersion { get; set; } = 1;

    /// <summary>下载完成后是否自动选择实例。</summary>
    public bool ToolDownloadAutoSelectVersion { get; set; } = true;

    /// <summary>是否修复 Authlib。</summary>
    public bool ToolFixAuthlib { get; set; } = true;

    /// <summary>第三方资源名称格式，与设置界面下拉框的选项序号一致。</summary>
    public int ToolDownloadTranslateV2 { get; set; } = 1;

    /// <summary>第三方资源下载源，与设置界面下拉框的选项序号一致。</summary>
    public int ToolDownloadMod { get; set; } = 1;

    /// <summary>已安装资源的本地名称显示方式，与设置界面下拉框的选项序号一致。</summary>
    public int ToolModLocalNameStyle { get; set; } = 0;

    /// <summary>快速下载的行为，与设置界面下拉框的选项序号一致。</summary>
    public int ToolDownloadQuickBehavior { get; set; } = 0;

    /// <summary>是否隐藏 Quilt 相关的资源。</summary>
    public bool ToolDownloadIgnoreQuilt { get; set; } = true;

    /// <summary>安装资源时是否自动安装依赖。</summary>
    public bool ToolDownloadAutoInstallDependencies { get; set; } = true;

    /// <summary>是否读取剪贴板中的资源链接。</summary>
    public bool ToolDownloadClipboard { get; set; } = false;

    /// <summary>是否提示 Minecraft 正式版更新。</summary>
    public bool ToolUpdateRelease { get; set; } = false;

    /// <summary>是否提示 Minecraft 快照版更新。</summary>
    public bool ToolUpdateSnapshot { get; set; } = false;

    /// <summary>是否在选择语言后自动切换游戏语言。</summary>
    public bool ToolHelpChinese { get; set; } = true;

    /// <summary>已提示过大下载线程风险，之后不再提示。</summary>
    public bool HintLargeDownloadThread { get; set; }

    #endregion

    #region 页面隐藏（对应 WPF 的 Config.Preference.Hide 中的设置子页面）

    public bool UiHiddenSetupLaunch { get; set; }
    public bool UiHiddenSetupUi { get; set; }
    public bool UiHiddenSetupLauncherLanguage { get; set; }
    public bool UiHiddenSetupLauncherMisc { get; set; }
    public bool UiHiddenSetupGameManage { get; set; }
    public bool UiHiddenSetupJava { get; set; }
    public bool UiHiddenSetupUpdate { get; set; }
    public bool UiHiddenSetupGameLink { get; set; }
    public bool UiHiddenSetupAbout { get; set; }
    public bool UiHiddenSetupFeedback { get; set; }
    public bool UiHiddenSetupLog { get; set; }

    #endregion

    #region 系统、缓存、联机与界面设置（对应 WPF 的 Config.System、Config.Link、Config.Preference、Config.Theme 与 States）

    /// <summary>实时日志最大行数的滑块刻度值。对应 WPF 的 SystemMaxLog。</summary>
    public int SystemMaxLog { get; set; } = 13;

    /// <summary>动画帧率上限。对应 WPF 的 UiAniFPS。</summary>
    public int UiAniFPS { get; set; } = 59;

    /// <summary>是否在启动游戏时随机增加延迟，仅用于调试。</summary>
    public bool SystemDebugDelay { get; set; }

    /// <summary>HTTP 代理地址。</summary>
    public string SystemHttpProxy { get; set; } = "";

    /// <summary>HTTP 代理类型，与设置界面下拉框的选项序号一致。</summary>
    public int SystemHttpProxyType { get; set; } = 1;

    /// <summary>自定义 HTTP 代理的用户名。</summary>
    public string SystemHttpProxyCustomUsername { get; set; } = "";

    /// <summary>自定义 HTTP 代理的密码。</summary>
    public string SystemHttpProxyCustomPassword { get; set; } = "";

    /// <summary>启动器自动更新方式，与设置界面下拉框的选项序号一致（对应 WPF 的 LauncherAutoUpdateBehavior）。</summary>
    public int SystemUpdateMode { get; set; } = 1;

    /// <summary>更新分支，与设置界面下拉框的选项序号一致（对应 WPF 的 UpdateChannel）。</summary>
    public int SystemUpdateChannel { get; set; }

    /// <summary>
    ///     渲染后端，与设置界面下拉框的选项序号一致，重启后生效。只在 Windows 上生效，其他平台始终由 Avalonia 自动选择。
    /// </summary>
    public RenderBackend SystemRenderBackend { get; set; }

    /// <summary>Mirror 酱 CDK。</summary>
    public string MirrorChyanKey { get; set; } = "";

    /// <summary>启动器启动次数。</summary>
    public int SystemLaunchCount { get; set; }

    /// <summary>是否已同意启动器用户协议。</summary>
    public bool LauncherEula { get; set; }

    /// <summary>上一次导出设置的路径。</summary>
    public string ExportConfigPath { get; set; } = "";

    /// <summary>上一次选择的下载目录。</summary>
    public string CacheDownloadFolder { get; set; } = "";

    /// <summary>已缓存的联网主页地址。</summary>
    public string CacheSavedPageUrl { get; set; } = "";

    /// <summary>已缓存的联网主页版本号。</summary>
    public string CacheSavedPageVersion { get; set; } = "";

    /// <summary>.minecraft 目录的自定义名称，键为规范化后的路径。</summary>
    public Dictionary<string, string> MinecraftFolderNames { get; set; } = new();

    /// <summary>收藏的实例目录。</summary>
    public List<string> StarredInstances { get; set; } = [];

    /// <summary>隐藏的实例目录。</summary>
    public List<string> HiddenInstances { get; set; } = [];

    /// <summary>联机大厅用户名。</summary>
    public string LinkUsername { get; set; } = "";

    /// <summary>延迟优先模式。</summary>
    public bool LinkLatencyFirstMode { get; set; } = true;

    /// <summary>传输协议优先策略。</summary>
    public LinkProtocolPreference LinkProtocolPreference { get; set; } = LinkProtocolPreference.Tcp;

    /// <summary>尝试使用端口猜测打通对称性 NAT。</summary>
    public bool LinkTryPunchSym { get; set; } = true;

    /// <summary>启用 IPv6。</summary>
    public bool LinkEnableIPv6 { get; set; } = true;

    /// <summary>在日志中输出联机组件的命令行信息，用于调试。</summary>
    public bool LinkEnableCliOutput { get; set; }

    /// <summary>中继行为（默认 / 强制中继）。对应 WPF 的 Config.Link.RelayType。</summary>
    public PCL.Shion.Core.Link.LinkRelayBehavior LinkRelayType { get; set; }

    /// <summary>中继服务器类型（0 仅社区，1 官方与社区，2 仅自定义）。对应 WPF 的 Config.Link.ServerType。</summary>
    public int LinkServerType { get; set; } = 1;

    /// <summary>自定义中继服务器，多个以分号分隔。</summary>
    public string LinkRelayServer { get; set; } = "";

    /// <summary>是否已同意联机用户协议。对应 WPF 的 States.Link.LinkEula。</summary>
    public bool LinkEula { get; set; }

    /// <summary>联机公告缓存。</summary>
    public string LinkAnnounceCache { get; set; } = "";

    /// <summary>联机公告缓存的版本号。</summary>
    public int LinkAnnounceCacheVer { get; set; }

    /// <summary>Natayark ID 的刷新令牌。</summary>
    public string LinkNaidRefreshToken { get; set; } = "";

    /// <summary>Natayark ID 刷新令牌的过期时间。</summary>
    public string LinkNaidRefreshExpiresAt { get; set; } = "";

    /// <summary>是否还没有做过首次网络测试。</summary>
    public bool LinkFirstTimeNetTest { get; set; } = true;

    /// <summary>主题模式（亮 / 暗 / 跟随系统）。对应 WPF 的 UiDarkMode。</summary>
    public ColorMode ColorMode { get; set; } = ColorMode.System;

    /// <summary>暗色配色主题。对应 WPF 的 UiDarkColor。</summary>
    public ColorTheme DarkColor { get; set; } = ColorTheme.CatBlue;

    /// <summary>亮色配色主题。对应 WPF 的 UiLightColor。</summary>
    public ColorTheme LightColor { get; set; } = ColorTheme.CatBlue;

    /// <summary>彩色底部填充。</summary>
    public bool BackgroundColorful { get; set; } = true;

    /// <summary>窗口透明度，取值为千分比。</summary>
    public int WindowOpacity { get; set; } = 600;

    /// <summary>窗口宽度。</summary>
    public double WindowWidth { get; set; } = 900;

    /// <summary>窗口高度。</summary>
    public double WindowHeight { get; set; } = 550;

    /// <summary>锁定窗口大小。</summary>
    public bool LockWindowSize { get; set; }

    /// <summary>界面语言，auto 表示跟随系统。</summary>
    public string UiLanguage { get; set; } = "auto";

    /// <summary>界面展示格式所使用的区域性，auto 表示跟随系统。</summary>
    public string UiFormatCulture { get; set; } = "auto";

    /// <summary>在启动游戏时显示「你知道吗」。</summary>
    public bool UiShowLaunchingHint { get; set; } = true;

    /// <summary>首页侧栏显示日期打卡。</summary>
    public bool UiSidebarCheckIn { get; set; }

    /// <summary>首页侧栏显示最近游玩。</summary>
    public bool UiSidebarRecentPlay { get; set; }

    /// <summary>首页侧栏显示 Minecraft 新闻。</summary>
    public bool UiSidebarNews { get; set; }

    /// <summary>启动页右侧的 CE 提示。对应 WPF 的 States.Hint.CEMessage。</summary>
    public bool UiLauncherCEHint { get; set; } = true;

    /// <summary>主页来源类型。</summary>
    public int UiCustomType { get; set; }

    /// <summary>主页预设选项。</summary>
    public int UiCustomPreset { get; set; }

    /// <summary>自定义主页地址。</summary>
    public string UiCustomNet { get; set; } = "";

    /// <summary>背景内容的裁剪模式。</summary>
    public int UiBackgroundSuit { get; set; }

    public bool UiHiddenPageDownload { get; set; }
    public bool UiHiddenPageSetup { get; set; }
    public bool UiHiddenPageTools { get; set; }
    public bool UiHiddenToolsGameLink { get; set; }
    public bool UiHiddenToolsTest { get; set; }
    public bool UiHiddenVersionEdit { get; set; }
    public bool UiHiddenVersionExport { get; set; }
    public bool UiHiddenVersionSave { get; set; }
    public bool UiHiddenVersionScreenshot { get; set; }
    public bool UiHiddenVersionMod { get; set; }
    public bool UiHiddenVersionResourcePack { get; set; }
    public bool UiHiddenVersionShader { get; set; }
    public bool UiHiddenVersionSchematic { get; set; }
    public bool UiHiddenVersionServer { get; set; }
    public bool UiHiddenFunctionSelect { get; set; }
    public bool UiHiddenFunctionModUpdate { get; set; }
    public bool UiHiddenFunctionHidden { get; set; }

    /// <summary>调试模式下的 Log4j2 配置提示是否已显示。对应 WPF 的 States.Hint.DebugLog4j2Config。</summary>
    public bool HintDebugLog4j2Config { get; set; }

    /// <summary>已提示过隐藏实例的功能。对应 WPF 的 States.Hint.HideGameInstance。</summary>
    public bool HintHideGameInstance { get; set; }

    /// <summary>清理垃圾文件的提示状态。对应 WPF 的 States.Hint.CleanJunkFile。</summary>
    public int HintClearRubbish { get; set; }

    /// <summary>已提示过实时日志行数的风险。对应 WPF 的 States.Hint.MaxGameLog。</summary>
    public bool HintMaxLog { get; set; }

    /// <summary>已提示过从安装页返回的说明。</summary>
    public bool HintInstallBack { get; set; }

    /// <summary>与 <see cref="HintInstallBack"/> 相同，供安装页使用的别名。</summary>
    [JsonIgnore]
    public bool HintInstallPageBack
    {
        get => HintInstallBack;
        set => HintInstallBack = value;
    }

    /// <summary>版本隔离 V2 的别名，与 <see cref="LaunchArgumentIndieV2"/> 相同。</summary>
    [JsonIgnore]
    public int IndieSolutionV2
    {
        get => LaunchArgumentIndieV2;
        set => LaunchArgumentIndieV2 = value;
    }

    #endregion

    #region 旧版 WPF Config 与 States 的访问入口

    /// <summary>
    /// 与 WPF 的 <c>Config</c> 对应的访问入口。Shell 中不单独保存：启动、下载、界面等设置读写上面的平铺属性，
    /// 实例独立的设置则保存在 <see cref="InstanceConfigValues"/> 与 <see cref="InstanceAuthConfigValues"/> 中。
    /// </summary>
    [JsonIgnore]
    public ConfigAccessor Config { get; }

    /// <summary>与 WPF 的 <c>States</c> 对应的访问入口，保存方式与 <see cref="Config"/> 相同。</summary>
    [JsonIgnore]
    public StatesAccessor States { get; }

    /// <summary>实例独立的设置（对应 WPF 的 Config.Instance），外层键为实例路径，内层键为 WPF 的配置键名。</summary>
    public InstanceValueStore InstanceConfigValues { get; set; } = new();

    /// <summary>实例独立的正版登录与服务器设置（对应 WPF 的 Config.InstanceAuth），结构同 <see cref="InstanceConfigValues"/>。</summary>
    public InstanceValueStore InstanceAuthConfigValues { get; set; } = new();

    /// <summary>实例的状态（对应 WPF 的 States.Instance），结构同 <see cref="InstanceConfigValues"/>。</summary>
    public InstanceValueStore InstanceStateValues { get; set; } = new();

    public AppSettings()
    {
        Config = new ConfigAccessor(this);
        States = new StatesAccessor(this);
    }

    /// <summary>
    /// 实例独立设置中的一项，按实例路径保存；未设置时返回 WPF 中的默认值。
    /// </summary>
    public sealed class InstanceArg<T> : IInstanceArg
    {
        private readonly Func<InstanceValueStore> _store;

        internal InstanceArg(string key, T defaultValue, Func<InstanceValueStore> store)
        {
            Key = key;
            DefaultValue = defaultValue;
            _store = store;
        }

        /// <summary>WPF 中的配置键名，也是界面控件的 Tag。</summary>
        public string Key { get; }

        /// <summary>未设置时的默认值。</summary>
        public T DefaultValue { get; }

        public T this[string path]
        {
            get
            {
                try
                {
                    if (_store().TryGetValue(path, out var values) && values.TryGetValue(Key, out var element))
                        return element.Deserialize<T>(JsonOptions) ?? DefaultValue;
                }
                catch (JsonException)
                {
                    // 数据损坏时退回默认值
                }

                return DefaultValue;
            }
            set
            {
                var store = _store();
                if (!store.TryGetValue(path, out var values))
                {
                    values = new Dictionary<string, JsonElement>();
                    store[path] = values;
                }

                values[Key] = JsonSerializer.SerializeToElement(value, JsonOptions);
            }
        }

        /// <summary>将该项恢复为默认值。</summary>
        public void Reset(string path)
        {
            if (_store().TryGetValue(path, out var values))
                values.Remove(Key);
        }

        bool IInstanceArg.SetFromObject(string path, object value)
        {
            this[path] = (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T),
                CultureInfo.InvariantCulture);
            return true;
        }
    }

    /// <summary>实例独立设置项的非泛型接口，供按键名写入。</summary>
    public interface IInstanceArg
    {
        string Key { get; }

        bool SetFromObject(string path, object value);
    }

    /// <summary>
    /// 一组实例独立设置。对应 WPF 中 <c>[ConfigGroup]</c> 标记的实例配置类。
    /// </summary>
    public abstract class InstanceArgGroup
    {
        private readonly Func<InstanceValueStore> _store;
        private readonly List<IInstanceArg> _args = [];

        protected InstanceArgGroup(Func<InstanceValueStore> store)
        {
            _store = store;
        }

        protected InstanceArg<T> Add<T>(string key, T defaultValue)
        {
            var arg = new InstanceArg<T>(key, defaultValue, _store);
            _args.Add(arg);
            return arg;
        }

        /// <summary>将该实例在本组内的所有设置恢复为默认值（对应 WPF 的 Reset）。</summary>
        public void Reset(string path) => _store().Remove(path);

        /// <summary>按 WPF 配置键名（即控件 Tag）写入某个实例的设置，键名不存在时返回 false。</summary>
        public bool TrySetValue(string key, object value, string path)
        {
            foreach (var arg in _args)
            {
                if (arg.Key == key)
                    return arg.SetFromObject(path, value);
            }

            return false;
        }
    }

    /// <summary>对应 WPF 的 <c>Config.Instance</c>。</summary>
    public sealed class InstanceConfigGroup : InstanceArgGroup
    {
        internal InstanceConfigGroup(Func<InstanceValueStore> store) : base(store)
        {
            JvmArgs = Add("VersionAdvanceJvm", "");
            GameArgs = Add("VersionAdvanceGame", "");
            Renderer = Add("VersionAdvanceRenderer", 0);
            AssetVerifySolutionV1 = Add("VersionAdvanceAssets", 0);
            DisableAssetVerifyV2 = Add("VersionAdvanceAssetsV2", false);
            IgnoreJavaCompatibility = Add("VersionAdvanceJava", false);
            DisableJlw = Add("VersionAdvanceDisableJLW", false);
            PreLaunchCommand = Add("VersionAdvanceRun", "");
            ClasspathHead = Add("VersionAdvanceClasspathHead", "");
            PreLaunchCommandWait = Add("VersionAdvanceRunWait", true);
            DisableLwjglUnsafeAgent = Add("VersionAdvanceDisableLwjglUnsafeAgent", false);
            UseProxy = Add("VersionAdvanceUseProxyV2", false);
            DisableLF = Add("VersionAdvanceDisableLF", false);
            UseDebugLof4j2Config = Add("VersionUseDebugLog4j2Config", false);
            MemorySolution = Add("VersionRamType", 2);
            CustomMemorySize = Add("VersionRamCustom", 15);
            CustomInitialMemorySize = Add("VersionRamCustomInitial", 0);
            Title = Add("VersionArgumentTitle", "");
            UseGlobalTitle = Add("VersionArgumentTitleEmpty", false);
            TypeInfo = Add("VersionArgumentInfo", "");
            IndieV1 = Add("VersionArgumentIndie", -1);
            IndieV2 = Add("VersionArgumentIndieV2", false);
            SelectedJava = Add("VersionArgumentJavaSelect", "使用全局设置");
            ServerToEnter = Add("VersionServerEnter", "");
        }

        public InstanceArg<string> JvmArgs { get; }
        public InstanceArg<string> GameArgs { get; }
        public InstanceArg<int> Renderer { get; }
        public InstanceArg<int> AssetVerifySolutionV1 { get; }

        /// <summary>与 <see cref="AssetVerifySolutionV1"/> 相同，供单独重置使用。</summary>
        public InstanceArg<int> AssetVerifySolutionV1Config => AssetVerifySolutionV1;

        public InstanceArg<bool> DisableAssetVerifyV2 { get; }
        public InstanceArg<bool> IgnoreJavaCompatibility { get; }
        public InstanceArg<bool> DisableJlw { get; }
        public InstanceArg<string> PreLaunchCommand { get; }
        public InstanceArg<string> ClasspathHead { get; }
        public InstanceArg<bool> PreLaunchCommandWait { get; }
        public InstanceArg<bool> DisableLwjglUnsafeAgent { get; }
        public InstanceArg<bool> UseProxy { get; }
        public InstanceArg<bool> DisableLF { get; }
        public InstanceArg<bool> UseDebugLof4j2Config { get; }

        /// <summary>内存分配方式：0 为自动，1 为手动，2 为跟随全局设置。</summary>
        public InstanceArg<int> MemorySolution { get; }

        public InstanceArg<int> CustomMemorySize { get; }
        public InstanceArg<int> CustomInitialMemorySize { get; }
        public InstanceArg<string> Title { get; }
        public InstanceArg<bool> UseGlobalTitle { get; }
        public InstanceArg<string> TypeInfo { get; }
        public InstanceArg<int> IndieV1 { get; }
        public InstanceArg<bool> IndieV2 { get; }
        public InstanceArg<string> SelectedJava { get; }
        public InstanceArg<string> ServerToEnter { get; }
    }

    /// <summary>对应 WPF 的 <c>Config.InstanceAuth</c>。</summary>
    public sealed class InstanceAuthConfigGroup : InstanceArgGroup
    {
        internal InstanceAuthConfigGroup(Func<InstanceValueStore> store) : base(store)
        {
            LoginRequirementSolution = Add("VersionServerLoginRequire", 0);
            AuthRegisterAddress = Add("VersionServerAuthRegister", "");
            AuthServerDisplayName = Add("VersionServerAuthName", "");
            AuthServerAddress = Add("VersionServerAuthServer", "");
            AuthLocked = Add("VersionServerLoginLock", false);
        }

        public InstanceArg<int> LoginRequirementSolution { get; }
        public InstanceArg<string> AuthRegisterAddress { get; }
        public InstanceArg<string> AuthServerDisplayName { get; }
        public InstanceArg<string> AuthServerAddress { get; }
        public InstanceArg<bool> AuthLocked { get; }
    }

    /// <summary>对应 WPF 的 <c>States.Instance</c>。</summary>
    public sealed class InstanceStatesGroup : InstanceArgGroup
    {
        internal InstanceStatesGroup(Func<InstanceValueStore> store) : base(store)
        {
            LaunchCount = Add("VersionLaunchCount", 0);
            Starred = Add("IsStar", false);
            CardType = Add("DisplayType", 0);
            LogoPath = Add("Logo", "");
            IsLogoCustom = Add("LogoCustom", false);
            CustomInfo = Add("CustomInfo", "");
            Info = Add("Info", "");
            ReleaseTime = Add("ReleaseTime", "");
            State = Add("State", 0);
            ModpackVersion = Add("VersionModpackVersion", "");
            ModpackSource = Add("VersionModpackSource", "");
            ModpackId = Add("VersionModpackId", "");
        }

        public InstanceArg<int> LaunchCount { get; }
        public InstanceArg<bool> Starred { get; }

        /// <summary>卡片显示类型，与实例卡片的类型序号一致。</summary>
        public InstanceArg<int> CardType { get; }

        public InstanceArg<string> LogoPath { get; }
        public InstanceArg<bool> IsLogoCustom { get; }
        public InstanceArg<string> CustomInfo { get; }
        public InstanceArg<string> Info { get; }
        public InstanceArg<string> ReleaseTime { get; }
        public InstanceArg<int> State { get; }
        public InstanceArg<string> ModpackVersion { get; }
        public InstanceArg<string> ModpackSource { get; }
        public InstanceArg<string> ModpackId { get; }
    }

    /// <summary>对应 WPF 的 <c>Config.Launch</c>，直接读写 <see cref="AppSettings"/> 中对应的平铺属性。</summary>
    public sealed class LaunchConfigAccessor
    {
        private readonly AppSettings _owner;

        internal LaunchConfigAccessor(AppSettings owner) => _owner = owner;

        public bool LockMemory
        {
            get => _owner.LaunchAdvanceLockMemory;
            set => _owner.LaunchAdvanceLockMemory = value;
        }

        public int MemoryAllocationMode
        {
            get => _owner.LaunchRamType;
            set => _owner.LaunchRamType = value;
        }

        public int CustomMemorySize
        {
            get => _owner.LaunchRamCustom;
            set => _owner.LaunchRamCustom = value;
        }

        public int CustomInitialMemorySize
        {
            get => _owner.LaunchRamCustomInitial;
            set => _owner.LaunchRamCustomInitial = value;
        }

        public int PreferredIpStack
        {
            get => _owner.LaunchPreferredIpStack;
            set => _owner.LaunchPreferredIpStack = value;
        }

        public string JvmArgs
        {
            get => _owner.LaunchAdvanceJvm;
            set => _owner.LaunchAdvanceJvm = value;
        }

        public string GameArgs
        {
            get => _owner.LaunchAdvanceGame;
            set => _owner.LaunchAdvanceGame = value;
        }

        public string PreLaunchCommand
        {
            get => _owner.LaunchAdvanceRun;
            set => _owner.LaunchAdvanceRun = value;
        }

        public bool PreLaunchCommandWait
        {
            get => _owner.LaunchAdvanceRunWait;
            set => _owner.LaunchAdvanceRunWait = value;
        }

        public bool DisableJlw
        {
            get => _owner.LaunchAdvanceDisableJLW;
            set => _owner.LaunchAdvanceDisableJLW = value;
        }

        public bool DisableLF
        {
            get => _owner.LaunchAdvanceDisableLF;
            set => _owner.LaunchAdvanceDisableLF = value;
        }

        public bool SetGpuPreference
        {
            get => _owner.LaunchAdvanceGraphicCard;
            set => _owner.LaunchAdvanceGraphicCard = value;
        }

        public bool NoJavaw
        {
            get => _owner.LaunchAdvanceNoJavaw;
            set => _owner.LaunchAdvanceNoJavaw = value;
        }

        public bool DisableLwjglUnsafeAgent
        {
            get => _owner.LaunchAdvanceDisableLwjglUnsafeAgent;
            set => _owner.LaunchAdvanceDisableLwjglUnsafeAgent = value;
        }

        public bool DisableCrashAnalysis
        {
            get => _owner.LaunchAdvanceDisableCrashAnalysis;
            set => _owner.LaunchAdvanceDisableCrashAnalysis = value;
        }

        public int Renderer
        {
            get => _owner.LaunchAdvanceRenderer;
            set => _owner.LaunchAdvanceRenderer = value;
        }

        public string Title
        {
            get => _owner.LaunchArgumentTitle;
            set => _owner.LaunchArgumentTitle = value;
        }

        public string TypeInfo
        {
            get => _owner.LaunchArgumentInfo;
            set => _owner.LaunchArgumentInfo = value;
        }

        public int IndieSolutionV2
        {
            get => _owner.LaunchArgumentIndieV2;
            set => _owner.LaunchArgumentIndieV2 = value;
        }

        public int LauncherVisibility
        {
            get => _owner.LaunchArgumentVisible;
            set => _owner.LaunchArgumentVisible = value;
        }

        public int ProcessPriority
        {
            get => _owner.LaunchArgumentPriority;
            set => _owner.LaunchArgumentPriority = value;
        }

        public int GameWindowWidth
        {
            get => _owner.LaunchArgumentWindowWidth;
            set => _owner.LaunchArgumentWindowWidth = value;
        }

        public int GameWindowHeight
        {
            get => _owner.LaunchArgumentWindowHeight;
            set => _owner.LaunchArgumentWindowHeight = value;
        }

        public int GameWindowMode
        {
            get => _owner.LaunchArgumentWindowType;
            set => _owner.LaunchArgumentWindowType = value;
        }

        public int LoginMsAuthType
        {
            get => _owner.LoginMsAuthType;
            set => _owner.LoginMsAuthType = value;
        }
    }

    /// <summary>对应 WPF 的 <c>Config.Preference.Hide</c>，读写 <c>UiHidden*</c> 开头的平铺属性。</summary>
    public sealed class HideConfigAccessor
    {
        private readonly AppSettings _owner;

        internal HideConfigAccessor(AppSettings owner) => _owner = owner;

        public bool PageDownload
        {
            get => _owner.UiHiddenPageDownload;
            set => _owner.UiHiddenPageDownload = value;
        }

        public bool PageSetup
        {
            get => _owner.UiHiddenPageSetup;
            set => _owner.UiHiddenPageSetup = value;
        }

        public bool PageTools
        {
            get => _owner.UiHiddenPageTools;
            set => _owner.UiHiddenPageTools = value;
        }

        public bool SetupLaunch
        {
            get => _owner.UiHiddenSetupLaunch;
            set => _owner.UiHiddenSetupLaunch = value;
        }

        public bool SetupUi
        {
            get => _owner.UiHiddenSetupUi;
            set => _owner.UiHiddenSetupUi = value;
        }

        public bool SetupLauncherLanguage
        {
            get => _owner.UiHiddenSetupLauncherLanguage;
            set => _owner.UiHiddenSetupLauncherLanguage = value;
        }

        public bool SetupLauncherMisc
        {
            get => _owner.UiHiddenSetupLauncherMisc;
            set => _owner.UiHiddenSetupLauncherMisc = value;
        }

        public bool SetupGameManage
        {
            get => _owner.UiHiddenSetupGameManage;
            set => _owner.UiHiddenSetupGameManage = value;
        }

        public bool SetupJava
        {
            get => _owner.UiHiddenSetupJava;
            set => _owner.UiHiddenSetupJava = value;
        }

        public bool SetupUpdate
        {
            get => _owner.UiHiddenSetupUpdate;
            set => _owner.UiHiddenSetupUpdate = value;
        }

        public bool SetupGameLink
        {
            get => _owner.UiHiddenSetupGameLink;
            set => _owner.UiHiddenSetupGameLink = value;
        }

        public bool SetupAbout
        {
            get => _owner.UiHiddenSetupAbout;
            set => _owner.UiHiddenSetupAbout = value;
        }

        public bool SetupFeedback
        {
            get => _owner.UiHiddenSetupFeedback;
            set => _owner.UiHiddenSetupFeedback = value;
        }

        public bool SetupLog
        {
            get => _owner.UiHiddenSetupLog;
            set => _owner.UiHiddenSetupLog = value;
        }

        public bool ToolsGameLink
        {
            get => _owner.UiHiddenToolsGameLink;
            set => _owner.UiHiddenToolsGameLink = value;
        }

        public bool ToolsTest
        {
            get => _owner.UiHiddenToolsTest;
            set => _owner.UiHiddenToolsTest = value;
        }

        public bool InstanceEdit
        {
            get => _owner.UiHiddenVersionEdit;
            set => _owner.UiHiddenVersionEdit = value;
        }

        public bool InstanceExport
        {
            get => _owner.UiHiddenVersionExport;
            set => _owner.UiHiddenVersionExport = value;
        }

        public bool InstanceSave
        {
            get => _owner.UiHiddenVersionSave;
            set => _owner.UiHiddenVersionSave = value;
        }

        public bool InstanceScreenshot
        {
            get => _owner.UiHiddenVersionScreenshot;
            set => _owner.UiHiddenVersionScreenshot = value;
        }

        public bool InstanceMod
        {
            get => _owner.UiHiddenVersionMod;
            set => _owner.UiHiddenVersionMod = value;
        }

        public bool InstanceResourcePack
        {
            get => _owner.UiHiddenVersionResourcePack;
            set => _owner.UiHiddenVersionResourcePack = value;
        }

        public bool InstanceShader
        {
            get => _owner.UiHiddenVersionShader;
            set => _owner.UiHiddenVersionShader = value;
        }

        public bool InstanceSchematic
        {
            get => _owner.UiHiddenVersionSchematic;
            set => _owner.UiHiddenVersionSchematic = value;
        }

        public bool InstanceServer
        {
            get => _owner.UiHiddenVersionServer;
            set => _owner.UiHiddenVersionServer = value;
        }

        public bool FunctionSelect
        {
            get => _owner.UiHiddenFunctionSelect;
            set => _owner.UiHiddenFunctionSelect = value;
        }

        public bool FunctionModUpdate
        {
            get => _owner.UiHiddenFunctionModUpdate;
            set => _owner.UiHiddenFunctionModUpdate = value;
        }

        public bool FunctionHidden
        {
            get => _owner.UiHiddenFunctionHidden;
            set => _owner.UiHiddenFunctionHidden = value;
        }
    }

    /// <summary>对应 WPF 的 <c>Config.Preference</c>，目前只包含功能隐藏部分。</summary>
    public sealed class PreferenceConfigAccessor
    {
        internal PreferenceConfigAccessor(AppSettings owner) => Hide = new HideConfigAccessor(owner);

        public HideConfigAccessor Hide { get; }
    }

    /// <summary>
    /// 与 WPF 的 <c>Config</c> 对应的访问入口。
    /// </summary>
    public sealed class ConfigAccessor
    {
        internal ConfigAccessor(AppSettings owner)
        {
            Launch = new LaunchConfigAccessor(owner);
            Preference = new PreferenceConfigAccessor(owner);
            Instance = new InstanceConfigGroup(() => owner.InstanceConfigValues);
            InstanceAuth = new InstanceAuthConfigGroup(() => owner.InstanceAuthConfigValues);
        }

        public LaunchConfigAccessor Launch { get; }

        public PreferenceConfigAccessor Preference { get; }

        /// <summary>实例独立设置。</summary>
        public InstanceConfigGroup Instance { get; }

        /// <summary>实例的登录与服务器设置。</summary>
        public InstanceAuthConfigGroup InstanceAuth { get; }

        /// <summary>按 WPF 配置键名写入某个实例的设置，实例设置与认证设置都会查找；键名不存在时返回 false。</summary>
        public bool TrySetValue(string tag, object value, string instancePath) =>
            Instance.TrySetValue(tag, value, instancePath) || InstanceAuth.TrySetValue(tag, value, instancePath);
    }

    /// <summary>对应 WPF 的 <c>States.Hint</c>，读写对应的平铺属性。</summary>
    public sealed class HintStatesAccessor
    {
        private readonly AppSettings _owner;

        internal HintStatesAccessor(AppSettings owner) => _owner = owner;

        public bool Renderer
        {
            get => _owner.HintRenderer;
            set => _owner.HintRenderer = value;
        }

        public bool DebugLog4j2Config
        {
            get => _owner.HintDebugLog4j2Config;
            set => _owner.HintDebugLog4j2Config = value;
        }

        public bool HideGameInstance
        {
            get => _owner.HintHideGameInstance;
            set => _owner.HintHideGameInstance = value;
        }

        public bool InstallPageBack
        {
            get => _owner.HintInstallBack;
            set => _owner.HintInstallBack = value;
        }

        public bool LargeDownloadThread
        {
            get => _owner.HintLargeDownloadThread;
            set => _owner.HintLargeDownloadThread = value;
        }

        public int CleanJunkFile
        {
            get => _owner.HintClearRubbish;
            set => _owner.HintClearRubbish = value;
        }

        public bool MaxGameLog
        {
            get => _owner.HintMaxLog;
            set => _owner.HintMaxLog = value;
        }

        public bool CEMessage
        {
            get => _owner.UiLauncherCEHint;
            set => _owner.UiLauncherCEHint = value;
        }
    }

    /// <summary>
    /// 与 WPF 的 <c>States</c> 对应的访问入口。
    /// </summary>
    public sealed class StatesAccessor
    {
        internal StatesAccessor(AppSettings owner)
        {
            Hint = new HintStatesAccessor(owner);
            Instance = new InstanceStatesGroup(() => owner.InstanceStateValues);
        }

        public HintStatesAccessor Hint { get; }

        /// <summary>实例的状态，例如收藏、卡片类型与日志。</summary>
        public InstanceStatesGroup Instance { get; }
    }

    #endregion

    private static string FilePath { get; } = Path.Combine(
        SpecialFolders.Get(Environment.SpecialFolder.ApplicationData), "PCL Shion", "settings.json");

    /// <summary>
    /// 恢复启动设置（对应 WPF 的 Config.Launch.Reset），包括默认 Java 的选择。
    /// </summary>
    public void ResetLaunch()
    {
        LaunchRamType = 0;
        LaunchRamCustom = 15;
        LaunchRamCustomInitial = 0;
        LaunchAdvanceLockMemory = false;
        LaunchPreferredIpStack = 0;
        LaunchAdvanceJvm = DefaultLaunchJvmArgs;
        LaunchAdvanceGame = "";
        LaunchAdvanceRun = "";
        LaunchAdvanceRunWait = true;
        LaunchAdvanceDisableJLW = true;
        LaunchAdvanceDisableLF = false;
        LaunchAdvanceGraphicCard = true;
        LaunchAdvanceNoJavaw = false;
        LaunchAdvanceDisableLwjglUnsafeAgent = false;
        LaunchAdvanceDisableCrashAnalysis = false;
        LaunchAdvanceRenderer = 0;
        LaunchArgumentTitle = "";
        LaunchArgumentInfo = "PCLCE";
        LaunchArgumentIndieV2 = 4;
        LaunchArgumentVisible = 5;
        LaunchArgumentPriority = 1;
        LaunchArgumentWindowWidth = 854;
        LaunchArgumentWindowHeight = 480;
        LaunchArgumentWindowType = 1;
        LoginMsAuthType = 1;
        DefaultJavaExecutable = null;
    }

    /// <summary>
    /// 恢复下载设置（对应 WPF 的 Config.Download.Reset）。
    /// </summary>
    public void ResetDownload()
    {
        ToolDownloadThread = 63;
        ToolDownloadFileConnection = 7;
        ToolDownloadSpeed = 42;
        ToolDownloadHttpMode = 0;
        ToolDownloadSource = 1;
        ToolDownloadVersion = 1;
        ToolDownloadAutoSelectVersion = true;
        ToolFixAuthlib = true;
        ToolDownloadTranslateV2 = 1;
        ToolDownloadMod = 1;
        ToolModLocalNameStyle = 0;
        ToolDownloadQuickBehavior = 0;
        ToolDownloadIgnoreQuilt = true;
        ToolDownloadAutoInstallDependencies = true;
        ToolDownloadClipboard = false;
    }

    /// <summary>
    /// 恢复工具设置（对应 WPF 的 Config.Tool.Reset）。
    /// </summary>
    public void ResetTool()
    {
        ToolHelpChinese = true;
        ToolUpdateRelease = false;
        ToolUpdateSnapshot = false;
    }

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            if (ReadLegacyRoot(json) is { } legacyRoot)
                settings.MinecraftFolders = [legacyRoot];
            else if (settings.MinecraftFolders.Count == 0)
                settings.MinecraftFolders.Add(MinecraftFolder.GetDefault());
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    /// <summary>旧版本只保存一个目录，键名为 <c>MinecraftRoot</c>。设置里还没有目录列表时返回它，作为迁移后唯一的目录。</summary>
    private static string? ReadLegacyRoot(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return !root.TryGetProperty(nameof(MinecraftFolders), out _)
               && root.TryGetProperty(nameof(MinecraftRoot), out var legacy)
               && legacy.ValueKind == JsonValueKind.String
            ? legacy.GetString()
            : null;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 设置只是便利功能，写入失败不应影响启动
        }
    }
}
