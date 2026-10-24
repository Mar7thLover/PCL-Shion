using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Shion.Localization;

namespace PCL.Shion.Pages.Homepage;

/// <summary>
/// Minecraft 新闻列表的数据与加载状态。
/// </summary>
public sealed partial class NewsViewModel : ObservableObject
{
    private const string ApiBaseUrl = "https://net-secondary.web.minecraft-services.net/api/v1.0";
    private const int PageSize = 24;

    private static readonly string[] AllowedNewsLinkHosts =
    [
        "minecraft.net",
        "minecraft-services.net",
        "microsoft.com"
    ];

    private string? _loadedLocale;
    private Task? _loadTask;

    public ObservableCollection<NewsItem> NewsItems { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// 加载新闻，需要在界面线程调用。
    /// 正在加载时直接等待当前请求；已有内容且新闻地区没有变化时，除非指定 <paramref name="reload"/>，否则不会再次请求。
    /// 加载失败不会抛出异常，而是设置 <see cref="ErrorMessage"/>。
    /// </summary>
    /// <param name="reload">是否强制重新请求。重新请求成功前会保留已有的新闻。</param>
    public Task LoadAsync(bool reload = false)
    {
        if (_loadTask is { IsCompleted: false })
            return _loadTask;
        if (!reload && NewsItems.Count > 0 && _loadedLocale == GetNewsLocale(CultureInfo.CurrentUICulture))
            return Task.CompletedTask;

        _loadTask = LoadCoreAsync();
        return _loadTask;
    }

    private async Task LoadCoreAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var locale = GetNewsLocale(CultureInfo.CurrentUICulture);
            var url = $"{ApiBaseUrl}/{locale}/search?pageSize={PageSize}&sortType=Recent&category=News&newsOnly=true&page=1";
            var response = await NewsHttp.Client.GetFromJsonAsync<NewsApiResponse>(url);

            var fresh = response?.Result?.Results ?? [];
            foreach (var item in fresh)
                item.Description = WebUtility.HtmlDecode(item.Description);

            // 请求成功后才替换旧内容，刷新失败时继续显示已有的新闻
            NewsItems.Clear();
            foreach (var item in fresh)
                NewsItems.Add(item);
            _loadedLocale = locale;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"{Lang.Text("Widget.News.Failed")}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 把界面语言映射为 Minecraft 新闻接口使用的地区代码。
    /// 简体中文、繁体中文、日语、法语、西班牙语使用对应地区，其他语言使用 en-us。
    /// </summary>
    internal static string GetNewsLocale(CultureInfo culture)
    {
        var name = culture.Name.ToLowerInvariant();
        return culture.TwoLetterISOLanguageName switch
        {
            "zh" => name.Contains("tw") || name.Contains("hk") || name.Contains("mo") || name.Contains("hant")
                ? "zh-tw"
                : "zh-cn",
            "ja" => "ja-jp",
            "fr" => "fr-fr",
            "es" => "es-es",
            _ => "en-us"
        };
    }

    /// <summary>
    /// 判断链接是否允许作为新闻链接打开：只允许 Minecraft 与微软相关的 http、https 网址。
    /// </summary>
    public static bool CanOpenNewsLink([NotNullWhen(true)] string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
            return false;

        var host = uri.IdnHost;
        foreach (var allowedHost in AllowedNewsLinkHosts)
        {
            if (host.Equals(allowedHost, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith($".{allowedHost}", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
