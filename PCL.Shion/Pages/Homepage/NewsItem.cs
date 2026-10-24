using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace PCL.Shion.Pages.Homepage;

/// <summary>
/// Minecraft 新闻列表中的一条新闻。
/// </summary>
public sealed class NewsItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; } = "";

    [JsonPropertyName("imageAltText")]
    public string? ImageAltText { get; set; }

    /// <summary>发布时间，Unix 时间戳（秒）。</summary>
    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonIgnore]
    public DateTime PublishDate => DateTimeOffset.FromUnixTimeSeconds(Time).LocalDateTime;

    /// <summary>按界面语言的日期加时间文字，用于新闻卡片。</summary>
    [JsonIgnore]
    public string PublishDateText => FormatPublishDate("g");

    /// <summary>按界面语言格式化发布时间。</summary>
    public string FormatPublishDate(string format) => PublishDate.ToString(format, CultureInfo.CurrentUICulture);
}

internal sealed class NewsApiResponse
{
    [JsonPropertyName("result")]
    public NewsApiResult? Result { get; set; }
}

internal sealed class NewsApiResult
{
    [JsonPropertyName("results")]
    public List<NewsItem>? Results { get; set; }
}
