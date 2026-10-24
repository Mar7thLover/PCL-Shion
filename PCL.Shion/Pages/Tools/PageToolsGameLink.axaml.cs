using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.Link;
using PCL.Shion.Core.Link.EasyTier;
using PCL.Shion.Core.Link.Lobby;
using PCL.Shion.Core.Link.Natayark;
using PCL.Shion.Core.Link.Scaffolding.Client.Models;
using PCL.Shion.Core.Link.Scaffolding.EasyTier;
using PCL.Shion.Core.Platform;
using PCL.Shion.Core.ServerQuery;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;
using Lang = PCL.Shion.Localization.Lang;
using LogWrapper = PCL.Shion.LogWrapper;

namespace PCL.Shion.Pages.Tools;

/// <summary>
///     工具页：联机。与 WPF 的 PageToolsGameLink 一致：通过 Natayark ID 登录，使用 EasyTier 与 Scaffolding 协议创建或加入大厅。
///     联机逻辑在 PCL.Shion.Core.Link 中，从 PCL.Core.Link 原样移植。
/// </summary>
public partial class PageToolsGameLink : MyPageRight
{
    private static readonly ModLoader.LoaderCombo<int> initLoader;

    static PageToolsGameLink()
    {
        initLoader = new ModLoader.LoaderCombo<int>(Lang.Text("Link.Mod.Task.InitLobby"),
            new[]
            {
                new ModLoader.LoaderTask<int, int>(Lang.Text("Common.Action.Initialize"), InitTask)
                    { ProgressWeight = 0.5d }
            });
    }

    public PageToolsGameLink()
    {
        InitializeComponent();
        LoaderInit();
        PageEnter += PageLinkLobby_OnPageEnter;
        Loaded += (_, _) => Reload();
        TextJoinLobbyId.KeyDown += TextJoinLobbyId_KeyDown;
        ComboWorldList.IsEnabled = false;
        BtnCreate.IsEnabled = false;
    }

    #region 初始化

    // 加载器初始化
    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanContent, null, initLoader, autoRun: false);

        LobbyService.OnNeedDownloadEasyTier += () => DownloadEasyTier();
        LobbyService.DiscoveredWorlds.CollectionChanged += OnDiscoveredWorldsChanged;
        LobbyService.Players.CollectionChanged += OnPlayersChanged;
        LobbyService.OnUserStopGame += OnUserStopGame;
        LobbyService.OnClientPing += OnClientPingHandler;
        LobbyService.OnServerShutDown += OnServerShuttedDownHandler;
        LobbyService.OnServerStarted += OnServerStartedHandler;
        LobbyService.OnServerException += OnServerExceptionHandler;
    }

    private static void InitTask(ModLoader.LoaderTask<int, int> task)
    {
        LobbyService.InitializeAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    ///     重新读取公告与大厅状态。由左侧栏的刷新按钮与页面加载时调用。
    /// </summary>
    public async void Reload()
    {
        try
        {
            HintAnnounce.IsVisible = true;
            HintAnnounce.Text = Lang.Text("Tools.GameLink.Loading.ConnectingServer");
            HintAnnounce.Theme = MyHint.Themes.Blue;

            _announceCts?.Cancel();
            _announceCts = new CancellationTokenSource();
            _announcements.Clear();
            _ = LoadAnnouncementsAsync(_announceCts.Token);

            await LobbyService.InitializeAsync();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "[Link] 刷新联机页面失败");
        }
    }

    private void PageLinkLobby_OnPageEnter()
    {
        PanEula.IsVisible = CurrentSubpage == Subpages.PanEula;
        PanSelect.IsVisible = CurrentSubpage == Subpages.PanSelect;
        PanFinish.IsVisible = CurrentSubpage == Subpages.PanFinish;
        PanFooter.IsVisible = CurrentSubpage != Subpages.PanFinish;
    }

    #endregion

    #region 子页面管理

    public enum Subpages
    {
        PanEula,
        PanSelect,
        PanFinish
    }

    private Subpages _currentSubpage = LinkHost.Settings.LinkEula ? Subpages.PanSelect : Subpages.PanEula;

    public Subpages CurrentSubpage
    {
        get => _currentSubpage;
        set
        {
            if (_currentSubpage == value)
                return;
            _currentSubpage = value;
            LogWrapper.Info($"[Link] 子页面更改为 {value}");
            try
            {
                PageOnContentExit();
            }
            catch (Exception ex)
            {
                // 加载器正在运行时不能触发内容退出动画，此时直接刷新可见性
                LogWrapper.Warn(ex, "[Link] 切换子页面动画失败");
                PageLinkLobby_OnPageEnter();
            }
        }
    }

    private void RunInUi(Action action) => Dispatcher.UIThread.Post(action);

    private void SwitchToSelect()
    {
        CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
        StackPlayerList.Children.Clear();
        CurrentSubpage = Subpages.PanSelect;
    }

    #endregion

    #region 协议

    private void BtnAgreeEula_Click(object? sender, PointerReleasedEventArgs e)
    {
        LinkHost.Settings.LinkEula = true;
        CurrentSubpage = Subpages.PanSelect;
    }

    private async void BtnEulaStop_Click(object? sender, EventArgs? e)
    {
        var result = await MsgBox.ShowAsync(Lang.Text("Tools.GameLink.Eula.RevokeConfirm"),
            Lang.Text("Tools.GameLink.Eula.RevokeTitle"),
            Lang.Text("Common.Action.Confirm"),
            Lang.Text("Common.Action.Cancel"),
            isWarn: true);
        if (result != 1)
            return;

        LinkHost.Settings.NaidRefreshToken = "";
        LinkHost.Settings.LinkEula = false;
        HintService.Hint(Lang.Text("Tools.GameLink.Eula.Disabled"));
        CurrentSubpage = Subpages.PanEula;
    }

    private void OpenUrlItem_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control { Tag: string url })
            OpenUrl(url);
    }

    private void OpenUrlText_Click(object? sender, EventArgs? e)
    {
        if (sender is Control { Tag: string url })
            OpenUrl(url);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            SystemShell.OpenUrl(url);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "[Link] 打开网址失败");
        }
    }

    #endregion

    #region 公告

    private readonly ObservableCollection<LinkAnnounceInfo> _announcements = [];
    private CancellationTokenSource? _announceCts;

    private async Task LoadAnnouncementsAsync(CancellationToken cancellationToken)
    {
        LinkAnnouncementResult result;
        try
        {
            result = await Task.Run(() => LinkAnnouncementService.FetchAsync(cancellationToken), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            switch (result.Failure)
            {
                case LinkAnnouncementFailure.NoServer:
                    ShowAnnounceError("Tools.GameLink.Error.NoServer");
                    return;
                case LinkAnnouncementFailure.UpdateRequired:
                    ShowAnnounceError("Tools.GameLink.Error.UpdateRequired");
                    return;
                case LinkAnnouncementFailure.ConnectFailed:
                    ShowAnnounceError("Tools.GameLink.Error.ConnectFailed");
                    return;
            }

            foreach (var announcement in result.Announcements)
                _announcements.Add(announcement);
            if (_announcements.Count == 0)
                HintAnnounce.IsVisible = false;
        });

        if (result.Failure is not null)
            return;

        RefreshNatayarkLabel();
        await RunAnnouncementCarouselAsync(cancellationToken);
    }

    private void ShowAnnounceError(string key)
    {
        HintAnnounce.IsVisible = true;
        HintAnnounce.Theme = MyHint.Themes.Red;
        HintAnnounce.Text = Lang.Text(key);
    }

    // 公告轮播：每 10 秒切换一条
    private async Task RunAnnouncementCarouselAsync(CancellationToken cancellationToken)
    {
        var index = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_announcements.Count > 0)
            {
                if (index >= _announcements.Count) index = 0;
                var info = _announcements[index];
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    HintAnnounce.IsVisible = true;
                    string prefix;
                    switch (info.Type)
                    {
                        case LinkAnnounceType.Important:
                            HintAnnounce.Theme = MyHint.Themes.Red;
                            prefix = Lang.Text("Tools.GameLink.Announcement.Important");
                            break;
                        case LinkAnnounceType.Warning:
                            HintAnnounce.Theme = MyHint.Themes.Yellow;
                            prefix = Lang.Text("Tools.GameLink.Announcement.Warning");
                            break;
                        default:
                            HintAnnounce.Theme = MyHint.Themes.Blue;
                            prefix = Lang.Text("Tools.GameLink.Announcement.Notice");
                            break;
                    }

                    HintAnnounce.Text = Lang.Text("Tools.GameLink.Announcement.Format", prefix, info.Content);
                });
                index++;
            }

            try
            {
                await Task.Delay(10000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    #endregion

    #region Natayark 账户

    private void RefreshNatayarkLabel()
    {
        if (string.IsNullOrWhiteSpace(LinkHost.Settings.NaidRefreshToken))
        {
            RunInUi(() =>
            {
                LabNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.Login");
                LabNatayarkUserName.Opacity = 1;
            });
            return;
        }

        RunInUi(() => LabNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.Loading"));
        if (string.IsNullOrEmpty(NatayarkProfileManager.NaidProfile.Username))
        {
            ReloadNaidData();
            return;
        }

        RunInUi(ShowNaidProfile);
    }

    private void ShowNaidProfile()
    {
        var profile = NatayarkProfileManager.NaidProfile;
        // 状态 0 为正常
        if (profile.Status == 0)
        {
            LabNatayarkUserName.Text = profile.Username;
            LabNatayarkUserName.Opacity = 1;
        }
        else
        {
            LabNatayarkUserName.Text = $"{profile.Username} {Lang.Text("Tools.GameLink.Natayark.Abnormal")}";
            LabNatayarkUserName.Opacity = 0.6;
        }
    }

    private async void ReloadNaidData()
    {
        try
        {
            // 检查令牌是否过期
            var expireText = LinkHost.Settings.NaidRefreshExpireTime;
            if (DateTime.TryParse(expireText, out var expireTime) && expireTime < DateTime.Now)
            {
                LinkHost.Settings.NaidRefreshToken = "";
                HintService.Hint(Lang.Text("Tools.GameLink.Natayark.TokenExpired"), HintType.Error);
                RunInUi(() => LabNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.Login"));
                return;
            }

            await Task.Run(() => NatayarkProfileManager.GetNaidDataAsync(LinkHost.Settings.NaidRefreshToken, true));

            // 等待用户名加载，最多 10 秒
            for (var retry = 0;
                 retry < 10 && string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username);
                 retry++)
                await Task.Delay(1000);

            if (string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username))
                throw new TimeoutException("Timeout waiting for username");

            RunInUi(ShowNaidProfile);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "[Link] 刷新 Natayark ID 信息失败，需要重新登录");
            RunInUi(() =>
            {
                LabNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.FetchFailed");
                LabNatayarkUserName.Opacity = 0.6;
            });
        }
    }

    private async void LabNatayarkUserName_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LinkHost.Settings.NaidRefreshToken))
        {
            // 当前未登录，显示登录选项
            var choice = await MsgBox.ShowAsync(Lang.Text("Tools.GameLink.Natayark.LoginPrompt"),
                Lang.Text("Tools.GameLink.Natayark.LoginTitle"),
                Lang.Text("Tools.GameLink.Natayark.Continue"),
                Lang.Text("Common.Action.Cancel"));
            if (choice != 1)
                return;

            LabNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.BrowserContinue");
            LabNatayarkUserName.Opacity = 0.6;
            BtnNatayarkUserName.IsEnabled = false;
            try
            {
                await Task.Run(() => NatayarkAuthorizer.AuthorizeAsync());
                HintService.Hint(Lang.Text("Tools.GameLink.Natayark.LoginComplete"), HintType.Success);
                ShowNaidProfile();
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "[Link] Natayark 登录失败");
                await MsgBox.ShowAsync(ex.Message, isWarn: true);
                LabNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.Login");
                LabNatayarkUserName.Opacity = 1;
            }
            finally
            {
                BtnNatayarkUserName.IsEnabled = true;
            }

            return;
        }

        // 当前已登录，显示登出选项
        var logout = await MsgBox.ShowAsync(Lang.Text("Tools.GameLink.Natayark.LogoutConfirm"),
            Lang.Text("Tools.GameLink.Natayark.LogoutTitle"),
            Lang.Text("Common.Action.Confirm"),
            Lang.Text("Common.Action.Cancel"));
        if (logout != 1)
            return;

        LinkHost.Settings.NaidRefreshToken = "";
        LabNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.Login");
        LabNatayarkUserName.Opacity = 1;
        LogWrapper.Info("[Link] 已退出登录 Natayark Network");
        HintService.Hint(Lang.Text("Tools.GameLink.Natayark.LogoutComplete"), HintType.Success, false);
    }

    #endregion

    #region 网络测试

    private async void BtnNetTest_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (!BtnNatTest.IsEnabled)
            return;
        try
        {
            BtnNatTest.IsEnabled = false;
            LabNatType.Text = Lang.Text("Tools.GameLink.Nat.Testing");
            var status = await CliNetTest.GetNetStatusAsync();
            if (status is null)
                throw new InvalidOperationException("EasyTier returned no result.");
            LabNatType.Text = Lang.Text("Tools.GameLink.Nat.Result",
                CliNetTest.GetNatTypeString(status.UdpNatType),
                CliNetTest.GetNatTypeString(status.TcpNatType));
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "[Link] 获取网络测试结果失败");
            HintService.Hint(Lang.Text("Tools.GameLink.Error.NetworkTestFailed"), HintType.Error, false);
            LabNatType.Text = Lang.Text("Tools.GameLink.Nat.Failed");
        }
        finally
        {
            BtnNatTest.IsEnabled = true;
        }
    }

    #endregion

    #region 大厅事件

    private void OnServerExceptionHandler(Exception ex)
    {
        RunInUi(async () =>
        {
            HintService.Hint(Lang.Text("Tools.GameLink.Error.ServerMessage", ex.Message), HintType.Error);
            await LeaveAndReturnAsync();
        });
    }

    private void OnServerStartedHandler()
    {
        LogWrapper.Info("[Link] Received server started event.");
        RunInUi(() =>
        {
            LabFinishId.Text = LobbyService.CurrentLobbyCode;
            StackPlayerList.Children.Clear();
            foreach (var player in LobbyService.Players)
                StackPlayerList.Children.Add(PlayerInfoItem(player));
        });
    }

    private void OnServerShuttedDownHandler()
    {
        RunInUi(async () => await LeaveAndReturnAsync());
    }

    private async Task LeaveAndReturnAsync()
    {
        try
        {
            await LobbyService.LeaveLobbyAsync();
            SwitchToSelect();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "[Link] Occurred an exception when exit server.");
            HintService.Hint(Lang.Text("Tools.GameLink.Error.ServerExit"), HintType.Error);
        }
    }

    private void OnClientPingHandler(long latency)
    {
        RunInUi(() =>
        {
            LabFinishQuality.Text = Lang.Text("Tools.GameLink.Finish.Connected");
            LabFinishPing.Text = Lang.Text("Tools.GameLink.Finish.PingMs", latency);
            LabConnectType.Text = Lang.Text("Tools.GameLink.Finish.Unavailable");
        });
    }

    private void OnUserStopGame()
    {
        RunInUi(async () =>
        {
            SwitchToSelect();
            await MsgBox.ShowAsync(Lang.Text("Tools.GameLink.Exit.Disbanded"),
                Lang.Text("Tools.GameLink.Exit.DisbandedTitle"));
        });
    }

    private void OnPlayersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        LogWrapper.Debug("[Link] 接收到玩家列表改变事件");
        RunInUi(() =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems is not null)
                        foreach (PlayerProfile player in e.NewItems)
                            StackPlayerList.Children.Add(PlayerInfoItem(player));
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is not null)
                        foreach (PlayerProfile player in e.OldItems)
                        {
                            var itemToRemove = StackPlayerList.Children.OfType<MyListItem>()
                                .FirstOrDefault(item => ((PlayerProfile)item.Tag!).MachineId == player.MachineId);
                            if (itemToRemove is not null)
                                StackPlayerList.Children.Remove(itemToRemove);
                        }

                    break;
                default:
                    StackPlayerList.Children.Clear();
                    foreach (var player in LobbyService.Players)
                        StackPlayerList.Children.Add(PlayerInfoItem(player));
                    break;
            }

            LabFinishQuality.Text = Lang.Text("Tools.GameLink.Finish.Connected");
            CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListCount", LobbyService.Players.Count);
        });
    }

    private void OnDiscoveredWorldsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        LogWrapper.Info("[Link] Found new world changes");
        RunInUi(() =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    ComboWorldList.Items.Clear();
                    foreach (var world in LobbyService.DiscoveredWorlds)
                        ComboWorldList.Items.Add(new MyComboBoxItem { Tag = world.Port, Content = world.Name });
                    break;
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems is not null)
                        foreach (FoundWorld world in e.NewItems)
                            ComboWorldList.Items.Add(new MyComboBoxItem { Tag = world.Port, Content = world.Name });
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is not null)
                    {
                        var portsToRemove = e.OldItems.Cast<FoundWorld>().Select(w => w.Port).ToHashSet();
                        var itemsToRemove = ComboWorldList.Items
                            .OfType<MyComboBoxItem>()
                            .Where(item => item.Tag is int port && portsToRemove.Contains(port))
                            .ToList();
                        foreach (var item in itemsToRemove)
                            ComboWorldList.Items.Remove(item);
                    }

                    break;
            }

            var hasItems = ComboWorldList.Items.Count > 0;
            ComboWorldList.IsEnabled = hasItems;
            BtnCreate.IsEnabled = hasItems;
            if (hasItems && ComboWorldList.SelectedIndex == -1)
                ComboWorldList.SelectedIndex = 0;
        });
    }

    private MyListItem PlayerInfoItem(PlayerProfile info)
    {
        var details = info.Kind == PlayerKind.HOST
            ? Lang.Text("Tools.GameLink.Player.Details", Lang.Text("Tools.GameLink.Player.Host"), info.Vendor)
            : info.Vendor;
        var item = new MyListItem
        {
            Title = info.Name,
            Info = details,
            Type = MyListItem.CheckType.Clickable,
            Tag = info
        };
        item.Click += PlayerInfoClick;
        return item;
    }

    private async void PlayerInfoClick(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not MyListItem { Tag: PlayerProfile info })
            return;
        await MsgBox.ShowAsync(Lang.Text("Tools.GameLink.Player.InfoMessage", info.Name, info.Vendor),
            Lang.Text("Tools.GameLink.Player.InfoTitle", info.Name));
    }

    #endregion

    #region 前置检查与 EasyTier 下载

    private enum EasyTierDownloadState
    {
        None,
        Loading,
        Failed
    }

    private static EasyTierDownloadState _downloadState;

    /// <summary>
    ///     在后台下载联机所需的 EasyTier，对应 WPF 的 ModLink.DownloadEasyTier。
    /// </summary>
    private static void DownloadEasyTier()
    {
        if (_downloadState == EasyTierDownloadState.Loading)
            return;
        if (!EasyTierInstaller.IsSupported)
        {
            HintService.Hint(Lang.Text("Link.Mod.DownloadEasyTierFailed"), HintType.Error);
            return;
        }

        _downloadState = EasyTierDownloadState.Loading;
        var cacheDir = Path.Combine(Path.GetTempPath(), "PCL Shion");
        _ = Task.Run(async () =>
        {
            try
            {
                await EasyTierInstaller.InstallAsync(App.Services.Engine, cacheDir, null, null, CancellationToken.None);
                _downloadState = EasyTierDownloadState.None;
                HintWrapper.Show(Lang.Text("Link.Mod.DownloadComplete"), Core.Link.HintTheme.Success);
            }
            catch (Exception ex)
            {
                _downloadState = EasyTierDownloadState.Failed;
                LogWrapper.Warn(ex, "[Link] Failed to download EasyTier dependency files");
                HintWrapper.Show(Lang.Text("Link.Mod.DownloadEasyTierFailed"), Core.Link.HintTheme.Error);
            }
        });
    }

    /// <summary>
    ///     创建或加入大厅前的检查，对应 WPF 的 ModLink.LobbyPrecheck。
    /// </summary>
    private static async Task<bool> LobbyPrecheckAsync()
    {
        if (!LobbyInfoProvider.IsLobbyAvailable)
        {
            HintService.Hint(Lang.Text("Link.Mod.LobbyUnavailable"), HintType.Error);
            return false;
        }

        var settings = LinkHost.Settings;
        if (LobbyInfoProvider.RequiresLogin)
        {
            if (string.IsNullOrWhiteSpace(settings.NaidRefreshToken))
            {
                HintService.Hint(Lang.Text("Link.Mod.LoginFirst"), HintType.Error);
                return false;
            }

            try
            {
                await Task.Run(() => NatayarkProfileManager.GetNaidDataAsync(settings.NaidRefreshToken, true));
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "[Link] 刷新 Natayark ID 信息失败，需要重新登录");
                HintService.Hint(Lang.Text("Link.Mod.ReLoginRequired"), HintType.Error);
                return false;
            }

            for (var waitCount = 0;
                 waitCount <= 30 && string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username);
                 waitCount++)
                await Task.Delay(500);

            if (string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username))
            {
                HintService.Hint(Lang.Text("Link.Mod.NaidFetchFailed"), HintType.Error);
                return false;
            }

            if (LobbyInfoProvider.RequiresRealName && !NatayarkProfileManager.NaidProfile.IsRealNamed)
            {
                HintService.Hint(Lang.Text("Link.Mod.RealNameRequired"), HintType.Error);
                return false;
            }

            if (NatayarkProfileManager.NaidProfile.Status != 0)
            {
                HintService.Hint(Lang.Text("Link.Mod.AccountBanned"), HintType.Error);
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(settings.Username) &&
            string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username))
        {
            HintService.Hint(Lang.Text("Link.Mod.UsernameOrLogin"), HintType.Error);
            return false;
        }

        if (_downloadState == EasyTierDownloadState.Loading)
        {
            HintService.Hint(Lang.Text("Link.Mod.EasyTierNotReady"));
            return false;
        }

        if (ETController.Precheck() == 1 || _downloadState == EasyTierDownloadState.Failed)
        {
            HintService.Hint(Lang.Text("Link.Mod.DownloadingDeps"));
            DownloadEasyTier();
            return false;
        }

        return true;
    }

    #endregion

    #region PanSelect | 种类选择页面

    private void PasteLobbyId(object? sender, PointerReleasedEventArgs e) => _ = PasteLobbyIdAsync();

    private async Task PasteLobbyIdAsync()
    {
        string? lobbyId;
        try
        {
            lobbyId = TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard
                ? await clipboard.TryGetTextAsync()
                : null;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "从剪贴板识别大厅编号出错");
            return;
        }

        if (!string.IsNullOrEmpty(lobbyId))
            TextJoinLobbyId.Text = lobbyId;
        else
            HintService.Hint(Lang.Text("Tools.GameLink.Join.InvalidText"));
    }

    private void ClearLobbyId(object? sender, PointerReleasedEventArgs e) => TextJoinLobbyId.Text = string.Empty;

    private void BtnRefresh_Click(object? sender, PointerReleasedEventArgs e) => _ = LobbyService.DiscoverWorldAsync();

    private async void BtnInputPort_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            BtnInputPort.IsEnabled = false;
            if (!await LobbyPrecheckAsync())
                return;
            var input = await MsgBox.InputAsync(Lang.Text("Tools.GameLink.Create.EnterPort"),
                validate: text => int.TryParse(text, out var value) && value is >= 1024 and <= 65535
                    ? null
                    : Lang.Text("Tools.GameLink.Create.NotMcPort"));
            if (!int.TryParse(input, out var port))
                return;

            using var ping = McPingServiceFactory.CreateService("127.0.0.1", port, 5000);
            var res = await ping.PingAsync();
            if (res is not null && res.Version.Protocol != 0)
                await CreateLobbyAsync(port);
            else
                HintService.Hint(Lang.Text("Tools.GameLink.Create.NotMcPort"), HintType.Error);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "[Link] 手动输入端口创建大厅失败");
            HintService.Hint(Lang.Text("Tools.GameLink.Create.NotMcPort"), HintType.Error);
        }
        finally
        {
            BtnInputPort.IsEnabled = true;
        }
    }

    private async void BtnCreate_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (ComboWorldList.SelectedItem is not MyComboBoxItem { Tag: int port })
        {
            HintService.Hint(Lang.Text("Tools.GameLink.Create.NoWorld"));
            return;
        }

        BtnCreate.IsEnabled = false;
        if (!await LobbyPrecheckAsync())
        {
            BtnCreate.IsEnabled = true;
            return;
        }

        await CreateLobbyAsync(port);
    }

    private async Task CreateLobbyAsync(int port)
    {
        LogWrapper.Info("[Link] 创建大厅，端口：" + port);
        var username = LobbyInfoProvider.GetUsername() ?? "";

        BtnFinishPing.IsVisible = false;
        LabFinishPing.Text = "-ms";
        BtnConnectType.IsVisible = false;
        LabConnectType.Text = Lang.Text("Tools.GameLink.Finish.Connecting");
        CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
        StackPlayerList.Children.Clear();
        LabConnectUserName.Text = username;
        LabConnectUserType.Text = Lang.Text("Tools.GameLink.Finish.Host");
        LabFinishId.Text = LobbyService.CurrentLobbyCode;
        BtnFinishCopyIp.IsVisible = false;
        BtnCreate.IsEnabled = true;
        BtnFinishExit.Text = Lang.Text("Tools.GameLink.Finish.CloseLobby");
        CurrentSubpage = Subpages.PanFinish;

        var res = await LobbyService.CreateLobbyAsync(port, username);
        if (!res)
            SwitchToSelect();
    }

    private async void BtnJoin_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (!await LobbyPrecheckAsync())
            return;

        LogWrapper.Info("[Link] Start to join lobby.");
        var id = TextJoinLobbyId.Text ?? "";
        var username = LobbyInfoProvider.GetUsername() ?? "";

        BtnFinishPing.IsVisible = true;
        LabFinishPing.Text = "-ms";
        BtnConnectType.IsVisible = true;
        LabConnectType.Text = Lang.Text("Tools.GameLink.Finish.Connecting");
        CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
        StackPlayerList.Children.Clear();
        LabConnectUserName.Text = username;
        LabConnectUserType.Text = Lang.Text("Tools.GameLink.Finish.Guest");
        LabFinishId.Text = id;
        BtnFinishCopyIp.IsVisible = true;
        BtnFinishExit.Text = Lang.Text("Tools.GameLink.Finish.Exit");
        CurrentSubpage = Subpages.PanFinish;

        var res = await LobbyService.JoinLobbyAsync(id, username);
        if (!res)
            SwitchToSelect();
    }

    private void TextJoinLobbyId_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            BtnJoin_Click(sender, null!);
    }

    #endregion

    #region PanLoad | 加载中页面

    // 承接重试
    private void CardLoad_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (initLoader.State != LoadState.Failed)
            return;
        initLoader.Start(isForceRestart: true);
    }

    // 取消加载
    private void CancelLoad(object? sender, PointerReleasedEventArgs e)
    {
        if (initLoader.State == LoadState.Loading)
        {
            CurrentSubpage = Subpages.PanSelect;
            initLoader.Abort();
        }
        else
        {
            initLoader.State = LoadState.Waiting;
        }
    }

    #endregion

    #region PanFinish | 加载完成页面

    private async void BtnFinishExit_Click(object? sender, RouteEventArgs e)
    {
        var confirm = await MsgBox.ShowAsync(
            Lang.Text(LobbyService.IsHost
                ? "Tools.GameLink.Exit.ConfirmMessageWithHost"
                : "Tools.GameLink.Exit.ConfirmMessage"),
            Lang.Text("Tools.GameLink.Exit.ConfirmTitle"),
            Lang.Text("Common.Action.Confirm"),
            Lang.Text("Common.Action.Cancel"),
            isWarn: true);
        if (confirm != 1)
            return;

        CurrentSubpage = Subpages.PanSelect;
        BtnFinishExit.Text = Lang.Text("Tools.GameLink.Finish.Exit");
        await LobbyService.LeaveLobbyAsync();
    }

    private async void BtnFinishCopy_Click(object? sender, RouteEventArgs e)
    {
        await SetClipboardAsync(LabFinishId.Text ?? "");
    }

    private async void BtnFinishCopyIp_Click(object? sender, RouteEventArgs e)
    {
        var port = LobbyInfoProvider.McForward?.LocalEndPoint?.Port;
        if (port is null)
            return;
        var ip = $"127.0.0.1:{port}";
        var choice = await MsgBox.ShowAsync(Lang.Text("Tools.GameLink.CopyIp.Message", ip),
            Lang.Text("Tools.GameLink.CopyIp.Title"),
            Lang.Text("Common.Action.Copy"),
            Lang.Text("Tools.GameLink.CopyIp.Back"));
        if (choice == 1)
            await SetClipboardAsync(ip);
    }

    private async Task SetClipboardAsync(string text)
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(text);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "[Link] 写入剪贴板失败");
        }
    }

    #endregion
}
