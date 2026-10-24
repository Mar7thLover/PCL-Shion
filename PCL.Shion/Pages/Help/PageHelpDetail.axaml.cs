using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PCL.Shion.Controls;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Homepage;
using PCL.Shion.Services;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Help;

public partial class PageHelpDetail : MyPageRight, IRefreshable
{
    internal sealed record HelpContent(string SourcePath, string Title, string Xaml);

    private HelpContent _content = null!;

    public override string? Title => _content.Title;

    internal PageHelpDetail(HelpContent content)
    {
        InitializeComponent();
        PanScroll = PanBack;
        Loaded += (_, _) => PanBack.ScrollToVerticalOffset(0d);
        ApplyContent(content);
    }

    /// <summary>
    ///     从帮助 JSON 及同名 XAML 文件读取详情页数据。失败会抛出异常。
    /// </summary>
    internal static HelpContent LoadContent(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("未找到帮助 JSON 文件", jsonPath);

        var json = ArgumentReplace(File.ReadAllText(jsonPath), SecurityElement.Escape);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("Title", out var titleElement) ||
            titleElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(titleElement.GetString()))
            throw new ArgumentException("帮助 JSON 中未找到有效的 Title 项", nameof(jsonPath));

        var xamlPath = Path.ChangeExtension(jsonPath, ".xaml");
        if (!File.Exists(xamlPath))
            throw new FileNotFoundException("未找到帮助 JSON 对应的 XAML 文件", xamlPath);

        var xaml = File.ReadAllText(xamlPath);
        if (string.IsNullOrWhiteSpace(xaml))
            throw new InvalidDataException("帮助 XAML 文件为空");

        return new HelpContent(jsonPath, titleElement.GetString()!, xaml);
    }

    private void ApplyContent(HelpContent content)
    {
        // 修改时应同时修改 PageLaunchRight.LoadContent。
        var xaml = HomepageSupport.WrapPanel(HomepageSupport.StripXmlns(ArgumentReplace(content.Xaml)));
        if (HomepageSupport.LoadXaml(xaml) is not Control element)
            throw new InvalidDataException("帮助 XAML 的根元素不是控件");
        ApplyElementDefaults(element);

        _content = content;
        PanCustom.Children.Clear();
        PanCustom.Children.Add(element);
        if (Navigator.Current == PageType.HelpDetail)
            Navigator.RefreshTitle();
    }

    /// <summary>
    ///     对应 WPF 中的样式：卡片默认的外边距与图片的插值方式、居中方式，内容中显式设置的值优先。
    /// </summary>
    private static void ApplyElementDefaults(Control root)
    {
        foreach (var card in root.GetSelfAndVisualDescendants().OfType<MyCard>())
            if (!card.IsSet(Layoutable.MarginProperty))
                card.Margin = new Thickness(0d, 0d, 0d, 15d);

        foreach (var image in root.GetSelfAndVisualDescendants().OfType<Image>())
        {
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
            if (!image.IsSet(Layoutable.HorizontalAlignmentProperty))
                image.HorizontalAlignment = HorizontalAlignment.Center;
        }
    }

    private static string ArgumentReplace(string text, Func<string, string>? escape = null)
    {
        escape ??= value => value;
        var now = DateTime.Now;
        return text
            .Replace("{pcl_version}", escape(AppInfo.VersionText))
            .Replace("{path}", escape(HomepageSupport.ExeDirectory.TrimEnd(Path.DirectorySeparatorChar)))
            .Replace("{path_temp}", escape(HomepageSupport.TempDirectory.TrimEnd(Path.DirectorySeparatorChar)))
            .Replace("{date}", now.ToString("d", CultureInfo.CurrentCulture))
            .Replace("{time}", now.ToString("T", CultureInfo.CurrentCulture))
            .Replace("{minecraft}", escape(App.Services.Settings.MinecraftRoot));
    }

    public void Refresh()
    {
        try
        {
            ApplyContent(LoadContent(_content.SourcePath));
        }
        catch (Exception ex)
        {
            HomepageSupport.Log(ex, "刷新帮助详情页失败");
            _ = MsgBox.ShowAsync(Lang.Text("Event.Error.ExecutionFailed", "OpenHelp", _content.SourcePath),
                Lang.Text("Event.Error.Title"));
        }
    }
}
