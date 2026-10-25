using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Launch;
using PCL.Shion.Services;
using PCL.Shion.Themes;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Setup;

public partial class PageSetupUI : MyPageRight
{
    private static PageSetupUI? instance;

    private static bool hiddenForceShow;

    /// <summary>
    ///     侧栏、顶部栏或设置页的隐藏项发生变化，需要刷新各处的显示。
    /// </summary>
    public static event Action? HiddenChanged;

    public bool isLoaded;

    public PageSetupUI()
    {
        InitializeComponent();
        instance ??= this;
        PanScroll = PanBack; // 不知道为啥不能在 XAML 设置
        lightThemeOptions = CreateThemeOptions(false);
        darkThemeOptions = CreateThemeOptions(true);
        ComboLightColor.ItemsSource = lightThemeOptions;
        ComboDarkColor.ItemsSource = darkThemeOptions;
        BorderThemeHueStrip.Background = CreateHueStripBrush();
        TextCustomNet.ValidateRules.Add(new HttpUrlValidator());
        HintCustom.Click += (_, _) =>
        {
            if (HintCustom.Tag is string url)
                SystemShell.OpenUrl(url);
        };
        Loaded += PageSetupUI_Loaded;
        Loaded += (_, _) => HiddenRefresh();
    }

    /// <summary>
    ///     首页设置页的唯一实例，与 WPF 的 ModMain.frmSetupUI 相同。
    /// </summary>
    public static PageSetupUI? Instance => instance;

    /// <summary>
    ///     配色下拉框中依次列出的主题，下拉框的序号与此数组的下标一一对应。
    /// </summary>
    private readonly ColorTheme[] selectableThemes = ThemeService.GetSelectableThemes(MainWindow.IsAprilEnabled);

    private readonly ThemeColorOption[] lightThemeOptions;

    private readonly ThemeColorOption[] darkThemeOptions;

    private ThemeColorOption[] CreateThemeOptions(bool isDarkMode) => selectableThemes
        .Select(theme => new ThemeColorOption(theme, Lang.Text(theme switch
        {
            ColorTheme.SkyBlue => "Setup.Ui.Theme.Color.SkyBlue",
            ColorTheme.CatBlue => "Setup.Ui.Theme.Color.CatBlue",
            ColorTheme.DeathBlue => "Setup.Ui.Theme.Color.CrashBlue",
            ColorTheme.HmclBlue => "Setup.Ui.Theme.Color.Hmcl",
            _ => "Setup.Ui.Theme.Color." + theme
        }), isDarkMode))
        .ToArray();

    /// <summary>
    ///     自定义配色的色相或饱和度改变后，刷新下拉框中自定义配色的色块。
    /// </summary>
    public void ThemeSwatchRefresh()
    {
        foreach (var option in lightThemeOptions.Concat(darkThemeOptions))
        {
            if (option.Theme == ColorTheme.Custom)
                option.RefreshSwatch();
        }
    }

    private int ThemeIndex(ColorTheme theme)
    {
        var index = Array.IndexOf(selectableThemes, theme);
        return index >= 0 ? index : Array.IndexOf(selectableThemes, ColorTheme.CatBlue);
    }

    /// <summary>
    ///     浅色或深色配色选择了自定义时，显示自定义配色的色相与饱和度。
    /// </summary>
    public void ThemeCustomRefresh()
    {
        var settings = SetupConfig.Settings;
        PanThemeCustom.IsVisible = settings.LightColor == ColorTheme.Custom || settings.DarkColor == ColorTheme.Custom;
    }

    // 色相滑动条下方的色环预览，亮度与彩度固定，只体现色相
    private static LinearGradientBrush CreateHueStripBrush()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative)
        };
        for (var hue = 0; hue <= 360; hue += 30)
            brush.GradientStops.Add(new GradientStop(LabColor.FromLch(0.7, 0.14, hue).ToAvaloniaColor(), hue / 360d));
        return brush;
    }

    private void PageSetupUI_Loaded(object? sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        ModAnimation.AniControlEnabled += 1;
        Reload(); // #4826，在每次进入页面时都刷新一下
        ModAnimation.AniControlEnabled -= 1;

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        SliderLoad();
    }

    public void Reload()
    {
        try
        {
            var settings = SetupConfig.Settings;
            var extra = SetupExtraSettings.Current;

            // 启动器
            SliderLauncherOpacity.Value = extra.UiLauncherTransparent;
            CheckLauncherLogo.Checked = extra.UiLauncherLogo;
            ComboDarkMode.SelectedIndex = (int)settings.ColorMode;
            ComboDarkColor.SelectedIndex = ThemeIndex(settings.DarkColor);
            ComboLightColor.SelectedIndex = ThemeIndex(settings.LightColor);
            SliderThemeCustomHue.Value = settings.ThemeCustomHue;
            SliderThemeCustomSaturation.Value = settings.ThemeCustomSaturation;
            ThemeCustomRefresh();
            CheckShowLaunchingHint.Checked = settings.UiShowLaunchingHint;

            // 字体设置
            ComboUiFont.SelectedFontTag = extra.UiFont;
            ComboUiMotdFont.SelectedFontTag = extra.UiMotdFont;

            CheckBlur.Checked = extra.UiBlur;
            SliderBlurValue.Value = extra.UiBlurValue;
            SliderBlurSamplingRate.Value = extra.UiBlurSamplingRate;
            ComboBlurType.SelectedIndex = extra.UiBlurType;
            PanBlurValue.IsVisible = CheckBlur.Checked == true;
            CheckLockWindowSize.Checked = extra.UiLockWindowSize;

            // 背景图片
            SliderBackgroundOpacity.Value = extra.UiBackgroundOpacity;
            SliderBackgroundBlur.Value = extra.UiBackgroundBlur;
            ComboBackgroundSuit.SelectedIndex = settings.UiBackgroundSuit;
            CheckBackgroundColorful.Checked = settings.BackgroundColorful;
            ComboBackgroundGradient.SelectedIndex = (int)settings.BackgroundGradientStyle;
            PanBackgroundGradient.IsVisible = settings.BackgroundColorful;
            var autoPauseVideo = extra.UiAutoPauseVideo;
            CheckAutoPauseVideo.Checked = autoPauseVideo;

            BackgroundRefresh(false, false);

            // 标题栏
            FindRadio("RadioLogoType" + extra.UiLogoType).Checked = true;
            CheckLogoLeft.IsVisible = RadioLogoType0.Checked;
            PanLogoText.IsVisible = RadioLogoType2.Checked;
            PanLogoChange.IsVisible = RadioLogoType3.Checked;
            TextLogoText.Text = extra.UiLogoText;
            CheckLogoLeft.Checked = extra.UiLogoLeft;

            // 背景音乐
            CheckMusicRandom.Checked = extra.UiMusicRandom;
            CheckMusicAuto.Checked = extra.UiMusicAuto;
            CheckMusicStop.Checked = extra.UiMusicStop;
            CheckMusicStart.Checked = extra.UiMusicStart;
            CheckMusicSMTC.Checked = extra.UiMusicSMTC;
            SliderMusicVolume.Value = extra.UiMusicVolume;
            MusicRefreshUI();

            // 主页
            try
            {
                ComboCustomPreset.SelectedIndex = settings.UiCustomPreset;
            }
            catch
            {
                SetupConfig.ResetProperties(name => name == nameof(AppSettings.UiCustomPreset));
            }

            FindRadio("RadioCustomType" + settings.UiCustomType).Checked = true;
            TextCustomNet.Text = settings.UiCustomNet;
            UiCustomType(settings.UiCustomType);

            // 首页侧栏
            CheckSidebarCheckIn.Checked = settings.UiSidebarCheckIn;
            CheckSidebarRecentPlay.Checked = settings.UiSidebarRecentPlay;
            CheckSidebarNews.Checked = settings.UiSidebarNews;

            // 功能隐藏
            // 主页面
            CheckHiddenPageDownload.Checked = settings.UiHiddenPageDownload;
            CheckHiddenPageSetup.Checked = settings.UiHiddenPageSetup;
            CheckHiddenPageTools.Checked = settings.UiHiddenPageTools;

            // 子页面 设置
            CheckHiddenSetupLaunch.Checked = settings.UiHiddenSetupLaunch;
            CheckHiddenSetupUI.Checked = settings.UiHiddenSetupUi;
            CheckHiddenSetupLauncherLanguage.Checked = settings.UiHiddenSetupLauncherLanguage;
            CheckHiddenSetupGameManage.Checked = settings.UiHiddenSetupGameManage;
            CheckHiddenSetupJava.Checked = settings.UiHiddenSetupJava;
            CheckHiddenLauncherMisc.Checked = settings.UiHiddenSetupLauncherMisc;
            CheckHiddenSetupUpdate.Checked = settings.UiHiddenSetupUpdate;
            CheckHiddenSetupGameLink.Checked = settings.UiHiddenSetupGameLink;
            CheckHiddenSetupAbout.Checked = settings.UiHiddenSetupAbout;
            CheckHiddenSetupFeedback.Checked = settings.UiHiddenSetupFeedback;
            CheckHiddenSetupLog.Checked = settings.UiHiddenSetupLog;

            // 子页面 工具
            CheckHiddenToolsGameLink.Checked = settings.UiHiddenToolsGameLink;
            CheckHiddenToolsTest.Checked = settings.UiHiddenToolsTest;

            // 子页面 实例设置
            CheckHiddenVersionEdit.Checked = settings.UiHiddenVersionEdit;
            CheckHiddenVersionExport.Checked = settings.UiHiddenVersionExport;
            CheckHiddenVersionSave.Checked = settings.UiHiddenVersionSave;
            CheckHiddenVersionScreenshot.Checked = settings.UiHiddenVersionScreenshot;
            CheckHiddenVersionMod.Checked = settings.UiHiddenVersionMod;
            CheckHiddenVersionResourcePack.Checked = settings.UiHiddenVersionResourcePack;
            CheckHiddenVersionShader.Checked = settings.UiHiddenVersionShader;
            CheckHiddenVersionSchematic.Checked = settings.UiHiddenVersionSchematic;
            CheckHiddenVersionServer.Checked = settings.UiHiddenVersionServer;

            // 特定功能
            CheckHiddenFunctionSelect.Checked = settings.UiHiddenFunctionSelect;
            CheckHiddenFunctionModUpdate.Checked = settings.UiHiddenFunctionModUpdate;
            CheckHiddenFunctionHidden.Checked = settings.UiHiddenFunctionHidden;
        }
        catch (NullReferenceException ex)
        {
            SetupConfig.Log(
                ex,
                Lang.Text("Setup.Ui.Error.ConfigReset"),
                SetupLogLevel.Msgbox,
                Lang.Text("Setup.Ui.Error.ConfigReset"));
            Reset();
        }
        catch (Exception ex)
        {
            SetupConfig.Log(
                ex,
                Lang.Text("Setup.Ui.Error.LoadFailed"),
                SetupLogLevel.Feedback,
                Lang.Text("Setup.Ui.Error.LoadFailed"));
        }
    }

    private MyRadioBox FindRadio(string name) => this.FindControl<MyRadioBox>(name)!;

    // 初始化
    public void Reset()
    {
        try
        {
            SetupConfig.ResetProperties(SetupConfig.IsUiKey);
            SetupConfig.Info("[Setup] 已初始化个性化设置！");
            HintService.Hint(Lang.Text("Setup.Ui.Initialized"), HintType.Success, false);
        }
        catch (Exception ex)
        {
            SetupConfig.Log(
                ex,
                Lang.Text("Setup.Ui.Error.InitFailed"),
                SetupLogLevel.Msgbox,
                Lang.Text("Setup.Ui.Error.InitFailed"));
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void SliderChange(object? senderRaw, bool user)
    {
        var sender = (MySlider)senderRaw!;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Value);
    }

    private void ComboChange(object? senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw!;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private void CheckBoxChange(object? senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw!;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Checked == true);
    }

    private void TextBoxChange(object? senderRaw, RoutedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw!;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Text ?? "");
    }

    private void RadioBoxChange(object? senderRaw, RouteEventArgs e)
    {
        var sender = (MyRadioBox)senderRaw!;
        var gotCfg = sender.Tag?.ToString()?.Split("/") ?? Array.Empty<string>();
        if (ModAnimation.AniControlEnabled == 0 && gotCfg.Length >= 2)
            SetByTag(gotCfg[0], int.Parse(gotCfg[1]));
    }

    private static void SetByTag(string? tag, object value)
    {
        if (tag is not null)
            SetupConfig.TrySetValue(tag, value);
    }

    private void ComboFontChange(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled == 0) SetByTag("UiFont", ComboUiFont.SelectedFontTag);
    }

    private void ComboMotdFontChange(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled == 0) SetByTag("UiMotdFont", ComboUiMotdFont.SelectedFontTag);
    }

    // 自定义主页的类型面板
    private void UiCustomType(int value)
    {
        switch (value)
        {
            case 0: // 无
            {
                PanCustomPreset.IsVisible = false;
                PanCustomLocal.IsVisible = false;
                PanCustomNet.IsVisible = false;
                HintCustom.IsVisible = false;
                HintCustomWarn.IsVisible = false;
                break;
            }
            case 1: // 本地
            {
                PanCustomPreset.IsVisible = false;
                PanCustomLocal.IsVisible = true;
                PanCustomNet.IsVisible = false;
                HintCustom.IsVisible = true;
                HintCustomWarn.IsVisible = true;
                HintCustom.Text = Lang.Text("Setup.Ui.Homepage.LocalFile.Hint");
                HintCustom.Tag = null;
                break;
            }
            case 2: // 联网
            {
                PanCustomPreset.IsVisible = false;
                PanCustomLocal.IsVisible = false;
                PanCustomNet.IsVisible = true;
                HintCustom.IsVisible = true;
                HintCustomWarn.IsVisible = true;
                HintCustom.Text = Lang.Text("Setup.Ui.Homepage.NetUpdate.Hint");
                HintCustom.Tag = ShionIssuesUrl;
                break;
            }
            case 3: // 预设
            {
                PanCustomPreset.IsVisible = true;
                PanCustomLocal.IsVisible = false;
                PanCustomNet.IsVisible = false;
                HintCustom.IsVisible = false;
                HintCustomWarn.IsVisible = false;
                break;
            }
        }

        CardCustom.TriggerForceResize();
    }

    private const string ShionIssuesUrl = "https://github.com/Mar7thLover/PCL-Shion/issues";

    // 背景图片
    private void BtnUIBgOpen_Click(object? sender, PointerReleasedEventArgs e)
    {
        Directory.CreateDirectory(SetupPaths.Pictures);
        SystemShell.OpenFolder(SetupPaths.Pictures);
    }

    private void BtnBackgroundRefresh_Click(object? sender, PointerReleasedEventArgs e)
    {
        BackgroundRefresh(true, true);
    }

    public void BackgroundRefreshUI(bool show, int count)
    {
        if (PanBackgroundOpacity is null)
            return;
        if (show)
        {
            PanBackgroundOpacity.IsVisible = true;
            PanBackgroundBlur.IsVisible = true;
            PanBackgroundSuit.IsVisible = true;
            BtnBackgroundClear.IsVisible = true;
            CheckAutoPauseVideo.IsVisible = true;
            CardBackground.Title = Lang.Text("Setup.Ui.Background.TitleWithCount", count);
        }
        else
        {
            PanBackgroundOpacity.IsVisible = false;
            PanBackgroundBlur.IsVisible = false;
            PanBackgroundSuit.IsVisible = false;
            BtnBackgroundClear.IsVisible = false;
            CheckAutoPauseVideo.IsVisible = false;
            CardBackground.Title = Lang.Text("Setup.Ui.Background.TitleDefault");
        }

        CardBackground.TriggerForceResize();
    }

    private async void BtnBackgroundClear_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (await MsgBox.ShowAsync(Lang.Text("Setup.Ui.Background.Clear.Confirm.Message"),
                Lang.Text("Common.Dialog.Warning"), button2: Lang.Text("Common.Action.Cancel"),
                isWarn: true) == 1)
        {
            DeleteDirectory(SetupPaths.Pictures);
            BackgroundRefresh(false, true);
            HintService.Hint(Lang.Text("Setup.Ui.Background.Clear.Success"), HintType.Success);
        }
    }

    /// <summary>
    ///     刷新背景图片及设置页 UI。
    /// </summary>
    /// <param name="isHint">是否显示刷新提示。</param>
    /// <param name="refresh">是否刷新图片显示。</param>
    public static void BackgroundRefresh(bool isHint, bool refresh)
    {
        try
        {
            // 获取可用的图片文件
            Directory.CreateDirectory(SetupPaths.Pictures);
            var pic = EnumerateFiles(SetupPaths.Pictures).Where(file =>
                    !(file.Extension.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
                      file.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase))).Select(file => file.FullName)
                .ToList();

            var backgroundCanvas = MainWindow.Current?.FindControl<Canvas>("ImgBack");
            // 加载
            if (pic.Count == 0)
            {
                if (refresh)
                {
                    if (backgroundCanvas is null || !backgroundCanvas.IsVisible)
                    {
                        if (isHint)
                            HintService.Hint(Lang.Text("Setup.Ui.Background.NoAvailableContent"), HintType.Error);
                    }
                    else
                    {
                        backgroundCanvas.IsVisible = false;
                        if (isHint)
                            HintService.Hint(Lang.Text("Setup.Ui.Background.Cleared"), HintType.Success);
                    }
                }

                instance?.BackgroundRefreshUI(false, 0);
            }
            else
            {
                if (refresh)
                {
                    var address = pic[Random.Shared.Next(pic.Count)];
                    try
                    {
                        if (backgroundCanvas is not null)
                        {
                            backgroundCanvas.Background = null;
                            SetupConfig.Info("[UI] 加载背景内容：" + address);
                            backgroundCanvas.Background = new ImageBrush(new Bitmap(address))
                            {
                                Stretch = SetupConfig.Settings.UiBackgroundSuit switch
                                {
                                    0 or 2 => Stretch.Uniform,
                                    3 => Stretch.Fill,
                                    _ => Stretch.None
                                }
                            };
                            backgroundCanvas.IsVisible = true;
                        }

                        if (isHint)
                            HintService.Hint(Lang.Text("Setup.Ui.Background.Refresh.Success", Path.GetFileName(address)),
                                HintType.Success, false);
                    }
                    catch (Exception ex)
                    {
                        SetupConfig.Log(ex, "[UI] 加载背景图片失败" + address);
                    }
                }

                instance?.BackgroundRefreshUI(true, pic.Count);
            }
        }
        catch (Exception ex)
        {
            SetupConfig.Log(
                ex,
                "刷新背景内容时出现未知错误",
                SetupLogLevel.Feedback,
                Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    private static IEnumerable<FileInfo> EnumerateFiles(string path)
    {
        return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.TopDirectoryOnly);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    // 顶部栏
    private async void BtnLogoChange_Click(object? sender, PointerReleasedEventArgs e)
    {
        var fileName = await SetupDialogs.SelectFileAsync(this,
            Lang.Text("Setup.Ui.ImageFile.SelectTitle"), Lang.Text("Setup.Ui.ImageFile.Filter"),
            SetupDialogs.ImagePatterns);
        if (string.IsNullOrEmpty(fileName))
            return;
        try
        {
            // 拷贝文件
            Directory.CreateDirectory(SetupPaths.DataRoot);
            File.Delete(SetupPaths.Logo);
            File.Copy(fileName, SetupPaths.Logo, true);
            // 设置当前显示
            SetTitleLogo(SetupPaths.Logo);
        }
        catch (Exception ex)
        {
            if (ex is ArgumentException or NotSupportedException)
                SetupConfig.Log(
                    ex,
                    "改变标题栏图片失败，该图片文件可能并非标准格式。",
                    SetupLogLevel.Msgbox,
                    Lang.Text("Setup.Ui.Error.TitleImageInvalidFormat"));
            else
                SetupConfig.Log(
                    ex,
                    "设置标题栏图片失败",
                    SetupLogLevel.Msgbox,
                    Lang.Text("Setup.Ui.Error.OperationFailed"));
            SetTitleLogo(null);
        }
    }

    /// <summary>
    ///     设置窗口标题栏图片，与 WPF 的 ImageTitleLogo.Source 对应。
    /// </summary>
    private static void SetTitleLogo(string? path)
    {
        var image = MainWindow.Current?.FindControl<MyImage>("ImageTitleLogo");
        if (image is null)
            return;
        image.Source = null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
        image.Source = path;
    }

    private void RadioLogoType3_Check(object? sender, RouteEventArgs e)
    {
        if (!(ModAnimation.AniControlEnabled == 0 && e.raiseByMouse))
            return;

        // 已有图片则不再选择
        if (File.Exists(SetupPaths.Logo))
        {
            try
            {
                SetTitleLogo(SetupPaths.Logo);
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException or NotSupportedException)
                    SetupConfig.Log(
                        ex,
                        "调整标题栏图片失败，该图片文件可能并非标准格式。",
                        SetupLogLevel.Msgbox,
                        Lang.Text("Setup.Ui.Error.TitleImageResizeInvalidFormat"));
                else
                    SetupConfig.Log(
                        ex,
                        "调整标题栏图片失败",
                        SetupLogLevel.Msgbox,
                        Lang.Text("Setup.Ui.Error.OperationFailed"));
                SetTitleLogo(null);
                e.handled = true;
                try
                {
                    File.Delete(SetupPaths.Logo);
                }
                catch (Exception exx)
                {
                    SetupConfig.Log(
                        exx,
                        "清理错误的标题栏图片失败",
                        SetupLogLevel.Msgbox,
                        Lang.Text("Setup.Ui.Error.OperationFailed"));
                }
            }

            return;
        }

        // 没有图片则要求选择。选择对话框是异步的，因此先取消本次选中，选好图片后再选中此项
        e.handled = true;
        _ = SelectLogoImageAsync();
    }

    private async Task SelectLogoImageAsync()
    {
        var fileName = await SetupDialogs.SelectFileAsync(this,
            Lang.Text("Setup.Ui.ImageFile.SelectTitle"), Lang.Text("Setup.Ui.ImageFile.Filter"),
            SetupDialogs.ImagePatterns);
        if (string.IsNullOrEmpty(fileName))
        {
            SetTitleLogo(null);
            return;
        }

        try
        {
            // 拷贝文件
            Directory.CreateDirectory(SetupPaths.DataRoot);
            File.Delete(SetupPaths.Logo);
            File.Copy(fileName, SetupPaths.Logo, true);
            SetTitleLogo(SetupPaths.Logo);
            RadioLogoType3.SetChecked(true, true);
        }
        catch (Exception ex)
        {
            SetupConfig.Log(
                ex,
                "复制标题栏图片失败",
                SetupLogLevel.Msgbox,
                Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    private void BtnLogoDelete_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            File.Delete(SetupPaths.Logo);
            RadioLogoType1.SetChecked(true, true);
            HintService.Hint(Lang.Text("Setup.Ui.Logo.Clear.Success"), HintType.Success);
        }
        catch (Exception ex)
        {
            SetupConfig.Log(
                ex,
                "清空标题栏图片失败",
                SetupLogLevel.Msgbox,
                Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    // 背景音乐
    private void BtnMusicOpen_Click(object? sender, PointerReleasedEventArgs e)
    {
        Directory.CreateDirectory(SetupPaths.Musics);
        SystemShell.OpenFolder(SetupPaths.Musics);
    }

    private void BtnMusicRefresh_Click(object? sender, PointerReleasedEventArgs e)
    {
        MusicRefreshUI();
    }

    public void MusicRefreshUI()
    {
        if (PanBackgroundOpacity is null)
            return;
        var musicCount = Directory.Exists(SetupPaths.Musics)
            ? EnumerateFiles(SetupPaths.Musics).Count(file =>
                SetupDialogs.AudioPatterns.Any(pattern => file.Name.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)))
            : 0;
        if (musicCount > 0)
        {
            PanMusicVolume.IsVisible = true;
            PanMusicDetail.IsVisible = true;
            BtnMusicClear.IsVisible = true;
            CardMusic.Title = Lang.Text("Setup.Ui.Music.TitleWithCount", musicCount);
        }
        else
        {
            PanMusicVolume.IsVisible = false;
            PanMusicDetail.IsVisible = false;
            BtnMusicClear.IsVisible = false;
            CardMusic.Title = Lang.Text("Setup.Ui.Music.Title");
        }

        CardMusic.TriggerForceResize();
    }

    private async void BtnMusicClear_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (await MsgBox.ShowAsync(Lang.Text("Setup.Ui.Music.Clear.Confirm.Message"),
                Lang.Text("Common.Dialog.Warning"), button2: Lang.Text("Common.Action.Cancel"),
                isWarn: true) != 1)
            return;
        // 删除文件
        try
        {
            await Task.Run(() => DeleteDirectory(SetupPaths.Musics));
            HintService.Hint(Lang.Text("Setup.Ui.Music.Delete.Success"), HintType.Success);
        }
        catch (Exception ex)
        {
            SetupConfig.Log(
                ex,
                "删除背景音乐失败",
                SetupLogLevel.Msgbox,
                Lang.Text("Setup.Ui.Error.OperationFailed"));
        }

        try
        {
            Directory.CreateDirectory(SetupPaths.Musics);
        }
        catch (Exception ex)
        {
            SetupConfig.Log(
                ex,
                "重建背景音乐文件夹失败",
                SetupLogLevel.Msgbox,
                Lang.Text("Setup.Ui.Error.OperationFailed"));
        }

        MusicRefreshUI();
    }

    private void CheckMusicStart_Change(object? sender, bool user)
    {
        CheckBoxChange(sender, user);
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (CheckMusicStart.Checked == true)
            CheckMusicStop.Checked = false;
    }

    private void CheckMusicStop_Change()
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (CheckMusicStop.Checked == true)
            CheckMusicStart.Checked = false;
    }

    // 主页

    private void BtnCustomRefresh_Click(object? sender, PointerReleasedEventArgs e)
    {
        PageLaunchRight.Instance.ForceRefresh();
        HintService.Hint(Lang.Text("Setup.Ui.Homepage.Refresh.Success"), HintType.Success);
    }

    private void BtnCustomTutorial_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenUrl("https://docs.pclc.cc/ce/customization/xaml-format");
    }

    // 主题
    private void ThemeColor_Change(object? senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw!;
        if (sender.SelectedIndex < 0 || sender.SelectedIndex >= selectableThemes.Length)
            return;
        SetByTag(sender.Tag?.ToString(), (int)selectableThemes[sender.SelectedIndex]);
        ThemeCustomRefresh();
        ThemeManager.ThemeRefresh();
    }

    // 滑动条
    private void SliderLoad()
    {
        SliderMusicVolume.getHintText = new Func<object, object>(v =>
            Lang.Number(Math.Ceiling(Convert.ToDouble(v) * 0.1d) / 100d, "P0"));
        SliderLauncherOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number(Math.Round(40 + Convert.ToDouble(v) * 0.1d) / 100d, "P0"));
        SliderBackgroundOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number(Math.Round(Convert.ToDouble(v) * 0.1d) / 100d, "P0"));
        SliderBackgroundBlur.getHintText = new Func<object, object>(v => Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderBlurValue.getHintText = new Func<object, object>(v => Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderBlurSamplingRate.getHintText = new Func<object, object>(v => Lang.Number(Convert.ToDouble(v) / 100d, "P0"));
        SliderThemeCustomHue.getHintText = new Func<object, object>(v => Lang.Text("Setup.Ui.Slider.Degree", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderThemeCustomSaturation.getHintText = new Func<object, object>(v => Lang.Number(Convert.ToDouble(v) / 100d, "P0"));
    }

    private void CheckMusicStart_OnChange(object? sender, bool user)
    {
        CheckBoxChange(sender, user);
        CheckMusicStart_Change(sender, user);
    }

    private void CheckMusicStop_OnChange(object? sender, bool user)
    {
        CheckBoxChange(sender, user);
        CheckMusicStop_Change();
    }

    #region 功能隐藏

    /// <summary>
    ///     是否强制显示被禁用的功能。
    /// </summary>
    public static bool HiddenForceShow
    {
        get => hiddenForceShow;
        set
        {
            hiddenForceShow = value;
            HiddenRefresh();
        }
    }

    /// <summary>
    ///     更新功能隐藏带来的显示变化。
    /// </summary>
    public static void HiddenRefresh()
    {
        var mainWindow = MainWindow.Current;
        var panTitleSelect = mainWindow?.FindControl<Control>("PanTitleSelect");
        if (panTitleSelect is null || !panTitleSelect.IsAttachedToVisualTree())
            return;
        try
        {
            // 获取配置组引用以缩短代码
            var conf = SetupConfig.Settings;

            // 顶部栏：下载、设置、工具
            var isAllTitleHidden = !HiddenForceShow && conf.UiHiddenPageDownload && conf.UiHiddenPageSetup && conf.UiHiddenPageTools;

            if (isAllTitleHidden)
            {
                panTitleSelect.IsVisible = false;
            }
            else
            {
                panTitleSelect.IsVisible = true;
                mainWindow!.FindControl<Control>("BtnTitleSelect1")!.IsVisible =
                    !(!HiddenForceShow && conf.UiHiddenPageDownload);
                mainWindow.FindControl<Control>("BtnTitleSelect2")!.IsVisible =
                    !(!HiddenForceShow && conf.UiHiddenPageSetup);
                mainWindow.FindControl<Control>("BtnTitleSelect3")!.IsVisible =
                    !(!HiddenForceShow && conf.UiHiddenPageTools);
            }

            // 功能隐藏设置卡片
            if (instance is not null)
            {
                instance.CardSwitch.IsVisible = !(!HiddenForceShow && conf.UiHiddenFunctionHidden);
                instance.CardSwitch.Title = HiddenForceShow
                    ? Lang.Text("Setup.Ui.FeatureHide.TitleTemporarilyDisabled")
                    : Lang.Text("Setup.Ui.FeatureHide.Title");
            }

            // 设置、工具、首页与其余页面的侧栏项目由各自页面订阅此事件后刷新
            HiddenChanged?.Invoke();
        }
        catch (Exception ex)
        {
            SetupConfig.Log(
                ex,
                "刷新功能隐藏项目失败",
                SetupLogLevel.Feedback,
                Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    // ================= 设置页面协同 =================
    private void HiddenSetupMain()
    {
        var isChecked = CheckHiddenPageSetup.Checked == true;
        CheckHiddenSetupLaunch.Checked = isChecked;
        CheckHiddenSetupUI.Checked = isChecked;
        CheckHiddenSetupLauncherLanguage.Checked = isChecked;
        CheckHiddenSetupGameManage.Checked = isChecked;
        CheckHiddenLauncherMisc.Checked = isChecked;
        CheckHiddenSetupJava.Checked = isChecked;
        CheckHiddenSetupUpdate.Checked = isChecked;
        CheckHiddenSetupGameLink.Checked = isChecked;
        CheckHiddenSetupAbout.Checked = isChecked;
        CheckHiddenSetupFeedback.Checked = isChecked;
        CheckHiddenSetupLog.Checked = isChecked;
    }

    // ================= 设置页面协同 =================
    private void HiddenSetupMain(object? sender, bool user)
    {
        if (!user)
            return; // 仅处理用户点击，防止死循环
        var isChecked = CheckHiddenPageSetup.Checked == true;
        CheckHiddenSetupLaunch.Checked = isChecked;
        CheckHiddenSetupUI.Checked = isChecked;
        CheckHiddenSetupLauncherLanguage.Checked = isChecked;
        CheckHiddenSetupGameManage.Checked = isChecked;
        CheckHiddenLauncherMisc.Checked = isChecked;
        CheckHiddenSetupJava.Checked = isChecked;
        CheckHiddenSetupUpdate.Checked = isChecked;
        CheckHiddenSetupGameLink.Checked = isChecked;
        CheckHiddenSetupAbout.Checked = isChecked;
        CheckHiddenSetupFeedback.Checked = isChecked;
        CheckHiddenSetupLog.Checked = isChecked;
    }

    private void HiddenSetupSub(object? sender, bool user)
    {
        if (!user)
            return;
        var conf = SetupConfig.Settings;
        // 判断是否全部勾选
        var allChecked = conf.UiHiddenSetupLaunch && conf.UiHiddenSetupUi && conf.UiHiddenSetupLauncherLanguage && conf.UiHiddenSetupJava &&
                         conf.UiHiddenSetupUpdate && conf.UiHiddenSetupGameLink && conf.UiHiddenSetupAbout && conf.UiHiddenSetupFeedback &&
                         conf.UiHiddenSetupLog && conf.UiHiddenSetupLauncherMisc && conf.UiHiddenSetupGameManage;
        CheckHiddenPageSetup.Checked = allChecked;
    }

    // ================= 工具页面协同 =================
    private void HiddenToolsMain(object? sender, bool user)
    {
        if (!user)
            return;
        var isChecked = CheckHiddenPageTools.Checked == true;
        CheckHiddenToolsGameLink.Checked = isChecked;
        CheckHiddenToolsTest.Checked = isChecked;
    }

    private void HiddenToolsSub(object? sender, bool user)
    {
        if (!user)
            return;
        var conf = SetupConfig.Settings;
        var allChecked = conf.UiHiddenToolsGameLink && conf.UiHiddenToolsTest;
        CheckHiddenPageTools.Checked = allChecked;
    }

    // 警告提示
    private void HiddenHint(object? sender, bool user)
    {
        if (ModAnimation.AniControlEnabled == 0 && sender is MyCheckBox checkBox && checkBox.Checked == true)
            HintService.Hint(Lang.Text("Setup.Ui.FeatureHide.TemporaryHint"));
    }

    #endregion
}
