using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace PCL.Shion.Controls;

public class MyImage : Image
{
    private const string PackImagePrefix = "pack://application:,,,/images/";
    private const string PackComponentPrefix = "pack://application:,,,/Plain Craft Launcher 2;component/Images/";
    private const string ComponentPrefix = "/Plain Craft Launcher 2;component/Images/";
    private const string AssetImagePrefix = "avares://PCL.Shion/Assets/Images/";

    private static readonly string CacheFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCL Shion", "Cache", "Images");

    private static readonly HttpClient _httpClient = new();

    private static readonly ConcurrentDictionary<string, Task<string>> _downloadTasks = new();

    private bool _isInitialized;
    private string? _actualSource;

    public MyImage()
    {
        Loaded += (_, _) =>
        {
            if (_isInitialized)
                return;
            _isInitialized = true;
            Load();
        };
        SizeChanged += (_, _) => UpdateClip();
    }

    /// <summary>
    ///     实际被呈现的图片地址。
    /// </summary>
    public string? ActualSource
    {
        get => _actualSource;
        set
        {
            if (string.IsNullOrEmpty(value))
                value = null;
            if ((_actualSource ?? "") == (value ?? ""))
                return;
            _actualSource = value;
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    if (value is null)
                    {
                        SetActualBitmap(null);
                    }
                    else
                    {
                        var source = value;
                        SetActualBitmap(await Task.Run(() => LoadBitmap(source)));
                    }
                }
                catch (Exception ex)
                {
                    Log(ex, $"加载图片失败（{value}）");
                    try
                    {
                        if (value is not null && value.StartsWith(CacheFolder, StringComparison.Ordinal) && File.Exists(value))
                            File.Delete(value);
                    }
                    catch
                    {
                    }
                }
            }); // 在这里先触发可能的文件读取，尽量避免在 UI 线程中读取文件
        }
    }

    private void SetActualBitmap(Bitmap? bitmap)
    {
        base.Source = bitmap;
    }

    private static Bitmap LoadBitmap(string filePathOrResourceName)
    {
        if (filePathOrResourceName.StartsWith(PackImagePrefix, StringComparison.Ordinal))
            filePathOrResourceName = AssetImagePrefix + filePathOrResourceName[PackImagePrefix.Length..];
        else if (filePathOrResourceName.StartsWith(PackComponentPrefix, StringComparison.Ordinal))
            filePathOrResourceName = AssetImagePrefix + filePathOrResourceName[PackComponentPrefix.Length..];
        else if (filePathOrResourceName.StartsWith(ComponentPrefix, StringComparison.Ordinal))
            filePathOrResourceName = AssetImagePrefix + filePathOrResourceName[ComponentPrefix.Length..];

        if (filePathOrResourceName.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
        {
            using (var resourceStream = AssetLoader.Open(new Uri(filePathOrResourceName)))
            {
                return new Bitmap(resourceStream);
            }
        }

        // 使用这种自己接管 FileStream 的方法加载才能解除文件占用
        using (var picStream = new FileStream(filePathOrResourceName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            return new Bitmap(picStream);
        }
    }

    private void Load() // 属性读取顺序修正：在完成 XAML 属性读取后再触发图片加载（#4868）
    {
        if (Source is null)
        {
            ActualSource = null;
            return;
        }

        if (!Source.StartsWith("http", StringComparison.Ordinal))
        {
            ActualSource = Source;
            return;
        }

        var url = Source;
        var tempPath = GetTempPath(url);
        var tempFile = new FileInfo(tempPath);
        var enableCache = EnableCache;
        if (enableCache && tempFile.Exists)
        {
            ActualSource = tempPath;
            if (DateTime.Now - tempFile.LastWriteTime < fileCacheExpiredTime)
                return; // 无需刷新缓存
        }

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                // 下载
                ActualSource = LoadingSource;

                var resp = await DownloadImageAsync(url);
                if (!string.IsNullOrEmpty(resp))
                {
                    ActualSource = resp;
                    return;
                }

                resp = await DownloadImageAsync(FallbackSource!);
                if (!string.IsNullOrEmpty(resp))
                {
                    ActualSource = resp;
                    return;
                }
            }
            catch (Exception ex)
            {
                // 更换备用地址
                Log(ex, $"Online image get fail（source = {url}, fallback = {FallbackSource}）", LogEventLevel.Debug);
                tempPath = GetTempPath(url);
                tempFile = new FileInfo(tempPath);
                if (enableCache && tempFile.Exists)
                {
                    ActualSource = tempPath;
                    if (DateTime.Now - tempFile.LastWriteTime < fileCacheExpiredTime)
                        return;
                }
            }
        });
    }

    public static Task<string> DownloadImageAsync(string url)
    {
        return _downloadTasks.GetOrAdd(url, key =>
        {
            var t = DownloadImageInternalAsync(key);
            t.ContinueWith(_ => _downloadTasks.TryRemove(url, out _));
            return t;
        });
    }

    public static string GetTempPath(string url)
    {
        return Path.Combine(CacheFolder, $"{GetStringMD5(url)}.png");
    }

    private static string GetStringMD5(string str)
    {
        return Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(str)));
    }

    private static async Task<string> DownloadImageInternalAsync(string url)
    {
        var tempPath = GetTempPath(url);
        var tempDownloadingPath = tempPath + Random.Shared.Next(0, 1000000);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!); // 重新实现下载，以避免携带 Header（#5072）
            using (var fs = new FileStream(tempDownloadingPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using (var nfs = await response.Content.ReadAsStreamAsync())
                    {
                        fs.SetLength(0L);
                        await nfs.CopyToAsync(fs);
                    }
                }
            }

            File.Move(tempDownloadingPath, tempPath, true);
            return tempPath;
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (File.Exists(tempDownloadingPath)) File.Delete(tempDownloadingPath);

            Log(ex, $"Try to get online image fail (url = {url}, dest = {tempPath})");
            return string.Empty;
        }
    }

    private static void Log(Exception ex, string message, LogEventLevel level = LogEventLevel.Error)
    {
        Logger.TryGet(level, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);
    }

    #region 公开属性

    public TimeSpan fileCacheExpiredTime = TimeSpan.FromDays(14d);

    public bool EnableCache
    {
        get => GetValue(EnableCacheProperty);
        set => SetValue(EnableCacheProperty, value);
    }

    public static readonly StyledProperty<bool> EnableCacheProperty =
        AvaloniaProperty.Register<MyImage, bool>(nameof(EnableCache), true);

    /// <summary>
    ///     与 Image 的 Source 类似。
    ///     若输入以 http 开头的字符串，则会尝试下载图片然后显示，图片会保存为本地缓存。
    ///     支持 WebP 格式的图片。
    /// </summary>
    public new string? Source // 覆写 Image 的 Source 属性
    {
        get => GetValue(SourceProperty);
        set
        {
            if (string.IsNullOrEmpty(value))
                value = null;
            if ((GetValue(SourceProperty) ?? "") == (value ?? ""))
                return;
            SetValue(SourceProperty, value);
        }
    }

    public new static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<MyImage, string?>(nameof(Source), string.Empty);

    /// <summary>
    ///     当 Source 首次下载失败时，会从该备用地址加载图片。
    /// </summary>
    public string? FallbackSource { get; set; }

    /// <summary>
    ///     正在下载网络图片时显示的本地图片。
    /// </summary>
    public string? LoadingSource { get; set; } = "pack://application:,,,/images/Icons/NoIcon.png";

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MyImage, CornerRadius>(nameof(CornerRadius), new CornerRadius(-1));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            if (!_isInitialized)
                return; // 属性读取顺序修正：在完成 XAML 属性读取后再触发图片加载（#4868）
            Load();
        }
        else if (change.Property == CornerRadiusProperty)
        {
            UpdateClip();
        }
    }

    private void UpdateClip() // Handles Me.SizeChanged will be added separately
    {
        if (Bounds.Width > 0 && Bounds.Height > 0 &&
            CornerRadius.TopLeft >= 0 && CornerRadius.TopRight >= 0)
        {
            Clip = new RectangleGeometry(
                new Rect(0, 0, Bounds.Width, Bounds.Height),
                CornerRadius.TopLeft,
                CornerRadius.TopRight);
        }
    }

    #endregion
}
