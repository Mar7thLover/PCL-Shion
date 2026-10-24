using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Setup;

public partial class PageSetupLauncherLanguage : MyPageRight
{
    private bool _isLoaded;

    public PageSetupLauncherLanguage()
    {
        InitializeComponent();
        PanScroll = PanBack; // 不知道为啥不能在 XAML 设置
        Loaded += PageSetupLauncherLanguage_Loaded;
        BtnTranslateWebsite.Click += (_, _) => SystemShell.OpenUrl("https://translate.pclc.cc/projects/pcl-ce/");
        BtnSubmitPullRequest.Click += (_, _) => SystemShell.OpenUrl("https://github.com/Mar7thLover/PCL-Shion/pulls");
    }

    private void PageSetupLauncherLanguage_Loaded(object? sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 非重复加载部分
        if (_isLoaded)
            return;
        _isLoaded = true;

        ModAnimation.AniControlEnabled += 1;
        Reload();
        ModAnimation.AniControlEnabled -= 1;
    }

    public void Reload()
    {
        ModAnimation.AniControlEnabled += 1;
        try
        {
            ReloadLanguageCombo();
            ReloadFormatCultureCombo();
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    public void Reset()
    {
        try
        {
            SetupConfig.ResetProperties(name => name is "UiLanguage" or "UiFormatCulture");
            ApplyLocalization();
            SetupConfig.Info("[Setup] 已初始化启动器-语言页设置");
            HintService.Hint(Lang.Text("Setup.Language.Reset.Success"), HintType.Success, false);
            Reload();
        }
        catch (Exception ex)
        {
            SetupConfig.Log(
                ex,
                "初始化启动器-语言页设置失败",
                SetupLogLevel.Msgbox,
                Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     按设置中的语言与格式区域应用界面语言，与 WPF 的 LocalizationService.ApplyFromConfig 对应。
    /// </summary>
    private static void ApplyLocalization()
    {
        var settings = SetupConfig.Settings;
        LocalizationService.Apply(settings.UiLanguage, settings.UiFormatCulture);
    }

    private void ReloadLanguageCombo()
    {
        ComboUiLanguage.Items.Clear();
        var autoLanguage = LocalizationService.ResolveLanguage(LocalizationService.Auto);
        ComboUiLanguage.Items.Add(CreateLanguageComboItem(
            Lang.Text("Setup.Language.UiLanguage.Auto", GetLanguageDisplay(autoLanguage)),
            LocalizationService.Auto,
            autoLanguage));

        foreach (var language in LocalizationService.SupportedLanguages)
            ComboUiLanguage.Items.Add(CreateLanguageComboItem(
                GetLanguageDisplay(language),
                language.Code,
                language));

        var configValue = NormalizeConfigValue(SetupConfig.Settings.UiLanguage);
        var selectedLanguageTag = LocalizationService.Auto;
        if (LocalizationService.IsLanguageSupported(configValue))
            selectedLanguageTag =
                string.Equals(configValue, LocalizationService.Auto, StringComparison.OrdinalIgnoreCase)
                    ? LocalizationService.Auto
                    : LocalizationService.ResolveLanguage(configValue).Code;
        SelectComboItem(ComboUiLanguage, selectedLanguageTag);
    }

    private void ReloadFormatCultureCombo()
    {
        ComboUiFormatCulture.Items.Clear();
        ComboUiFormatCulture.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Setup.Language.FormatCulture.Auto"),
            Tag = LocalizationService.Auto
        });
        ComboUiFormatCulture.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Setup.Language.FormatCulture.FollowLanguage"),
            Tag = LocalizationService.FormatCultureFollowLanguage
        });

        foreach (var culture in GetBuiltInFormatCultures())
            ComboUiFormatCulture.Items.Add(new MyComboBoxItem
            {
                Content = GetCultureDisplay(culture),
                Tag = culture.Name
            });

        var configValue = NormalizeConfigValue(SetupConfig.Settings.UiFormatCulture);
        if (!IsFormatCultureItemExisting(configValue) && TryGetCulture(configValue, out var customCulture))
            ComboUiFormatCulture.Items.Add(new MyComboBoxItem
            {
                Content = GetCultureDisplay(customCulture),
                Tag = customCulture.Name
            });

        SelectComboItem(ComboUiFormatCulture,
            IsFormatCultureItemExisting(configValue) ? configValue : LocalizationService.Auto);
    }

    private void ComboUiLanguage_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (ComboUiLanguage.SelectedItem is not MyComboBoxItem item)
            return;

        var value = item.Tag?.ToString() ?? LocalizationService.Auto;
        if (SetupConfig.Settings.UiLanguage == value)
            return;

        SetupConfig.TrySetValue(nameof(AppSettings.UiLanguage), value);
        ApplyLocalization();
        HintService.Hint(Lang.Text("Setup.Language.Changed"), HintType.Success, false);
        Reload();
    }

    private void ComboUiFormatCulture_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (ComboUiFormatCulture.SelectedItem is not MyComboBoxItem item)
            return;

        var value = item.Tag?.ToString() ?? LocalizationService.Auto;
        if (SetupConfig.Settings.UiFormatCulture == value)
            return;

        SetupConfig.TrySetValue(nameof(AppSettings.UiFormatCulture), value);
        ApplyLocalization();
        HintService.Hint(Lang.Text("Setup.Language.Changed"), HintType.Success, false);
        Reload();
    }

    private static IEnumerable<CultureInfo> GetBuiltInFormatCultures()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in LocalizationService.SupportedLanguages)
        {
            var culture = CultureInfo.GetCultureInfo(language.CultureName);
            if (used.Add(culture.Name)) yield return culture;
        }
    }

    private static MyComboBoxItem CreateLanguageComboItem(string content, string tag, LocalizationLanguage language)
    {
        return new MyComboBoxItem
        {
            Content = content,
            Tag = tag,
            FontFamily = LocalizationFontService.BuildRepresentativeFontFamily(language)
        };
    }

    private static string GetLanguageDisplay(LocalizationLanguage language)
    {
        return $"{language.NativeName}";
    }

    private static string GetCultureDisplay(CultureInfo culture)
    {
        return $"{culture.NativeName}";
    }

    private static string NormalizeConfigValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? LocalizationService.Auto : value;
    }

    private static bool TryGetCulture(string value, out CultureInfo culture)
    {
        try
        {
            culture = CultureInfo.GetCultureInfo(value);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
            return false;
        }
    }

    private static void SelectComboItem(MyComboBox comboBox, string tag)
    {
        foreach (var item in comboBox.Items.OfType<MyComboBoxItem>())
        {
            if (!string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) continue;
            comboBox.SelectedItem = item;
            return;
        }

        comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
    }

    private bool IsFormatCultureItemExisting(string tag)
    {
        return ComboUiFormatCulture.Items.OfType<MyComboBoxItem>()
            .Any(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase));
    }
}
