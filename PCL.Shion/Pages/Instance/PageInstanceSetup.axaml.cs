using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using FluentValidation;
using PCL.Shion.Controls;
using PCL.Shion.Core.Java;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Setup;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Instance;

public partial class PageInstanceSetup : MyPageRight
{
    /// <summary>
    ///     Java 下拉框中“跟随全局设置”项的标记值。
    /// </summary>
    private const string FollowGlobalJava = "\0global";

    public PageInstanceSetup()
    {
        Loaded += PageSetupSystem_Loaded;
        InitializeComponent();
        PanScroll = PanBack;

        ComboArgumentIndieV2.SelectionChanged += ComboArgumentIndieV2_SelectionChanged;
        TextArgumentTitle.TextChanged += TextArgumentTitle_TextChanged;
        TextArgumentInfo.TextChanged += TextBoxChange;
        ComboArgumentJava.SelectionChanged += JavaSelectionUpdate;

        RadioRamType2.Check += RadioBoxChange;
        RadioRamType0.Check += RadioBoxChange;
        RadioRamType1.Check += RadioBoxChange;
        SliderRamCustom.Change += SliderChange;
        SliderRamInitialCustom.Change += SliderChange;

        ComboServerLoginRequire.SelectionChanged += ComboServerLogin_Changed;
        TextServerAuthServer.TextChanged += TextBoxChange;
        TextServerAuthServer.LostFocus += TextServerAuthServer_MouseLeave;
        TextServerAuthRegister.TextChanged += TextBoxChange;
        TextServerAuthName.TextChanged += TextBoxChange;
        TextServerEnter.TextChanged += TextBoxChange;
        BtnServerAuthLittle.Click += BtnServerAuthLittle_Click;
        BtnServerAuthLock.Click += BtnServerAuthLock_Click;
        BtnServerNewProfile.Click += BtnServerNewProfile_Click;

        ComboAdvanceRenderer.SelectionChanged += ComboAdvanceRenderer_SelectionChanged;
        TextAdvanceJvm.TextChanged += TextBoxChange;
        TextAdvanceGame.TextChanged += TextBoxChange;
        TextAdvanceClasspathHead.TextChanged += TextBoxChange;
        TextAdvancePostExit.TextChanged += TextBoxChange;
        TextAdvanceWrapper.TextChanged += TextBoxChange;
        TextAdvanceEnv.TextChanged += TextBoxChange;
        TextAdvanceRun.TextChanged += TextAdvanceRun_TextChanged;
        CheckAdvanceRunWait.Change += CheckBoxChange;
        CheckAdvanceJava.Change += CheckBoxChange;
        CheckAdvanceAssetsV2.Change += CheckBoxChange;
        CheckAdvanceUseProxyV2.Change += CheckBoxChange;
        CheckAdvanceDisableJLW.Change += CheckBoxChange;
        CheckAdvanceDisableLF.Change += CheckBoxChange;
        CheckUseDebugLog4j2Config.Change += CheckUseDebugLog4j2Config_CheckChanged;
        CheckAdvanceDisableLwjglUnsafeAgent.Change += CheckBoxChange;

        BtnSwitch.Click += BtnSwitch_Click;

        TextServerEnter.TextChanged += TextServerEnter_Change;
        ComboArgumentJava.DropDownOpened += ComboArgumentJava_DropDownOpened;
        CheckArgumentTitleEmpty.Change += CheckArgumentTitleEmpty_Change;
    }

    private static AppSettings Settings => App.Services.Settings;

    private static string PathInstance => PageInstanceLeft.McInstance!.PathInstance;

    private void PageSetupSystem_Loaded(object? sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        RefreshRam(false);

        // 由于各个实例不同，每次都需要重新加载
        ModAnimation.AniControlEnabled += 1;
        Reload();
        ModAnimation.AniControlEnabled -= 1;

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        // 内存自动刷新
        var timer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 1) };
        timer.Tick += (_, _) => RefreshRam();
        timer.Start();
        RectRamGame.PropertyChanged += (_, args) =>
        {
            if (args.Property == BoundsProperty)
                RefreshRamText();
        };
    }

    private bool isLoaded;

    // 内存条的三段列定义，XAML 中 ColumnDefinition 上的 x:Name 不会生成字段，因此从 PanRamDisplay 按顺序获取
    private ColumnDefinition ColumnRamUsed => PanRamDisplay.ColumnDefinitions[0];
    private ColumnDefinition ColumnRamGame => PanRamDisplay.ColumnDefinitions[1];
    private ColumnDefinition ColumnRamEmpty => PanRamDisplay.ColumnDefinitions[2];

    public void Reload()
    {
        try
        {
            var path = PathInstance;
            var config = Settings.Config.Instance;
            var auth = Settings.Config.InstanceAuth;

            // 启动参数
            TextArgumentTitle.Text = config.Title[path];
            CheckArgumentTitleEmpty.Checked = config.UseGlobalTitle[path];
            TextArgumentInfo.Text = config.TypeInfo[path];
            var _unused = PageInstanceLeft.McInstance!.PathIndie; // 触发自动判定
            ComboArgumentIndieV2.SelectedIndex = config.IndieV2[path] ? 0 : 1;
            CheckArgumentTitleEmpty.IsVisible = TextArgumentTitle.Text?.Length > 0 ? false : true;
            TextArgumentTitle.HintText = CheckArgumentTitleEmpty.Checked == true ? Lang.Text("Common.Option.Default") : Lang.Text("Instance.Setup.FollowGlobal");
            RefreshJavaComboBox();

            // 游戏内存
            var ramType = config.MemorySolution[path];
            ((MyRadioBox)this.FindControl<Control>("RadioRamType" + ramType)!).Checked = true;
            SliderRamCustom.Value = config.CustomMemorySize[path];
            SliderRamInitialCustom.Value = config.CustomInitialMemorySize[path];
            RamType(ramType);

            // 服务器
            TextServerEnter.Text = config.ServerToEnter[path];
            ComboServerLoginRequire.SelectedIndex = auth.LoginRequirementSolution[path];
            comboServerLoginLast = ComboServerLoginRequire.SelectedIndex;
            TextServerAuthServer.Text = auth.AuthServerAddress[path];
            TextServerAuthName.Text = auth.AuthServerDisplayName[path];
            TextServerAuthRegister.Text = auth.AuthRegisterAddress[path];
            ServerLogin(ComboServerLoginRequire.SelectedIndex);

            // 高级设置
            ComboAdvanceRenderer.SelectedIndex = config.Renderer[path];
            TextAdvanceClasspathHead.Text = config.ClasspathHead[path];
            TextAdvanceJvm.Text = config.JvmArgs[path];
            TextAdvanceGame.Text = config.GameArgs[path];
            TextAdvanceRun.Text = config.PreLaunchCommand[path];
            TextAdvancePostExit.Text = config.PostExitCommand[path];
            TextAdvanceWrapper.Text = config.WrapperCommand[path];
            TextAdvanceEnv.Text = config.EnvironmentVariables[path];
            CheckAdvanceRunWait.Checked = config.PreLaunchCommandWait[path];
            CheckAdvanceDisableLwjglUnsafeAgent.Checked = config.DisableLwjglUnsafeAgent[path];
            if (config.AssetVerifySolutionV1[path] == 2)
            {
                config.AssetVerifySolutionV1Config.Reset(path);
                config.DisableAssetVerifyV2[path] = true;
            }

            CheckAdvanceAssetsV2.Checked = config.DisableAssetVerifyV2[path];
            CheckAdvanceUseProxyV2.Checked = config.UseProxy[path];
            CheckAdvanceJava.Checked = config.IgnoreJavaCompatibility[path];
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                CheckAdvanceDisableJLW.Checked = true;
                CheckAdvanceDisableJLW.IsEnabled = false;
                ToolTip.SetTip(CheckAdvanceDisableJLW, Lang.Text("Setup.Launch.Advanced.DisableJlw.Arm64ToolTip"));
            }
            else
            {
                CheckAdvanceDisableJLW.Checked = config.DisableJlw[path];
            }

            CheckUseDebugLog4j2Config.Checked = config.UseDebugLof4j2Config[path];
            CheckAdvanceDisableLF.Checked = config.DisableLF[path];
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Instance.Setup.Error.OperationFailed"), HintType.Error);
        }
    }

    // 初始化
    public void Reset()
    {
        try
        {
            var path = PathInstance;
            if (!Settings.Config.InstanceAuth.AuthLocked[path])
                Settings.Config.InstanceAuth.Reset(path);

            Settings.Config.Instance.Reset(path);
            Settings.Save();
            HintService.Hint(Lang.Text("Instance.Setup.Initialize.Success"), HintType.Success, false);
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Instance.Setup.Error.OperationFailed"), HintType.Error);
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private static void SetByTag(string tag, object value)
    {
        Settings.Config.TrySetValue(tag, value, PathInstance);
        Settings.Save();
    }

    private void RadioBoxChange(object o, RouteEventArgs routeEventArgs)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (o is not MyRadioBox { Tag: string tag }) return;

        var slash = tag.IndexOf('/');
        if (slash < 0) return;

        SetByTag(tag[..slash], int.Parse(tag[(slash + 1)..]));
    }

    private void TextBoxChange(object? o, TextChangedEventArgs textChangedEventArgs)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (o is not MyTextBox textBox) return;

        SetByTag(textBox.Tag?.ToString() ?? "", textBox.Text ?? "");
    }

    private void SliderChange(object o, bool user)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (o is not MySlider slider) return;

        SetByTag(slider.Tag?.ToString() ?? "", slider.Value);
    }

    private void ComboChange(MyComboBox sender, object? e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;

        SetByTag(sender.Tag?.ToString() ?? "", sender.SelectedIndex);
    }

    private void CheckBoxChange(object sender, bool user)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (sender is not MyCheckBox checkBox) return;

        SetByTag(checkBox.Tag?.ToString() ?? "", checkBox.Checked.GetValueOrDefault());
    }

    // 切换到全局设置
    private void BtnSwitch_Click(object? sender, PointerReleasedEventArgs e)
    {
        Navigator.Go(PageType.Setup);
    }

    #region 游戏内存

    public void RamType(int type)
    {
        if (SliderRamCustom is null)
            return;
        SliderRamCustom.IsEnabled = type == 1;
        // 锁定内存为全局配置，启用时 -Xms 恒等于 -Xmx，初始内存设置不生效
        SliderRamInitialCustom.IsEnabled = type == 1 && !Settings.Config.Launch.LockMemory;
    }

    /// <summary>
    ///     刷新 UI 上的 RAM 显示。
    /// </summary>
    public void RefreshRam(bool showAnim)
    {
        if (LabRamGame is null || LabRamUsed is null ||
            Navigator.Current != PageType.InstanceSetup ||
            PageInstanceLeft.frmInstanceLeft?.pageID != PageSubType.VersionSetup)
            return;
        // 获取内存情况
        var ramGame = Math.Round(GetRam(PageInstanceLeft.McInstance), 5);
        var (phyTotal, phyAvailable) = PhysicalMemory();
        var ramTotal = Math.Round(phyTotal, 1);
        var ramAvailable = Math.Round(phyAvailable, 1);
        var ramGameActual = Math.Round(Math.Min(ramGame, ramAvailable), 5);
        var ramUsed = Math.Round(ramTotal - ramAvailable, 5);
        var ramEmpty = Math.Round(Math.Clamp(ramTotal - ramUsed - ramGame, 0d, 1000d), 1);
        // 设置最大可用内存
        if (ramTotal <= 1.5d)
            SliderRamCustom.MaxValue = (int)Math.Round(Math.Max(Math.Floor((ramTotal - 0.3d) / 0.1d), 1d));
        else if (ramTotal <= 8d)
            SliderRamCustom.MaxValue = (int)Math.Round(Math.Floor((ramTotal - 1.5d) / 0.5d) + 12d);
        else if (ramTotal <= 16d)
            SliderRamCustom.MaxValue = (int)Math.Round(Math.Floor((ramTotal - 8d) / 1d) + 25d);
        else
            SliderRamCustom.MaxValue = (int)Math.Round(Math.Floor((ramTotal - 16d) / 2d) + 33d);
        // 设置文本
        var ramInitial = GetInitialRam(PageInstanceLeft.McInstance);
        string suffixText;
        if (ramInitial.HasValue && ramGame != ramGameActual)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixBoth", Lang.Number(ramInitial.Value, "N1"), Lang.Number(ramGameActual, "N1"));
        else if (ramInitial.HasValue)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixInitial", Lang.Number(ramInitial.Value, "N1"));
        else if (ramGame != ramGameActual)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixAvailable", Lang.Number(ramGameActual, "N1"));
        else
            suffixText = "";
        LabRamGame.Text = $"{Lang.Number(ramGame, "N1")} GiB{(suffixText.Length > 0 ? $" ({suffixText})" : "")}";
        LabRamUsed.Text = $"{Lang.Number(ramUsed, "N1")} GiB";
        LabRamTotal.Text = $" / {Lang.Number(ramTotal, "N1")} GiB";
        LabRamWarn.IsVisible =
            ramGame == 1d && !IsGameSet64BitJava(PageInstanceLeft.McInstance) && Environment.Is64BitOperatingSystem &&
            JavaLocator.FindAll().Count > 0;
        HintRamTooHigh.IsVisible = ramGame / ramTotal > 0.75d;
        if (showAnim)
        {
            // 宽度动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaGridLengthWidth(ColumnRamUsed, ramUsed - ColumnRamUsed.Width.Value, 800,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                    ModAnimation.AaGridLengthWidth(ColumnRamGame, ramGameActual - ColumnRamGame.Width.Value, 800,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                    ModAnimation.AaGridLengthWidth(ColumnRamEmpty, ramEmpty - ColumnRamEmpty.Width.Value, 800,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong))
                }, "VersionSetup Ram Grid");
        }
        else
        {
            // 宽度设置
            ColumnRamUsed.Width = new GridLength(ramUsed, GridUnitType.Star);
            ColumnRamGame.Width = new GridLength(ramGameActual, GridUnitType.Star);
            ColumnRamEmpty.Width = new GridLength(ramEmpty, GridUnitType.Star);
        }
    }

    /// <summary>
    ///     读取物理内存（总量与可用量），单位为 GB。
    /// </summary>
    private static (double Total, double Available) PhysicalMemory()
    {
        var info = GC.GetGCMemoryInfo();
        const double gib = 1024d * 1024d * 1024d;
        var total = info.TotalAvailableMemoryBytes / gib;
        var available = Math.Max(0d, (info.TotalAvailableMemoryBytes - info.MemoryLoadBytes) / gib);
        return (total, available);
    }

    private static bool IsGameSet64BitJava(GameInstance? version) => Environment.Is64BitProcess;

    private void RefreshRam()
    {
        RefreshRam(true);
    }

    private int ramTextLeft = 2;
    private int ramTextRight = 1;

    /// <summary>
    ///     刷新 UI 上的文本位置。
    /// </summary>
    private void RefreshRamText()
    {
        // 获取宽度信息
        var rectUsedWidth = RectRamUsed.Bounds.Width;
        var totalWidth = PanRamDisplay.Bounds.Width;
        var labGameWidth = LabRamGame.Bounds.Width;
        var labUsedWidth = LabRamUsed.Bounds.Width;
        var labTotalWidth = LabRamTotal.Bounds.Width;
        var labGameTitleWidth = LabRamGameTitle.Bounds.Width;
        var labUsedTitleWidth = LabRamUsedTitle.Bounds.Width;
        // 左侧
        int left;
        if (rectUsedWidth - 30d < labUsedWidth || rectUsedWidth - 30d < labUsedTitleWidth)
            // 全写不下了
            left = 0;
        else if (rectUsedWidth - 25d < labUsedWidth + labTotalWidth)
            // 显示不下完整数据
            left = 1;
        else
            // 正常
            left = 2;
        if (ramTextLeft != left)
        {
            ramTextLeft = left;
            switch (left)
            {
                case 0:
                {
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(LabRamUsed, -LabRamUsed.Opacity, 100),
                            ModAnimation.AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            ModAnimation.AaOpacity(LabRamUsedTitle, -LabRamUsedTitle.Opacity, 100)
                        }, "VersionSetup Ram TextLeft");
                    break;
                }
                case 1:
                {
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(LabRamUsed, 1d - LabRamUsed.Opacity, 100),
                            ModAnimation.AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            ModAnimation.AaOpacity(LabRamUsedTitle, 0.7d - LabRamUsedTitle.Opacity, 100)
                        }, "VersionSetup Ram TextLeft");
                    break;
                }
                case 2:
                {
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(LabRamUsed, 1d - LabRamUsed.Opacity, 100),
                            ModAnimation.AaOpacity(LabRamTotal, 1d - LabRamTotal.Opacity, 100),
                            ModAnimation.AaOpacity(LabRamUsedTitle, 0.7d - LabRamUsedTitle.Opacity, 100)
                        }, "VersionSetup Ram TextLeft");
                    break;
                }
            }
        }

        // 右侧
        int right;
        if (totalWidth < labGameWidth + 2d + rectUsedWidth || totalWidth < labGameTitleWidth + 2d + rectUsedWidth)
            // 挤到最右边
            right = 0;
        else
            // 正常情况
            right = 1;
        if (right == 0)
        {
            if (ModAnimation.AniControlEnabled == 0 &&
                (ramTextRight != right || ModAnimation.AniIsRun("VersionSetup Ram TextRight")))
            {
                // 需要动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaX(LabRamGame, totalWidth - labGameWidth - LabRamGame.Margin.Left, 100,
                            ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaX(LabRamGameTitle, totalWidth - labGameTitleWidth - LabRamGameTitle.Margin.Left,
                            100, ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                    }, "VersionSetup Ram TextRight");
            }
            else
            {
                // 不需要动画
                LabRamGame.Margin = new Thickness(totalWidth - labGameWidth, 3d, 0d, 0d);
                LabRamGameTitle.Margin = new Thickness(totalWidth - labGameTitleWidth, 0d, 0d, 5d);
            }
        }
        else if (ModAnimation.AniControlEnabled == 0 &&
                 (ramTextRight != right || ModAnimation.AniIsRun("VersionSetup Ram TextRight")))
        {
            // 需要动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaX(LabRamGame, 2d + rectUsedWidth - LabRamGame.Margin.Left, 100,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaX(LabRamGameTitle, 2d + rectUsedWidth - LabRamGameTitle.Margin.Left, 100,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                }, "VersionSetup Ram TextRight");
        }
        else
        {
            // 不需要动画
            LabRamGame.Margin = new Thickness(2d + rectUsedWidth, 3d, 0d, 0d);
            LabRamGameTitle.Margin = new Thickness(2d + rectUsedWidth, 0d, 0d, 5d);
        }

        ramTextRight = right;
    }

    /// <summary>
    ///     获取当前设置的 RAM 值。单位为 GB。
    /// </summary>
    public static double GetRam(GameInstance? version, bool? is32BitJava = default)
    {
        var instancePath = version?.PathInstance ?? "";
        // 跟随全局设置
        if (Settings.Config.Instance.MemorySolution[instancePath] == 2)
            return PageSetupLaunch.GetRam(version, true, is32BitJava);

        // ------------------------------------------
        // 修改下方代码时需要一并修改 PageSetupLaunch
        // ------------------------------------------

        // 使用当前实例的设置
        var ramGive = default(double);
        if (Settings.Config.Instance.MemorySolution[instancePath] == 0)
        {
            // 自动配置
            var ramAvailable = Math.Round(PhysicalMemory().Available * 10) / 10;
            // 确定需求的内存值
            double ramMininum; // 无论如何也需要保证的最低限度内存
            double ramTarget1; // 估计能勉强带动了的内存
            double ramTarget2; // 估计没啥问题了的内存
            double ramTarget3; // 安装过多附加组件需要的内存
            if (version is not null && version.Modable)
            {
                // 可安装 Mod 的实例
                var modDir = new DirectoryInfo(version.PathIndie + "mods");
                var modCount = modDir.Exists ? modDir.GetFiles().Length : 0;
                ramMininum = 0.5d + modCount / 150d;
                ramTarget1 = 1.5d + modCount / 90d;
                ramTarget2 = 2.7d + modCount / 50d;
                ramTarget3 = 4.5d + modCount / 25d;
            }
            else if (version is not null && version.Loader?.ToLowerInvariant() == "optifine")
            {
                // OptiFine 实例
                ramMininum = 0.5d;
                ramTarget1 = 1.5d;
                ramTarget2 = 3d;
                ramTarget3 = 5d;
            }
            else
            {
                // 普通实例
                ramMininum = 0.5d;
                ramTarget1 = 1.5d;
                ramTarget2 = 2.5d;
                ramTarget3 = 4d;
            }

            double ramDelta;
            // 预分配内存，阶段一，0 ~ T1，100%
            ramDelta = ramTarget1;
            ramGive += Math.Min(ramAvailable, ramDelta);
            ramAvailable -= ramDelta;
            if (ramAvailable >= 0.1d)
            {
                // 预分配内存，阶段二，T1 ~ T2，70%
                ramDelta = ramTarget2 - ramTarget1;
                ramGive += Math.Min(ramAvailable * 0.7d, ramDelta);
                ramAvailable -= ramDelta / 0.7d;
                if (ramAvailable >= 0.1d)
                {
                    // 预分配内存，阶段三，T2 ~ T3，40%
                    ramDelta = ramTarget3 - ramTarget2;
                    ramGive += Math.Min(ramAvailable * 0.4d, ramDelta);
                    ramAvailable -= ramDelta / 0.4d;
                    if (ramAvailable >= 0.1d)
                    {
                        // 预分配内存，阶段四，T3 ~ T3 * 2，15%
                        ramDelta = ramTarget3;
                        ramGive += Math.Min(ramAvailable * 0.15d, ramDelta);
                        ramAvailable -= ramDelta / 0.15d;
                    }
                }
            }

            // 不低于最低值
            ramGive = Math.Round(Math.Max(ramGive, ramMininum), 1);
        }
        else
        {
            // 手动配置
            var value = Settings.Config.Instance.CustomMemorySize[instancePath];
            ramGive = PageSetupLaunch.GetRamFromTick(value);
        }

        // 若使用 32 位 Java，则限制为 1G
        if (is32BitJava ?? !IsGameSet64BitJava(PageInstanceLeft.McInstance))
            ramGive = Math.Min(1d, ramGive);
        return ramGive;
    }

    /// <summary>
    ///     获取当前设置的初始堆大小（-Xms）。单位为 GB；未启用自定义初始大小时返回 null。
    /// </summary>
    public static double? GetInitialRam(GameInstance? version, bool? is32BitJava = default)
    {
        // 锁定内存为全局配置，启用时 -Xms 恒等于 -Xmx，由锁定内存逻辑处理
        if (Settings.Config.Launch.LockMemory)
            return null;
        // 自定义 JVM 参数含 -Xms 时由其决定实际初始堆，不再显示滑块值
        if (PageSetupLaunch.HasCustomXms(version))
            return null;
        var instancePath = version?.PathInstance ?? "";
        switch (Settings.Config.Instance.MemorySolution[instancePath])
        {
            case 2:
                return PageSetupLaunch.GetInitialRam(version, true, is32BitJava);
            case 1:
                var initialTick = Settings.Config.Instance.CustomInitialMemorySize[instancePath];
                if (initialTick <= 0)
                    return null;
                return Math.Min(PageSetupLaunch.GetRamFromTick(initialTick), GetRam(version, is32BitJava));
            default:
                return null;
        }
    }

    #endregion

    #region 服务器

    // 全局
    private int comboServerLoginLast;

    private void ComboServerLogin_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        ServerLogin(ComboServerLoginRequire.SelectedIndex);
        BtnServerAuthLock.IsEnabled = TextServerAuthServer.IsValidated;
        if ((ComboServerLoginRequire.SelectedIndex == 2 || ComboServerLoginRequire.SelectedIndex == 3) &&
            !TextServerAuthServer.IsValidated)
            return;
        if (comboServerLoginLast == ComboServerLoginRequire.SelectedIndex)
            return;
        comboServerLoginLast = ComboServerLoginRequire.SelectedIndex;
        Settings.Config.InstanceAuth.LoginRequirementSolution[PathInstance] = ComboServerLoginRequire.SelectedIndex;
        Settings.Save();
    }

    private void TextServerAuthServer_MouseLeave(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TextServerAuthServer.Text))
            return;
        if (!(TextServerAuthServer.Text.EndsWith("/api/yggdrasil/", StringComparison.Ordinal) ||
              TextServerAuthServer.Text.EndsWith("/api/yggdrasil", StringComparison.Ordinal)))
        {
            if (TextServerAuthServer.Text.EndsWith("/", StringComparison.Ordinal))
                TextServerAuthServer.Text = $"{TextServerAuthServer.Text}api/yggdrasil";
            else
                TextServerAuthServer.Text = $"{TextServerAuthServer.Text}/api/yggdrasil";
            HintService.Hint(Lang.Text("Instance.Setup.Server.AuthServer.AutoFormatted"));
        }

        if (TextServerAuthServer.Text.EndsWith("/api/yggdrasil/", StringComparison.Ordinal))
        {
            TextServerAuthServer.Text = TextServerAuthServer.Text[..^1];
            HintService.Hint(Lang.Text("Instance.Setup.Server.AuthServer.AutoFormatted"));
        }

        comboServerLoginLast = ComboServerLoginRequire.SelectedIndex;
        ComboChange(ComboServerLoginRequire, null);
    }

    public void ServerLogin(int type)
    {
        var showAuth = type == 2 || type == 3;
        LabServerAuthName.IsVisible = showAuth;
        TextServerAuthName.IsVisible = showAuth;
        LabServerAuthRegister.IsVisible = showAuth;
        TextServerAuthRegister.IsVisible = showAuth;
        LabServerAuthServer.IsVisible = showAuth;
        TextServerAuthServer.IsVisible = showAuth;
        BtnServerAuthLittle.IsVisible = showAuth;
        BtnServerNewProfile.IsVisible = showAuth && App.Services.CanCreateOtherProfile();
        BtnServerAuthLock.IsVisible = !(type == 0 || type == 1);

        var locked = Settings.Config.InstanceAuth.AuthLocked[PathInstance];
        HintServerLoginLock.IsVisible = locked;
        ComboServerLoginRequire.IsEnabled = !locked;
        TextServerAuthServer.IsEnabled = !locked;
        TextServerAuthName.IsEnabled = !locked;
        TextServerAuthRegister.IsEnabled = !locked;
        BtnServerAuthLittle.IsEnabled = !locked;

        CardServer.TriggerForceResize();
        // 避免正版验证和离线验证出现此提示
        if (!showAuth)
        {
            LabServerAuthServerSecurity.IsVisible = false;
            LabServerAuthServerSecurityVerify.IsVisible = false;
        }
        // 如果开头为 https:// 或 http:// 给予提示
        else if (TextServerAuthServer.Text?.StartsWith("https://", StringComparison.Ordinal) == true)
        {
            LabServerAuthServerSecurity.IsVisible = false;
            LabServerAuthServerSecurityVerify.IsVisible = true;
        }
        else if (TextServerAuthServer.Text?.StartsWith("http://", StringComparison.Ordinal) == true)
        {
            LabServerAuthServerSecurity.IsVisible = true;
            LabServerAuthServerSecurityVerify.IsVisible = false;
        }
        else
        {
            LabServerAuthServerSecurity.IsVisible = false;
            LabServerAuthServerSecurityVerify.IsVisible = false;
        }
    }

    // LittleSkin
    private async void BtnServerAuthLittle_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TextServerAuthServer.Text) &&
            TextServerAuthServer.Text != "https://littleskin.cn/api/yggdrasil" &&
            await MsgBox.ShowAsync(
                Lang.Text("Instance.Setup.Server.LittleSkin.Override.Message"),
                Lang.Text("Instance.Setup.Server.LittleSkin.Override.Title"), Lang.Text("Instance.Setup.Server.LittleSkin.Override.Continue"), Lang.Text("Common.Action.Cancel")) == 2)
            return;
        TextServerAuthServer.Text = "https://littleskin.cn/api/yggdrasil";
        TextServerAuthRegister.Text = "https://littleskin.cn/auth/register";
        TextServerAuthName.Text = Lang.Text("Instance.Setup.Server.LittleSkin.Name");
    }

    // 锁定设置
    private async void BtnServerAuthLock_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (await MsgBox.ShowAsync(
                Lang.Text("Instance.Setup.Server.LockLoginMethod.Message"),
                Lang.Text("Instance.Setup.Server.LockLoginMethod.Title"), Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true) == 1)
        {
            Settings.Config.InstanceAuth.AuthLocked[PathInstance] = true;
            Settings.Save();
            Reload();
        }
    }

    // 跳转新建档案
    private void BtnServerNewProfile_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (!App.Services.CanCreateOtherProfile())
            return;

        Navigator.Go(PageType.Launch);
        App.Services.RequestNewAuthProfile(TextServerAuthServer.Text ?? "");
    }

    private static void TextServerEnter_Change(object? sender, TextChangedEventArgs e)
    {
        if (sender is MyTextBox textBox) textBox.Text = textBox.Text?.Replace("：", ":");
    }

    #endregion

    #region Java 选择

    // 刷新 Java 下拉框显示
    public void RefreshJavaComboBox()
    {
        if (ComboArgumentJava is null)
            return;

        var path = PathInstance;
        var selected = Settings.Config.Instance.SelectedJava[path];

        // === 1. 初始化固定选项 ===
        ComboArgumentJava.Items.Clear();

        // 选项 0: 跟随全局设置
        ComboArgumentJava.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Instance.Setup.FollowGlobal"),
            Tag = FollowGlobalJava
        });

        // 选项 1: 自动选择
        ComboArgumentJava.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Instance.Setup.Options.Java.AutoSelect"),
            Tag = null
        });

        // === 2. 添加所有可用 Java 运行时 ===
        MyComboBoxItem? selectedItem = null;
        var runtimes = JavaLocator.FindAll();
        foreach (var java in runtimes)
        {
            var item = new MyComboBoxItem
            {
                Content = java.ToString(),
                Tag = java.Executable
            };
            ToolTip.SetTip(item, Lang.Text("Instance.Setup.Options.Java.Details.ToolTip", java.Executable, java.Version, java.Executable));
            ComboArgumentJava.Items.Add(item);
            if (string.Equals(java.Executable, selected, StringComparison.OrdinalIgnoreCase))
                selectedItem = item;
        }

        // === 3. 根据当前偏好设置选中项 ===
        if (string.IsNullOrEmpty(selected))
            selectedItem = ComboArgumentJava.Items[1] as MyComboBoxItem;
        else if (selected == FollowGlobalJava)
            selectedItem = ComboArgumentJava.Items[0] as MyComboBoxItem;

        // 降级处理：无匹配项时回退到自动选择
        selectedItem ??= ComboArgumentJava.Items[1] as MyComboBoxItem;

        // 设置选中项
        if (selectedItem is not null) ComboArgumentJava.SelectedItem = selectedItem;

        // === 4. 无可用 Java 时的降级处理 ===
        if (runtimes.Count == 0)
        {
            ComboArgumentJava.Items.Clear();
            var noJavaItem = new MyComboBoxItem
            {
                Content = Lang.Text("Instance.Setup.Options.Java.NoRuntime"),
                IsEnabled = false
            };
            ToolTip.SetTip(noJavaItem, Lang.Text("Instance.Setup.Options.Java.NoRuntime.ToolTip"));
            ComboArgumentJava.Items.Add(noJavaItem);
            ComboArgumentJava.SelectedItem = noJavaItem;
        }

        // === 5. 刷新关联控件 ===
        RefreshRam(true);
    }

    // 阻止在无效状态下展开下拉框
    private void ComboArgumentJava_DropDownOpened(object? sender, EventArgs e)
    {
        if (ComboArgumentJava.SelectedItem is null)
        {
            ComboArgumentJava.IsDropDownOpen = false;
            return;
        }

        if (ComboArgumentJava.Items[0] is MyComboBoxItem { IsEnabled: false })
            ComboArgumentJava.IsDropDownOpen = false;
    }

    // 下拉框选择更改处理（保存新格式配置）
    private void JavaSelectionUpdate(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (ComboArgumentJava.SelectedItem is not MyComboBoxItem selectedItem)
            return;

        string value;
        if (selectedItem.Tag is null)
        {
            // 自动选择：存储空字符串
            value = "";
        }
        else if (selectedItem.Tag is string { } tag && tag == FollowGlobalJava)
        {
            value = FollowGlobalJava;
        }
        else if (selectedItem.Tag is string executable)
        {
            value = executable;
        }
        else
        {
            return;
        }

        // 保存配置
        Settings.Config.Instance.SelectedJava[PathInstance] = value;
        Settings.Save();
        RefreshRam(true);
    }

    #endregion

    #region 其他设置

    // 版本隔离警告
    private bool isReverting;

    private async void ComboArgumentIndieV2_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (isReverting)
            return;
        if (await MsgBox.ShowAsync(
                Lang.Text("Instance.Setup.Options.InstanceIsolation.Message"),
                Lang.Text("Common.Dialog.Warning"), Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
        {
            isReverting = true;
            ComboArgumentIndieV2.SelectedItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
            isReverting = false;
        }
        else
        {
            bool newValue = ComboArgumentIndieV2.SelectedIndex == 0;
            Settings.Config.Instance.IndieV2[PathInstance] = newValue;
            Settings.Save();
        }
    }

    // 游戏窗口
    private void CheckArgumentTitleEmpty_Change(object sender, bool e)
    {
        TextArgumentTitle.HintText = CheckArgumentTitleEmpty.Checked == true ? Lang.Text("Common.Option.Default") : Lang.Text("Instance.Setup.FollowGlobal");
        CheckBoxChange(sender, e);
    }

    // TextArgumentTitle 是可编辑的 MyComboBox，其 TextChanged 事件的参数类型与 MyTextBox 不同
    private void TextArgumentTitle_TextChanged(object? sender, RoutedEventArgs? e)
    {
        CheckArgumentTitleEmpty.IsVisible = !(TextArgumentTitle.Text?.Length > 0);
        if (ModAnimation.AniControlEnabled != 0)
            return;
        SetByTag(TextArgumentTitle.Tag?.ToString() ?? "", TextArgumentTitle.Text ?? "");
    }

    #endregion

    #region 高级设置

    private void TextAdvanceRun_TextChanged(object? sender, TextChangedEventArgs e)
    {
        CheckAdvanceRunWait.IsVisible = !string.IsNullOrEmpty(TextAdvanceRun.Text);
        TextBoxChange(sender, e);
    }

    private async void ComboAdvanceRenderer_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;

        if (!Settings.States.Hint.Renderer && ComboAdvanceRenderer.SelectedIndex != 0)
        {
            if (await MsgBox.ShowAsync(Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Message"),
                    Lang.Text("Common.Dialog.Warning"),
                    Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
            {
                ComboAdvanceRenderer.SelectedItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
            }
            else
            {
                ComboChange(ComboAdvanceRenderer, e);
                Settings.States.Hint.Renderer = true;
                Settings.Save();
            }
        }
        else
        {
            ComboChange(ComboAdvanceRenderer, e);
        }
    }

    private async void CheckUseDebugLog4j2Config_CheckChanged(object sender, bool e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        var checkBox = sender as MyCheckBox;
        if (checkBox is null) return;

        if (checkBox.Checked.GetValueOrDefault() && !Settings.States.Hint.DebugLog4j2Config)
        {
            if (await MsgBox.ShowAsync(
                    Lang.Text("Instance.Setup.Advanced.UseDebugLog4j.Message"),
                    Lang.Text("Common.Dialog.Warning"), Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
            {
                checkBox.Checked = false;
            }
            else
            {
                CheckBoxChange(sender, e);
                Settings.States.Hint.DebugLog4j2Config = true;
                Settings.Save();
            }
        }
        else
        {
            CheckBoxChange(sender, e);
        }
    }

    #endregion
}

/// <summary>
///     文本不得包含黑名单中的任何片段，对应 WPF 版 PCL.Core.Utils.Validate.BlacklistValidator。
/// </summary>
public sealed class BlacklistValidator : AbstractValidator<string>
{
    public BlacklistValidator()
    {
        RuleFor(text => text)
            .Must(text => !Blacklist.Any(word => text.Contains(word, StringComparison.Ordinal)))
            .WithMessage("包含不允许的内容");
    }

    /// <summary>
    ///     不允许出现的文本片段。
    /// </summary>
    public IList<string> Blacklist { get; } = new List<string>();
}

/// <summary>
///     文本必须是 http:// 或 https:// 开头的绝对地址，对应 WPF 版 PCL.Core.Utils.Validate.HttpValidator。
/// </summary>
public sealed class HttpValidator : AbstractValidator<string>
{
    public HttpValidator()
    {
        RuleFor(text => text)
            .Custom((text, context) =>
            {
                if (string.IsNullOrEmpty(text))
                {
                    if (!AllowsNullOrEmpty)
                        context.AddFailure("地址不能为空");
                    return;
                }

                if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    context.AddFailure("地址必须以 http:// 或 https:// 开头");
            });
    }

    /// <summary>
    ///     是否允许为空。
    /// </summary>
    public bool AllowsNullOrEmpty { get; set; }
}
