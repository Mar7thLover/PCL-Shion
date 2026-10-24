using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using PCL.Shion.Controls;
using PCL.Shion.Core.Online;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;
using PCL.Shion.Pages.Download;

namespace PCL.Shion.Pages.Instance;

public partial class PageInstanceInstall : MyPageRight
{
    private enum InstallAction
    {
        Modify,
        Reset
    }

    private bool isLoad;
    private string? lastVersionName;
    private InstallAction _installAction;

    public PageInstanceInstall()
    {
        InitializeComponent();
        PanScroll = PanBack;
        InitializeEvents();
        LoaderInit();
        Loaded += (_, _) => Init();
        LoadMinecraft.Text = Lang.Text("Download.Version.LoadingList");
    }

    /// <summary>
    ///     WPF 中的 XAML 事件特性（Click、MouseLeftButtonUp、PreviewSwap）在 Avalonia 中改为代码绑定。
    /// </summary>
    private void InitializeEvents()
    {
        BtnSelectStart.Click += BtnSelectStart_Click;
        CardMinecraft.PreviewSwap += CardMinecraft_PreviewSwap;
        CardForge.PreviewSwap += CardForge_PreviewSwap;
        CardCleanroom.PreviewSwap += CardCleanroom_PreviewSwap;
        CardNeoForge.PreviewSwap += CardNeoForge_PreviewSwap;
        CardFabric.PreviewSwap += CardFabric_PreviewSwap;
        CardLegacyFabric.PreviewSwap += CardLegacyFabric_PreviewSwap;
        CardFabricApi.PreviewSwap += CardFabricApi_PreviewSwap;
        CardLegacyFabricApi.PreviewSwap += CardLegacyFabricApi_PreviewSwap;
        CardLabyMod.PreviewSwap += CardLabyMod_PreviewSwap;
        CardOptiFine.PreviewSwap += CardOptiFine_PreviewSwap;
        CardOptiFabric.PreviewSwap += CardOptiFabric_PreviewSwap;
        CardLiteLoader.PreviewSwap += CardLiteLoader_PreviewSwap;
        BtnForgeClear.PointerReleased += Forge_Clear;
        BtnCleanroomClear.PointerReleased += Cleanroom_Clear;
        BtnNeoForgeClear.PointerReleased += NeoForge_Clear;
        BtnFabricClear.PointerReleased += Fabric_Clear;
        BtnLegacyFabricClear.PointerReleased += LegacyFabric_Clear;
        BtnFabricApiClear.PointerReleased += FabricApi_Clear;
        BtnLegacyFabricApiClear.PointerReleased += LegacyFabricApi_Clear;
        BtnLabyModClear.PointerReleased += LabyMod_Clear;
        BtnOptiFineClear.PointerReleased += OptiFine_Clear;
        BtnOptiFabricClear.PointerReleased += OptiFabric_Clear;
        BtnLiteLoaderClear.PointerReleased += LiteLoader_Clear;
    }

    private IBrush Brush(string key) => this.FindBrushOrTransparent(key);

    private void LoaderInit()
    {
        disabledPageAnimControls.Add(BtnSelectStart);
        // PageLoaderInit(LoadMinecraft, PanLoad, PanBack, Nothing, DlClientListLoader, AddressOf LoadMinecraft_OnFinish)
        PageLoaderInit(LoadMinecraft, PanLoad, PanAllBack, null, ModDownload.dlClientListLoader, _ => GetCurrentInfo());
        LoadOptiFine.StateChanged += (_, _, _) => { OptiFine_Loaded(); ReloadSelected(); };
        LoadLiteLoader.StateChanged += (_, _, _) => { LiteLoader_Loaded(); ReloadSelected(); };
        LoadForge.StateChanged += (_, _, _) => { Forge_Loaded(); ReloadSelected(); };
        LoadNeoForge.StateChanged += (_, _, _) => { NeoForge_Loaded(); ReloadSelected(); };
        LoadCleanroom.StateChanged += (_, _, _) => { Cleanroom_Loaded(); ReloadSelected(); };
        LoadFabric.StateChanged += (_, _, _) => { Fabric_Loaded(); ReloadSelected(); };
        LoadFabricApi.StateChanged += (_, _, _) => { FabricApi_Loaded(); ReloadSelected(); };
        LoadLegacyFabric.StateChanged += (_, _, _) => { LegacyFabric_Loaded(); ReloadSelected(); };
        LoadLegacyFabricApi.StateChanged += (_, _, _) => { LegacyFabricApi_Loaded(); ReloadSelected(); };
        LoadOptiFabric.StateChanged += (_, _, _) => { OptiFabric_Loaded(); ReloadSelected(); };
        LoadLabyMod.StateChanged += (_, _, _) => { LabyMod_Loaded(); ReloadSelected(); };
        PageExit += () => isInSelectPage = false;
    }

    private void Init()
    {
        PanBack.ScrollToHome();

        GetCurrentInfo();

        var needRefresh = lastVersionName is null || (lastVersionName ?? "") != (_vanillaName ?? "");
        lastVersionName = _vanillaName;

        ModDownload.dlOptiFineListLoader.Start(isForceRestart: needRefresh);
        ModDownload.dlLiteLoaderListLoader.Start(isForceRestart: needRefresh);
        ModDownload.dlFabricListLoader.Start(isForceRestart: needRefresh);
        ModDownload.dlNeoForgeListLoader.Start(isForceRestart: needRefresh);
        ModDownload.dlCleanroomListLoader.Start(isForceRestart: needRefresh);
        ModDownload.dlLabyModListLoader.Start(isForceRestart: needRefresh);
        ModDownload.dlLegacyFabricListLoader.Start(isForceRestart: needRefresh);
        ModDownload.dlFabricApiLoader.Start(isForceRestart: needRefresh);
        ModDownload.dlLegacyFabricApiLoader.Start(isForceRestart: needRefresh);
        ModDownload.dlOptiFabricLoader.Start(isForceRestart: needRefresh);

        // 非重复加载部分
        if (isLoad)
        {
            ReloadSelected();
            return;
        }
        isLoad = true;

        ModDownloadLib.McDownloadForgeRecommendedRefresh();

        LoadOptiFine.State = ModDownload.dlOptiFineListLoader;
        LoadLiteLoader.State = ModDownload.dlLiteLoaderListLoader;
        LoadFabric.State = ModDownload.dlFabricListLoader;
        LoadFabricApi.State = ModDownload.dlFabricApiLoader;
        LoadNeoForge.State = ModDownload.dlNeoForgeListLoader;
        LoadCleanroom.State = ModDownload.dlCleanroomListLoader;
        LoadOptiFabric.State = ModDownload.dlOptiFabricLoader;
        LoadLabyMod.State = ModDownload.dlLabyModListLoader;
        LoadLegacyFabric.State = ModDownload.dlLegacyFabricListLoader;
        LoadLegacyFabricApi.State = ModDownload.dlLegacyFabricApiLoader;
    }

    #region 安装

    private async void BtnSelectStart_Click(object? sender, RoutedEventArgs e)
    {
        // Quilt 实例无法通过安装管线重装/修改（已移除 Quilt 安装支持）
        if (PageInstanceLeft.McInstance.Info.HasQuilt)
        {
            Hint.Show(Lang.Text("Instance.Overall.Reset.QuiltUnsupported"));
            return;
        }

        // 确认版本隔离
        if (_selectedLoaderType is not null &&
            (App.Services.Settings.IndieSolutionV2 == 0 ||
             App.Services.Settings.IndieSolutionV2 == 2))
            if (await MsgBox.ShowAsync(
                    Lang.Text("Download.Install.InstanceIsolation.Warning.Message"),
                    Lang.Text("Download.Install.InstanceIsolation.Warning.Title"),
                    Lang.Text("Download.Install.InstanceIsolation.Warning.Cancel"),
                    Lang.Text("Download.Install.InstanceIsolation.Warning.Continue")) == 1)
                return;

        if (_installAction == InstallAction.Reset)
            if (await MsgBox.ShowAsync(
                    Lang.Text("Instance.Install.Reset.Message"),
                    Lang.Text("Instance.Install.Reset.Title"),
                    Lang.Text("Common.Action.Continue"),
                    Lang.Text("Common.Action.Cancel")
                ) == 2)
                return;

        // 删除 LabyMod Neo 文件
        if ((PageInstanceLeft.McInstance.PathIndie ?? "") != (PageInstanceLeft.McInstance.PathInstance ?? "") &&
            PageInstanceLeft.McInstance.Info.HasLabyMod)
            Directory.Delete(System.IO.Path.Combine(PageInstanceLeft.McInstance.PathIndie, "labymod-neo"), true);
        // 备份实例核心文件
        File.Copy(PageInstanceLeft.McInstance.PathInstance + PageInstanceLeft.McInstance.Name + ".json",
            PageInstanceLeft.McInstance.PathInstance + @"PCLInstallBackups\" + PageInstanceLeft.McInstance.Name + ".json", true);
        if (File.Exists(PageInstanceLeft.McInstance.PathInstance + PageInstanceLeft.McInstance.Name + ".jar"))
            File.Copy(PageInstanceLeft.McInstance.PathInstance + PageInstanceLeft.McInstance.Name + ".jar",
                PageInstanceLeft.McInstance.PathInstance + @"PCLInstallBackups\" + PageInstanceLeft.McInstance.Name +
                ".jar", true);
        // 确认独立 API (如 Fabric API 等) 是否需要被修改
        if (selectedFabricApi?.Equals(_currentFabricApi) == true)
            selectedFabricApi = null;
        if (selectedLegacyFabricApi?.Equals(_currentLegacyFabricApi) == true)
            selectedLegacyFabricApi = null;
        if (selectedOptiFabric?.Equals(_currentOptiFabric) == true)
            selectedOptiFabric = null;
        // 提交安装申请
        var request = new ModDownloadLib.McInstallRequest
        {
            targetInstanceName = PageInstanceLeft.McInstance.Name,
            targetInstanceFolder = $@"{App.Services.Settings.MinecraftRoot}versions\{PageInstanceLeft.McInstance.Name}\",
            minecraftJson = _vanillaData?["url"]?.ToString(),
            minecraftName = _vanillaName,
            optiFineEntry = selectedOptiFine,
            forgeEntry = selectedForge,
            neoForgeEntry = selectedNeoForge,
            neoForgeVersion = selectedNeoForgeVersion,
            cleanroomEntry = selectedCleanroom,
            cleanroomVersion = selectedCleanroomVersion,
            fabricVersion = selectedFabric,
            fabricApi = selectedFabricApi,
            optiFabric = selectedOptiFabric,
            liteLoaderEntry = selectedLiteLoader,
            labyModChannel = selectedLabyModChannel,
            labyModCommitRef = selectedLabyModCommitRef,
            legacyFabricVersion = selectedLegacyFabric,
            legacyFabricApi = selectedLegacyFabricApi
        };
        BtnSelectStart.IsEnabled = false;
        if (!ModDownloadLib.McInstall(request, _installAction == InstallAction.Modify ? Lang.Text("Instance.Install.Action.ModifyLabel") : Lang.Text("Common.Action.Reset")))
            return;
        // 删除旧的独立 API 文件
        if (selectedFabricApi is not null && _currentFabricApiPath is not null)
            File.Delete(_currentFabricApiPath);
        if (selectedLegacyFabricApi is not null && _currentLegacyFabricApiPath is not null)
            File.Delete(_currentLegacyFabricApiPath);
        if (selectedOptiFabric is not null && _currentOptiFabricPath is not null)
            File.Delete(_currentOptiFabricPath);
        // 返回主页
        Navigator.Go(PageType.Launch);
    }

    #endregion

    private string? GetLoaderError(MyLoading loader)
    {
        if (loader is null || !loader.State.IsLoader)
            return Lang.Text("Download.Install.State.Getting");
        switch (loader.State.LoadingState)
        {
            case MyLoading.MyLoadingState.Run:
            {
                return Lang.Text("Download.Install.State.Getting");
            }
            case MyLoading.MyLoadingState.Error:
            {
                var message = loader.State.Error!.Message;
                return message == Lang.Text("Download.Install.State.NoVersion") ? Lang.Text("Download.Install.State.NoVersion") : Lang.Text("Download.Install.State.GetFailed", message);
            }
            case MyLoading.MyLoadingState.Unloaded:
            {
                return Lang.Text("Download.Install.State.UnknownUnloaded");
            }

            default:
            {
                return null;
            }
        }
    }

    #region 页面切换

    // 页面切换动画
    public bool isInSelectPage;
    private bool isFirstLoaded;

    private void EnterSelectPage()
    {
        if (isInSelectPage)
            return;
        isInSelectPage = true;

        disabledPageAnimControls.Remove(BtnSelectStart);
        BtnSelectStart.Show = true;
        autoSelectedFabricApi = false;
        autoSelectedOptiFabric = false;
        PanSelect.IsVisible = true;
        PanSelect.IsHitTestVisible = true;
        PanMinecraft.IsHitTestVisible = false;
        PanBack.IsHitTestVisible = false;
        PanBack.ScrollToHome();

        CardMinecraft.IsSwapped = true;
        CardOptiFine.IsSwapped = true;
        CardLiteLoader.IsSwapped = true;
        CardForge.IsSwapped = true;
        CardNeoForge.IsSwapped = true;
        CardCleanroom.IsSwapped = true;
        CardFabric.IsSwapped = true;
        CardFabricApi.IsSwapped = true;
        CardOptiFabric.IsSwapped = true;
        CardLabyMod.IsSwapped = true;
        CardLegacyFabric.IsSwapped = true;
        CardLegacyFabricApi.IsSwapped = true;

        if (!App.Services.Settings.HintInstallPageBack)
        {
            App.Services.Settings.HintInstallPageBack = true;
            Hint.Show(Lang.Text("Download.Install.Hint.MinecraftBack"));
        }

        // 如果在选择页面按了刷新键，选择页的东西可能会由于动画被隐藏，但不会由于加载结束而再次显示，因此这里需要手动恢复
        foreach (var Card in GetAllAnimControls(PanSelect))
        {
            Card.Opacity = 1d;
            Card.RenderTransform = new TranslateTransform();
        }

        // 启动 Forge 加载
        if (McInstanceInfo.IsFormatFit(_vanillaName))
        {
            var forgeLoader =
                new ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>(
                    "DlForgeVersion " + _vanillaName, ModDownload.DlForgeVersionMain);
            LoadForge.State = forgeLoader;
            forgeLoader.Start(_vanillaName);
        }

        // 启动 Fabric API、Legacy Fabric API、OptiFabric 加载
        ModDownload.dlFabricApiLoader.Start();
        ModDownload.dlLegacyFabricApiLoader.Start();
        ModDownload.dlOptiFabricLoader.Start();

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(PanMinecraft, -PanMinecraft.Opacity, 100, 10),
            ModAnimation.AaCode(() =>
            {
                PanBack.ScrollToHome();
                OptiFine_Loaded();
                LiteLoader_Loaded();
                Forge_Loaded();
                NeoForge_Loaded();
                Cleanroom_Loaded();
                Fabric_Loaded();
                LegacyFabric_Loaded();
                FabricApi_Loaded();
                LegacyFabricApi_Loaded();
                LabyMod_Loaded();
                OptiFabric_Loaded();
                ReloadSelected();
            }, after: true),
            ModAnimation.AaOpacity(PanSelect, 1d - PanSelect.Opacity, 250, 150),
            ModAnimation.AaCode(() =>
            {
                PanMinecraft.IsVisible = false;
                PanBack.IsHitTestVisible = true;
                // 初始化 Binding
                if (isFirstLoaded)
                    return;
                isFirstLoaded = true;
                BtnOptiFineClearInner.Bind(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardOptiFine.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLiteLoaderClearInner.Bind(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardLiteLoader.MainTextBlock, Mode = BindingMode.OneWay });
                BtnForgeClearInner.Bind(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardForge.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLegacyFabricClearInner.Bind(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardLegacyFabric.MainTextBlock, Mode = BindingMode.OneWay });
                BtnNeoForgeClearInner.Bind(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardNeoForge.MainTextBlock, Mode = BindingMode.OneWay });
                BtnCleanroomClearInner.Bind(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardCleanroom.MainTextBlock, Mode = BindingMode.OneWay });
                BtnFabricClearInner.Bind(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardFabric.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLegacyFabricApiClearInner.Bind(Shape.FillProperty,
                    new Binding("Foreground")
                        { Source = CardLegacyFabricApi.MainTextBlock, Mode = BindingMode.OneWay });
                BtnFabricApiClearInner.Bind(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardFabricApi.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLabyModClearInner.Bind(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardLabyMod.MainTextBlock, Mode = BindingMode.OneWay });
                BtnOptiFabricClearInner.Bind(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardOptiFabric.MainTextBlock, Mode = BindingMode.OneWay });
            }, after: true)
        }, "FrmInstanceInstall SelectPageSwitch", true);
    }

    public void ExitSelectPage()
    {
        if (!isInSelectPage)
            return;
        isInSelectPage = false;

        LoadMinecraft_OnFinish();

        disabledPageAnimControls.Add(BtnSelectStart);
        BtnSelectStart.Show = false;

        ClearSelected(); // 清除已选择项
        PanMinecraft.IsVisible = true;
        PanSelect.IsHitTestVisible = false;
        PanMinecraft.IsHitTestVisible = true;
        PanBack.IsHitTestVisible = false;
        PanBack.ScrollToHome();

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(PanSelect, -PanSelect.Opacity, 90, 10),
            ModAnimation.AaCode(() => PanBack.ScrollToHome(), after: true),
            ModAnimation.AaOpacity(PanMinecraft, 1d - PanMinecraft.Opacity, 150, 100),
            ModAnimation.AaCode(() =>
            {
                PanSelect.IsVisible = false;
                PanBack.IsHitTestVisible = true;
            }, after: true)
        }, "FrmInstanceInstall SelectPageSwitch");
    }

    // 页面切换触发
    public void MinecraftSelected(MyListItem sender, RoutedEventArgs? e)
    {
        _vanillaName = sender.Title;
        _vanillaData = (JsonObject)sender.Tag!;
        _vanillaIcon = sender.Logo;
        EnterSelectPage();
    }

    private void CardMinecraft_PreviewSwap(object sender, RouteEventArgs e)
    {
        ExitSelectPage();
        e.handled = true;
    }

    #endregion

    #region 选择

    // Minecraft
    private string? _vanillaName;
    private JsonObject? _vanillaData;
    private string? _vanillaIcon;
    private int VanillaDrop => McInstanceInfo.VersionToDrop(_vanillaName, true);

    // OptiFine
    private ModDownload.DlOptiFineListEntry? selectedOptiFine;

    /// <summary>
    ///     选定的主 Mod 加载器。
    /// </summary>
    private ModLoaderType? _selectedLoaderType;

    /// <summary>
    ///     选定的 Mod Loader API 名称，内容应为 Fabric API
    /// </summary>
    private string? selectedAPIName;

    // LiteLoader
    private ModDownload.DlLiteLoaderListEntry? selectedLiteLoader;

    // Forge
    private ModDownload.DlForgeVersionEntry? selectedForge;

    // Cleanroom
    private ModDownload.DlCleanroomListEntry? selectedCleanroom;
    private string? selectedCleanroomVersion;

    // NeoForge
    private ModDownload.DlNeoForgeListEntry? selectedNeoForge;
    private string? selectedNeoForgeVersion;

    // Fabric
    private string? selectedFabric;

    // FabricApi
    private CompFile? selectedFabricApi;

    // LegacyFabric
    private string? selectedLegacyFabric;

    // Legacy FabricApi
    private CompFile? selectedLegacyFabricApi;

    // LabyMod
    private string? selectedLabyModChannel;
    private string? selectedLabyModCommitRef;
    private string? selectedLabyModVersion;

    // OptiFabric
    private CompFile? selectedOptiFabric;

    private bool _ReloadSelected_Ongoing; // #3742 中，LoadOptiFineGetError 会初始化 LoadOptiFine，触发事件 LoadOptiFine.StateChanged，导致再次调用 SelectReload

    /// <summary>
    ///     重载已选择的项目的显示。
    /// </summary>
    private void ReloadSelected()
    {
        if (_vanillaName is null || _ReloadSelected_Ongoing)
            return;
        _ReloadSelected_Ongoing = true;
        try
        {
            var selectedInfo = GetSelectInfo();
            // 主预览
            ItemSelect.Title = PageInstanceLeft.McInstance.Name;
            ItemSelect.Logo = GetSelectLogo();
            BtnSelectStart.IsEnabled = true;
            if ((selectedInfo ?? "") == (currentInfo ?? ""))
            {
                ItemSelect.Info = selectedInfo;
                BtnSelectStart.Text = Lang.Text("Instance.Install.Action.StartReset");
                _installAction = InstallAction.Reset;
                BtnSelectStart.SvgIcon = "lucide/rotate-ccw";
            }
            else
            {
                ItemSelect.Info = currentInfo + " → " + selectedInfo;
                BtnSelectStart.Text = Lang.Text("Instance.Install.Action.StartModify");
                _installAction = InstallAction.Modify;
                BtnSelectStart.SvgIcon = "lucide/pencil";
            }

            // Minecraft
            ImgMinecraft.Source = _vanillaIcon;
            LabMinecraft.Text = _vanillaName;
            LabMinecraft.Foreground = Brush("ColorBrushGray1");
            // OptiFine
            if (!McVersionComparer.CompareVersionGe(_vanillaName, "1.7.2"))
            {
                CardOptiFine.IsVisible = false;
            }
            else
            {
                CardOptiFine.IsVisible = true;
                var optiFineError = LoadOptiFineGetError();
                CardOptiFine.MainSwap!.IsVisible = optiFineError is null;
                if (optiFineError is not null)
                    CardOptiFine.IsSwapped = true;
                SetPanelVisibility(PanOptiFineInfo, CardOptiFine.IsSwapped);
                if (selectedOptiFine is null)
                {
                    BtnOptiFineClear.IsVisible = false;
                    ImgOptiFine.IsVisible = false;
                    LabOptiFine.Text = optiFineError ?? Lang.Text("Download.Install.State.CanAdd");
                    LabOptiFine.Foreground = Brush("ColorBrushGray4");
                }
                else
                {
                    BtnOptiFineClear.IsVisible = true;
                    ImgOptiFine.IsVisible = true;
                    LabOptiFine.Text = selectedOptiFine.DisplayName.Replace(_vanillaName + " ", "");
                    LabOptiFine.Foreground = Brush("ColorBrushGray1");
                }
            }

            // LiteLoader
            if (!McInstanceInfo.IsFormatFit(_vanillaName)
                || !McVersionComparer.CompareVersionGe(_vanillaName, "1.5.2")
                || !McVersionComparer.CompareVersionGe("1.12.2", _vanillaName))
            {
                CardLiteLoader.IsVisible = false;
            }
            else
            {
                CardLiteLoader.IsVisible = true;
                var liteLoaderError = LoadLiteLoaderGetError();
                CardLiteLoader.MainSwap!.IsVisible = liteLoaderError is null;
                if (liteLoaderError is not null)
                    CardLiteLoader.IsSwapped = true; // 例如在同时展开卡片时选择了不兼容项则强制折叠
                SetPanelVisibility(PanLiteLoaderInfo, CardLiteLoader.IsSwapped);
                if (selectedLiteLoader is null)
                {
                    BtnLiteLoaderClear.IsVisible = false;
                    ImgLiteLoader.IsVisible = false;
                    LabLiteLoader.Text = liteLoaderError ?? Lang.Text("Download.Install.State.CanAdd");
                    LabLiteLoader.Foreground = Brush("ColorBrushGray4");
                }
                else
                {
                    BtnLiteLoaderClear.IsVisible = true;
                    ImgLiteLoader.IsVisible = true;
                    LabLiteLoader.Text = selectedLiteLoader.Inherit;
                    LabLiteLoader.Foreground = Brush("ColorBrushGray1");
                }
            }

            // Forge
            if (!McInstanceInfo.IsFormatFit(_vanillaName)
                || !McVersionComparer.CompareVersionGe(_vanillaName, "1.1"))
            {
                CardForge.IsVisible = false;
            }
            else
            {
                CardForge.IsVisible = true;
                var forgeError = LoadForgeGetError();
                CardForge.MainSwap!.IsVisible = forgeError is null;
                if (forgeError is not null)
                    CardForge.IsSwapped = true;
                SetPanelVisibility(PanForgeInfo, CardForge.IsSwapped);
                if (selectedForge is null)
                {
                    BtnForgeClear.IsVisible = false;
                    ImgForge.IsVisible = false;
                    LabForge.Text = forgeError ?? Lang.Text("Download.Install.State.CanAdd");
                    LabForge.Foreground = Brush("ColorBrushGray4");
                }
                else
                {
                    BtnForgeClear.IsVisible = true;
                    ImgForge.IsVisible = true;
                    LabForge.Text = selectedForge.VersionName;
                    LabForge.Foreground = Brush("ColorBrushGray1");
                }
            }

            // Cleanroom
            if (_vanillaName == "1.12.2")
            {
                CardCleanroom.IsVisible = true;
                var cleanroomError = LoadCleanroomGetError();
                CardCleanroom.MainSwap!.IsVisible = cleanroomError is null;
                if (cleanroomError is not null)
                    CardCleanroom.IsSwapped = true;
                SetPanelVisibility(PanCleanroomInfo, CardCleanroom.IsSwapped);
                if (selectedCleanroom is null)
                {
                    BtnCleanroomClear.IsVisible = false;
                    ImgCleanroom.IsVisible = false;
                    LabCleanroom.Text = cleanroomError ?? Lang.Text("Download.Install.State.CanAdd");
                    LabCleanroom.Foreground = Brush("ColorBrushGray4");
                }
                else
                {
                    BtnCleanroomClear.IsVisible = true;
                    ImgCleanroom.IsVisible = true;
                    LabCleanroom.Text = selectedCleanroom.VersionName;
                    LabCleanroom.Foreground = Brush("ColorBrushGray1");
                }
            }
            else
            {
                CardCleanroom.IsVisible = false;
            }

            // NeoForge
            if (!McVersionComparer.CompareVersionGe(_vanillaName, "1.20.1"))
            {
                CardNeoForge.IsVisible = false;
            }
            else
            {
                CardNeoForge.IsVisible = true;
                var neoForgeError = LoadNeoForgeGetError();
                CardNeoForge.MainSwap!.IsVisible = neoForgeError is null;
                if (neoForgeError is not null)
                    CardNeoForge.IsSwapped = true;
                SetPanelVisibility(PanNeoForgeInfo, CardNeoForge.IsSwapped);
                if (selectedNeoForge is null)
                {
                    BtnNeoForgeClear.IsVisible = false;
                    ImgNeoForge.IsVisible = false;
                    LabNeoForge.Text = neoForgeError ?? Lang.Text("Download.Install.State.CanAdd");
                    LabNeoForge.Foreground = Brush("ColorBrushGray4");
                }
                else
                {
                    BtnNeoForgeClear.IsVisible = true;
                    ImgNeoForge.IsVisible = true;
                    LabNeoForge.Text = selectedNeoForge.VersionName;
                    LabNeoForge.Foreground = Brush("ColorBrushGray1");
                }
            }

            // Fabric
            if (VanillaDrop < 130
                || (VanillaDrop == 130 && !McVersionComparer.CompareVersionGe(_vanillaName, "18w43b")))
            {
                CardFabric.IsVisible = false;
            }
            else
            {
                CardFabric.IsVisible = true;
                var fabricError = LoadFabricGetError();
                CardFabric.MainSwap!.IsVisible = fabricError is null;
                if (fabricError is not null)
                    CardFabric.IsSwapped = true;
                SetPanelVisibility(PanFabricInfo, CardFabric.IsSwapped);
                if (selectedFabric is null)
                {
                    BtnFabricClear.IsVisible = false;
                    ImgFabric.IsVisible = false;
                    LabFabric.Text = fabricError ?? Lang.Text("Download.Install.State.CanAdd");
                    LabFabric.Foreground = Brush("ColorBrushGray4");
                }
                else
                {
                    BtnFabricClear.IsVisible = true;
                    ImgFabric.IsVisible = true;
                    LabFabric.Text = selectedFabric.Replace("+build", "");
                    LabFabric.Foreground = Brush("ColorBrushGray1");
                }
            }

            // FabricApi
            if (selectedFabric is null)
            {
                CardFabricApi.IsVisible = false;
            }
            else
            {
                CardFabricApi.IsVisible = true;
                var fabricApiError = LoadFabricApiGetError();
                CardFabricApi.MainSwap!.IsVisible = fabricApiError is null;
                if (fabricApiError is not null || selectedFabric is null)
                    CardFabricApi.IsSwapped = true;
                SetPanelVisibility(PanFabricApiInfo, CardFabricApi.IsSwapped);
                if (selectedFabricApi is null)
                {
                    BtnFabricApiClear.IsVisible = false;
                    ImgFabricApi.IsVisible = false;
                    LabFabricApi.Text = fabricApiError ?? Lang.Text("Download.Install.State.CanAdd");
                    LabFabricApi.Foreground = Brush("ColorBrushGray4");
                }
                else
                {
                    BtnFabricApiClear.IsVisible = true;
                    ImgFabricApi.IsVisible = true;
                    LabFabricApi.Text = selectedFabricApi.DisplayName.Split("]")[1].Replace("Fabric API ", "")
                        .Replace(" build ", ".").Split("+").First().Trim();
                    LabFabricApi.Foreground = Brush("ColorBrushGray1");
                }
            }

            // LegacyFabric
            if (VanillaDrop < 30 || VanillaDrop > 130)
            {
                CardLegacyFabric.IsVisible = false;
            }
            else
            {
                CardLegacyFabric.IsVisible = true;
                var legacyFabricError = LoadLegacyFabricGetError();
                CardLegacyFabric.MainSwap!.IsVisible = legacyFabricError is null;
                if (legacyFabricError is not null)
                    CardLegacyFabric.IsSwapped = true;
                SetPanelVisibility(PanLegacyFabricInfo, CardLegacyFabric.IsSwapped);
                if (selectedLegacyFabric is null)
                {
                    BtnLegacyFabricClear.IsVisible = false;
                    ImgLegacyFabric.IsVisible = false;
                    LabLegacyFabric.Text = legacyFabricError ?? Lang.Text("Download.Install.State.CanAdd");
                    LabLegacyFabric.Foreground = Brush("ColorBrushGray4");
                }
                else
                {
                    BtnLegacyFabricClear.IsVisible = true;
                    ImgLegacyFabric.IsVisible = true;
                    LabLegacyFabric.Text = selectedLegacyFabric.Replace("+build", "");
                    LabLegacyFabric.Foreground = Brush("ColorBrushGray1");
                }
            }

            // LegacyFabricApi
            if (selectedLegacyFabric is null)
            {
                CardLegacyFabricApi.IsVisible = false;
            }
            else
            {
                CardLegacyFabricApi.IsVisible = true;
                var legacyFabricApiError = LoadLegacyFabricApiGetError();
                CardLegacyFabricApi.MainSwap!.IsVisible = legacyFabricApiError is null;
                if (legacyFabricApiError is not null || selectedLegacyFabric is null)
                    CardLegacyFabricApi.IsSwapped = true;
                SetPanelVisibility(PanLegacyFabricApiInfo, CardLegacyFabricApi.IsSwapped);
                if (selectedLegacyFabricApi is null)
                {
                    BtnLegacyFabricApiClear.IsVisible = false;
                    ImgLegacyFabricApi.IsVisible = false;
                    LabLegacyFabricApi.Text = legacyFabricApiError ?? Lang.Text("Download.Install.State.CanAdd");
                    LabLegacyFabricApi.Foreground = Brush("ColorBrushGray4");
                }
                else
                {
                    BtnLegacyFabricApiClear.IsVisible = true;
                    ImgLegacyFabricApi.IsVisible = true;
                    LabLegacyFabricApi.Text = selectedLegacyFabricApi.DisplayName.Replace("Legacy Fabric API ", "");
                    LabLegacyFabricApi.Foreground = Brush("ColorBrushGray1");
                }
            }

            // LabyMod
            if (!McInstanceInfo.IsFormatFit(_vanillaName)
                || !McVersionComparer.CompareVersionGe(_vanillaName, "1.8.9"))
            {
                CardLabyMod.IsVisible = false;
            }
            else
            {
                CardLabyMod.IsVisible = true;
                var labyModError = LoadLabyModGetError();
                CardLabyMod.MainSwap!.IsVisible = labyModError is null;
                if (labyModError is not null)
                    CardLabyMod.IsSwapped = true;
                SetPanelVisibility(PanLabyModInfo, CardLabyMod.IsSwapped);
                if (selectedLabyModVersion is null)
                {
                    BtnLabyModClear.IsVisible = false;
                    ImgLabyMod.IsVisible = false;
                    LabLabyMod.Text = labyModError ?? Lang.Text("Download.Install.State.CanAdd");
                    LabLabyMod.Foreground = Brush("ColorBrushGray4");
                }
                else
                {
                    BtnLabyModClear.IsVisible = true;
                    ImgLabyMod.IsVisible = true;
                    LabLabyMod.Text = selectedLabyModVersion;
                    LabLabyMod.Foreground = Brush("ColorBrushGray1");
                }
            }

            // OptiFabric
            if (selectedFabric is null || selectedOptiFine is null)
            {
                CardOptiFabric.IsVisible = false;
            }
            else
            {
                CardOptiFabric.IsVisible = true;
                var optiFabricError = LoadOptiFabricGetError();
                CardOptiFabric.MainSwap!.IsVisible = optiFabricError is null;
                if (optiFabricError is not null || selectedFabric is null)
                    CardOptiFabric.IsSwapped = true;
                SetPanelVisibility(PanOptiFabricInfo, CardOptiFabric.IsSwapped);
                if (selectedOptiFabric is null)
                {
                    BtnOptiFabricClear.IsVisible = false;
                    ImgOptiFabric.IsVisible = false;
                    LabOptiFabric.Text = optiFabricError ?? Lang.Text("Download.Install.State.CanAdd");
                    LabOptiFabric.Foreground = Brush("ColorBrushGray4");
                }
                else
                {
                    BtnOptiFabricClear.IsVisible = true;
                    ImgOptiFabric.IsVisible = true;
                    LabOptiFabric.Text = selectedOptiFabric.DisplayName.ToLower().Replace("optifabric-", "")
                        .Replace(".jar", "").Trim().TrimStart('v');
                    LabOptiFabric.Foreground = Brush("ColorBrushGray1");
                }
            }

            // 主警告
            HintFabricAPI.IsVisible = selectedFabric is not null && selectedFabricApi is null;
            HintLegacyFabricAPI.IsVisible = selectedLegacyFabric is not null && selectedLegacyFabricApi is null;

            if ((selectedFabric is not null || selectedLegacyFabric is not null) && selectedOptiFine is not null &&
                selectedOptiFabric is null)
            {
                if (VanillaDrop >= 140 && VanillaDrop <= 150)
                {
                    HintOptiFabric.IsVisible = false;
                    HintLegacyOptiFabric.IsVisible = false;
                    HintOptiFabricOld.IsVisible = true;
                }
                else if (selectedLegacyFabric is not null)
                {
                    HintOptiFabric.IsVisible = false;
                    HintLegacyOptiFabric.IsVisible = true;
                    HintOptiFabricOld.IsVisible = false;
                }
                else
                {
                    HintOptiFabric.IsVisible = true;
                    HintOptiFabricOld.IsVisible = false;
                    HintLegacyOptiFabric.IsVisible = false;
                }
            }
            else
            {
                HintOptiFabric.IsVisible = false;
                HintOptiFabricOld.IsVisible = false;
                HintLegacyOptiFabric.IsVisible = false;
            }

            HintModOptiFine.IsVisible = VanillaDrop >= 160 && selectedOptiFine is not null &&
                                        (selectedForge is not null || selectedFabric is not null);
            // 结束
        }
        finally
        {
            _ReloadSelected_Ongoing = false;
        }
    }

    /// <summary>
    ///     清空已选择的项目。
    /// </summary>
    private void ClearSelected()
    {
        _vanillaName = null;
        _vanillaData = null;
        _vanillaIcon = null;
        selectedOptiFine = null;
        selectedLiteLoader = null;
        _selectedLoaderType = null;
        selectedAPIName = null;
        selectedForge = null;
        selectedNeoForge = null;
        selectedNeoForgeVersion = null;
        selectedCleanroom = null;
        selectedCleanroomVersion = null;
        selectedFabric = null;
        selectedFabricApi = null;
        selectedOptiFabric = null;
        selectedLabyModCommitRef = null;
        selectedLabyModVersion = null;
        selectedLabyModChannel = null;
        selectedLegacyFabric = null;
        selectedLegacyFabricApi = null;
    }

    /// <summary>
    ///     选择主 Mod 加载器时，清除其它主加载器及其专属组件。
    ///     OptiFine 与 LiteLoader 可与部分加载器共存，故不在此处清除。
    /// </summary>
    private void SelectModLoader(ModLoaderType loaderType)
    {
        if (loaderType != ModLoaderType.Forge)
            selectedForge = null;

        if (loaderType != ModLoaderType.NeoForge)
        {
            selectedNeoForge = null;
            selectedNeoForgeVersion = null;
        }

        if (loaderType != ModLoaderType.Cleanroom)
        {
            selectedCleanroom = null;
            selectedCleanroomVersion = null;
        }

        if (loaderType != ModLoaderType.Fabric)
        {
            selectedFabric = null;
            selectedFabricApi = null;
            selectedOptiFabric = null;
            autoSelectedFabricApi = false;
            autoSelectedOptiFabric = false;
        }

        if (loaderType != ModLoaderType.LegacyFabric)
        {
            selectedLegacyFabric = null;
            selectedLegacyFabricApi = null;
            autoSelectedLegacyFabricApi = false;
        }

        if (loaderType != ModLoaderType.LabyMod)
        {
            selectedLabyModChannel = null;
            selectedLabyModCommitRef = null;
            selectedLabyModVersion = null;
        }

        _selectedLoaderType = loaderType;
        selectedAPIName = null;
    }

    // 信息栏动画
    private void SetPanelVisibility(Grid panel, bool visible)
    {
        if (Equals(panel.Tag, visible.ToString()))
            return;
        panel.Tag = visible.ToString();
        if (visible)
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaTranslateY(panel, -((TranslateTransform)panel.RenderTransform!).Y, 150,
                        ease: new ModAnimation.AniEaseOutFluent()),
                    ModAnimation.AaOpacity(panel, 1d - panel.Opacity, 60)
                }, "PageDownloadInstall Visibility " + panel.Name);
        else
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaTranslateY(panel, 6d - ((TranslateTransform)panel.RenderTransform!).Y, 60),
                    ModAnimation.AaOpacity(panel, -panel.Opacity, 60)
                }, "PageDownloadInstall Visibility " + panel.Name);
    }

    /// <summary>
    ///     获取实例图标。
    /// </summary>
    private string? GetSelectLogo()
    {
        if (selectedFabric is not null) return "avares://PCL.Shion/Assets/Images/Blocks/Fabric.png";

        if (selectedLegacyFabric is not null) return "avares://PCL.Shion/Assets/Images/Blocks/Fabric.png";

        if (selectedForge is not null) return "avares://PCL.Shion/Assets/Images/Blocks/Anvil.png";

        if (selectedNeoForge is not null) return "avares://PCL.Shion/Assets/Images/Blocks/NeoForge.png";

        if (selectedLiteLoader is not null) return "avares://PCL.Shion/Assets/Images/Blocks/Egg.png";

        if (selectedOptiFine is not null) return "avares://PCL.Shion/Assets/Images/Blocks/GrassPath.png";

        if (selectedCleanroom is not null) return "avares://PCL.Shion/Assets/Images/Blocks/Cleanroom.png";

        if (selectedLabyModVersion is not null) return "avares://PCL.Shion/Assets/Images/Blocks/LabyMod.png";

        return _vanillaIcon;
    }

    /// <summary>
    ///     获取实例描述信息。
    /// </summary>
    private string GetSelectInfo()
    {
        var parts = new List<string>
        {
            _vanillaName!
        };

        var loaderInfos = new (string NameKey, string? Version)[]
        {
            ("Common.Installation.Fabric", selectedFabric?.Replace("+build", "")),
            ("Common.Installation.LegacyFabric", selectedLegacyFabric),
            ("Common.Installation.Forge", selectedForge?.VersionName),
            ("Common.Installation.NeoForge",
                selectedNeoForge?.VersionName ?? VersionOrNull(selectedNeoForgeVersion)),
            ("Common.Installation.Cleanroom",
                selectedCleanroom?.VersionName ?? VersionOrNull(selectedCleanroomVersion)),
            ("Common.Installation.LabyMod", selectedLabyModVersion),
            ("Common.Installation.OptiFine",
                selectedOptiFine?.DisplayName.Replace(_vanillaName + " ", ""))
        };

        parts.AddRange(
            loaderInfos
                .Where(info => !string.IsNullOrWhiteSpace(info.Version))
                .Select(info => $"{Lang.Text(info.NameKey)} {info.Version}")
        );

        if (selectedLiteLoader is not null) parts.Add(Lang.Text("Common.Installation.LiteLoader"));

        if (parts.Count == 1) parts.Add(Lang.Text("Instance.Install.NoExtraInstall"));

        return string.Join("  |  ", parts);
    }

    private static string? VersionOrNull<T>(T version)
    {
        return EqualityComparer<T>.Default.Equals(version, default!)
            ? null
            : version?.ToString();
    }

    #endregion

    #region 当前信息获取

    private CompFile? _currentFabricApi; // 加载完成后直接调用以提高性能
    private string? _currentFabricApiPath;

    private object? GetCurrentFabricApi() // 进入页面和联网加载时调用
    {
        var loaderOutput = ModDownload.dlFabricApiLoader.output;
        if (loaderOutput is null)
            return null; // 确保联网信息已加载
        var localComp = ModLocalComp.GetModLocalCompByKeywords(PageInstanceLeft.McInstance,
            new[] { "fabric-api", "fabric" }, "fabric", "api");
        if (localComp is null)
            return null;
        var result = loaderOutput.FirstOrDefault(comp => (comp.Hash ?? "") == (localComp.ModrinthHash ?? ""));
        if (result is not null)
        {
            _currentFabricApi = result;
            _currentFabricApiPath = localComp.path;
        }

        return result;
    }

    private CompFile? _currentLegacyFabricApi; // 加载完成后直接调用以提高性能
    private string? _currentLegacyFabricApiPath;

    private object? GetCurrentLegacyFabricApi() // 进入页面和联网加载时调用
    {
        var loaderOutput = ModDownload.dlLegacyFabricApiLoader.output;
        if (loaderOutput is null)
            return null; // 确保联网信息已加载
        var localComp = ModLocalComp.GetModLocalCompByKeywords(PageInstanceLeft.McInstance,
            new[] { "legacy-fabric-api", "legacy-fabric" }, "legacy-fabric", "api");
        if (localComp is null)
            return null;
        var result = loaderOutput.FirstOrDefault(comp => (comp.Hash ?? "") == (localComp.ModrinthHash ?? ""));
        if (result is not null)
        {
            _currentLegacyFabricApi = result;
            _currentLegacyFabricApiPath = localComp.path;
        }

        return result;
    }

    private CompFile? _currentOptiFabric;
    private string? _currentOptiFabricPath;

    private object? GetCurrentOptiFabric()
    {
        var loaderOutput = ModDownload.dlOptiFabricLoader.output;
        if (loaderOutput is null)
            return null;
        var localComp =
            ModLocalComp.GetModLocalCompByKeywords(PageInstanceLeft.McInstance, "optifabric", "optifabric", "opti");
        if (localComp is null)
            return null;
        var result = loaderOutput.FirstOrDefault(comp => (comp.Hash ?? "") == (localComp.ModrinthHash ?? ""));
        if (result is not null)
        {
            _currentOptiFabric = result;
            _currentOptiFabricPath = localComp.path;
        }

        return result;
    }

    // 当前信息获取
    public void GetCurrentInfo()
    {
        ClearSelected();
        BtnSelectStart.IsEnabled = true;
        var currentInstance = PageInstanceLeft.McInstance.Info;
        _vanillaName = currentInstance.VanillaName;
        if (currentInstance.HasLiteLoader)
            selectedLiteLoader = new ModDownload.DlLiteLoaderListEntry { Inherit = currentInstance.VanillaName };
        if (currentInstance.HasOptiFine)
            selectedOptiFine = new ModDownload.DlOptiFineListEntry
            {
                DisplayName = currentInstance.VanillaName + " " + currentInstance.OptiFine.Replace("_", " "),
                IsPreview = currentInstance.OptiFine.Contains("pre"), Inherit = currentInstance.VanillaName,
                NameVersion = currentInstance.VanillaName + "-OptiFine_HD_U_" + currentInstance.OptiFine
            };
        if (currentInstance.HasCleanroom)
        {
            _selectedLoaderType = ModLoaderType.Cleanroom;
            selectedAPIName = "Cleanroom";
            selectedCleanroomVersion = currentInstance.Cleanroom;
            selectedCleanroom = new ModDownload.DlCleanroomListEntry(selectedCleanroomVersion);
        }
        else if (currentInstance.HasForge)
        {
            _selectedLoaderType = ModLoaderType.Forge;
            selectedForge =
                new ModDownload.DlForgeVersionEntry(currentInstance.Forge, null, currentInstance.VanillaName)
                {
                    Category = "installer", forgeType = ModDownload.DlForgelikeEntry.ForgelikeType.Forge,
                    Inherit = currentInstance.VanillaName
                };
        }
        else if (currentInstance.HasLegacyFabric)
        {
            _selectedLoaderType = ModLoaderType.LegacyFabric;
            selectedLegacyFabric = currentInstance.LegacyFabric;
            selectedLegacyFabricApi = (CompFile?)GetCurrentLegacyFabricApi();
        }
        else if (currentInstance.HasFabric)
        {
            _selectedLoaderType = ModLoaderType.Fabric;
            selectedFabric = currentInstance.Fabric;
            selectedFabricApi = (CompFile?)GetCurrentFabricApi();
        }
        else if (currentInstance.HasLabyMod)
        {
            _selectedLoaderType = ModLoaderType.LabyMod;
            selectedLabyModVersion = currentInstance.LabyMod;
        }
        else if (currentInstance.HasNeoForge)
        {
            _selectedLoaderType = ModLoaderType.NeoForge;
            selectedNeoForgeVersion = currentInstance.NeoForge;
            selectedNeoForge = new ModDownload.DlNeoForgeListEntry(currentInstance.NeoForge)
            {
                VersionName = currentInstance.NeoForge, Inherit = currentInstance.VanillaName,
                forgeType = ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge
            };
        }

        if (currentInstance.HasFabric && currentInstance.HasOptiFine)
            selectedOptiFabric = (CompFile?)GetCurrentOptiFabric();
        _vanillaIcon = "avares://PCL.Shion/Assets/Images/Blocks/Grass.png"; // TODO: 需要判断 Icon
        currentInfo = GetSelectInfo();
        EnterSelectPage();
    }

    private string? currentInfo;

    #endregion

    #region 加载器

    // 结果数据化
    private static string GetVersionTypeTitle(string key) => key switch
    {
        "正式版" => Lang.Text("Download.Version.Type.Release"),
        "预览版" => Lang.Text("Download.Version.Type.Development"),
        "远古版" => Lang.Text("Download.Version.Type.BeforeRelease"),
        "愚人节版" => Lang.Text("Download.Version.Type.AprilFools"),
        _ => key
    };

    private void LoadMinecraft_OnFinish()
    {
        ExitSelectPage(); // 返回
        do
        {
            try
            {
                var dict = new Dictionary<string, List<JsonObject>>
                {
                    { "正式版", new List<JsonObject>() }, { "预览版", new List<JsonObject>() }, { "远古版", new List<JsonObject>() },
                    { "愚人节版", new List<JsonObject>() }
                };
                var versions = (JsonArray)ModDownload.dlClientListLoader.output!.Value["versions"]!;
                foreach (JsonObject Version in versions.Cast<JsonObject>())
                {
                    // 确定分类
                    var type = Version["type"]!.ToString();
                    var versionId = Version["id"]!.ToString().ToLower();
                    switch (type ?? "")
                    {
                        case "release":
                        {
                            type = "正式版";
                            break;
                        }
                        case "snapshot":
                        case "pending":
                        {
                            type = "预览版";
                            // Mojang 误分类
                            if (versionId.StartsWith("1.") && !versionId.Contains("combat") &&
                                !versionId.Contains("rc") && !versionId.Contains("experimental") &&
                                !versionId.Equals("1.2") && !versionId.Contains("pre"))
                            {
                                type = "正式版";
                                Version["type"] = "release";
                            }

                            // 愚人节版本
                            switch (Version["id"]!.ToString().ToLower() ?? "")
                            {
                                case "2point0_blue":
                                case "2point0_red":
                                case "2point0_purple":
                                case "2.0_blue":
                                case "2.0_red":
                                case "2.0_purple":
                                case "2.0":
                                {
                                    type = "愚人节版";
                                    Version["id"] = Version["id"]!.ToString().Replace("point", ".");
                                    Version["type"] = "special";
                                    Version.Add("lore", McVersionClassifier.GetMcFoolName((string)Version["id"]!));
                                    break;
                                }
                                case "20w14infinite":
                                case "20w14∞":
                                {
                                    type = "愚人节版";
                                    Version["id"] = "20w14∞";
                                    Version["type"] = "special";
                                    Version.Add("lore", McVersionClassifier.GetMcFoolName((string)Version["id"]!));
                                    break;
                                }
                                case "3d shareware v1.34":
                                case "1.rv-pre1":
                                case "15w14a":
                                case var @case when @case == "2.0":
                                case "22w13oneblockatatime":
                                case "23w13a_or_b":
                                case "24w14potato":
                                case "25w14craftmine":
                                case "26w14a":
                                {
                                    type = "愚人节版";
                                    Version["type"] = "special";
                                    Version.Add("lore",
                                        McVersionClassifier.GetMcFoolName((string)Version["id"]!)); // 4/1 自动视作愚人节版
                                    break;
                                }

                                default:
                                {
                                    var releaseDate = McVersionClassifier.GetReleaseTime(Version).ToUniversalTime().AddHours(2d);
                                    if (releaseDate.Month == 4 && releaseDate.Day == 1)
                                    {
                                        type = "愚人节版";
                                        Version["type"] = "special";
                                    }

                                    break;
                                }
                            }

                            break;
                        }
                        case "special":
                        {
                            // 已被处理的愚人节版
                            type = "愚人节版";
                            break;
                        }

                        default:
                        {
                            type = "远古版";
                            break;
                        }
                    }

                    // 加入辞典
                    dict[type!].Add(Version);
                }

                // 排序
                foreach (var Pair in dict.ToList())
                    dict[Pair.Key] = Pair.Value.OrderByDescending(McVersionClassifier.GetReleaseTime).ToList();
                // 清空当前
                PanMinecraft.Children.Clear();
                // 添加最新版本
                var cardInfo = new MyCard { Title = Lang.Text("Download.Version.Latest.Title"), Margin = new Thickness(0d, 15d, 0d, 15d) };
                var topestVersions = new List<JsonObject>();
                var release = (JsonObject)dict["正式版"][0].DeepClone();
                release["lore"] = Lang.Text("Download.Version.Latest.Release", Lang.Date(McVersionClassifier.GetReleaseTime(release), "g"));
                topestVersions.Add(release);
                if (McVersionClassifier.GetReleaseTime(dict["正式版"][0]) < McVersionClassifier.GetReleaseTime(dict["预览版"][0]))
                {
                    var snapshot = (JsonObject)dict["预览版"][0].DeepClone();
                    snapshot["lore"] = Lang.Text("Download.Version.Latest.Development", Lang.Date(McVersionClassifier.GetReleaseTime(snapshot), "g"));
                    topestVersions.Add(snapshot);
                }

                var panInfo = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = topestVersions
                };

                void StackInstall(StackPanel stack)
                {
                    foreach (var item in (IEnumerable)stack.Tag!)
                        stack.Children.Add(ModDownloadLib.McDownloadListItem((JsonObject)item,
                            (sender, e) => MinecraftSelected((MyListItem)sender!, e), false));
                }

                MyCard.StackInstall(ref panInfo, StackInstall);
                cardInfo.Children.Add(panInfo);
                PanMinecraft.Children.Insert(0, cardInfo);
                // 添加其他版本
                foreach (var Pair in dict)
                {
                    if (!Pair.Value.Any())
                        continue;
                    // 增加卡片
                    var newCard = new MyCard
                        { Title = GetVersionTypeTitle(Pair.Key) + " (" + Pair.Value.Count + ")", Margin = new Thickness(0d, 0d, 0d, 15d) };
                    var newStack = new StackPanel
                    {
                        Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                        Tag = Pair.Value
                    };
                    newCard.Children.Add(newStack);
                    newCard.SwapControl = newStack;
                    newCard.InstallMethod = StackInstall;
                    newCard.IsSwapped = true;
                    PanMinecraft.Children.Add(newCard);
                }

                // 自动选择版本
                if (mcVersionWaitingForSelect is null)
                    break;
                Log("[Download] 自动选择 MC 版本：" + mcVersionWaitingForSelect);
                foreach (JsonObject Version in versions.Cast<JsonObject>())
                {
                    if ((Version["id"]!.ToString() ?? "") != (mcVersionWaitingForSelect ?? ""))
                        continue;
                    var item = ModDownloadLib.McDownloadListItem(Version, (_, _) => { }, false);
                    MinecraftSelected(item, null);
                }
            }
            catch (Exception ex)
            {
                LogFeedback(ex, "可视化安装版本列表出错");
            }
        } while (false);
    }

    /// <summary>
    ///     当 MC 版本列表加载完时，立即自动选择的版本。用于外部调用。
    /// </summary>
    public static string? mcVersionWaitingForSelect = null;

    #endregion

    private void Log(string message) => ModBase.Log(message);

    private void LogFeedback(Exception ex, string message) => ModBase.Log(
        ex,
        message,
        ModBase.LogLevel.Feedback,
        userSummary: Lang.Text("Instance.Install.Error.OperationFailed"));
}
