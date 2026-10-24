using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentValidation;
using PCL.Shion.Controls;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Instance;
using PCL.Shion.Pages.Launch;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Setup;

public partial class PageSetupLaunch : MyPageRight
{
    private static AppSettings Settings => App.Services.Settings;

    private bool isLoad;

    private int ramTextLeft = 2;
    private int ramTextRight = 1;

    private ColumnDefinition ColumnRamUsed => PanRamDisplay.ColumnDefinitions[0];
    private ColumnDefinition ColumnRamGame => PanRamDisplay.ColumnDefinitions[1];
    private ColumnDefinition ColumnRamEmpty => PanRamDisplay.ColumnDefinitions[2];

    public PageSetupLaunch()
    {
        InitializeComponent();
        Loaded += PageSetupLaunch_Loaded;
        TextArgumentInfo.ValidateRules = new Collection<IValidator<string>>
        {
            CreateBlacklistValidator("\"", "“", "”")
        };
        TextArgumentWindowWidth.ValidateRules = new Collection<IValidator<string>>
        {
            CreateIntValidator(1, 9999)
        };
        TextArgumentWindowHeight.ValidateRules = new Collection<IValidator<string>>
        {
            CreateIntValidator(1, 9999)
        };
    }

    private void PageSetupLaunch_Loaded(object? sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        RefreshRam(false);
        BtnSwitch.IsVisible = PageLaunchLeft.McInstanceSelected is not null;

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;

        ModAnimation.AniControlEnabled += 1;
        Reload();
        ModAnimation.AniControlEnabled -= 1;

        // 内存自动刷新
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => RefreshRam();
        timer.Start();
        RectRamGame.SizeChanged += (_, _) => RefreshRamText();
    }

    public void Reload()
    {
        try
        {
            var settings = Settings;

            // 启动参数
            TextArgumentTitle.Text = settings.LaunchArgumentTitle;
            TextArgumentInfo.Text = settings.LaunchArgumentInfo;
            ComboArgumentIndieV2.SelectedIndex = settings.LaunchArgumentIndieV2;
            ComboArgumentVisibie.SelectedIndex = settings.LaunchArgumentVisible;
            var priorityTag = settings.LaunchArgumentPriority.ToString(CultureInfo.InvariantCulture);
            ComboArgumentPriority.SelectedItem = ComboArgumentPriority.Items
                .OfType<Control>()
                .FirstOrDefault(item => Equals(item.Tag, priorityTag));
            ComboArgumentWindowType.SelectedIndex = settings.LaunchArgumentWindowType;
            TextArgumentWindowWidth.Text = settings.LaunchArgumentWindowWidth.ToString(CultureInfo.InvariantCulture);
            TextArgumentWindowHeight.Text = settings.LaunchArgumentWindowHeight.ToString(CultureInfo.InvariantCulture);
            ComboMsAuthType.SelectedIndex = settings.LoginMsAuthType;
            ComboPreferredIpStack.SelectedIndex = settings.LaunchPreferredIpStack;
            WindowTypeUIRefresh();

            // 游戏内存
            (settings.LaunchRamType == 1 ? RadioRamType1 : RadioRamType0).Checked = true;
            SliderRamCustom.Value = settings.LaunchRamCustom;
            SliderRamInitialCustom.Value = settings.LaunchRamCustomInitial;
            RamType(settings.LaunchRamType);

            // 高级设置
            ComboAdvanceRenderer.SelectedIndex = settings.LaunchAdvanceRenderer;
            TextAdvanceJvm.Text = settings.LaunchAdvanceJvm;
            TextAdvanceGame.Text = settings.LaunchAdvanceGame;
            TextAdvanceRun.Text = settings.LaunchAdvanceRun;
            CheckAdvanceRunWait.Checked = settings.LaunchAdvanceRunWait;
            CheckAdvanceDisableLF.Checked = settings.LaunchAdvanceDisableLF;
            CheckAdvanceGraphicCard.Checked = settings.LaunchAdvanceGraphicCard;
            CheckAdvanceNoJavaw.Checked = settings.LaunchAdvanceNoJavaw;
            CheckAdvanceDisableLwjglUnsafeAgent.Checked = settings.LaunchAdvanceDisableLwjglUnsafeAgent;
            CheckAdvanceDisableCrashAnalysis.Checked = settings.LaunchAdvanceDisableCrashAnalysis;
            CheckAdvanceLockMemory.Checked = settings.LaunchAdvanceLockMemory;
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
            {
                CheckAdvanceDisableJLW.Checked = true;
                CheckAdvanceDisableJLW.IsEnabled = false;
                ToolTip.SetTip(CheckAdvanceDisableJLW, Lang.Text("Setup.Launch.Advanced.DisableJlw.Arm64Notice"));
            }
            else
            {
                CheckAdvanceDisableJLW.Checked = settings.LaunchAdvanceDisableJLW;
            }
        }
        catch (NullReferenceException ex)
        {
            Trace.TraceError($"启动设置配置损坏，已重置：{ex}");
            Hint.Show(Lang.Text("Setup.Launch.Error.ConfigReset"), HintType.Error);
            Reset();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"读取启动设置失败：{ex}");
            Hint.Show(Lang.Text("Setup.Launch.Error.LoadFailed"), HintType.Error);
        }
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Settings.ResetLaunch();
            Settings.Save();
            Trace.TraceInformation("[Setup] 已初始化启动设置");
            Hint.Show(Lang.Text("Setup.Launch.Initialized"), HintType.Success);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"初始化启动设置失败：{ex}");
            Hint.Show(Lang.Text("Setup.Launch.Error.InitFailed"), HintType.Error);
        }

        Reload();
    }

    #region 控件事件

    // 将控件改变路由到设置改变
    private void RadioBoxChange(object sender, RouteEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        var type = ReferenceEquals(sender, RadioRamType1) ? 1 : 0;
        Settings.LaunchRamType = type;
        Settings.Save();
        RamType(type);
    }

    private void TextBoxChange(object sender, RoutedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0 || sender is not MyTextBox box)
            return;
        var settings = Settings;
        if (ReferenceEquals(box, TextArgumentInfo))
            settings.LaunchArgumentInfo = box.Text ?? "";
        else if (ReferenceEquals(box, TextAdvanceJvm))
            settings.LaunchAdvanceJvm = box.Text ?? "";
        else if (ReferenceEquals(box, TextAdvanceGame))
            settings.LaunchAdvanceGame = box.Text ?? "";
        else if (ReferenceEquals(box, TextAdvanceRun))
            settings.LaunchAdvanceRun = box.Text ?? "";
        else if (ReferenceEquals(box, TextArgumentWindowWidth) && TryParseWindowSize(box.Text, out var width))
            settings.LaunchArgumentWindowWidth = width;
        else if (ReferenceEquals(box, TextArgumentWindowHeight) && TryParseWindowSize(box.Text, out var height))
            settings.LaunchArgumentWindowHeight = height;
        else
            return;
        settings.Save();
    }

    private void TextArgumentTitle_OnTextChanged(object? sender, RoutedEventArgs? e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        Settings.LaunchArgumentTitle = TextArgumentTitle.Text ?? "";
        Settings.Save();
    }

    private void SliderChange(object sender, bool user)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        var settings = Settings;
        if (ReferenceEquals(sender, SliderRamCustom))
            settings.LaunchRamCustom = SliderRamCustom.Value;
        else if (ReferenceEquals(sender, SliderRamInitialCustom))
            settings.LaunchRamCustomInitial = SliderRamInitialCustom.Value;
        settings.Save();
    }

    private void CheckBoxChange(object sender, bool user)
    {
        if (sender is not MyCheckBox box)
            return;
        if (ModAnimation.AniControlEnabled == 0)
        {
            var settings = Settings;
            var value = box.Checked == true;
            if (ReferenceEquals(box, CheckAdvanceRunWait))
                settings.LaunchAdvanceRunWait = value;
            else if (ReferenceEquals(box, CheckAdvanceDisableJLW))
                settings.LaunchAdvanceDisableJLW = value;
            else if (ReferenceEquals(box, CheckAdvanceDisableLF))
                settings.LaunchAdvanceDisableLF = value;
            else if (ReferenceEquals(box, CheckAdvanceGraphicCard))
                settings.LaunchAdvanceGraphicCard = value;
            else if (ReferenceEquals(box, CheckAdvanceNoJavaw))
                settings.LaunchAdvanceNoJavaw = value;
            else if (ReferenceEquals(box, CheckAdvanceDisableLwjglUnsafeAgent))
                settings.LaunchAdvanceDisableLwjglUnsafeAgent = value;
            else if (ReferenceEquals(box, CheckAdvanceDisableCrashAnalysis))
                settings.LaunchAdvanceDisableCrashAnalysis = value;
            else if (ReferenceEquals(box, CheckAdvanceLockMemory))
                settings.LaunchAdvanceLockMemory = value;
            settings.Save();
        }

        // 锁定内存状态变化时同步刷新初始内存滑块的可用状态
        if (ReferenceEquals(box, CheckAdvanceLockMemory))
            RamType(Settings.LaunchRamType);
    }

    private void ComboArgumentIndie_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        Settings.LaunchArgumentIndieV2 = ComboArgumentIndieV2.SelectedIndex;
        Settings.Save();
        _ = MsgBox.ShowAsync(Lang.Text("Setup.Launch.Options.InstanceIsolation.DefaultPolicyHint"));
    }

    // 可见性选择直接关闭的警告
    private async void ComboArgumentVisibie_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (ComboArgumentVisibie.SelectedIndex == 0 &&
            await MsgBox.ShowAsync(
                Lang.Text("Setup.Launch.Options.Visibility.CloseImmediately.Warning.Message"),
                Lang.Text("Setup.Launch.Options.Visibility.CloseImmediately.Warning.Title"),
                Lang.Text("Setup.Launch.Options.Visibility.CloseImmediately.Warning.Continue"),
                Lang.Text("Common.Action.Cancel")) == 2)
        {
            if (e.RemovedItems.Count > 0)
                ComboArgumentVisibie.SelectedItem = e.RemovedItems[0];
            return;
        }

        Settings.LaunchArgumentVisible = ComboArgumentVisibie.SelectedIndex;
        Settings.Save();
    }

    private void ComboArgumentPriority_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0 || ComboArgumentPriority.SelectedItem is not Control { Tag: { } tag })
            return;
        Settings.LaunchArgumentPriority = Convert.ToInt32(tag, CultureInfo.InvariantCulture);
        Settings.Save();
    }

    private void ComboArgumentWindowType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        Settings.LaunchArgumentWindowType = ComboArgumentWindowType.SelectedIndex;
        Settings.Save();
        WindowTypeUIRefresh();
    }

    private void ComboMsAuthType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        Settings.LoginMsAuthType = ComboMsAuthType.SelectedIndex;
        Settings.Save();
    }

    private void ComboPreferredIpStack_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        Settings.LaunchPreferredIpStack = ComboPreferredIpStack.SelectedIndex;
        Settings.Save();
    }

    private void WindowTypeUIRefresh()
    {
        if (ComboArgumentWindowType is null)
            return;
        // 3 为自定义窗口大小
        var isCustom = ComboArgumentWindowType.SelectedIndex == 3;
        LabArgumentWindowMiddle.IsVisible = isCustom;
        TextArgumentWindowHeight.IsVisible = isCustom;
        TextArgumentWindowWidth.IsVisible = isCustom;
    }

    private static bool TryParseWindowSize(string? text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
               value is >= 1 and <= 9999;
    }

    private static InlineValidator<string> CreateBlacklistValidator(params string[] blacklist)
    {
        var validator = new InlineValidator<string>();
        validator.RuleFor(input => input).Custom((input, context) =>
        {
            foreach (var item in blacklist)
                if (input.Contains(item, StringComparison.Ordinal))
                    context.AddFailure($"输入内容不能包含 {item}！");
        });
        return validator;
    }

    private static InlineValidator<string> CreateIntValidator(int min, int max)
    {
        var validator = new InlineValidator<string>();
        validator.RuleFor(input => input)
            .Must(x => x.Length < 9).WithMessage("请输入一个大小合理的数字！")
            .Must(x => int.TryParse(x, out _)).WithMessage("请输入一个整数！")
            .Must(x => int.TryParse(x, out var value) && value <= max).WithMessage($"不可超过 {max}！")
            .Must(x => int.TryParse(x, out var value) && value >= min).WithMessage($"不可低于 {min}！");
        return validator;
    }

    #endregion

    #region 游戏内存

    public void RamType(int type)
    {
        if (SliderRamCustom is null)
            return;
        SliderRamCustom.IsEnabled = type == 1;
        // 锁定内存时 -Xms 恒等于 -Xmx，初始内存设置不生效
        SliderRamInitialCustom.IsEnabled = type == 1 && !Settings.LaunchAdvanceLockMemory;
    }

    /// <summary>
    ///     刷新 UI 上的 RAM 显示。
    /// </summary>
    public void RefreshRam(bool showAnim)
    {
        if (Navigator.Current != PageType.Setup || Navigator.PageLeft is not PageSetupLeft left ||
            left.pageID != PageSubType.SetupLaunch)
            return;
        var instance = PageLaunchLeft.McInstanceSelected;
        // 获取内存情况
        var ramGame = Math.Round(GetRam(instance, false), 5);
        var (phyTotal, phyAvailable) = SystemMemory.Get();
        var ramTotal = Math.Round((double)phyTotal / 1024 / 1024 / 1024, 1);
        var ramAvailable = Math.Round((double)phyAvailable / 1024 / 1024 / 1024, 1);
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
        var ramInitial = GetInitialRam(instance, false);
        string suffixText;
        if (ramInitial.HasValue && ramGame != ramGameActual)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixBoth", Lang.Number(ramInitial.Value, "N1"),
                Lang.Number(ramGameActual, "N1"));
        else if (ramInitial.HasValue)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixInitial", Lang.Number(ramInitial.Value, "N1"));
        else if (ramGame != ramGameActual)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixAvailable", Lang.Number(ramGameActual, "N1"));
        else
            suffixText = "";
        LabRamGame.Text = $"{Lang.Number(ramGame, "N1")} GiB{(suffixText.Length > 0 ? $" ({suffixText})" : "")}";
        LabRamUsed.Text = $"{Lang.Number(ramUsed, "N1")} GiB";
        LabRamTotal.Text = $" / {Lang.Number(ramTotal, "N1")} GiB";
        LabRamWarn.IsVisible = ramGame == 1d && !PageSetupJava.IsGameSet64BitJava() &&
                               Environment.Is64BitOperatingSystem && PageSetupJava.ExistAnyJava();
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
                }, "SetupLaunch Ram Grid");
        }
        else
        {
            // 宽度设置
            ColumnRamUsed.Width = new GridLength(ramUsed, GridUnitType.Star);
            ColumnRamGame.Width = new GridLength(ramGameActual, GridUnitType.Star);
            ColumnRamEmpty.Width = new GridLength(ramEmpty, GridUnitType.Star);
        }
    }

    private void RefreshRam()
    {
        RefreshRam(true);
    }

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
                        }, "SetupLaunch Ram TextLeft");
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
                        }, "SetupLaunch Ram TextLeft");
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
                        }, "SetupLaunch Ram TextLeft");
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
                (ramTextRight != right || ModAnimation.AniIsRun("SetupLaunch Ram TextRight")))
            {
                // 需要动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaX(LabRamGame, totalWidth - labGameWidth - LabRamGame.Margin.Left, 100,
                            ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaX(LabRamGameTitle, totalWidth - labGameTitleWidth - LabRamGameTitle.Margin.Left,
                            100, ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                    }, "SetupLaunch Ram TextRight");
            }
            else
            {
                // 不需要动画
                ModAnimation.AniStop("SetupLaunch Ram TextRight");
                LabRamGame.Margin = new Thickness(totalWidth - labGameWidth, 3d, 0d, 0d);
                LabRamGameTitle.Margin = new Thickness(totalWidth - labGameTitleWidth, 0d, 0d, 5d);
            }
        }
        else if (ModAnimation.AniControlEnabled == 0 &&
                 (ramTextRight != right || ModAnimation.AniIsRun("SetupLaunch Ram TextRight")))
        {
            // 需要动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaX(LabRamGame, 2d + rectUsedWidth - LabRamGame.Margin.Left, 100,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaX(LabRamGameTitle, 2d + rectUsedWidth - LabRamGameTitle.Margin.Left, 100,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                }, "SetupLaunch Ram TextRight");
        }
        else
        {
            // 不需要动画
            ModAnimation.AniStop("SetupLaunch Ram TextRight");
            LabRamGame.Margin = new Thickness(2d + rectUsedWidth, 3d, 0d, 0d);
            LabRamGameTitle.Margin = new Thickness(2d + rectUsedWidth, 0d, 0d, 5d);
        }

        ramTextRight = right;
    }

    /// <summary>
    ///     将内存滑块刻度值转换为 GB。
    /// </summary>
    public static double GetRamFromTick(int value)
    {
        return value switch
        {
            <= 12 => value * 0.1d + 0.3d,
            <= 25 => (value - 12) * 0.5d + 1.5d,
            <= 33 => (value - 25) * 1 + 8,
            _ => (value - 33) * 2 + 16
        };
    }

    /// <summary>
    ///     判断生效的自定义 JVM 参数中是否已包含 -Xms。
    ///     此时启动路径会跳过自动追加的初始堆，实际值以自定义参数为准。
    /// </summary>
    public static bool HasCustomXms(GameInstance? version)
    {
        var effectiveArgs = Settings.LaunchAdvanceJvm;
        return !string.IsNullOrEmpty(effectiveArgs) &&
               effectiveArgs.Contains("-Xms", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     获取全局设置的初始堆大小（-Xms）。单位为 GB；未启用自定义初始大小时返回 null。
    /// </summary>
    public static double? GetInitialRam(GameInstance? version, bool useVersionJavaSetup, bool? is32BitJava = default)
    {
        var settings = Settings;
        // 锁定内存时 -Xms 恒等于 -Xmx，由锁定内存逻辑处理
        if (settings.LaunchAdvanceLockMemory)
            return null;
        // 自定义 JVM 参数含 -Xms 时由其决定实际初始堆，不再显示滑块值
        if (HasCustomXms(version))
            return null;
        // 仅手动分配模式下生效
        if (settings.LaunchRamType != 1)
            return null;
        var initialTick = settings.LaunchRamCustomInitial;
        if (initialTick <= 0)
            return null;
        return Math.Min(GetRamFromTick(initialTick), GetRam(version, useVersionJavaSetup, is32BitJava));
    }

    /// <summary>
    ///     获取当前设置的 RAM 值。单位为 GB。
    /// </summary>
    public static double GetRam(GameInstance? version, bool useVersionJavaSetup, bool? is32BitJava = default)
    {
        var settings = Settings;
        var ramGive = default(double);
        if (settings.LaunchRamType == 0)
        {
            // 自动配置
            var ramAvailable = Math.Round((double)SystemMemory.Get().Available / 1024 / 1024 / 1024 * 10) / 10;
            // 确定需求的内存值
            double ramMininum; // 无论如何也需要保证的最低限度内存
            double ramTarget1; // 估计能勉强带动了的内存
            double ramTarget2; // 估计没啥问题了的内存
            double ramTarget3; // 放一百万个材质和 Mod 和光影需要的内存
            if (version is not null && IsModable(version))
            {
                // 可安装 Mod 的实例
                var modDir = Path.Combine(GetGameDirectory(version), "mods");
                var modCount = Directory.Exists(modDir) ? Directory.GetFiles(modDir).Length : 0;
                ramMininum = 0.5d + modCount / 150d;
                ramTarget1 = 1.5d + modCount / 90d;
                ramTarget2 = 2.7d + modCount / 50d;
                ramTarget3 = 4.5d + modCount / 25d;
            }
            else if (version is not null && IsOptiFine(version))
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

            var ramStages = new[]
            {
                (Delta: ramTarget1, Ratio: 1d),
                (Delta: ramTarget2 - ramTarget1, Ratio: 0.7d),
                (Delta: ramTarget3 - ramTarget2, Ratio: 0.4d),
                (Delta: ramTarget3, Ratio: 0.15d)
            };
            foreach (var (ramDelta, ramRatio) in ramStages)
            {
                ramGive += Math.Min(ramAvailable * ramRatio, ramDelta);
                ramAvailable -= ramDelta / ramRatio;
                if (ramAvailable < 0.1d)
                    break;
            }

            // 不低于最低值
            ramGive = Math.Round(Math.Max(ramGive, ramMininum), 1);
        }
        else
        {
            // 手动配置
            ramGive = GetRamFromTick(settings.LaunchRamCustom);
        }

        // 若使用 32 位 Java，则限制为 1G
        if (is32BitJava ?? !PageSetupJava.IsGameSet64BitJava())
            ramGive = Math.Min(1d, ramGive);
        return ramGive;
    }

    private static bool IsModable(GameInstance version) => version.Loader is not null;

    private static bool IsOptiFine(GameInstance version) =>
        string.Equals(version.Loader, "optifine", StringComparison.OrdinalIgnoreCase);

    private static string GetGameDirectory(GameInstance version) =>
        version.IsIsolated ? version.Directory : Settings.MinecraftRoot;

    /// <summary>
    ///     获取物理内存情况，单位为字节。
    /// </summary>
    private static class SystemMemory
    {
        public static (ulong Total, ulong Available) Get()
        {
            if (OperatingSystem.IsWindows())
            {
                var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
                if (GlobalMemoryStatusEx(ref status))
                    return (status.ullTotalPhys, status.ullAvailPhys);
            }
            else if (OperatingSystem.IsLinux())
            {
                var meminfo = ReadLinuxMemInfo();
                if (meminfo.Total > 0)
                    return meminfo;
            }

            var info = GC.GetGCMemoryInfo();
            var total = (ulong)info.TotalAvailableMemoryBytes;
            var used = (ulong)info.MemoryLoadBytes;
            return (total, total > used ? total - used : 0);
        }

        private static (ulong Total, ulong Available) ReadLinuxMemInfo()
        {
            ulong total = 0;
            ulong available = 0;
            try
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                        total = ParseKiB(line);
                    else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                        available = ParseKiB(line);
                }
            }
            catch (IOException)
            {
                return (0, 0);
            }

            return (total, available);
        }

        private static ulong ParseKiB(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var kib)
                ? kib * 1024
                : 0;
        }

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        [SupportedOSPlatform("windows")]
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }
    }

    #endregion

    #region 其他选项

    private void BtnSwitch_Click(object? sender, PointerReleasedEventArgs e)
    {
        var instance = PageLaunchLeft.McInstanceSelected;
        if (instance is null)
            return;
        PageInstanceLeft.McInstance = instance;
        Navigator.Go(PageType.InstanceSetup, PageSubType.VersionSetup);
    }

    #endregion

    #region 高级设置

    private void TextAdvanceRun_TextChanged(object sender, TextChangedEventArgs e)
    {
        CheckAdvanceRunWait.IsVisible = !string.IsNullOrEmpty(TextAdvanceRun.Text);
    }

    // JVM 参数重设
    private void TextAdvanceJvm_TextChanged(object sender, TextChangedEventArgs e)
    {
        var isDefault = TextAdvanceJvm.Text == AppSettings.DefaultLaunchJvmArgs;
        BtnAdvanceJvmReset.Opacity = isDefault ? 0d : 1d;
        BtnAdvanceJvmReset.IsHitTestVisible = !isDefault;
    }

    private void BtnAdvanceJvmReset_Click(object? sender, PointerReleasedEventArgs e)
    {
        Settings.LaunchAdvanceJvm = AppSettings.DefaultLaunchJvmArgs;
        Settings.Save();
        Reload();
    }

    private async void ComboAdvanceRenderer_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (!Settings.HintRenderer && ComboAdvanceRenderer.SelectedIndex != 0)
        {
            var confirmed = await MsgBox.ShowAsync(Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Message"),
                Lang.Text("Common.Dialog.Warning"),
                Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Confirm"),
                Lang.Text("Common.Action.Cancel"), isWarn: true) == 1;
            if (!confirmed)
            {
                if (e.RemovedItems.Count > 0)
                    ComboAdvanceRenderer.SelectedItem = e.RemovedItems[0];
                return;
            }

            Settings.HintRenderer = true;
        }

        Settings.LaunchAdvanceRenderer = ComboAdvanceRenderer.SelectedIndex;
        Settings.Save();
    }

    #endregion
}
