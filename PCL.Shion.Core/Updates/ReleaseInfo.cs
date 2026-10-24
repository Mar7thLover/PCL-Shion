namespace PCL.Shion.Core.Updates;

/// <summary>
/// GitHub 上发布的一个版本。
/// </summary>
/// <param name="Tag">发布标签，如 v0.2.0。</param>
/// <param name="Version">从标签解析出的版本号，不含预发布后缀。</param>
/// <param name="Name">发布标题；没有标题时为标签。</param>
/// <param name="HtmlUrl">发布页面的地址。</param>
/// <param name="PublishedAt">发布时间。</param>
/// <param name="Prerelease">是否为预发布版本。</param>
public sealed record ReleaseInfo(string Tag, Version Version, string Name, Uri HtmlUrl, DateTimeOffset PublishedAt, bool Prerelease);
