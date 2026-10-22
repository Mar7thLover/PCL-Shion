using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Model.Tools.News;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http;

namespace PCL.Core.ViewModel.Homepage;

public partial class NewsViewModel : ObservableObject
{
    private const string ApiBaseUrl = "https://net-secondary.web.minecraft-services.net/api/v1.0";
    private const string DefaultNewsLocale = "en-us";
    private const int PageSize = 24;
    private static readonly string[] AllowedNewsLinkHosts =
    [
        "minecraft.net",
        "minecraft-services.net",
        "microsoft.com"
    ];
    private int _currentPage = 1;
    private string? _loadedLocale;
    private Task? _loadTask;

    public ObservableCollection<NewsItem> NewsItems { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// 创建视图模型，并立即开始加载新闻。
    /// </summary>
    public NewsViewModel() : this(true)
    {
    }

    /// <summary>
    /// 创建视图模型。
    /// </summary>
    /// <param name="loadImmediately">是否立即开始加载新闻。传入 false 时由调用方在合适的时机调用 <see cref="LoadAsync"/>。</param>
    public NewsViewModel(bool loadImmediately)
    {
        if (loadImmediately) _ = LoadAsync();
    }

    /// <summary>
    /// 加载新闻。需要在 UI 线程调用。
    /// 正在加载时不会重复请求，而是直接等待当前请求；已有内容且新闻地区没有变化时，除非指定 <paramref name="reload"/>，否则不会再次请求。
    /// 加载失败不会抛出异常，而是记录日志并设置 <see cref="ErrorMessage"/>。
    /// </summary>
    /// <param name="reload">是否强制重新请求。重新请求成功前会保留已有的新闻。</param>
    public Task LoadAsync(bool reload = false)
    {
        if (_loadTask is { IsCompleted: false }) return _loadTask;
        if (!reload && NewsItems.Count > 0 && _loadedLocale == _GetCurrentNewsLocale()) return Task.CompletedTask;

        _currentPage = 1;
        _loadTask = _LoadDataAsync();
        return _loadTask;
    }

    [RelayCommand]
#pragma warning disable IDE1006 // 命名样式
    private async Task _LoadDataAsync()
#pragma warning restore IDE1006 // 命名样式
    {
        if (IsLoading) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var locale = _GetCurrentNewsLocale();
            var url = $"{ApiBaseUrl}/{locale}/search?pageSize={PageSize}&sortType=Recent&category=News&newsOnly=true&page={_currentPage}";
            using var resp = await HttpRequest
                .Create(url)
                .SendAsync();

            resp.EnsureSuccessStatusCode();
            var json = await resp.AsJsonAsync<ApiResponse>();

            var fresh = new List<NewsItem>();
            if (json?.Result?.Results is not null)
            {
                foreach (var item in json.Result.Results)
                {
                    item.Description = WebUtility.HtmlDecode(item.Description);
                    fresh.Add(item);
                }
            }

            // 请求成功后才替换旧内容，刷新失败时继续显示已有的新闻
            NewsItems.Clear();
            foreach (var item in fresh) NewsItems.Add(item);
            _loadedLocale = locale;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"{Lang.Text("Widget.News.Failed")}: {ex.Message}";
            LogWrapper.Error(ex, "Minecraft 信息流主页加载失败");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
#pragma warning disable IDE1006 // 命名样式
    private void _OpenRead(string? url)
#pragma warning restore IDE1006 // 命名样式
    {
        if (!IsSafeNewsLink(url))
        {
            LogWrapper.Warn("Homepage", $"已拦截不安全的 Minecraft 信息流链接：{url ?? "<null>"}");
            return;
        }

        Basics.OpenPath(url!);
    }

    /// <summary>
    /// 把界面语言代码映射为 Minecraft 新闻接口使用的地区代码。
    /// 简体中文、繁体中文、日语、法语、西班牙语使用对应地区，其他语言（包括 en-US、en-GB）使用 en-us。
    /// </summary>
    /// <param name="languageCode">界面语言代码，例如 zh-CN。</param>
    internal static string GetNewsLocale(string? languageCode)
    {
        return languageCode?.Trim().ToLowerInvariant() switch
        {
            "zh-cn" => "zh-cn",
            "zh-tw" => "zh-tw",
            "ja-jp" => "ja-jp",
            "fr-fr" => "fr-fr",
            "es-es" => "es-es",
            _ => DefaultNewsLocale
        };
    }

    private static string _GetCurrentNewsLocale() => GetNewsLocale(LocalizationService.CurrentLanguage.Code);

    /// <summary>
    /// 判断链接是否允许作为新闻链接打开。仅供界面层在打开链接前校验使用。
    /// </summary>
    public static bool CanOpenNewsLink(string? url) => IsSafeNewsLink(url);

    internal static bool IsSafeNewsLink(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = uri.IdnHost;
        foreach (var allowedHost in AllowedNewsLinkHosts)
        {
            if (host.Equals(allowedHost, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith($".{allowedHost}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

}
