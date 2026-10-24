using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.Accounts;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Java;
using PCL.Shion.Core.Launch;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Instance;
using PCL.Shion.Pages.Setup;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Launch;

public partial class PageLaunchLeft : MyPageLeft
{
    private double actualUsedHeight;
    private double actualUsedWidth;
    private int btnLaunchState;
    private string? _btnLaunchLanguage;
    private GameInstance? btnLaunchVersion;
    private bool isHeightAnimating;
    public interface ILoginPage { void Reload(); }

    private enum LaunchButtonAction
    {
        Loading,
        Launch,
        Download,
        Disabled
    }

    private LaunchButtonAction _launchButtonAction;

    // 加载当前实例
    private bool isLoad;

    private bool isLoadFinished;

    // 尺寸改变动画
    private bool isWidthAnimating;
    private double showProgress;

    /// <summary>当前选中的实例，对应 WPF 的 ModInstanceList.McMcInstanceSelected。</summary>
    public static GameInstance? McInstanceSelected { get; private set; }

    /// <summary>实例列表是否正在读取，对应 WPF 的 mcInstanceListLoader 加载状态。</summary>
    private static bool instanceListLoading;

    // 启动流程状态，对应 WPF 的 mcLaunchLoader 与 mcLaunchLoaderReal
    private static bool isLaunchRunning;
    private static bool isLaunchStarted;
    private static bool isLaunchDownloading;
    private static bool isNonAsciiPathAcknowledged;
    private static string launchStage = "";
    private static double launchProgress;
    private static double launchSpeed;
    private static CancellationTokenSource? launchCancel;
    private static RunningGame? launchGame;
    private DispatcherTimer? launchingTimer;

    // 愚人节投降动画的两个变换，对应 XAML 中 PanApril 的 RenderTransform（TransformGroup）
    private ScaleTransform AprilScaleTrans => (ScaleTransform)((TransformGroup)PanApril.RenderTransform!).Children[0];

    private TranslateTransform AprilPosTrans => (TranslateTransform)((TransformGroup)PanApril.RenderTransform!).Children[1];

    // 启动进度条的两列，对应 XAML 中 PanLaunchingProgress 的 ColumnDefinitions
    private ColumnDefinition ProgressLaunchingFinished => PanLaunchingProgress.ColumnDefinitions[0];

    private ColumnDefinition ProgressLaunchingUnfinished => PanLaunchingProgress.ColumnDefinitions[1];

    public PageLaunchLeft()
    {
        InitializeComponent();
        Loaded += PageLaunchLeft_Loaded;
        WeakLanguageChanged.Add(this, OnLanguageChanged);
        BtnLaunch.Loaded += (_, _) => RefreshButtonsUI();
        PanLaunchingInfo.SizeChanged += PanLaunchingInfo_SizeChangedW;
        PanLaunchingInfo.SizeChanged += PanLaunchingInfo_SizeChangedH;
        App.Services.InstancesChanged += OnInstancesChanged;
    }

    private static void OnLanguageChanged(PageLaunchLeft page) => Dispatcher.UIThread.Post(page.RefreshButtonsUI);

    private void OnInstancesChanged() => Dispatcher.UIThread.Post(() => _ = LoadInstancesAsync());

    public void PageLaunchLeft_Loaded(object? sender, RoutedEventArgs e)
    {
        if (isLoad)
            RefreshPage(false);

        AprilPosTrans.X = 0d;
        AprilPosTrans.Y = 0d;

        if (isLoad)
            return;
        isLoad = true;
        ModAnimation.AniControlEnabled += 1;

        // 开始按钮
        RefreshButtonsUI();

        // 初始化档案
        ProfileService.Load();

        // 加载实例
        _ = LoadInstancesAsync();

        // 改变页面
        RefreshPage(false);

        ModAnimation.AniControlEnabled -= 1;
    }

    /// <summary>
    ///     读取实例列表并确认当前选中的实例，对应 WPF 的 “Instance Check” 加载线程。
    /// </summary>
    private async Task LoadInstancesAsync()
    {
        var settings = App.Services.Settings;
        var root = settings.MinecraftRoot;
        instanceListLoading = true;
        RefreshButtonsUI();

        GameInstance? instance = null;
        try
        {
            var instances = await Task.Run(() => InstanceScanner.Scan(root));
            var selection = settings.SelectedInstance;
            instance = instances.FirstOrDefault(i => i.Name == selection && i.Error is null)
                       ?? instances.FirstOrDefault(i => i.Error is null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            instance = null;
        }

        if (settings.SelectedInstance != instance?.Name)
        {
            settings.SelectedInstance = instance?.Name;
            settings.Save();
        }

        instanceListLoading = false;
        McInstanceSelected = instance;
        isLoadFinished = true;
        RefreshButtonsUI();
        RefreshPage(false); // 有可能选择的版本变化了，需要重新刷新
    }

    /// <summary>
    ///     改变当前选中的实例，并刷新按钮。对应 WPF 中直接赋值 ModInstanceList.McMcInstanceSelected 的做法。
    /// </summary>
    public static void SelectInstance(GameInstance? instance)
    {
        McInstanceSelected = instance;
        var settings = App.Services.Settings;
        settings.SelectedInstance = instance?.Name;
        settings.Save();
        Dispatcher.UIThread.Post(() =>
        {
            if (Navigator.PageLeft is PageLaunchLeft left)
                left.RefreshButtonsUI();
        });
    }

    // 实例选择按钮
    private void BtnInstance_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (isLaunchRunning)
            return;
        Navigator.Go(PageType.InstanceSelect);
    }

    // 启动按钮
    public void LaunchButtonClick()
    {
        if (isLaunchRunning || !BtnLaunch.IsEnabled ||
            (Navigator.PageRight is { } right &&
             right.PageState != MyPageRight.PageStates.ContentStay &&
             right.PageState != MyPageRight.PageStates.ContentEnter))
            return;
        // 愚人节处理
        if (MainWindow.IsAprilEnabled)
        {
            AprilScaleTrans.ScaleX = 1d;
            AprilScaleTrans.ScaleY = 1d;
            AprilPosTrans.X = 0d;
            AprilPosTrans.Y = 0d;
            MainWindow.Current?.AprilGiveup();
        }

        // 实际的启动
        switch (_launchButtonAction)
        {
            case LaunchButtonAction.Launch:
            {
                if (File.Exists(Path.Combine(McInstanceSelected!.Directory, ".pclignore")))
                {
                    HintService.Hint(Lang.Text("Launch.Home.Instance.InstallingCannotLaunch"), HintType.Error);
                    return;
                }

                McLaunchStart();
                break;
            }
            case LaunchButtonAction.Download:
            {
                Navigator.Go(PageType.Download, PageSubType.DownloadInstall);
                break;
            }
        }
    }

    public void RefreshButtonsUI()
    {
        if (!BtnLaunch.IsLoaded)
            return;
        // 获取当前状态
        int currentState;
        if (!isLoadFinished || instanceListLoading)
        {
            currentState = 0;
        }
        else if (McInstanceSelected is null)
        {
            if (App.Services.Settings.UiHiddenPageDownload && !PageSetupUI.HiddenForceShow)
                currentState = 1;
            else
                currentState = 2;
        }
        else
        {
            currentState = 3;
        }

        // 更新状态。
        var currentLanguage = Lang.Culture.Name;
        if (currentState == btnLaunchState &&
            currentLanguage == _btnLaunchLanguage &&
            (McInstanceSelected?.Directory ?? "") == (btnLaunchVersion?.Directory ?? ""))
            goto ExitRefresh;
        _btnLaunchLanguage = currentLanguage;
        btnLaunchVersion = McInstanceSelected;
        btnLaunchState = currentState;
        switch (currentState)
        {
            case 0:
            {
                _launchButtonAction = LaunchButtonAction.Loading;
                BtnLaunch.Text = Lang.Text("Launch.Home.Button.Loading");
                BtnLaunch.IsEnabled = false;
                LabVersion.Text = Lang.Text("Launch.Home.Instance.Loading");
                BtnInstance.IsEnabled = false;
                BtnMore.IsVisible = false;
                break;
            }
            case 1:
            {
                _launchButtonAction = LaunchButtonAction.Disabled;
                BtnLaunch.Text = Lang.Text("Launch.Home.Button.Launch");
                BtnLaunch.IsEnabled = false;
                LabVersion.Text = Lang.Text("Launch.Home.Instance.NotFound");
                BtnInstance.IsEnabled = true;
                BtnMore.IsVisible = false;
                break;
            }
            case 2:
            {
                _launchButtonAction = LaunchButtonAction.Download;
                BtnLaunch.Text = Lang.Text("Launch.Home.Button.Download");
                BtnLaunch.IsEnabled = true;
                LabVersion.Text = Lang.Text("Launch.Home.Instance.NotFound");
                BtnInstance.IsEnabled = true;
                BtnMore.IsVisible = false;
                break;
            }
            case 3:
            {
                _launchButtonAction = LaunchButtonAction.Launch;
                BtnLaunch.Text = Lang.Text("Launch.Home.Button.Launch");
                BtnInstance.IsEnabled = true;
                BtnLaunch.IsEnabled = ProfileService.Current is not null;
                LabVersion.Text = McInstanceSelected!.Name;
                break;
            }
            // FrmLaunchLeft.BtnMore.Visibility = Visibility.Visible '由功能隐藏设置修改
        }

        ExitRefresh: ;

        // 功能隐藏
        BtnInstance.IsVisible = !(!PageSetupUI.HiddenForceShow && App.Services.Settings.UiHiddenFunctionSelect);
        if (currentState == 3) BtnMore.IsVisible = BtnInstance.IsVisible;
    }

    // 取消按钮
    private void BtnCancel_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (!isLaunchRunning)
            return;
        launchCancel?.Cancel();
        try
        {
            launchGame?.Kill();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            HintService.Hint(Lang.Text("Minecraft.Launch.Error.CancelProcess"), HintType.Error);
        }
    }

    // 实例设置按钮
    private void BtnMore_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (isLaunchRunning || McInstanceSelected is null)
            return;
        PageInstanceLeft.McInstance = McInstanceSelected;
        if (File.Exists(Path.Combine(McInstanceSelected.Directory, ".pclignore")))
        {
            HintService.Hint(Lang.Text("Launch.Home.Instance.InstallingCannotSetup"), HintType.Error);
            return;
        }

        Navigator.Go(PageType.InstanceSetup);
    }

    /// <summary>
    ///     每 0.2s 执行一次，刷新启动的数据 UI 显示。
    /// </summary>
    public void LaunchingRefresh()
    {
        try
        {
            if (!isLaunchRunning)
                return;
            // 阶段状态获取
            var isLaunched = isLaunchStarted; // 是否已经启动游戏
            LabLaunchingStage.Text = launchStage;

            if (ModAnimation.AniIsRun("Launch State Page"))
                isLaunched = false; // 等待页面切换动画完成
            // 计算应显示的进度
            var actualProgress = launchProgress;
            if (actualProgress >= showProgress)
                showProgress += (actualProgress - showProgress) * 0.2d + 0.005d; // 向实际进度靠一点
            if (actualProgress <= showProgress)
                showProgress = actualProgress; // 原来或处理后变得比实际进度高，直接回退
            if (isLaunched)
                showProgress = 1d; // 如果已经完成了，就不卖关子了
            // 文本
            LabLaunchingTitle.Text = isLaunched
                ? Lang.Text("Launch.Status.Title.Launched")
                : Lang.Text("Launch.Status.Title.Launching");
            LabLaunchingProgress.Text = Lang.Number(showProgress, "P2");
            var hasLaunchDownloader = isLaunchDownloading;
            LabLaunchingDownload.Text = ByteFormatter.FormatSpeed(launchSpeed);
            var shouldShowHint = App.Services.Settings.UiShowLaunchingHint;
            // 进度改变动画
            var animList = new List<ModAnimation.AniData>
            {
                ModAnimation.AaGridLengthWidth(ProgressLaunchingFinished,
                    showProgress - ProgressLaunchingFinished.Width.Value, 260,
                    ease: new ModAnimation.AniEaseOutFluent()),
                ModAnimation.AaGridLengthWidth(ProgressLaunchingUnfinished,
                    1d - showProgress - ProgressLaunchingUnfinished.Width.Value, 260,
                    ease: new ModAnimation.AniEaseOutFluent())
            };
            var isDownloadStateChanged = hasLaunchDownloader == !LabLaunchingDownload.IsVisible;
            if (isDownloadStateChanged)
            {
                LabLaunchingDownload.IsVisible = true;
                LabLaunchingDownloadLeft.IsVisible = true;
                animList.AddRange(new[]
                {
                    ModAnimation.AaOpacity(LabLaunchingDownload,
                        (hasLaunchDownloader ? 1 : 0) - LabLaunchingDownload.Opacity, 100),
                    ModAnimation.AaOpacity(LabLaunchingDownloadLeft,
                        (hasLaunchDownloader ? 0.5d : 0d) - LabLaunchingDownloadLeft.Opacity, 100),
                    ModAnimation.AaCode(() =>
                    {
                        if (!hasLaunchDownloader)
                        {
                            LabLaunchingDownload.IsVisible = false;
                            LabLaunchingDownloadLeft.IsVisible = false;
                        }
                    }, 110)
                });
            }

            var isProgressStateChanged = !isLaunched == !LabLaunchingProgress.IsVisible;
            if (isProgressStateChanged)
            {
                LabLaunchingProgress.IsVisible = true;
                LabLaunchingProgressLeft.IsVisible = true;
                if (isLaunched && shouldShowHint) PanLaunchingHint.IsVisible = true;
                animList.AddRange(new[]
                {
                    ModAnimation.AaOpacity(LabLaunchingProgress, (!isLaunched ? 1 : 0) - LabLaunchingProgress.Opacity,
                        100),
                    ModAnimation.AaOpacity(LabLaunchingProgressLeft,
                        (!isLaunched ? 0.5d : 0d) - LabLaunchingProgressLeft.Opacity, 100),
                    ModAnimation.AaOpacity(PanLaunchingHint,
                        (isLaunched && shouldShowHint ? 1 : 0) - PanLaunchingHint.Opacity, 100)
                });
            }

            ModAnimation.AniStart(animList, "Launching Progress");
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Minecraft.Launch.Error.RefreshInfo"), HintType.Error);
        }
    }

    private void PanLaunchingInfo_SizeChangedW(object? sender, SizeChangedEventArgs e)
    {
        var deltaWidth = e.NewSize.Width - e.PreviousSize.Width;
        if (e.PreviousSize.Width == 0d || isWidthAnimating || Math.Abs(deltaWidth) < 1d ||
            PanLaunchingInfo.Bounds.Width == 0d)
            return;
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaWidth(PanLaunchingInfo, deltaWidth, 180, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(() =>
            {
                isWidthAnimating = false;
                PanLaunchingInfo.Width = actualUsedWidth;
            }, after: true)
        }, "Launching Info Width");
        isWidthAnimating = true;
        actualUsedWidth = PanLaunchingInfo.Width;
        PanLaunchingInfo.Width = e.PreviousSize.Width;
    }

    private void PanLaunchingInfo_SizeChangedH(object? sender, SizeChangedEventArgs e)
    {
        var deltaHeight = e.NewSize.Height - e.PreviousSize.Height;
        if (e.PreviousSize.Height == 0d || isHeightAnimating || Math.Abs(deltaHeight) < 1d ||
            PanLaunchingInfo.Bounds.Height == 0d)
            return;
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaHeight(PanLaunchingInfo, deltaHeight, 180, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(() =>
            {
                isHeightAnimating = false;
                PanLaunchingInfo.Height = actualUsedHeight;
            }, after: true)
        }, "Launching Info Height");
        isHeightAnimating = true;
        actualUsedHeight = PanLaunchingInfo.Height;
        PanLaunchingInfo.Height = e.PreviousSize.Height;
    }

    // 启动游戏按钮
    private void BtnLaunch_Click(object? sender, PointerReleasedEventArgs e)
    {
        LaunchButtonClick();
    }

    #region 启动流程

    /// <summary>
    ///     尝试启动 Minecraft：预检测、补全文件、启动游戏进程。对应 WPF 的 ModLaunch.McLaunchStart，
    ///     实际启动通过 <see cref="GameRunner" /> 完成。
    /// </summary>
    private async void McLaunchStart()
    {
        if (isLaunchRunning)
        {
            HintService.Hint(Lang.Text("Minecraft.Launch.Error.AlreadyLaunching"), HintType.Error);
            return;
        }

        var instance = McInstanceSelected;
        if (instance is null)
            return;

        isLaunchRunning = true;
        isLaunchStarted = false;
        isLaunchDownloading = false;
        launchStage = Lang.Text("Common.Action.Initialize");
        launchProgress = 0d;
        launchSpeed = 0;
        launchGame = null;
        var cancel = new CancellationTokenSource();
        launchCancel = cancel;

        // 开始动画
        PageChangeToLaunching();
        StartLaunchingTimer();
        var settings = App.Services.Settings;
        var root = settings.MinecraftRoot;
        var outcome = LaunchOutcome.Failed;
        try
        {
            // 预检测（预检测的错误将直接抛出）
            if (!await McLaunchPrecheckAsync(instance))
            {
                outcome = LaunchOutcome.Cancelled;
                return;
            }

            var profile = ProfileService.Current ?? throw new OperationCanceledException();
            var instanceSettings = await Task.Run(() => InstanceStore.Load(root, instance.Name), cancel.Token);
            var java = await ResolveJavaAsync(root, instance, instanceSettings, settings);
            if (java is null)
                throw new InvalidOperationException(Lang.Text("Minecraft.Launch.Error.NoJava"));

            // 登录：刷新令牌并取得游戏账户
            launchStage = Lang.Text("Minecraft.Launch.Stage.WaitWindow");
            var account = await CreateLaunchAccountAsync(profile, cancel.Token);
            var request = CreateRequest(root, instance, instanceSettings, account, java, settings);

            var plan = await Task.Run(() => LaunchPlanner.Build(request), cancel.Token);
            if (plan.MissingFiles.Count > 0)
            {
                await CompleteFilesAsync(root, instance.Name, cancel.Token);
                plan = await Task.Run(() => LaunchPlanner.Build(request), cancel.Token);
                if (plan.MissingFiles.Count > 0)
                    throw new InvalidOperationException(
                        Lang.Text("Minecraft.Launch.Error.LaunchFailed", string.Join(", ", plan.MissingFiles)));
            }

            cancel.Token.ThrowIfCancellationRequested();
            launchStage = Lang.Text("Minecraft.Launch.Stage.Root");
            launchProgress = 0.95d;
            var game = await Task.Run(() => App.Services.Runner.Start(
                root, instance.Name, plan, instanceSettings, null,
                code => Dispatcher.UIThread.Post(() => OnGameExited(code))), cancel.Token);

            launchGame = game;
            isLaunchStarted = true;
            launchProgress = 1d;
            settings.SelectedInstance = instance.Name;
            settings.Save();
            outcome = LaunchOutcome.Started;
        }
        catch (OperationCanceledException)
        {
            outcome = LaunchOutcome.Cancelled;
        }
        catch (PreLaunchCommandException e)
        {
            HintService.Hint(Lang.Text("Minecraft.Launch.Error.CustomCommand", e.ExitCode), HintType.Error);
        }
        catch (Exception e)
        {
            HintService.Hint(Lang.Text("Minecraft.Launch.Precheck.Failed.WithDetail", e.Message), HintType.Error);
        }
        finally
        {
            if (outcome != LaunchOutcome.Started)
                FinishLaunch();
        }
    }

    private enum LaunchOutcome
    {
        Failed,
        Cancelled,
        Started
    }

    /// <summary>检查实例路径与状态，对应 WPF 的 McLaunchPrecheck。返回 false 表示用户选择了返回。</summary>
    private async Task<bool> McLaunchPrecheckAsync(GameInstance instance)
    {
        if (App.Services.Settings.SystemDebugDelay)
            await Task.Delay(Random.Shared.Next(100, 2000));
        // 检查路径
        if (instance.Directory.Contains('!') || instance.Directory.Contains(';'))
            throw new InvalidOperationException(
                Lang.Text("Minecraft.Launch.Precheck.InvalidPathChars", instance.Directory));
        if (!isNonAsciiPathAcknowledged && !instance.Directory.All(char.IsAscii))
        {
            var userChoice = await MsgBox.ShowAsync(
                Lang.Text("Minecraft.Launch.Precheck.NonAsciiPath.Message", instance.Name),
                Lang.Text("Minecraft.Launch.Precheck.NonAsciiPath.Title"),
                Lang.Text("Minecraft.Launch.Precheck.NonAsciiPath.Continue"),
                Lang.Text("Minecraft.Launch.Precheck.NonAsciiPath.Back"),
                Lang.Text("Common.Hint.DoNotShowAgain"));
            if (userChoice == 2)
                return false;
            if (userChoice == 3)
                isNonAsciiPathAcknowledged = true;
        }

        // 检查实例
        if (instance.Error is { } error)
            throw new InvalidOperationException(Lang.Text("Minecraft.Launch.Precheck.InstanceError", error));
        return true;
    }

    /// <summary>
    ///     依次采用实例设置里指定的 Java、设置里的默认 Java，都没有时按版本要求的 Java 主版本自动选择。
    /// </summary>
    private static async Task<string?> ResolveJavaAsync(string root, GameInstance instance,
        InstanceSettings instanceSettings, AppSettings settings)
    {
        if (instanceSettings.JavaExecutable is { } instanceJava && File.Exists(instanceJava))
            return instanceJava;
        if (settings.DefaultJavaExecutable is { } defaultJava && File.Exists(defaultJava))
            return defaultJava;

        return await Task.Run(() =>
        {
            var version = VersionResolver.Resolve(root, instance.Name);
            var runtimes = JavaLocator.FindAll();
            return JavaLocator.Select(runtimes, version.JavaMajorVersion)?.Executable;
        });
    }

    /// <summary>用实例设置与全局设置组装启动请求；实例设置里没有指定的项沿用全局设置。</summary>
    private static LaunchRequest CreateRequest(string root, GameInstance instance, InstanceSettings settings,
        OfflineAccount account, string java, AppSettings appSettings)
    {
        var request = new LaunchRequest(
            root, instance.Name, account, java, settings.MaxMemoryMb ?? appSettings.MaxMemoryMb,
            GameDirectoryOverride: InstanceManager.GetGameDirectory(root, instance, settings),
            ExtraJvmArguments: settings.ExtraJvmArguments,
            ExtraGameArguments: settings.ExtraGameArguments,
            WrapperCommand: settings.WrapperCommand,
            EnvironmentVariables: settings.EnvironmentVariables,
            QuickPlay: settings.QuickPlay);
        return request with
        {
            WindowWidth = settings.WindowWidth ?? request.WindowWidth,
            WindowHeight = settings.WindowHeight ?? request.WindowHeight,
        };
    }

    /// <summary>
    ///     取得用于启动的账户：正版与第三方账户的令牌过期时先刷新，离线账户直接使用档案中的 UUID。
    /// </summary>
    internal static async Task<OfflineAccount> CreateLaunchAccountAsync(McProfile profile, CancellationToken token)
    {
        var usable = new OfflineAccount(profile.UserName, profile.Uuid, profile.AccessToken);
        if (profile.ProfileType == ProfileType.Offline)
            return usable;
        if (profile.ExpiresAt is { } expiresAt && expiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
            return usable;

        AccountLoginResult? result = null;
        if (profile.ProfileType == ProfileType.Microsoft && profile.RefreshToken is { } refreshToken)
            result = await MicrosoftAccount.LoginWithRefreshTokenAsync(LaunchHttp, refreshToken, null, token);
        else if (profile.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect)
            result = await AuthlibAccount.RefreshProfileAsync(LaunchHttp, profile, token);
        if (result is null)
            return usable;

        var updated = ProfileService.ApplyLogin(result, profile, true);
        return new OfflineAccount(updated.UserName, updated.Uuid, updated.AccessToken);
    }

    private static readonly HttpClient LaunchHttp = new();

    /// <summary>补全实例缺失或损坏的文件，失败时抛出异常。</summary>
    private async Task CompleteFilesAsync(string root, string instanceName, CancellationToken token)
    {
        launchStage = Lang.Text("Minecraft.Launch.Stage.CompleteFiles");
        launchProgress = 0d;
        var result = await App.Services.CreateInstaller().CompleteAsync(
            root, instanceName, new Progress<InstallProgress>(ShowInstallProgress), token);
        isLaunchDownloading = false;
        launchSpeed = 0;
        if (!result.Succeeded)
            throw new InvalidOperationException(Lang.Text("Minecraft.Launch.Error.LaunchFailed",
                string.Join(", ", result.Failed.Select(item => item.TargetPath))));
    }

    private void ShowInstallProgress(InstallProgress progress)
    {
        launchStage = progress.Stage;
        if (progress.Download is not { } download)
            return;

        isLaunchDownloading = true;
        launchSpeed = download.BytesPerSecond;
        launchProgress = download.TotalBytes > 0
            ? (double)download.DownloadedBytes / download.TotalBytes * 0.9d
            : download.TotalFiles > 0 ? (double)download.CompletedFiles / download.TotalFiles * 0.9d : 0d;
    }

    /// <summary>游戏退出：恢复页面，对应 WPF 的 McLaunchState 进入登录页面。</summary>
    private void OnGameExited(int exitCode)
    {
        launchGame = null;
        FinishLaunch();
        _ = LoadInstancesAsync();
    }

    /// <summary>结束启动流程并切换回登录页面。</summary>
    private void FinishLaunch()
    {
        isLaunchRunning = false;
        isLaunchStarted = false;
        isLaunchDownloading = false;
        launchCancel?.Dispose();
        launchCancel = null;
        launchingTimer?.Stop();
        PageChangeToLogin();
    }

    private void StartLaunchingTimer()
    {
        if (launchingTimer is null)
        {
            launchingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            launchingTimer.Tick += (_, _) => LaunchingRefresh();
        }

        launchingTimer.Start();
    }

    #endregion

    #region 切换大页面

    /// <summary>
    ///     切换至启动中页面。
    /// </summary>
    public void PageChangeToLaunching()
    {
        // 修改验证方式
        switch (ProfileService.Current?.ProfileType)
        {
            case ProfileType.Offline:
            {
                LabLaunchingMethod.Text = Lang.Text("Launch.Account.Type.Offline");
                break;
            }
            case ProfileType.Microsoft:
            {
                LabLaunchingMethod.Text = Lang.Text("Launch.Account.Type.Microsoft");
                break;
            }
            case ProfileType.Authlib:
            case ProfileType.YggdrasilConnect:
            {
                LabLaunchingMethod.Text = Lang.Text("Launch.Account.Type.ThirdParty") +
                                          (!string.IsNullOrEmpty(ProfileService.Current?.ServerName)
                                              ? " / " + ProfileService.Current.ServerName
                                              : "");
                break;
            }
        }

        // 初始化页面
        LabLaunchingName.Text = McInstanceSelected?.Name ?? "";
        LabLaunchingStage.Text = Lang.Text("Common.Action.Initialize");
        LabLaunchingTitle.Text = Lang.Text("Launch.Status.Title.Launching");
        LabLaunchingProgress.Text = Lang.Number(0d, "P2");
        LabLaunchingProgress.Opacity = 1d;
        LabLaunchingDownload.IsVisible = true;
        LabLaunchingProgressLeft.Opacity = 0.6d;
        LabLaunchingDownload.Text = ByteFormatter.FormatSpeed(0);
        LabLaunchingDownload.Opacity = 0d;
        LabLaunchingDownload.IsVisible = false;
        LabLaunchingDownloadLeft.Opacity = 0d;
        LabLaunchingDownloadLeft.IsVisible = false;
        ProgressLaunchingFinished.Width = new GridLength(0d, GridUnitType.Star);
        ProgressLaunchingUnfinished.Width = new GridLength(1d, GridUnitType.Star);
        PanLaunchingHint.Opacity = 0d;
        PanLaunchingHint.IsVisible = false;
        PanLaunchingInfo.Width = double.NaN; // 重置宽度改变动画
        showProgress = 0d;

        var shouldShowHint = App.Services.Settings.UiShowLaunchingHint;
        LabLaunchingHint.Text = shouldShowHint ? PageLaunchRight.GetRandomHint(true, true) : "";

        // 初始化其他页面
        PanInput.IsHitTestVisible = false;
        PanLaunching.IsHitTestVisible = false;
        LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Run;
        PanLaunching.IsVisible = true;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaOpacity(PanInput, 0d, 50),
                ModAnimation.AaOpacity(PanInput, -PanInput.Opacity, 110, ease: new ModAnimation.AniEaseInFluent(),
                    after: true),
                ModAnimation.AaScaleTransform(PanInput, 1.2d - ((ScaleTransform)PanInput.RenderTransform!).ScaleX, 160),
                ModAnimation.AaOpacity(PanLaunching, 1d - PanLaunching.Opacity, 150, 100),
                ModAnimation.AaScaleTransform(PanLaunching, 1d - ((ScaleTransform)PanLaunching.RenderTransform!).ScaleX,
                    500, 100, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaCode(() => PanLaunching.IsHitTestVisible = true, 150)
            }, "Launch State Page"); // 略作延迟，这样如果预检测失败，不会出现奇怪的弹一下的动画
    }

    /// <summary>
    ///     切换至登录页面。
    /// </summary>
    public void PageChangeToLogin()
    {
        if (PageGet(pageCurrent) is ILoginPage loginPage) loginPage.Reload();
        PanInput.IsHitTestVisible = false;
        PanLaunching.IsHitTestVisible = false;
        LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Stop;
        PanInput.IsVisible = true;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaOpacity(PanLaunching, -PanLaunching.Opacity, 150),
                ModAnimation.AaScaleTransform(PanLaunching,
                    0.8d - ((ScaleTransform)PanLaunching.RenderTransform!).ScaleX, 150,
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaOpacity(PanInput, 1d - PanInput.Opacity, 250, 50),
                ModAnimation.AaScaleTransform(PanInput, 1d - ((ScaleTransform)PanInput.RenderTransform!).ScaleX, 300, 50,
                    new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaCode(() => PanInput.IsHitTestVisible = true, 200)
            }, "Launch State Page", true);
    }

    #endregion

    #region 切换登录页面

    private enum LoginPageType
    {
        None,
        Auth,
        Ms,
        Profile,
        ProfileSkin,
        Offline
    }

    /// <summary>
    ///     当前页面的种类。
    /// </summary>
    private LoginPageType pageCurrent = LoginPageType.None;

    /// <summary>登录页面实例缓存，对应 WPF 的 ModMain.frmLogin*。</summary>
    internal static PageLoginAuth? frmLoginAuth;
    internal static PageLoginMs? frmLoginMs;
    internal static PageLoginProfile? frmLoginProfile;
    internal static PageLoginProfileSkin? frmLoginProfileSkin;
    internal static PageLoginOffline? frmLoginOffline;

    private object PageGet(LoginPageType type)
    {
        switch (type)
        {
            case LoginPageType.Auth:
            {
                frmLoginAuth ??= new PageLoginAuth();
                return frmLoginAuth;
            }
            case LoginPageType.Ms:
            {
                frmLoginMs ??= new PageLoginMs();
                return frmLoginMs;
            }
            case LoginPageType.Profile:
            {
                frmLoginProfile ??= new PageLoginProfile();
                return frmLoginProfile;
            }
            case LoginPageType.ProfileSkin:
            {
                frmLoginProfileSkin ??= new PageLoginProfileSkin();
                return frmLoginProfileSkin;
            }
            case LoginPageType.Offline:
            {
                frmLoginOffline ??= new PageLoginOffline();
                return frmLoginOffline;
            }

            default:
            {
                throw new ArgumentOutOfRangeException(nameof(type), "即将切换的登录分页编号越界");
            }
        }
    }

    /// <summary>
    ///     切换现有登录页面种类，返回新页面的实例。
    /// </summary>
    /// <param name="type">新页面的种类。</param>
    /// <param name="anim">是否显示动画。</param>
    private object PageChange(LoginPageType type, bool anim)
    {
        object pageNew = frmLoginMs!; // 初始化一个东西，避免在执行时出现异常导致雪崩
        try
        {
            #region 确定更改的页面实例并实例化

            if (pageCurrent == type)
                return pageNew;
            pageNew = PageGet(type);

            #endregion

            #region 切换页面

            ModAnimation.AniStop("FrmLogin PageChange");
            // 清除页面关联性
            if (pageNew is Control element)
                Navigator.DetachFromParent(element);
            if (anim)
            {
                // 动画
                // 执行动画
                Dispatcher.UIThread.Post(() => ModAnimation.AniStart(new[]
                {
                    ModAnimation.AaOpacity(PanLogin, -PanLogin.Opacity, 100, ease: new ModAnimation.AniEaseOutFluent()),
                    ModAnimation.AaCode(() =>
                    {
                        ModAnimation.AniControlEnabled += 1;
                        PanLogin.Children.Clear();
                        PanLogin.Children.Add((Control)pageNew);
                        ModAnimation.AniControlEnabled -= 1;
                    }, 100),
                    ModAnimation.AaOpacity(PanLogin, 1d, 100, 120, new ModAnimation.AniEaseInFluent())
                }, "FrmLogin PageChange"), DispatcherPriority.Render);
            }
            else
            {
                // 无动画
                ModAnimation.AniControlEnabled += 1;
                PanLogin.Children.Clear();
                PanLogin.Children.Add((Control)pageNew);
                ModAnimation.AniControlEnabled -= 1;
            }

            #endregion

            pageCurrent = type;
            return pageNew;
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Launch.Account.Error.SwitchPage", type.ToString()), HintType.Error);
            return pageNew;
        }
    }

    /// <summary>
    ///     确认当前显示的子页面正确，并刷新该页面。
    /// </summary>
    /// <param name="anim">是否显示动画</param>
    /// <param name="targetLoginType">目标验证方式，若正在创建档案需填</param>
    public void RefreshPage(bool anim, LoginType? targetLoginType = null)
    {
        var type = LoginPageType.None;
        if (targetLoginType is { } loginType)
        {
            type = loginType switch
            {
                LoginType.Microsoft => LoginPageType.Ms,
                LoginType.Authlib => LoginPageType.Auth,
                LoginType.Offline => LoginPageType.Offline,
                _ => LoginPageType.None
            };
        }
        else if (ProfileService.Current is not null)
        {
            type = LoginPageType.ProfileSkin;
            BtnLaunch.IsEnabled = true;
        }
        else
        {
            type = LoginPageType.Profile;
            if (_launchButtonAction != LaunchButtonAction.Download)
                BtnLaunch.IsEnabled = false;
        }

        // 刷新页面
        if (pageCurrent == type)
            return;
        PageChange(type, anim);
    }

    #endregion

    #region 皮肤

    /// <summary>皮肤加载器的输入：玩家名与 UUID，与 WPF 中 EqualableList&lt;string&gt; 的两项相同。</summary>
    public sealed record SkinInput(string UserName, string Uuid);

    // 正版皮肤
    public static ModLoader.LoaderTask<SkinInput, string> skinMs = new("Loader Skin Ms", SkinMsLoad,
        SkinMsInput);

    private static SkinInput SkinMsInput()
    {
        // 获取名称
        return new SkinInput(ProfileService.Current?.UserName ?? "", ProfileService.Current?.Uuid ?? "");
    }

    private static void SkinMsLoad(ModLoader.LoaderTask<SkinInput, string> data)
    {
        // 清空已有皮肤
        // 如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
        ClearLoginSkin();
        // 获取 Url
        var userName = data.input!.UserName;
        var uuid = data.input.Uuid;
        if (ProfileService.Current is not null)
        {
            userName = ProfileService.Current.UserName;
            uuid = ProfileService.Current.Uuid;
        }

        if (string.IsNullOrEmpty(userName))
        {
            data.output = DefaultSkin(SkinSex(OfflineAccount.Create(userName).Uuid));
            goto Finish;
        }

        try
        {
            var result = SkinNetwork.GetSkinUrlAsync(uuid, null).GetAwaiter().GetResult();
            if (data.IsAborted)
                throw new ThreadInterruptedException("当前任务已取消：" + userName);
            result = SkinNetwork.DownloadSkinAsync(result).GetAwaiter().GetResult();
            if (data.IsAborted)
                throw new ThreadInterruptedException("当前任务已取消：" + userName);
            data.output = result;
        }
        catch (Exception ex)
        {
            if (ex is ThreadInterruptedException)
            {
                data.output = "";
                return;
            }

            if (ex.ToString().Contains("429"))
            {
                data.output = DefaultSkin(SkinSex(OfflineAccount.Create(userName).Uuid));
                HintService.Hint(Lang.Text("Launch.Skin.Error.MsRateLimited", userName), HintType.Error);
            }
            else if (ex.ToString().Contains("未设置自定义皮肤"))
            {
                data.output = DefaultSkin(SkinSex(OfflineAccount.Create(userName).Uuid));
            }
            else
            {
                data.output = DefaultSkin(SkinSex(OfflineAccount.Create(userName).Uuid));
                HintService.Hint(Lang.Text("Launch.Skin.Error.MsGet", userName), HintType.Error);
            }
        }

        Finish: ;

        // 刷新显示
        RefreshLoginSkin(data);
    }

    // 离线皮肤
    public static ModLoader.LoaderTask<SkinInput, string> skinLegacy = new("Loader Skin Legacy",
        SkinLegacyLoad, SkinLegacyInput);

    private static SkinInput SkinLegacyInput()
    {
        return new SkinInput(ProfileService.Current?.UserName ?? "", ProfileService.Current?.Uuid ?? "");
    }

    private static void SkinLegacyLoad(ModLoader.LoaderTask<SkinInput, string> data)
    {
        // 清空已有皮肤
        ClearLoginSkin();
        data.output = DefaultSkin(SkinSex(data.input!.Uuid));
        // 刷新显示
        RefreshLoginSkin(data);
    }

    // Authlib-Injector 皮肤
    public static ModLoader.LoaderTask<SkinInput, string> skinAuth = new("Loader Skin Auth",
        SkinAuthLoad, SkinAuthInput);

    private static SkinInput SkinAuthInput()
    {
        // 获取名称
        return new SkinInput(ProfileService.Current?.UserName ?? "", ProfileService.Current?.Uuid ?? "");
    }

    private static void SkinAuthLoad(ModLoader.LoaderTask<SkinInput, string> data)
    {
        // 清空已有皮肤
        // 如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
        ClearLoginSkin();
        // 获取 Url
        var userName = data.input!.UserName;
        var uuid = data.input.Uuid;
        if (string.IsNullOrEmpty(userName))
        {
            data.output = DefaultSkin("Steve");
            goto Finish;
        }

        try
        {
            var result = SkinNetwork.GetSkinUrlAsync(uuid, ProfileService.Current?.Server).GetAwaiter().GetResult();
            if (data.IsAborted)
                throw new ThreadInterruptedException("当前任务已取消：" + userName);
            result = SkinNetwork.DownloadSkinAsync(result).GetAwaiter().GetResult();
            if (data.IsAborted)
                throw new ThreadInterruptedException("当前任务已取消：" + userName);
            data.output = result;
        }
        catch (Exception ex)
        {
            if (ex is ThreadInterruptedException)
            {
                data.output = "";
                return;
            }

            if (ex.ToString().Contains("429"))
            {
                data.output = DefaultSkin("Steve");
                HintService.Hint(Lang.Text("Launch.Skin.Error.AuthlibRateLimited"), HintType.Error);
            }
            else if (ex.ToString().Contains("未设置自定义皮肤"))
            {
                data.output = DefaultSkin("Steve");
            }
            else
            {
                data.output = DefaultSkin("Steve");
                HintService.Hint(Lang.Text("Launch.Skin.Error.AuthGet", userName), HintType.Error);
            }
        }

        Finish: ;

        // 刷新显示
        RefreshLoginSkin(data);
    }

    // 全部皮肤加载器
    // 需要放在其中元素的后面，否则会因为它提前被加载而莫名其妙变成 Nothing
    public static List<ModLoader.LoaderTask<SkinInput, string>> skinLoaders = new()
        { skinMs, skinLegacy, skinAuth };

    private static void ClearLoginSkin()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (frmLoginProfileSkin?.Skin is { } skin)
                skin.Clear();
        });
    }

    /// <summary>刷新显示；若皮肤控件已换成别的加载器，则放弃输入以便下次重新加载。</summary>
    private static void RefreshLoginSkin(ModLoader.LoaderTask<SkinInput, string> data)
    {
        if (frmLoginProfileSkin?.Skin is { } skin && ReferenceEquals(skin.loader, data))
            Dispatcher.UIThread.Post(skin.Load);
        else if (!data.IsAborted)
            data.input = null; // 清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
    }

    /// <summary>默认皮肤图片的地址。</summary>
    internal static string DefaultSkin(string name) => $"avares://PCL.Shion/Assets/Images/Skins/{name}.png";

    /// <summary>
    ///     根据 UUID 决定默认皮肤是 Alex 还是 Steve，与 Minecraft 的算法相同：UUID 的 hashCode 为奇数时为 Alex。
    /// </summary>
    internal static string SkinSex(string uuid)
    {
        var hex = uuid.Replace("-", "");
        if (hex.Length != 32)
            return "Steve";
        var msb = (long)Convert.ToUInt64(hex[..16], 16);
        var lsb = (long)Convert.ToUInt64(hex[16..], 16);
        var hilo = msb ^ lsb;
        var hash = (int)(hilo >> 32) ^ (int)hilo;
        return (hash & 1) == 1 ? "Alex" : "Steve";
    }

    #endregion
}
