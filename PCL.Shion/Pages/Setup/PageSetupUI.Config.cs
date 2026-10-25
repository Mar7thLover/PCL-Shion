using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Platform.Storage;
using FluentValidation;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Services;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Setup;

/// <summary>
///     设置页用到的本地数据目录，与 WPF 的 <c>ModBase.exePath + @"PCL\..."</c> 对应。
/// </summary>
internal static class SetupPaths
{
    public static string DataRoot { get; } = Path.Combine(
        SpecialFolders.Get(Environment.SpecialFolder.ApplicationData), "PCL Shion");

    public static string Pictures => Path.Combine(DataRoot, "Pictures");

    public static string Musics => Path.Combine(DataRoot, "Musics");

    public static string Logo => Path.Combine(DataRoot, "Logo.png");

    public static string Logs => Path.Combine(DataRoot, "Logs");
}

/// <summary>
///     设置页共用的配置读写。与 WPF 的 ConfigService 对应：按配置键名（即控件的 Tag）读写 <see cref="AppSettings"/> 的属性。
/// </summary>
internal static class SetupConfig
{
    public static AppSettings Settings => App.Services.Settings;

    /// <summary>
    ///     设置文件的位置，与 <see cref="AppSettings"/> 保存的位置相同。
    /// </summary>
    public static string SettingsFilePath => Path.Combine(SetupPaths.DataRoot, "settings.json");

    /// <summary>
    ///     按配置键名写入设置并保存。键名不存在时返回 false。
    /// </summary>
    public static bool TrySetValue(string tag, object value)
    {
        var property = typeof(AppSettings).GetProperty(tag, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !property.CanWrite)
        {
            if (!SetupExtraSettings.Contains(tag))
                return false;
            SetupExtraSettings.Current.SetValue(tag, value);
            ModSetup.TryApply(tag, value);
            return true;
        }

        var type = property.PropertyType;
        object converted = type.IsEnum
            ? Enum.ToObject(type, value)
            : Convert.ChangeType(value, Nullable.GetUnderlyingType(type) ?? type, CultureInfo.InvariantCulture);
        property.SetValue(Settings, converted);
        Settings.Save();
        ModSetup.TryApply(tag, converted);
        return true;
    }

    /// <summary>
    ///     将名称满足条件的设置恢复为默认值。
    /// </summary>
    public static void ResetProperties(Func<string, bool> filter)
    {
        var defaults = new AppSettings();
        foreach (var property in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || !filter(property.Name))
                continue;
            property.SetValue(Settings, property.GetValue(defaults));
        }

        SetupExtraSettings.Current.Reset(filter);
        Settings.Save();
    }

    /// <summary>
    ///     界面设置的键名（不含语言）。
    /// </summary>
    public static bool IsUiKey(string name) =>
        (name.StartsWith("Ui", StringComparison.Ordinal) && name is not ("UiLanguage" or "UiFormatCulture")) ||
        name is "ColorMode" or "DarkColor" or "LightColor" or "BackgroundColorful" or "BackgroundGradientStyle"
            or "ThemeCustomHue" or "ThemeCustomSaturation";

    /// <summary>
    ///     记录一般日志。与 WPF 的 ModBase.Log(string) 对应。
    /// </summary>
    public static void Info(string message)
    {
        Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(null, "{Message}", message);
    }

    /// <summary>
    ///     记录错误日志，并按需要向用户显示提示或消息框。与 WPF 的 ModBase.Log(ex, message, level, userSummary) 对应。
    /// </summary>
    public static void Log(Exception? ex, string message, SetupLogLevel level = SetupLogLevel.Log,
        string? userSummary = null)
    {
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);
        if (userSummary is null)
            return;
        switch (level)
        {
            case SetupLogLevel.Hint:
                HintService.Hint(userSummary, HintType.Error);
                break;
            case SetupLogLevel.Msgbox:
            case SetupLogLevel.Feedback:
                _ = MsgBox.ShowAsync(userSummary);
                break;
        }
    }
}

/// <summary>
///     设置页使用、但 <see cref="AppSettings"/> 中尚未定义的设置项（对应 WPF 中 Config 的 Ui*、System* 项）。
///     键名与 WPF 配置键名相同，默认值与 WPF 的 ConfigItem 默认值一致，保存在设置目录下的 <c>setup-extra.json</c> 中。
///     TODO(shion): 这些项应迁移为 <see cref="AppSettings"/> 的正式属性，以便其他页面直接读取。
/// </summary>
internal sealed class SetupExtraSettings
{
    private static readonly Dictionary<string, object> Defaults = new()
    {
        ["UiLauncherLogo"] = true,
        ["UiLauncherTransparent"] = 600,
        ["UiLockWindowSize"] = false,
        ["UiLogoType"] = 1,
        ["UiLogoText"] = "",
        ["UiLogoLeft"] = false,
        ["UiFont"] = "",
        ["UiMotdFont"] = "",
        ["UiBlur"] = false,
        ["UiBlurValue"] = 16,
        ["UiBlurSamplingRate"] = 70,
        ["UiBlurType"] = 0,
        ["UiBackgroundOpacity"] = 1000,
        ["UiBackgroundBlur"] = 0,
        ["UiAutoPauseVideo"] = true,
        ["UiMusicVolume"] = 500,
        ["UiMusicStop"] = false,
        ["UiMusicStart"] = false,
        ["UiMusicAuto"] = true,
        ["UiMusicRandom"] = true,
        ["UiMusicSMTC"] = true,
        ["SystemSystemActivity"] = 0,
        ["SystemDisableHardwareAcceleration"] = false,
        ["SystemTelemetry"] = false,
        ["SystemNetEnableDoH"] = true,
        ["SystemDebugAnim"] = 9,
        ["SystemDebugSkipCopy"] = false,
        ["SystemDebugMode"] = false,
    };

    private readonly object gate = new();

    private Dictionary<string, JsonElement>? values;

    private SetupExtraSettings()
    {
    }

    /// <summary>
    ///     唯一实例。
    /// </summary>
    public static SetupExtraSettings Current { get; } = new();

    private static string FilePath => Path.Combine(SetupPaths.DataRoot, "setup-extra.json");

    /// <summary>
    ///     是否为本类管理的设置键名。
    /// </summary>
    public static bool Contains(string key) => Defaults.ContainsKey(key);

    public bool UiLauncherLogo
    {
        get => Get<bool>(nameof(UiLauncherLogo));
        set => SetValue(nameof(UiLauncherLogo), value);
    }

    public int UiLauncherTransparent
    {
        get => Get<int>(nameof(UiLauncherTransparent));
        set => SetValue(nameof(UiLauncherTransparent), value);
    }

    public bool UiLockWindowSize
    {
        get => Get<bool>(nameof(UiLockWindowSize));
        set => SetValue(nameof(UiLockWindowSize), value);
    }

    public int UiLogoType
    {
        get => Get<int>(nameof(UiLogoType));
        set => SetValue(nameof(UiLogoType), value);
    }

    public string UiLogoText
    {
        get => Get<string>(nameof(UiLogoText));
        set => SetValue(nameof(UiLogoText), value);
    }

    public bool UiLogoLeft
    {
        get => Get<bool>(nameof(UiLogoLeft));
        set => SetValue(nameof(UiLogoLeft), value);
    }

    public string UiFont
    {
        get => Get<string>(nameof(UiFont));
        set => SetValue(nameof(UiFont), value);
    }

    public string UiMotdFont
    {
        get => Get<string>(nameof(UiMotdFont));
        set => SetValue(nameof(UiMotdFont), value);
    }

    public bool UiBlur
    {
        get => Get<bool>(nameof(UiBlur));
        set => SetValue(nameof(UiBlur), value);
    }

    public int UiBlurValue
    {
        get => Get<int>(nameof(UiBlurValue));
        set => SetValue(nameof(UiBlurValue), value);
    }

    public int UiBlurSamplingRate
    {
        get => Get<int>(nameof(UiBlurSamplingRate));
        set => SetValue(nameof(UiBlurSamplingRate), value);
    }

    public int UiBlurType
    {
        get => Get<int>(nameof(UiBlurType));
        set => SetValue(nameof(UiBlurType), value);
    }

    public int UiBackgroundOpacity
    {
        get => Get<int>(nameof(UiBackgroundOpacity));
        set => SetValue(nameof(UiBackgroundOpacity), value);
    }

    public int UiBackgroundBlur
    {
        get => Get<int>(nameof(UiBackgroundBlur));
        set => SetValue(nameof(UiBackgroundBlur), value);
    }

    public bool UiAutoPauseVideo
    {
        get => Get<bool>(nameof(UiAutoPauseVideo));
        set => SetValue(nameof(UiAutoPauseVideo), value);
    }

    public int UiMusicVolume
    {
        get => Get<int>(nameof(UiMusicVolume));
        set => SetValue(nameof(UiMusicVolume), value);
    }

    public bool UiMusicStop
    {
        get => Get<bool>(nameof(UiMusicStop));
        set => SetValue(nameof(UiMusicStop), value);
    }

    public bool UiMusicStart
    {
        get => Get<bool>(nameof(UiMusicStart));
        set => SetValue(nameof(UiMusicStart), value);
    }

    public bool UiMusicAuto
    {
        get => Get<bool>(nameof(UiMusicAuto));
        set => SetValue(nameof(UiMusicAuto), value);
    }

    public bool UiMusicRandom
    {
        get => Get<bool>(nameof(UiMusicRandom));
        set => SetValue(nameof(UiMusicRandom), value);
    }

    public bool UiMusicSMTC
    {
        get => Get<bool>(nameof(UiMusicSMTC));
        set => SetValue(nameof(UiMusicSMTC), value);
    }

    public int SystemSystemActivity
    {
        get => Get<int>(nameof(SystemSystemActivity));
        set => SetValue(nameof(SystemSystemActivity), value);
    }

    public bool SystemDisableHardwareAcceleration
    {
        get => Get<bool>(nameof(SystemDisableHardwareAcceleration));
        set => SetValue(nameof(SystemDisableHardwareAcceleration), value);
    }

    public bool SystemTelemetry
    {
        get => Get<bool>(nameof(SystemTelemetry));
        set => SetValue(nameof(SystemTelemetry), value);
    }

    public bool SystemNetEnableDoH
    {
        get => Get<bool>(nameof(SystemNetEnableDoH));
        set => SetValue(nameof(SystemNetEnableDoH), value);
    }

    public int SystemDebugAnim
    {
        get => Get<int>(nameof(SystemDebugAnim));
        set => SetValue(nameof(SystemDebugAnim), value);
    }

    public bool SystemDebugSkipCopy
    {
        get => Get<bool>(nameof(SystemDebugSkipCopy));
        set => SetValue(nameof(SystemDebugSkipCopy), value);
    }

    public bool SystemDebugMode
    {
        get => Get<bool>(nameof(SystemDebugMode));
        set => SetValue(nameof(SystemDebugMode), value);
    }

    /// <summary>
    ///     按键名写入一个值并保存。调用方保证键名属于 <see cref="Contains"/>。
    /// </summary>
    public void SetValue(string key, object value)
    {
        lock (gate)
        {
            var store = Load();
            store[key] = JsonSerializer.SerializeToElement(value);
            Save(store);
        }
    }

    /// <summary>
    ///     将名称满足条件的项恢复为默认值。
    /// </summary>
    public void Reset(Func<string, bool> filter)
    {
        lock (gate)
        {
            var store = Load();
            foreach (var key in Defaults.Keys.Where(filter).ToList())
                store.Remove(key);
            Save(store);
        }
    }

    private T Get<T>(string key)
    {
        lock (gate)
        {
            if (Load().TryGetValue(key, out var element))
            {
                try
                {
                    return Read<T>(element);
                }
                catch (Exception e) when (e is FormatException or InvalidCastException or InvalidOperationException
                                              or OverflowException)
                {
                    // 已保存的值类型不符，回退到默认值
                }
            }
        }

        return (T)Convert.ChangeType(Defaults[key], typeof(T), CultureInfo.InvariantCulture);
    }

    private static T Read<T>(JsonElement element)
    {
        object value = element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String => element.GetString() ?? "",
            _ => throw new FormatException("不支持的设置值类型")
        };
        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    private Dictionary<string, JsonElement> Load()
    {
        if (values is not null)
            return values;
        try
        {
            if (File.Exists(FilePath))
                values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(FilePath));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // 读取失败时使用默认值，不影响设置页
        }

        values ??= new Dictionary<string, JsonElement>();
        return values;
    }

    private static void Save(Dictionary<string, JsonElement> store)
    {
        try
        {
            Directory.CreateDirectory(SetupPaths.DataRoot);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(store));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 设置只是便利功能，写入失败不应影响页面
        }
    }
}

/// <summary>
///     设置页错误的展示方式。
/// </summary>
internal enum SetupLogLevel
{
    Log,
    Hint,
    Msgbox,
    Feedback
}

/// <summary>
///     设置页用到的系统对话框。
/// </summary>
internal static class SetupDialogs
{
    public static readonly string[] ImagePatterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp"];

    public static readonly string[] AudioPatterns = ["*.mp3", "*.wav", "*.flac", "*.ogg", "*.m4a", "*.aac"];

    /// <summary>
    ///     选择一个已有文件，取消时返回 null。
    /// </summary>
    public static async Task<string?> SelectFileAsync(Visual anchor, string title, string filterName,
        string[] patterns)
    {
        var topLevel = TopLevel.GetTopLevel(anchor);
        if (topLevel is null)
            return null;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(filterName) { Patterns = patterns }]
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    /// <summary>
    ///     选择一个保存位置，取消时返回 null。
    /// </summary>
    public static async Task<string?> SelectSaveFileAsync(Visual anchor, string title, string suggestedName,
        string filterName, string[] patterns)
    {
        var topLevel = TopLevel.GetTopLevel(anchor);
        if (topLevel is null)
            return null;
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType(filterName) { Patterns = patterns }]
        });
        return file?.TryGetLocalPath();
    }
}

/// <summary>
///     主页 HTTP 地址的校验规则，与 PCL.Core 的 HttpValidator 相同（允许空值）。
/// </summary>
internal sealed class HttpUrlValidator : AbstractValidator<string>
{
    public HttpUrlValidator()
    {
        RuleFor(url => url)
            .Must(url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("输入的网址无效！");
    }
}
