using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Setup;

public partial class PageSetupAbout : MyPageRight
{
    private const string ShionApiRepoUrl = "https://api.github.com/repos/Mar7thLover/PCL-Shion";

    /// <summary>
    ///     PCL Shion 使用的第三方组件及其许可证。
    /// </summary>
    private static readonly ThirdPartyLicense[] ThirdPartyLicenses =
    [
        new("Avalonia", "跨平台 .NET UI 框架", "https://avaloniaui.net",
            "https://github.com/AvaloniaUI/Avalonia/blob/master/LICENSE.TXT"),
        new("CommunityToolkit.Mvvm", "MVVM 基础设施与源代码生成器", "https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/",
            "https://github.com/CommunityToolkit/dotnet/blob/main/LICENSE.TXT"),
        new("FluentValidation", "强类型的验证规则库", "https://fluentvalidation.net",
            "https://github.com/FluentValidation/FluentValidation/blob/main/LICENSE"),
        new("Humanizer", "字符串、枚举与时间的人性化格式化", "https://github.com/Humanizer/Humanizer",
            "https://github.com/Humanizer/Humanizer/blob/main/LICENSE")
    ];

    // 彩蛋
    private int clickCount;

    private bool isLoaded;

    public PageSetupAbout()
    {
        InitializeComponent();
        PanScroll = PanBack; // 不知道为啥不能在 XAML 设置
        Loaded += PageOtherAbout_Loaded;
    }

    public ObservableCollection<GitHubContributor> Contributors { get; set; } = new();

    private void PageOtherAbout_Loaded(object? sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        ItemAboutPcl.Info = ItemAboutPcl.Info.Replace("%VERSION%", AppInfo.VersionText)
            .Replace("%VERSIONCODE%", AppInfo.Version.Build.ToString()).Replace("%BRANCH%", "PCL Shion")
            .Replace("%COMMIT_HASH%", "native");

        if (!Lang.IsChineseMainland)
        {
            ItemMcmod.IsVisible = false;
            BtnMcmod.IsVisible = false;
            ImgMcmod.IsVisible = false;
            GridSpecialThanks.RowDefinitions[1].Height = new GridLength(0);
        }

        LoadLicenses();
        LoadContributersAsync();
    }

    private void LoadLicenses()
    {
        foreach (var license in ThirdPartyLicenses)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "LicenseName" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });

            var name = new TextBlock
            {
                Text = license.Name,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(name, 0);
            Grid.SetColumn(name, 0);
            grid.Children.Add(name);

            var information = new TextBlock
            {
                Text = license.Information,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(information, 0);
            Grid.SetColumn(information, 2);
            Grid.SetColumnSpan(information, 2);
            grid.Children.Add(information);

            if (license.WebsiteUri is not null)
            {
                var website = new MyButton
                {
                    MinWidth = 140,
                    Margin = new Thickness(0, 7, 20, 18),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(13, 0),
                    Text = Lang.Text("Setup.About.Licenses.ViewWebsite"),
                    Tag = license.WebsiteUri
                };
                website.Click += OpenUrlButton_Click;
                Grid.SetRow(website, 1);
                Grid.SetColumn(website, 2);
                grid.Children.Add(website);
            }

            if (license.LicenseUri is not null)
            {
                var licenseButton = new MyButton
                {
                    MinWidth = 140,
                    Margin = new Thickness(0, 7, 20, 18),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(13, 0),
                    Text = Lang.Text("Setup.About.Licenses.ViewLicense"),
                    Tag = license.LicenseUri
                };
                licenseButton.Click += OpenUrlButton_Click;
                Grid.SetRow(licenseButton, 1);
                Grid.SetColumn(licenseButton, 3);
                grid.Children.Add(licenseButton);
            }

            PanLicenses.Children.Add(grid);
        }
    }

    private async void LoadContributersAsync()
    {
        try
        {
            var json = await App.Services.Engine.Http.GetStringAsync(ShionApiRepoUrl + "/contributors");
            var cos = JsonSerializer.Deserialize<List<GitHubContributor>>(json) ?? [];
            Contributors.Clear();
            PanContributors.Children.Clear();
            foreach (var item in cos)
            {
                Contributors.Add(item);
                PanContributors.Children.Add(CreateContributorItem(item));
            }
        }
        catch (Exception ex)
        {
            SetupConfig.Log(ex, Lang.Text("Setup.About.Error.LoadContributorsFailed"));
        }
    }

    private static Control CreateContributorItem(GitHubContributor item)
    {
        var avatar = new MyImage
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Source = item.AvatarUrl
        };
        RenderOptions.SetBitmapInterpolationMode(avatar, BitmapInterpolationMode.HighQuality);

        var login = new MyTextButton
        {
            MaxWidth = 90,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 14,
            Text = item.Login ?? "",
            Tag = item.HtmlUrl
        };
        ToolTip.SetTip(login, item.Login);
        login.Click += OpenUrlTextButton_Click;

        var panel = new StackPanel
        {
            Width = 72,
            Margin = new Thickness(4, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            Orientation = Orientation.Vertical
        };
        panel.Children.Add(avatar);
        panel.Children.Add(login);
        return panel;
    }

    private void ImgPCLCommunity_Click(object? sender, PointerPressedEventArgs e)
    {
        ModAnimation.AniStart(new[] { ModAnimation.AaRotateTransform(sender!, 360d) });
    }

    private async void ImgPCLLogo_Click(object? sender, PointerPressedEventArgs e)
    {
        if (clickCount < 200)
        {
            clickCount += 1;
            switch (clickCount)
            {
                case 5:
                {
                    HintService.Hint(Lang.Text("Setup.About.EasterEgg.NiceClick"));
                    break;
                }
                case 15:
                {
                    HintService.Hint(Lang.Text("Setup.About.EasterEgg.StillClicking"));
                    break;
                }
                case 25:
                {
                    switch (await MsgBox.ShowAsync(Lang.Text("Setup.About.EasterEgg.Bored.Message"), Lang.Text("Setup.About.EasterEgg.Bored.Title"), Lang.Text("Setup.About.EasterEgg.Bored.Yes"), Lang.Text("Setup.About.EasterEgg.Bored.No")))
                    {
                        case 2:
                        {
                            HintService.Hint(Lang.Text("Setup.About.EasterEgg.Bored.Response"));
                            break;
                        }
                    }

                    break;
                }
                case 50:
                {
                    HintService.Hint(Lang.Text("Setup.About.EasterEgg.Encouragement"));
                    break;
                }
                case 75:
                {
                    HintService.Hint(Lang.Text("Setup.About.EasterEgg.HiddenTheme"));
                    break;
                }
                case 100:
                {
                    HintService.Hint(Lang.Text("Setup.About.EasterEgg.StillStaring"));
                    break;
                }
                case 130:
                {
                    HintService.Hint(Lang.Text("Setup.About.EasterEgg.NothingBehind"));
                    break;
                }
                case 150:
                {
                    switch (await MsgBox.ShowAsync(Lang.Text("Setup.About.EasterEgg.Tired.Message1"), Lang.Text("Setup.About.EasterEgg.Tired.Title1"), Lang.Text("Setup.About.EasterEgg.Tired.Exhausted"), Lang.Text("Setup.About.EasterEgg.Tired.NotTired")))
                    {
                        case 1:
                        {
                            HintService.Hint(Lang.Text("Setup.About.EasterEgg.Tired.StopClicking"));
                            break;
                        }
                        case 2:
                        {
                            switch (await MsgBox.ShowAsync(Lang.Text("Setup.About.EasterEgg.Tired.Message2"), Lang.Text("Setup.About.EasterEgg.Tired.Title2"), Lang.Text("Setup.About.EasterEgg.Tired.Exhausted"), Lang.Text("Setup.About.EasterEgg.Tired.NotTired")))
                            {
                                case 1:
                                {
                                    HintService.Hint(Lang.Text("Setup.About.EasterEgg.Tired.StopClicking"));
                                    break;
                                }
                                case 2:
                                {
                                    switch (await MsgBox.ShowAsync(Lang.Text("Setup.About.EasterEgg.Tired.Message3"), Lang.Text("Setup.About.EasterEgg.Tired.Title3"), Lang.Text("Setup.About.EasterEgg.Tired.Exhausted"), Lang.Text("Setup.About.EasterEgg.Tired.ReallyNotTired")))
                                    {
                                        case 1:
                                        {
                                            HintService.Hint(Lang.Text("Setup.About.EasterEgg.Tired.StopClicking"));
                                            break;
                                        }
                                        case 2:
                                        {
                                            HintService.Hint(Lang.Text("Setup.About.EasterEgg.Tired.FinallyGiveUp"));
                                            break;
                                        }
                                    }

                                    break;
                                }
                            }

                            break;
                        }
                    }

                    break;
                }
                case 200:
                {
                    HintService.Hint(Lang.Text("Setup.About.EasterEgg.ClickDisabled"));
                    ImgPCLLogo.IsHitTestVisible = false;
                    return;
                }
            }

            var rand = new Random();
            var mx = rand.Next(-1, 1);
            if (mx == 0)
                mx = 1;
            var my = rand.Next(-1, 1);
            if (my == 0)
                my = 1;
            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaTranslateX(sender!, mx, 0), ModAnimation.AaTranslateY(sender!, my, 0),
                ModAnimation.AaTranslateX(sender!, -mx, 0, 100), ModAnimation.AaTranslateY(sender!, -my, 0, 100)
            });
        }
    }

    private static void OpenUrlButton_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control { Tag: string url })
            SystemShell.OpenUrl(url);
    }

    private static void OpenUrlTextButton_Click(object? sender, EventArgs? e)
    {
        if (sender is Control { Tag: string url })
            SystemShell.OpenUrl(url);
    }

    private sealed record ThirdPartyLicense(string Name, string Information, string? WebsiteUri, string? LicenseUri);

    public class GitHubContributor
    {
        [JsonPropertyName("login")] public string Login { get; set; }

        [JsonPropertyName("avatar_url")] public string AvatarUrl { get; set; }

        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; }

        [JsonPropertyName("contributions")] public int Contributions { get; set; }
    }
}
