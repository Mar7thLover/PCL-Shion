using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Shion.Localization;
using PCL.Shion.Themes;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Setup;

/// <summary>
///     设置项变化后的界面应用，对应 WPF 版 <c>PCL/Modules/Base/ModSetup.cs</c> 中的界面部分。
///     方法名与配置键名相同：<see cref="TryApply"/> 会按键名反射调用同名方法，因此新增的界面设置只需在此添加同名方法即可。
///     所有方法都必须在 UI 线程上执行，<see cref="TryApply"/> 与 <see cref="ApplyAll"/> 会自行切换线程。
/// </summary>
public static class ModSetup
{
    // 与 PageSetupUI 中的反馈地址保持一致
    private const string ShionIssuesUrl = "https://github.com/Mar7thLover/PCL-Shion/issues";

    /// <summary>
    ///     按配置键名应用设置。存在同名的一参数方法时调用它并返回 true，否则返回 false。
    ///     对应 WPF 中 ConfigService 观察者通过反射调用 ModSetup 同名方法的机制。
    /// </summary>
    public static bool TryApply(string key, object? value)
    {
        var method = typeof(ModSetup).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == key && m.GetParameters().Length == 1);
        if (method is null)
            return false;

        var parameterType = method.GetParameters()[0].ParameterType;
        RunOnUi(() =>
        {
            try
            {
                method.Invoke(null, [ConvertArgument(value, parameterType)]);
            }
            catch (Exception ex)
            {
                SetupConfig.Log(ex is TargetInvocationException { InnerException: { } inner } ? inner : ex,
                    "[UI] 应用设置失败：" + key);
            }
        });
        return true;
    }

    /// <summary>
    ///     主动应用所有当前界面设置。对应 WPF 的 ModSetup.ApplyAll，在主窗口打开后调用；窗口不存在时直接返回。
    /// </summary>
    public static void ApplyAll()
    {
        RunOnUi(ApplyAllCore);
    }

    private static void ApplyAllCore()
    {
        if (MainWindow.Current is null)
            return;

        var settings = App.Services.Settings;
        var extra = SetupExtraSettings.Current;
        SyncThemeSettings();

        // 启动器
        Safe("启动器透明度", () => UiLauncherTransparent(extra.UiLauncherTransparent));
        Safe("启动器主题", () => UiLauncherTheme((int)ThemeService.CurrentTheme));
        Safe("彩色背景", () => UiBackgroundColorful(settings.BackgroundColorful));
        Safe("锁定窗口大小", () => UiLockWindowSize(extra.UiLockWindowSize));

        // 背景图片：启动时页面尚未创建，需要主动加载一次图片，否则窗口没有背景
        Safe("背景图片", () => PageSetupUI.BackgroundRefresh(false, true));
        Safe("背景不透明度", () => UiBackgroundOpacity(extra.UiBackgroundOpacity));
        Safe("背景模糊", () => UiBackgroundBlur(extra.UiBackgroundBlur));
        Safe("背景布局", () => UiBackgroundSuit(settings.UiBackgroundSuit));

        // 字体
        Safe("字体", () => UiFont(extra.UiFont));

        // 主页
        Safe("自定义主页", () => UiCustomType(settings.UiCustomType));

        // 高级材质
        Safe("高级材质", () =>
        {
            if (extra.UiBlur)
            {
                UiBlurValue(extra.UiBlurValue);
                UiBlurSamplingRate(extra.UiBlurSamplingRate);
            }
            else
            {
                UiBlurValue(0);
            }

            UiBlur(extra.UiBlur);
        });

        // 顶部栏
        Safe("标题栏类型", () => UiLogoType(extra.UiLogoType));
        Safe("标题栏文本", () => UiLogoText(extra.UiLogoText));
        Safe("标题栏左对齐", () => UiLogoLeft(extra.UiLogoLeft));

        // 功能隐藏
        Safe("功能隐藏", PageSetupUI.HiddenRefresh);

        // 系统
        Safe("调试模式", () => SystemDebugMode(extra.SystemDebugMode));
        Safe("动画速度", () => SystemDebugAnim(extra.SystemDebugAnim));

        // 启动
        Safe("内存分配", () => LaunchRamType(settings.LaunchRamType));

        Safe("主题刷新", () => ThemeManager.ThemeRefresh());
    }

    #region Launch

    // 游戏内存
    public static void LaunchRamType(int type)
    {
        PageSetupLeft.frmSetupLaunch?.RamType(type);
    }

    #endregion

    #region 启动器

    /// <summary>
    ///     窗口透明度。对应 WPF 的 frmMain.Opacity，取值范围 0.4 到 1.0。
    /// </summary>
    public static void UiLauncherTransparent(int value)
    {
        if (MainWindow.Current is not { } window)
            return;
        window.Opacity = value / 1000d + 0.4d;
    }

    public static void UiLauncherTheme(int value)
    {
        SyncThemeSettings();
        ThemeManager.ThemeRefresh(value);
    }

    public static void UiBackgroundColorful(bool value)
    {
        SyncThemeSettings();
        ThemeManager.ThemeRefresh();
    }

    public static void UiLockWindowSize(bool value)
    {
        if (MainWindow.Current is not { } window)
            return;
        if (value)
            window.RemoveResizer();
        else
            window.AddResizer();
    }

    /// <summary>
    ///     配色模式（亮 / 暗 / 跟随系统）。切换后由主题服务重新计算配色。
    /// </summary>
    public static void ColorMode(int value)
    {
        SyncThemeSettings();
        ThemeService.RefreshColorMode();
    }

    /// <summary>
    ///     亮色模式下的配色主题。
    /// </summary>
    public static void LightColor(int value) => RefreshThemeColors();

    /// <summary>
    ///     暗色模式下的配色主题。
    /// </summary>
    public static void DarkColor(int value) => RefreshThemeColors();

    private static void RefreshThemeColors()
    {
        SyncThemeSettings();
        ThemeService.ApplyColorResources();
        ThemeManager.ThemeRefresh();
    }

    #endregion

    #region 背景图片

    /// <summary>
    ///     背景图片的不透明度，取值 0 到 1000。
    /// </summary>
    public static void UiBackgroundOpacity(int value)
    {
        if (MainWindow.Current is not { } window)
            return;
        window.ImgBack.Opacity = value / 1000d;
    }

    public static void UiBackgroundBlur(int value)
    {
        if (MainWindow.Current is not { } window)
            return;
        // TODO(shion): Avalonia 12 没有 WPF 的 BlurEffect，背景模糊暂不生效，这里只保留 WPF 中的边距补偿。
        window.ImgBack.Margin = new Thickness(-(value + 1) / 1.8d);
    }

    /// <summary>
    ///     背景图片的排布方式，与 WPF 的 WallpaperSuitMode 对应：0 智能，1 居中，2 适应，3 拉伸，4 平铺，5 左上，6 右上，7 左下，8 右下。
    /// </summary>
    public static void UiBackgroundSuit(int value)
    {
        if (MainWindow.Current is not { } window)
            return;
        var canvas = window.ImgBack;
        if (canvas.Background is not ImageBrush brush || brush.Source is not IImage image)
            return;

        var size = image.Size;
        if (value == 0)
        {
            // 智能：图片较小时平铺，较大时适应
            var area = window.PanMain.Bounds;
            value = size.Width < area.Width / 2d && size.Height < area.Height / 2d ? 4 : 2;
        }

        brush.TileMode = TileMode.None;
        brush.DestinationRect = new RelativeRect(0d, 0d, 1d, 1d, RelativeUnit.Relative);
        switch (value)
        {
            case 1: // 居中
                Place(canvas, HorizontalAlignment.Center, VerticalAlignment.Center, size);
                brush.Stretch = Stretch.None;
                break;
            case 2: // 适应
                Place(canvas, HorizontalAlignment.Stretch, VerticalAlignment.Stretch, null);
                brush.Stretch = Stretch.UniformToFill;
                break;
            case 3: // 拉伸
                Place(canvas, HorizontalAlignment.Stretch, VerticalAlignment.Stretch, null);
                brush.Stretch = Stretch.Fill;
                break;
            case 4: // 平铺
                Place(canvas, HorizontalAlignment.Stretch, VerticalAlignment.Stretch, null);
                brush.Stretch = Stretch.None;
                brush.TileMode = TileMode.Tile;
                brush.DestinationRect = new RelativeRect(0d, 0d, size.Width, size.Height, RelativeUnit.Absolute);
                break;
            case 5: // 左上
                Place(canvas, HorizontalAlignment.Left, VerticalAlignment.Top, size);
                brush.Stretch = Stretch.None;
                break;
            case 6: // 右上
                Place(canvas, HorizontalAlignment.Right, VerticalAlignment.Top, size);
                brush.Stretch = Stretch.None;
                break;
            case 7: // 左下
                Place(canvas, HorizontalAlignment.Left, VerticalAlignment.Bottom, size);
                brush.Stretch = Stretch.None;
                break;
            case 8: // 右下
                Place(canvas, HorizontalAlignment.Right, VerticalAlignment.Bottom, size);
                brush.Stretch = Stretch.None;
                break;
        }
    }

    /// <summary>
    ///     设置背景画布的对齐方式与尺寸。natural 为 null 时尺寸交给布局决定。
    /// </summary>
    private static void Place(Canvas canvas, HorizontalAlignment horizontal, VerticalAlignment vertical, Size? natural)
    {
        canvas.HorizontalAlignment = horizontal;
        canvas.VerticalAlignment = vertical;
        canvas.Width = natural?.Width ?? double.NaN;
        canvas.Height = natural?.Height ?? double.NaN;
    }

    #endregion

    #region 字体

    /// <summary>
    ///     启动器字体。空值表示使用默认字体。
    /// </summary>
    public static void UiFont(string value)
    {
        try
        {
            LocalizationFontService.ApplyLaunchFont(string.IsNullOrWhiteSpace(value) ? null : value);
        }
        catch (Exception ex)
        {
            SetupConfig.Log(ex, "字体加载失败", SetupLogLevel.Hint, Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    #endregion

    #region 主页

    /// <summary>
    ///     自定义主页类型：0 无，1 本地，2 联网，3 预设。切换后显示对应的设置面板。
    /// </summary>
    public static void UiCustomType(int value)
    {
        var page = PageSetupUI.Instance;
        if (page is null)
            return;
        switch (value)
        {
            case 0: // 无
                page.PanCustomPreset.IsVisible = false;
                page.PanCustomLocal.IsVisible = false;
                page.PanCustomNet.IsVisible = false;
                page.HintCustom.IsVisible = false;
                page.HintCustomWarn.IsVisible = false;
                break;
            case 1: // 本地
                page.PanCustomPreset.IsVisible = false;
                page.PanCustomLocal.IsVisible = true;
                page.PanCustomNet.IsVisible = false;
                page.HintCustom.IsVisible = true;
                page.HintCustomWarn.IsVisible = true;
                page.HintCustom.Text = Lang.Text("Setup.Ui.Homepage.LocalFile.Hint");
                page.HintCustom.Tag = null;
                break;
            case 2: // 联网
                page.PanCustomPreset.IsVisible = false;
                page.PanCustomLocal.IsVisible = false;
                page.PanCustomNet.IsVisible = true;
                page.HintCustom.IsVisible = true;
                page.HintCustomWarn.IsVisible = true;
                page.HintCustom.Text = Lang.Text("Setup.Ui.Homepage.NetUpdate.Hint");
                page.HintCustom.Tag = ShionIssuesUrl;
                break;
            case 3: // 预设
                page.PanCustomPreset.IsVisible = true;
                page.PanCustomLocal.IsVisible = false;
                page.PanCustomNet.IsVisible = false;
                page.HintCustom.IsVisible = false;
                page.HintCustomWarn.IsVisible = false;
                break;
        }

        page.CardCustom.TriggerForceResize();
    }

    #endregion

    #region 高级材质

    /// <summary>
    ///     是否启用高级材质。关闭时模糊半径为 0。
    /// </summary>
    public static void UiBlur(bool value)
    {
        var page = PageSetupUI.Instance;
        if (page is not null)
            page.PanBlurValue.IsVisible = value;
        UiBlurValue(value ? SetupExtraSettings.Current.UiBlurValue : 0);
    }

    /// <summary>
    ///     模糊半径。写入 BlurRadius 资源，目前 Shion 中尚无读取该资源的模糊效果。
    /// </summary>
    public static void UiBlurValue(int value)
    {
        // TODO(shion): 模糊效果尚未在 Shion 中实现，资源仅保留数值以便后续接入
        if (Application.Current is { } app)
            app.Resources["BlurRadius"] = value * 1.0d;
    }

    /// <summary>
    ///     模糊采样率，取值 0 到 100，对应资源 BlurSamplingRate 的 0 到 1。
    /// </summary>
    public static void UiBlurSamplingRate(int value)
    {
        // TODO(shion): 同 UiBlurValue，模糊采样尚未实现。WPF 的 KernelType 在 Shion 中没有对应类型，故 UiBlurType 暂不移植。
        if (Application.Current is { } app)
            app.Resources["BlurSamplingRate"] = value * 0.01d;
    }

    #endregion

    #region 顶部栏

    /// <summary>
    ///     标题栏类型：0 无，1 默认 Logo，2 文本，3 图片，4 HMCL 愚人节样式。
    /// </summary>
    public static void UiLogoType(int value)
    {
        // 主题为 HMCL 时强制使用愚人节样式
        if (ThemeService.CurrentTheme == ColorTheme.HmclBlue)
            value = 4;

        if (MainWindow.Current is not { } window)
            return;
        var page = PageSetupUI.Instance;

        switch (value)
        {
            case 0: // 无
                SetTitleLogo(window, shape: false, help: false, hmcl: false, label: false, image: false, badge: false);
                SetLogoPanels(page, left: true, text: false, change: false);
                break;
            case 1: // 默认
                SetTitleLogo(window, shape: true, help: false, hmcl: false, label: false, image: false, badge: true);
                SetLogoPanels(page, left: false, text: false, change: false);
                break;
            case 2: // 文本
                SetTitleLogo(window, shape: false, help: false, hmcl: false, label: true, image: false, badge: true);
                SetLogoPanels(page, left: false, text: true, change: false);
                break;
            case 3: // 图片
                SetTitleLogo(window, shape: false, help: false, hmcl: false, label: false, image: true, badge: true);
                SetLogoPanels(page, left: false, text: false, change: true);
                try
                {
                    window.ImageTitleLogo.Source = null; // 防止 Source 前后相同时不刷新
                    window.ImageTitleLogo.Source = SetupPaths.Logo;
                }
                catch (Exception ex)
                {
                    window.ImageTitleLogo.Source = null;
                    SetupConfig.Log(ex, "显示标题栏图片失败", SetupLogLevel.Msgbox,
                        Lang.Text("Setup.Ui.Error.OperationFailed"));
                }

                break;
            case 4: // HMCL（愚人节）
                SetTitleLogo(window, shape: false, help: true, hmcl: true, label: false, image: false, badge: null);
                SetLogoPanels(page, left: false, text: false, change: false);
                break;
        }

        UiLogoLeft(SetupExtraSettings.Current.UiLogoLeft);
        page?.CardLogo.TriggerForceResize();
    }

    /// <summary>
    ///     标题栏文本，仅在标题栏类型为文本时显示。
    /// </summary>
    public static void UiLogoText(string value)
    {
        if (MainWindow.Current is not { } window)
            return;
        window.LabTitleLogo.Text = value;
    }

    /// <summary>
    ///     标题栏内容是否靠左对齐。仅在没有 Logo 时生效，此时左侧列宽为 0。
    /// </summary>
    public static void UiLogoLeft(bool value)
    {
        if (MainWindow.Current is not { } window)
            return;
        var noLogo = SetupExtraSettings.Current.UiLogoType == 0;
        window.PanTitleMain.ColumnDefinitions[0].Width = new GridLength(value && noLogo ? 0 : 1, GridUnitType.Star);
    }

    /// <summary>
    ///     设置标题栏各个 Logo 元素的显示状态。badge 为 null 时保持 CELogo 的当前状态（与 WPF 的 HMCL 分支一致）。
    /// </summary>
    private static void SetTitleLogo(MainWindow window, bool shape, bool help, bool hmcl, bool label, bool image,
        bool? badge)
    {
        window.ShapeTitleLogo.IsVisible = shape;
        window.BtnTitleHelp.IsVisible = help;
        window.ShapeHMCLTitleLogo.IsVisible = hmcl;
        window.LabTitleLogo.IsVisible = label;
        window.ImageTitleLogo.IsVisible = image;
        if (badge is { } visible)
            window.CELogo.IsVisible = visible;
    }

    /// <summary>
    ///     设置页中与标题栏相关的面板显示状态。页面尚未创建时不做任何事。
    /// </summary>
    private static void SetLogoPanels(PageSetupUI? page, bool left, bool text, bool change)
    {
        if (page is null)
            return;
        page.CheckLogoLeft.IsVisible = left;
        page.PanLogoText.IsVisible = text;
        page.PanLogoChange.IsVisible = change;
    }

    #endregion

    #region 系统

    /// <summary>
    ///     调试模式。WPF 中可在运行时切换，Shion 中目前只能由 --debug 启动参数开启（HomepageSupport.ModeDebug 只读），故暂不切换。
    /// </summary>
    public static void SystemDebugMode(bool value)
    {
        // TODO(shion): HomepageSupport.ModeDebug 为只读属性，需要改为可写后才能在运行时切换
    }

    /// <summary>
    ///     动画速度。对应 WPF 的 ModAnimation.aniSpeed。
    /// </summary>
    public static void SystemDebugAnim(int value)
    {
        ModAnimation.aniSpeed = value >= 30
            ? 200d
            : Math.Clamp(value * 0.1d + 0.1d, 0.1d, 3d);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    ///     在 UI 线程上执行。已经在 UI 线程时立即执行，否则投递到 UI 线程。
    /// </summary>
    private static void RunOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    /// <summary>
    ///     单个设置应用失败时只记录日志，不影响其他设置。
    /// </summary>
    private static void Safe(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            SetupConfig.Log(ex, "[UI] 应用设置失败：" + name);
        }
    }

    /// <summary>
    ///     让主题服务使用与设置页相同的 AppSettings 实例。否则 ThemeService 读取的是另行加载的副本，改动无法生效。
    /// </summary>
    private static void SyncThemeSettings()
    {
        ThemeService.Settings = App.Services.Settings;
    }

    /// <summary>
    ///     将配置值转换为方法参数类型。
    /// </summary>
    private static object? ConvertArgument(object? value, Type type)
    {
        if (value is null)
            return null;
        if (type.IsInstanceOfType(value))
            return value;
        var target = Nullable.GetUnderlyingType(type) ?? type;
        return target.IsEnum
            ? Enum.ToObject(target, value)
            : Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }

    #endregion
}
