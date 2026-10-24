using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Logging;
using Avalonia.Platform.Storage;
using FluentValidation;
using PCL.Shion.Controls;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Tools;

public partial class PageToolsTest : MyPageRight
{
    private WriteableBitmap? currentSkinBitmap;
    private WriteableBitmap? generatedHeadBitmap;

    private int headSize = 64;
    private string? skinPath = "";

    public PageToolsTest()
    {
        InitializeComponent();
        CmbHeadSize.SelectionChanged += CmbHeadSize_SelectionChanged;
        Loaded += (_, _) => MeLoaded();

        AchievementBlockTextBox.ValidateRules.Add(new RegexValidator("^[a-z0-9_]+$",
            Lang.Text("Tools.Test.Achievement.InvalidId")));
        AchievementBlockTextBox.ValidatedTextChanged += (_, _) => StartButtonRefresh();
        AchievementTitleTextBox.ValidatedTextChanged += (_, _) => StartButtonRefresh();
        AchievementString1TextBox.ValidatedTextChanged += (_, _) => StartButtonRefresh();
    }

    private static void LogInfo(string message) =>
        Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(null, "{Message}", message);

    private static void LogWarn(Exception ex, string message) =>
        Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);

    private static void LogError(Exception ex, string message) =>
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);

    /// <summary>
    ///     正则校验规则，对应 WPF 版 PCL.Core.Utils.Validate.RegexValidator。
    /// </summary>
    private sealed class RegexValidator : AbstractValidator<string>
    {
        public RegexValidator(string pattern, string errorMessage)
        {
            RuleFor(text => text)
                .Must(text => text is not null && Regex.IsMatch(text, pattern))
                .WithMessage(errorMessage);
        }
    }

    private void MeLoaded()
    {
        BtnDownloadStart.IsEnabled = false;

        TextDownloadFolder.Text = App.Services.Settings.CacheDownloadFolder;
        TextDownloadFolder.Validate();

        if (!string.IsNullOrEmpty(TextDownloadFolder.ValidateResult) || string.IsNullOrEmpty(TextDownloadFolder.Text))
            TextDownloadFolder.Text = Path.Combine(AppContext.BaseDirectory, "PCL", "MyDownload") + Path.DirectorySeparatorChar;

        TextDownloadFolder.Validate();
        TextDownloadName.Validate();
        TextDownloadUrl.Validate();
        StartButtonRefresh();
    }

    private void StartButtonRefresh()
    {
        BtnDownloadStart.IsEnabled = string.IsNullOrEmpty(TextDownloadFolder.ValidateResult) &&
                                     string.IsNullOrEmpty(TextDownloadUrl.ValidateResult) &&
                                     string.IsNullOrEmpty(TextDownloadName.ValidateResult);

        BtnDownloadOpen.IsEnabled = string.IsNullOrEmpty(TextDownloadFolder.ValidateResult);

        BtnAchievementPreview.IsEnabled = string.IsNullOrEmpty(AchievementBlockTextBox.ValidateResult) &&
                                          string.IsNullOrEmpty(AchievementTitleTextBox.ValidateResult) &&
                                          string.IsNullOrEmpty(AchievementString1TextBox.ValidateResult);

        BtnAchievementSave.IsEnabled = string.IsNullOrEmpty(AchievementBlockTextBox.ValidateResult) &&
                                       string.IsNullOrEmpty(AchievementTitleTextBox.ValidateResult) &&
                                       string.IsNullOrEmpty(AchievementString1TextBox.ValidateResult);
    }

    private void SaveCacheDownloadFolder(object? sender, RoutedEventArgs e)
    {
        App.Services.Settings.CacheDownloadFolder = TextDownloadFolder.Text;
        App.Services.Settings.Save();
        TextDownloadName.Validate();
    }

    private void TextDownloadName_ValidateChanged(object? sender, RoutedEventArgs e)
    {
        StartButtonRefresh();
    }

    /// <summary>
    ///     开始自定义下载。<paramref name="folder" /> 为空时弹出保存对话框选择位置。
    /// </summary>
    public async Task StartCustomDownload(string url, string fileName, string? folder = null)
    {
        try
        {
            string target;
            if (string.IsNullOrWhiteSpace(folder))
            {
                var top = TopLevel.GetTopLevel(this);
                if (top is null)
                    return;
                var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = Lang.Text("Tools.Test.CustomDownload.SelectLocation"),
                    SuggestedFileName = fileName
                });
                target = file?.TryGetLocalPath() ?? "";
                if (string.IsNullOrEmpty(target))
                    return;
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(folder);
                }
                catch (Exception ex)
                {
                    LogError(ex, $"访问文件夹失败（{folder}）");
                    Hint.Show(Lang.Text("Tools.Test.Error.OperationFailed"), HintType.Error);
                    return;
                }
                target = Path.Combine(folder, fileName);
            }

            LogInfo("[Download] 自定义下载文件名：" + fileName);
            LogInfo("[Download] 自定义下载文件目标：" + target);
            var name = Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName);

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            {
                var result = await App.Services.Engine.DownloadAsync(
                    [new DownloadItem([uri], target)], null, CancellationToken.None);
                if (result.Succeeded)
                    Hint.Show(Lang.Text("Tools.Test.CustomDownload.Finished", name), HintType.Success);
                else
                    Hint.Show(Lang.Text("Tools.Test.Error.OperationFailed"), HintType.Error);
            }
            else // UNC 路径
            {
                await Task.Run(() => File.Copy(url, target, true));
                Hint.Show(Lang.Text("Tools.Test.CustomDownload.Finished", name), HintType.Success);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "开始自定义下载失败");
            Hint.Show(Lang.Text("Tools.Test.Error.OperationFailed"), HintType.Error);
        }
    }

    public static void Jrrp()
    {
        var random = new Random(GenerateDailySeed());
        var luckValue = random.Next(0, 101);
        var rating = GetRating(luckValue);
        var title = Lang.Text("Tools.Test.Luck.MsgboxTitle", Lang.Date(DateTime.Now, "d"));

        if (luckValue >= 60)
            _ = MsgBox.ShowAsync(Lang.Text("Tools.Test.Luck.MessageGood", luckValue, rating), title);
        else
            _ = MsgBox.ShowAsync(Lang.Text("Tools.Test.Luck.MessageBad", luckValue, rating), title, isWarn: luckValue <= 30);
    }

    /// <summary>
    ///     清理垃圾：删除 Minecraft 文件夹中的崩溃报告、日志和 Natives 文件，以及启动器的缓存。
    /// </summary>
    public async Task RubbishClear()
    {
        BtnClear.IsEnabled = false;
        try
        {
            // 只有当没有运行中的 Minecraft 游戏时才能清理
            if (App.Services.Runner.Running.Count > 0)
            {
                Hint.Show(Lang.Text("Tools.Test.Clean.CloseGameFirst"));
                return;
            }

            var settings = App.Services.Settings;
            if (settings.HintClearRubbish <= 2)
            {
                if (await MsgBox.ShowAsync(
                        Lang.Text("Tools.Test.Clean.ConfirmMessage"),
                        Lang.Text("Tools.Test.Clean.ConfirmTitle"),
                        Lang.Text("Common.Action.Confirm"),
                        Lang.Text("Common.Action.Cancel")) == 2)
                    return;
                settings.HintClearRubbish += 1;
                settings.Save();
            }

            var num = 0;
            var cleanDirectories = new List<DirectoryInfo>();
            foreach (var root in settings.MinecraftFolders)
            {
                if (!Directory.Exists(root))
                    continue;
                cleanDirectories.Add(new DirectoryInfo(root));
                var versions = Path.Combine(root, "versions");
                if (Directory.Exists(versions))
                    cleanDirectories.AddRange(new DirectoryInfo(versions).EnumerateDirectories());
            }

            foreach (var dirInfo in cleanDirectories)
            {
                num += DeleteDirectory(Path.Combine(dirInfo.FullName, "crash-reports"));
                num += DeleteDirectory(Path.Combine(dirInfo.FullName, "logs"));
                foreach (var fileInfo in dirInfo.EnumerateFiles("*"))
                    if (fileInfo.Name.StartsWith("hs_err_pid") || fileInfo.Name.EndsWith(".log") ||
                        fileInfo.Name == "WailaErrorOutput.txt")
                    {
                        fileInfo.Delete();
                        num += 1;
                    }

                foreach (var subDirInfo in dirInfo.EnumerateDirectories())
                    if (subDirInfo.Name == "natives-windows-x86_64")
                        num += DeleteDirectory(subDirInfo.FullName);
            }

            num += DeleteDirectory(Path.Combine(Path.GetTempPath(), "PCL Shion"));
            num += DeleteDirectory(Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory) ?? "", "ProgramData", "PCL"));

            if (num != 0)
            {
                await MsgBox.ShowAsync(Lang.Text("Tools.Test.Clean.ClearedMessage", num),
                    Lang.Text("Tools.Test.Clean.Cleared"), Lang.Text("Common.Action.Confirm"), "", "", false);
                if (Environment.ProcessPath is { } exePath)
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.Shutdown();
            }
            else
            {
                Hint.Show(Lang.Text("Tools.Test.Clean.NoFiles"));
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "清理垃圾失败");
            Hint.Show(Lang.Text("Tools.Test.Error.OperationFailed"), HintType.Error);
        }
        finally
        {
            BtnClear.IsEnabled = true;
        }
    }

    private static int DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return 0;
        try
        {
            var count = Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories).Count();
            Directory.Delete(path, true);
            return count;
        }
        catch (Exception ex)
        {
            LogWarn(ex, "删除文件夹失败：" + path);
            return 0;
        }
    }

    public static string GetRandomCave()
    {
        return Lang.Text("Tools.Test.CeNotice");
    }

    public static string GetRandomHint()
    {
        return Lang.Text("Tools.Test.CeNotice");
    }

    public static string GetRandomPresetHint()
    {
        return Lang.Text("Tools.Test.CeNotice");
    }

    private void TextDownloadUrl_TextChanged(object? sender, TextChangedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(TextDownloadName.Text) || string.IsNullOrEmpty(TextDownloadUrl.Text)) return;
            TextDownloadName.Text = Path.GetFileName(WebUtility.UrlDecode(TextDownloadUrl.Text).TrimEnd('/'));
            StartButtonRefresh();
        }
        catch
        {
        }
    }

    private async void MyTextButton_Click(object? sender, EventArgs? e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Lang.Text("Tools.Test.CustomDownload.SelectLocation"),
            AllowMultiple = false
        });
        var text = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(text)) TextDownloadFolder.Text = text;
    }

    private void BtnDownloadOpen_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            var text = TextDownloadFolder.Text;
            Directory.CreateDirectory(text);
            SystemShell.OpenFolder(text);
        }
        catch (Exception ex)
        {
            LogError(ex, "打开下载文件夹失败");
        }
    }

    private void BtnDownloadStart_Click(object? sender, PointerReleasedEventArgs e)
    {
        _ = StartCustomDownload(TextDownloadUrl.Text ?? "", TextDownloadName.Text ?? "", TextDownloadFolder.Text);
        TextDownloadUrl.Text = "";
        TextDownloadUrl.Validate();
        TextDownloadUrl.ForceShowAsSuccess();
        TextDownloadName.Text = "";
        TextDownloadName.Validate();
        TextDownloadName.ForceShowAsSuccess();
        StartButtonRefresh();
    }

    private void TextDownloadUrl_ValidateChanged(object? sender, RoutedEventArgs e)
    {
        StartButtonRefresh();
    }

    private void TextDownloadFolder_OnValidatedTextChanged(object? sender, RoutedEventArgs e)
    {
        SaveCacheDownloadFolder(sender, e);
        TextDownloadName_ValidateChanged(sender, e);
    }

    private void TextDownloadFolder_ValidateChanged(object? sender, RoutedEventArgs e)
    {
        StartButtonRefresh();
    }

    private void BtnClear_Click(object? sender, PointerReleasedEventArgs e)
    {
        _ = RubbishClear();
    }

    // 下载正版玩家皮肤
    private async void BtnSkinSave_Click(object? sender, PointerReleasedEventArgs e)
    {
        var id = TextSkinID.Text ?? "";
        Hint.Show(Lang.Text("Tools.Test.Skin.Fetching"));
        try
        {
            if (id.Length < 3)
            {
                Hint.Show(Lang.Text("Tools.Test.Skin.InvalidId"));
                return;
            }

            var uuid = await GetMojangUuidAsync(id);
            var skinUrl = await GetMojangSkinUrlAsync(uuid);
            var bytes = await App.Services.Engine.Http.GetByteArrayAsync(skinUrl);

            var top = TopLevel.GetTopLevel(this);
            if (top is null)
                return;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Lang.Text("Tools.Test.Skin.Save"),
                SuggestedFileName = $"{id}.png",
                FileTypeChoices = [new FilePickerFileType(Lang.Text("Tools.Test.Skin.FileFilter")) { Patterns = ["*.png"] }]
            });
            var path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
                return;
            await File.WriteAllBytesAsync(path, bytes);
            Hint.Show(Lang.Text("Tools.Test.Skin.Saved", id), HintType.Success);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            Hint.Show(Lang.Text("Tools.Test.Skin.TooFrequent"), HintType.Error);
            LogWarn(ex, $"获取正版皮肤失败（{id}）：获取皮肤太过频繁，请 5 分钟后再试！");
        }
        catch (Exception ex)
        {
            LogError(ex, $"获取正版皮肤失败（{id}）");
        }
    }

    private static async Task<string> GetMojangUuidAsync(string name)
    {
        using var response = await App.Services.Engine.Http.GetAsync(
            $"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(name)}");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString() ?? throw new InvalidDataException("uuid");
    }

    private static async Task<string> GetMojangSkinUrlAsync(string uuid)
    {
        using var response = await App.Services.Engine.Http.GetAsync(
            $"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var textures = doc.RootElement.GetProperty("properties").EnumerateArray()
            .First(p => p.GetProperty("name").GetString() == "textures").GetProperty("value").GetString() ?? "";
        using var texDoc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(textures)));
        return texDoc.RootElement.GetProperty("textures").GetProperty("SKIN").GetProperty("url").GetString()
               ?? throw new InvalidDataException("skin url");
    }

    // 今日人品
    private void BtnLuck_Click(object? sender, PointerReleasedEventArgs e)
    {
        Jrrp();
    }

    public static int GenerateDailySeed()
    {
        var datePart = DateTime.Today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        return DJB2Hash(datePart + LauncherIdFallback);
    }

    private static string? _launcherIdFallback;

    /// <summary>
    ///     替代 WPF 版 PCL.Core.Utils.Secret.Identify.LauncherId。该实现依赖 WMI，无法跨平台使用，
    ///     这里改为根据机器名与系统描述计算稳定的识别码。
    /// </summary>
    // TODO(shion): 与 WPF 版的硬件识别码不同，人品结果不会与 WPF 版一致；如需一致需要提供跨平台的硬件信息读取
    private static string LauncherIdFallback
    {
        get
        {
            if (_launcherIdFallback is not null)
                return _launcherIdFallback;
            try
            {
                var source = "PCL-CE|" + Environment.MachineName + "|" + Environment.OSVersion + "|LauncherId";
                var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source));
                var hex = Convert.ToHexString(hash);
                _launcherIdFallback = hex.Substring(0, 16).Insert(4, "-").Insert(9, "-").Insert(14, "-");
            }
            catch (Exception)
            {
                _launcherIdFallback = "PCL2-CECE-GOOD-2025";
            }

            return _launcherIdFallback;
        }
    }

    private static int DJB2Hash(string str)
    {
        var hash = 5381L;
        var prime = 33L;
        foreach (var c in str)
        {
            long charValue = c;
            hash = (hash * prime + charValue) % 0x100000000L;
        }

        return (int)(hash & 0x7FFFFFFFL);
    }

    public static string GetRating(int luckValue)
    {
        var key = luckValue switch
        {
            100 => "Tools.Test.Luck.Rating100",
            >= 95 => "Tools.Test.Luck.Rating95",
            >= 90 => "Tools.Test.Luck.Rating90",
            >= 60 => "Tools.Test.Luck.Rating60",
            >= 40 => "Tools.Test.Luck.Rating40",
            >= 30 => "Tools.Test.Luck.Rating30",
            >= 10 => "Tools.Test.Luck.Rating10",
            _ => "Tools.Test.Luck.Rating0"
        };

        return Lang.Text(key);
    }

    private async void BtnCreateShortcut_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            var shortcutName = Lang.Text("Tools.Test.Shortcut.FileName", ".lnk");
            var desktopName = Lang.Text("Tools.Test.Shortcut.Desktop");
            var startName = Lang.Text("Tools.Test.Shortcut.StartMenu");
            var desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), shortcutName);
            var start = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs",
                shortcutName);
            var choice = await MsgBox.ShowAsync(
                Lang.Text("Tools.Test.Shortcut.ConfirmMessage", desktopName, desktop, startName, start),
                Lang.Text("Tools.Test.Shortcut.SelectLocation"), Lang.Text("Common.Action.Cancel"), desktopName,
                startName);
            if (choice == 1)
                return;
            var shortcutPath = choice == 2 ? desktop : start;
            var locationName = choice == 2 ? desktopName : startName;
            CreateShortcut(shortcutPath, Environment.ProcessPath ?? "");
            Hint.Show(Lang.Text("Tools.Test.Shortcut.Created", locationName), HintType.Success);
        }
        catch (Exception ex)
        {
            LogError(ex, "创建快捷方式失败");
            Hint.Show(Lang.Text("Tools.Test.Error.OperationFailed"), HintType.Error);
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        if (!OperatingSystem.IsWindows())
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.CreateShortcut(shortcutPath);
        link.TargetPath = targetPath;
        link.WorkingDirectory = Path.GetDirectoryName(targetPath);
        link.Save();
    }

    // 启动计数显示
    private void BtnLaunchCount_Click(object? sender, PointerReleasedEventArgs e)
    {
        _ = MsgBox.ShowAsync(Lang.Text("Tools.Test.LaunchCount.Message", App.Services.Settings.SystemLaunchCount),
            Lang.Text("Tools.Test.LaunchCount.Title"));
    }

    private async void BtnAchievementPreview_Click(object? sender, PointerReleasedEventArgs e)
    {
        var url = GetAchievementUrl();
        LogInfo("[Net] 获取网络结果" + url);
        await LoadImageAsync(url);
    }

    private async Task LoadImageAsync(string imageUrl)
    {
        try
        {
            using var response = await App.Services.Engine.Http.GetAsync(imageUrl);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync();
                var bitmap = new Bitmap(stream);
                AchievementImage.Source = bitmap;
                AchievementImage.IsVisible = true;
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                LogInfo("获取成就图片失败（404）");
                Hint.Show(Lang.Text("Tools.Test.Achievement.FetchFailed"), HintType.Error);
            }
            else
            {
                LogInfo("获取成就图片失败（" + (int)response.StatusCode + "）");
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "获取成就图片失败");
        }
    }

    private async void BtnAchievementSave_Click(object? sender, PointerReleasedEventArgs e)
    {
        var url = GetAchievementUrl();
        await DownloadImageToLocalAsync(url);
    }

    private async Task DownloadImageToLocalAsync(string imageUrl)
    {
        try
        {
            using var response = await App.Services.Engine.Http.GetAsync(imageUrl);
            if (response.IsSuccessStatusCode)
            {
                var imageBytes = await response.Content.ReadAsByteArrayAsync();

                var top = TopLevel.GetTopLevel(this);
                if (top is null)
                    return;
                var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = Lang.Text("Tools.Test.Achievement.Save"),
                    SuggestedFileName = AchievementTitleTextBox.Text + ".png",
                    FileTypeChoices = [new FilePickerFileType(Lang.Text("Tools.Test.Achievement.FileFilter")) { Patterns = ["*.png"] }]
                });
                var path = file?.TryGetLocalPath();
                if (string.IsNullOrEmpty(path))
                {
                    LogInfo("用户取消了保存操作");
                    return;
                }

                await File.WriteAllBytesAsync(path, imageBytes);
                Hint.Show(Lang.Text("Tools.Test.Achievement.Saved"), HintType.Success);
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                LogInfo("获取成就图片失败（404）");
                Hint.Show(Lang.Text("Tools.Test.Achievement.FetchFailed"), HintType.Error);
            }
            else
            {
                LogInfo("获取成就图片失败（" + (int)response.StatusCode + "）");
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "获取成就图片失败");
        }
    }

    private string GetAchievementUrl()
    {
        var block = AchievementBlockTextBox.Text?.Trim() ?? "";
        var title = (AchievementTitleTextBox.Text ?? "").Replace(" ", "..");
        var str1 = (AchievementString1TextBox.Text ?? "").Replace(" ", "..");
        var str2 = (AchievementString2TextBox.Text ?? "").Replace(" ", "..");
        var url = $"https://minecraft-api.com/api/achivements/{block}/{title}/{str1}";
        if (!string.IsNullOrEmpty(str2)) url += $"/{str2}";
        return url;
    }

    private int GetHeadSize() => CmbHeadSize.SelectedIndex switch
    {
        0 => 64,
        1 => 96,
        2 => 128,
        _ => 64
    };

    private async void BtnSelectSkin_Click(object? sender, PointerReleasedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Lang.Text("Tools.Test.Avatar.SelectSkinFile"),
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(Lang.Text("Tools.Test.Avatar.FileFilter")) { Patterns = ["*.png"] }]
        });
        var filePath = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(filePath)) LoadAndGenerateHead(filePath);
    }

    private void LoadAndGenerateHead(string skinPath)
    {
        try
        {
            using (var stream = File.OpenRead(skinPath))
            {
                currentSkinBitmap = WriteableBitmap.Decode(stream);
            }

            this.skinPath = skinPath;

            if (currentSkinBitmap.PixelSize.Width != currentSkinBitmap.PixelSize.Height)
            {
                Hint.Show(Lang.Text("Tools.Test.Avatar.InvalidSize"), HintType.Error);
                SkinPreviewBorder.IsVisible = false;
                return;
            }

            generatedHeadBitmap = GenerateHeadFromSkin(currentSkinBitmap);

            ImgFace.Source = generatedHeadBitmap;
            ImgHair.Source = null;

            SkinPreviewBorder.IsVisible = true;
            Hint.Show(Lang.Text("Tools.Test.Avatar.Generated"), HintType.Success);
        }
        catch (Exception ex)
        {
            LogError(ex, "生成头像失败");
            Hint.Show(Lang.Text("Tools.Test.Avatar.GenerateFailed", ex.Message), HintType.Error);
            SkinPreviewBorder.IsVisible = false;
        }
    }

    /// <summary>
    ///     由皮肤生成头像：先绘制脸部层，再把头发层中不透明的像素覆盖上去（最近邻缩放）。
    /// </summary>
    private WriteableBitmap GenerateHeadFromSkin(WriteableBitmap skinBitmap)
    {
        var src = ReadPixels(skinBitmap, out var skinWidth, out _);
        var scale = skinWidth / 64;
        headSize = GetHeadSize();
        var head = new byte[headSize * headSize * 4];

        // 脸部层
        var faceSize = headSize - headSize / 8;
        var offset = headSize / 16;
        DrawScaled(src, skinWidth, 8 * scale, 8 * scale, 8 * scale, 8 * scale, head, headSize, offset, offset,
            faceSize, faceSize, false);

        // 头发层（仅覆盖不透明像素）
        if (skinWidth >= 64)
            DrawScaled(src, skinWidth, 40 * scale, 8 * scale, 8 * scale, 8 * scale, head, headSize, 0, 0, headSize,
                headSize, true);

        return ToBitmap(head, headSize);
    }

    private static void DrawScaled(byte[] src, int srcStride, int sx, int sy, int sw, int sh, byte[] dst, int dstStride,
        int dx, int dy, int dw, int dh, bool onlyOpaque)
    {
        for (var y = 0; y < dh; y++)
        {
            var srcY = sy + y * sh / dh;
            for (var x = 0; x < dw; x++)
            {
                var srcX = sx + x * sw / dw;
                var si = (srcY * srcStride + srcX) * 4;
                if (onlyOpaque && src[si + 3] == 0)
                    continue;
                var di = ((dy + y) * dstStride + dx + x) * 4;
                Array.Copy(src, si, dst, di, 4);
            }
        }
    }

    private static byte[] ReadPixels(WriteableBitmap bitmap, out int width, out int height)
    {
        width = bitmap.PixelSize.Width;
        height = bitmap.PixelSize.Height;
        var buffer = new byte[width * height * 4];
        using var framebuffer = bitmap.Lock();
        for (var y = 0; y < height; y++)
            Marshal.Copy(IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), buffer, y * width * 4, width * 4);
        return buffer;
    }

    private static WriteableBitmap ToBitmap(byte[] pixels, int size)
    {
        var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        using var framebuffer = bitmap.Lock();
        for (var y = 0; y < size; y++)
            Marshal.Copy(pixels, y * size * 4, IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), size * 4);
        return bitmap;
    }

    private async void BtnSaveHead_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (generatedHeadBitmap is null)
        {
            Hint.Show(Lang.Text("Tools.Test.Avatar.SelectFirst"), HintType.Error);
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Lang.Text("Tools.Test.Avatar.Save"),
            SuggestedFileName = "Head.png",
            FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }]
        });
        var savePath = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(savePath))
            return;

        generatedHeadBitmap.Save(savePath);
        Hint.Show(Lang.Text("Tools.Test.Avatar.Saved"), HintType.Success);
    }

    private void CmbHeadSize_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (currentSkinBitmap is not null && skinPath is not null) LoadAndGenerateHead(skinPath);
    }
}

/// <summary>
///     非空校验，对应 WPF 版 PCL.Core.Utils.Validate.NullOrEmptyValidator，供 XAML 中的 ValidateRules 使用。
/// </summary>
public class NullOrEmptyValidator : AbstractValidator<string>
{
    public NullOrEmptyValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x)).WithMessage("输入内容不能为空！");
    }
}

/// <summary>
///     网址或 UNC 路径校验，对应 WPF 版 PCL.Core.Utils.Validate.HttpAndUncValidator。
/// </summary>
public class HttpAndUncValidator : AbstractValidator<string>
{
    private static readonly Regex HttpUriPattern =
        new(@"^https?://[^\s/$.?#].[^\s]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UncPathPattern =
        new(@"^\\\\[^\\/:*?""<>|\s]+\\[^\\/:*?""<>|]+", RegexOptions.Compiled);

    public HttpAndUncValidator() : this(false)
    {
    }

    public HttpAndUncValidator(bool allowNullOrEmpty)
    {
        RuleFor(x => x)
            .Must(x => (allowNullOrEmpty && string.IsNullOrEmpty(x)) ||
                       (x is not null && (HttpUriPattern.IsMatch(x) || UncPathPattern.IsMatch(x))))
            .WithMessage("输入的网址无效！");
    }
}

/// <summary>
///     文件夹路径校验，对应 WPF 版 PCL.Core.Utils.Validate.FolderPathValidator 的主要规则。
/// </summary>
public class FolderPathValidator : AbstractValidator<string>
{
    public FolderPathValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x)).WithMessage("输入内容不能为空！")
            .Must(x => x is null || !x.EndsWith(' ')).WithMessage("文件夹名不能以空格结尾！")
            .Must(x => x is null || !x.EndsWith('.')).WithMessage("文件夹名不能以小数点结尾！")
            .Must(x => x is null || x.IndexOfAny(Path.GetInvalidPathChars()) < 0).WithMessage("文件夹路径包含非法字符！")
            .Must(x => x is null || x.IndexOfAny(['!', ';']) < 0).WithMessage("文件夹路径不可包含 ! 或 ; 字符！");
    }
}

/// <summary>
///     文件名校验，对应 WPF 版 PCL.Core.Utils.Validate.FileNameValidator 的主要规则。
/// </summary>
public class FileNameValidator : AbstractValidator<string>
{
    private static readonly Regex Ntfs83Pattern = new(@".{2,}~\d", RegexOptions.Compiled);

    public FileNameValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("输入内容不能为空！")
            .Must(x => x is null || !x.StartsWith(' ')).WithMessage("文件名不能以空格开头！")
            .Must(x => x is null || !x.EndsWith(' ')).WithMessage("文件名不能以空格结尾！")
            .Must(x => x is null || !x.EndsWith('.')).WithMessage("文件名不能以小数点结尾！")
            .Must(x => x is null || x.IndexOfAny(Path.GetInvalidFileNameChars()) < 0).WithMessage("文件名包含非法字符！")
            .Must(x => x is null || x.IndexOfAny(['!', ';']) < 0).WithMessage("文件名不可包含 ! 或 ; 字符！")
            .Must(x => x is null || !Ntfs83Pattern.IsMatch(x)).WithMessage("文件名不能包含这一特殊格式！");
    }
}
