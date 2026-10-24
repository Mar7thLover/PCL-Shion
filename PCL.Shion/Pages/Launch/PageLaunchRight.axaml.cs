using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Homepage;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Launch;

public partial class PageLaunchRight : MyPageRight, IRefreshable
{
    private static PageLaunchRight? _instance;

    /// <summary>
    ///     首页右侧页面的唯一实例，与 WPF 的 ModMain.frmLaunchRight 相同。
    /// </summary>
    public static PageLaunchRight Instance => _instance ??= new PageLaunchRight();

    public PageLaunchRight()
    {
        InitializeComponent();
        _instance ??= this;
        onlineLoader = new ModLoader.LoaderTask<string, int>(
            Lang.Text("Launch.Homepage.Task.Download"),
            OnlineLoaderSub)
        {
            reloadTimeout = 10 * 60 * 1000
        };
        Loaded += (_, _) => Init();
        Loaded += (_, _) => Refresh();
        Loaded += (_, _) => RefreshWidgetsCore(true);
        Unloaded += (_, _) => _DisposeHomepageLiveWatcher();
    }

    private void Init()
    {
        PanBack.ScrollToHome();
        PanScroll = PanBack; // 不知道为啥不能在 XAML 设置
        PanLog.IsVisible = HomepageSupport.ModeDebug;
        // 社区版提示
        PanHint.IsVisible = App.Services.Settings.UiLauncherCEHint;
        LabHint1.Text = Lang.Text("Launch.Right.CommunityHint.Message");
        LabHint2.Text = Lang.Text("Launch.Right.CommunityHint.HidePrompt");
        _EnsureHomepageLiveWatcher();
    }

    // 暂时关闭快照版提示
    private async void BtnHintClose_Click(object? sender, PointerReleasedEventArgs e)
    {
        var input = await MsgBox.InputAsync(Lang.Text("Launch.Right.CommunityHint.InputTitle"));
        if (string.IsNullOrWhiteSpace(input))
            return;
        input = new string(input.Where(char.IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant();
        if (input.Contains("mar7thlover"))
        {
            ModAnimation.AniDispose(PanHint, true);
            var settings = App.Services.Settings;
            settings.UiLauncherCEHint = false;
            settings.Save();
        }
        else
        {
            Hint.Show(Lang.Text("Launch.Right.CommunityHint.WrongInput"));
        }
    }

    #region 主页

    /// <summary>
    ///     刷新主页。
    /// </summary>
    private void Refresh()
    {
        Task.Run(() =>
        {
            try
            {
                lock (refreshLock)
                {
                    RefreshReal();
                }
            }
            catch (Exception ex)
            {
                HomepageSupport.ReportError(ex, "加载 PCL 主页自定义信息失败",
                    Lang.Text("Launch.Error.OperationFailed"));
            }
        });
    }

    private void RefreshReal()
    {
        var content = "";
        string? url;
        var settings = App.Services.Settings;

        var uiCustomType = settings.UiCustomType;

        if (uiCustomType == 1)
        {
            // 本地文件
            HomepageSupport.Log("[Page] 主页自定义数据来源：本地文件");
            content = HomepageSupport.ReadFile(Path.Combine(HomepageSupport.ExeDirectory, "PCL", "Custom.xaml"));
        }
        else if (uiCustomType == 2)
        {
            // 网络文件
            url = settings.UiCustomNet;
            content = LoadFromNetwork(url);
        }
        else if (uiCustomType == 3)
        {
            // 预设主页
            var preset = settings.UiCustomPreset;
            switch (preset)
            {
                case 0:
                    HomepageSupport.Log("[Page] 主页预设：你知道吗");
                    var hintText = GetRandomHint();
                    content = $@"
    <c:MyCard Title=""{{DynamicResource Launch.Status.Trivia}}"" Margin=""0,0,0,15"">
        <TextBlock Margin=""25,38,23,15"" FontSize=""13.5"" IsHitTestVisible=""False"" Text=""{hintText}"" TextWrapping=""Wrap"" Foreground=""{{DynamicResource ColorBrush1}}"" />
        <c:MyIconButton Height=""22"" Width=""22"" Margin=""9"" VerticalAlignment=""Top"" HorizontalAlignment=""Right""
            Tag=""{HomepageSupport.RefreshTag}""
            SvgIcon=""lucide/refresh-cw"" />
    </c:MyCard>";
                    break;

                case 1:
                    HomepageSupport.Log("[Page] 主页预设：回声洞 已被移除");
                    var removed = Lang.Text("Launch.Homepage.Preset.EchoCave.Removed");
                    HomepageSupport.RunInUi(() => _ = MsgBox.ShowAsync(removed));
                    return;

                case 2:
                    HomepageSupport.Log("[Page] 主页预设：Minecraft 新闻");
                    url = "https://news.bugjump.net";
                    content = LoadFromNetwork(url);
                    break;

                case 3:
                    HomepageSupport.Log("[Page] 主页预设：每日整合包推荐");
                    url = "https://pclsub.sodamc.com/";
                    content = LoadFromNetwork(url);
                    break;

                case 4:
                    HomepageSupport.Log("[Page] 主页预设：Minecraft 皮肤推荐");
                    url = "https://forgepixel.com/pcl_sub_file";
                    content = LoadFromNetwork(url);
                    break;

                case 5:
                    HomepageSupport.Log("[Page] 主页预设：OpenBMCLAPI 仪表盘 Lite");
                    url = "https://pcl-bmcl.milu.ink/";
                    content = LoadFromNetwork(url);
                    break;

                case 6:
                    HomepageSupport.Log("[Page] 主页预设：PCL 新功能说明书");
                    url = "https://raw.gitcode.com/WForst-Breeze/WhatsNewPCL/raw/main/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 7:
                    HomepageSupport.Log("[Page] 主页预设：杂志主页");
                    url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Ext1nguisher/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 8:
                    HomepageSupport.Log("[Page] 主页预设：PCL GitHub 仪表盘");
                    url = "https://ddf.pcl-community.org/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 9:
                    HomepageSupport.Log("[Page] 主页预设：Minecraft 更新摘要");
                    url = "https://raw.gitcode.com/ENC_Euphony/PCL-AI-Summary-HomePage/raw/master/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 10:
                    HomepageSupport.Log("[Page] 主页预设：今日新闻热点");
                    url = "https://pcl.wyc-w.top/index.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 11:
                    HomepageSupport.Log("[Page] 主页预设：Minecraft 芝士站");
                    url = "https://www.xxag.top/mkss";
                    content = LoadFromNetwork(url);
                    break;

                case 12:
                    HomepageSupport.Log("[Page] 主页预设：整合包推荐引擎");
                    url = "https://qawsedrftgyhujiko.fun/pcl2/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 13:
                    HomepageSupport.Log("[Page] 主页预设：Bangumi 番剧主页");
                    url = "https://bangumi.p.kaphia.qzz.io";
                    content = LoadFromNetwork(url);
                    break;

                case 14:
                    HomepageSupport.Log("[Page] 主页预设：PCL CE 公告栏");
                    url = "https://s3.pysio.online/pcl2-ce/apiv2/pages/announce.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 15:
                    HomepageSupport.Log("[Page] 主页预设：Minecraft 信息流");
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        _frmHomepageNews ??= new PageHomepageNewsView();
                        PanCustom.Children.Clear();
                        PanCustom.Children.Add(_frmHomepageNews);
                    });
                    return;
            }
        }

        HomepageSupport.RunInUi(() => LoadContent(content));
    }

    /// <summary>
    ///     根据 URL 加载网络内容，优先使用缓存。
    /// </summary>
    private string LoadFromNetwork(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";

        var cachePath = Path.Combine(HomepageSupport.TempDirectory, "Cache", "Custom.xaml");
        var settings = App.Services.Settings;
        var cachedUrl = settings.CacheSavedPageUrl;

        if (url == cachedUrl && File.Exists(cachePath))
        {
            HomepageSupport.Log("[Page] 主页自定义数据来源：联网缓存文件");
            // 后台更新缓存
            onlineLoader.Start(url);
            return HomepageSupport.ReadFile(cachePath);
        }

        HomepageSupport.Log("[Page] 主页自定义数据来源：联网全新下载");
        HomepageSupport.RunInUi(() => Hint.Show(Lang.Text("Launch.Homepage.Loading")));
        HomepageSupport.RunInUiWait(() => LoadContent("")); // 先清空页面
        settings.CacheSavedPageVersion = "";
        settings.Save();
        onlineLoader.Start(url); // 下载完成后将会再次触发更新
        return "";
    }

    private readonly object refreshLock = new();

    public static string GetRandomHint(bool enableLengthLimit = false, bool raw = false)
    {
        string[]? lines = null;

        // 外部文件
        var externalPath = Path.Combine(HomepageSupport.ExeDirectory, "PCL", "hints.txt");
        if (File.Exists(externalPath))
        {
            try
            {
                lines = File.ReadAllLines(externalPath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .ToArray();
            }
            catch (Exception ex)
            {
                var message = Lang.Text("Launch.Homepage.Error.ExternalFile", externalPath);
                HomepageSupport.ReportError(ex, message, message);
            }
        }

        // 嵌入式资源
        if (lines is null || lines.Length == 0)
            lines = HomepageSupport.LoadEmbeddedHints(LocalizationService.CurrentLanguage.Code);
        if (lines is null || lines.Length == 0)
            return "";

        // 长度限制
        if (enableLengthLimit)
        {
            var shortLines = lines.Where(l => l.Length < 50).ToArray();
            if (shortLines.Length > 0) lines = shortLines;
        }

        // 随机返回
        var hint = lines[Random.Shared.Next(lines.Length)];
        return raw ? hint : hint.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    // 联网获取主页文件
    private readonly ModLoader.LoaderTask<string, int> onlineLoader;

    private void OnlineLoaderSub(ModLoader.LoaderTask<string, int> task)
    {
        var address = task.input ?? ""; // #3721 中连续触发两次导致内容变化
        try
        {
            // 获取版本校验地址
            string versionAddress;
            if (address.Contains(".xaml"))
            {
                versionAddress = address.Replace(".xaml", ".xaml.ini");
            }
            else
            {
                versionAddress = address.Split('?')[0];
                if (!versionAddress.EndsWith("/"))
                    versionAddress += "/";
                versionAddress += "version";
                if (address.Contains("?"))
                    versionAddress += "?" + address[(address.IndexOf('?') + 1)..];
            }

            // 校验版本
            var version = "";
            var needDownload = true;
            try
            {
                version = HomepageSupport.FetchString(versionAddress);
                if (version.Length > 1000)
                    throw new Exception($"获取的主页版本过长（{version.Length} 字符）");
                var currentVersion = App.Services.Settings.CacheSavedPageVersion;
                if (!string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(currentVersion) &&
                    version == currentVersion)
                {
                    HomepageSupport.Log($"[Page] 当前缓存的主页已为最新，当前版本：{version}，检查源：{versionAddress}");
                    needDownload = false;
                }
                else
                {
                    HomepageSupport.Log($"[Page] 需要下载联网主页，当前版本：{version}，检查源：{versionAddress}");
                }
            }
            catch (Exception exx)
            {
                HomepageSupport.Log(exx, "联网获取主页版本失败", LogEventLevel.Debug);
                HomepageSupport.Log($"[Page] 无法检查联网主页版本，将直接下载，检查源：{versionAddress}");
            }

            // 实际下载
            if (needDownload)
            {
                var fileContent = HomepageSupport.FetchString(address);
                HomepageSupport.Log($"[Page] 已联网下载主页，内容长度：{fileContent.Length}，来源：{address}");
                var settings = App.Services.Settings;
                settings.CacheSavedPageUrl = address;
                settings.CacheSavedPageVersion = version;
                settings.Save();
                HomepageSupport.WriteFile(Path.Combine(HomepageSupport.TempDirectory, "Cache", "Custom.xaml"), fileContent);
            }

            // 要求刷新
            HomepageSupport.RunInUi(Refresh); // 不直接调用 Refresh，以防止死循环（#6245）
        }
        catch (Exception ex)
        {
            HomepageSupport.ReportError(ex, Lang.Text("Launch.Homepage.Error.Download", address),
                Lang.Text("Launch.Homepage.Error.Download", address));
        }
    }

    /// <summary>
    ///     立即强制刷新主页。
    ///     必须在 UI 线程调用。
    /// </summary>
    public void ForceRefresh()
    {
        HomepageSupport.Log("[Page] 要求强制刷新主页");
        ClearCache();
        // 实际的刷新
        if (Navigator.Current == PageType.Launch)
        {
            PanBack.ScrollToHome();
            Refresh();
            RefreshWidgetsCore(true);
        }
        else
        {
            Navigator.Go(PageType.Launch);
        }
    }

    void IRefreshable.Refresh()
    {
        ForceRefresh();
    }

    /// <summary>
    ///     清空主页缓存信息。
    /// </summary>
    private void ClearCache()
    {
        loadedContentHash = -1;
        onlineLoader.input = "";
        var settings = App.Services.Settings;
        settings.CacheSavedPageUrl = "";
        settings.CacheSavedPageVersion = "";
        settings.Save();
        HomepageSupport.Log("[Page] 已清空主页缓存");
    }

    /// <summary>
    ///     从文本内容中加载主页。
    ///     必须在 UI 线程调用。
    /// </summary>
    private void LoadContent(string content)
    {
        lock (loadContentLock)
        {
            // 如果加载目标内容一致则不加载
            var hash = content.GetHashCode();
            if (hash == loadedContentHash)
            {
                _ApplyHomepageLivePatchesFromFile();
                return;
            }

            loadedContentHash = hash;
            // 实际加载内容
            PanCustom.Children.Clear();
            if (string.IsNullOrWhiteSpace(content))
            {
                HomepageSupport.Log("[Page] 实例化：清空主页 UI，来源为空");
                return;
            }

            var loadStartTime = DateTime.Now;
            try
            {
                content = HomepageSupport.ArgumentReplace(content);
                content = HomepageSupport.StripXmlns(content);
                content = HomepageSupport.WrapPanel(content);
                HomepageSupport.Log($"[Page] 实例化：加载主页 UI 开始，最终内容长度：{content.Length}");
                var panel = HomepageSupport.LoadXaml(content);
                if (panel is Control control)
                {
                    PanCustom.Children.Add(control);
                    HomepageSupport.BindRefreshButtons(control, ForceRefresh);
                }

                _ApplyHomepageLivePatchesFromFile();
            }
            catch (Exception ex)
            {
                if (HomepageSupport.ModeDebug)
                {
                    HomepageSupport.Log(ex, $"加载失败的主页内容：\r\n{content}");
                    var text = ex is UnauthorizedAccessException
                        ? ex.Message
                        : Lang.Text("Launch.Homepage.LoadFailed.Message", ex);
                    Dispatcher.UIThread.Post(async () =>
                    {
                        if (await MsgBox.ShowAsync(text, Lang.Text("Launch.Homepage.LoadFailed.Title"),
                                Lang.Text("Launch.Homepage.LoadFailed.Retry"),
                                Lang.Text("Common.Action.Cancel")) == 1)
                            ForceRefresh(); // 防止 SyncLock 死锁
                    });
                }
                else
                {
                    HomepageSupport.ReportError(ex, Lang.Text("Launch.Homepage.LoadFailed.Title"),
                        Lang.Text("Launch.Homepage.LoadFailed.Title"));
                }

                return;
            }

            var loadCostTime = (DateTime.Now - loadStartTime).Milliseconds;
            HomepageSupport.Log($"[Page] 实例化：加载主页 UI 完成，耗时 {loadCostTime}ms");
            if (loadCostTime > 3000)
                Hint.Show(Lang.Text("Launch.Homepage.SlowWarning",
                    Math.Round(loadCostTime / 1000d, 1).ToString("N1", CultureInfo.CurrentCulture)));
        }
    }

    private int loadedContentHash = -1;
    private readonly object loadContentLock = new();
    private PageHomepageNewsView? _frmHomepageNews;

    #endregion

    #region 首页小工具

    // 控件只创建一次并缓存，开关关闭时仅从 PanWidgets 移除
    private CheckInWidget? checkInWidget;
    private RecentPlayWidget? recentPlayWidget;
    private NewsWidget? newsWidget;

    /// <summary>
    ///     根据 “首页侧栏” 的显示开关刷新小工具：按开关创建、添加或移除控件。
    ///     可在任意线程调用。页面尚未加载（或当前不在首页）时不做任何事，页面加载时会自动同步。
    /// </summary>
    public void RefreshWidgets()
    {
        HomepageSupport.RunInUi(() => RefreshWidgetsCore(false));
    }

    /// <summary>
    ///     刷新小工具的实际逻辑。必须在 UI 线程调用。
    /// </summary>
    /// <param name="refreshContent">是否对已显示的控件调用其 Refresh。刚创建的控件会自行加载，不会再被调用。</param>
    private void RefreshWidgetsCore(bool refreshContent)
    {
        try
        {
            if (!IsLoaded)
                return;

            var sidebar = App.Services.Settings;
            var wanted = new List<Control>();
            var created = new HashSet<Control>();

            // 按固定顺序收集需要显示的控件
            if (sidebar.UiSidebarCheckIn)
                CollectWidget(ref checkInWidget, "CheckInWidget", wanted, created);
            if (sidebar.UiSidebarRecentPlay)
                CollectWidget(ref recentPlayWidget, "RecentPlayWidget", wanted, created);
            if (sidebar.UiSidebarNews)
                CollectWidget(ref newsWidget, "NewsWidget", wanted, created);

            // 移除多余的控件，并把需要显示的控件按顺序放入 PanWidgets
            var children = PanWidgets.Children;
            for (var i = children.Count - 1; i >= 0; i--)
                if (children[i] is not Control child || !wanted.Contains(child))
                    children.RemoveAt(i);
            var readded = new List<Control>(); // 之前被关掉、现在重新显示的缓存控件，内容可能已过期
            for (var i = 0; i < wanted.Count; i++)
            {
                var widget = wanted[i];
                var index = children.IndexOf(widget);
                if (index == i)
                    continue;
                if (index >= 0)
                    children.RemoveAt(index);
                else if (!created.Contains(widget))
                    readded.Add(widget);
                children.Insert(i, widget);
            }

            // 全部关闭时，整个小工具区域隐藏
            PanWidgets.IsVisible = wanted.Count > 0;

            // 刷新已显示的控件
            var toRefresh = refreshContent ? wanted.Where(widget => !created.Contains(widget)).ToList() : readded;
            foreach (var widget in toRefresh)
                RefreshWidget(widget);
        }
        catch (Exception ex)
        {
            HomepageSupport.Log(ex, "刷新首页侧栏小工具失败");
        }
    }

    /// <summary>
    ///     取出缓存的小工具控件，没有则创建，并按显示顺序加入列表。创建失败的控件不显示。
    /// </summary>
    private static void CollectWidget<T>(ref T? cache, string name, List<Control> wanted,
        HashSet<Control> created) where T : Control, new()
    {
        if (cache is null)
        {
            try
            {
                cache = new T();
                // 控件自己没有设置外边距时，补上与首页卡片一致的间距
                if (cache.Margin == default(Thickness))
                    cache.Margin = new Thickness(0, 0, 0, 15);
                created.Add(cache);
            }
            catch (Exception ex)
            {
                cache = null;
                HomepageSupport.Log(ex, $"创建首页侧栏小工具失败：{name}");
                return;
            }
        }

        wanted.Add(cache);
    }

    private void RefreshWidget(Control widget)
    {
        try
        {
            switch (widget)
            {
                case CheckInWidget checkIn:
                    checkIn.Refresh();
                    break;
                case RecentPlayWidget recentPlay:
                    recentPlay.Refresh();
                    break;
                case NewsWidget news:
                    news.Refresh(); // 距离上次成功加载较近时，控件自己会跳过联网请求
                    break;
            }
        }
        catch (Exception ex)
        {
            HomepageSupport.Log(ex, $"刷新首页侧栏小工具失败：{widget.GetType().Name}");
        }
    }

    #endregion
}
