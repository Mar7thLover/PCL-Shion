using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Services;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Setup;

public partial class PageSetupFeedback : MyPageRight
{
    private const string ShionApiRepoUrl = "https://api.github.com/repos/Mar7thLover/PCL-Shion";

    public enum TagId : long
    {
        Processing = 6820804544L,
        WaitingProcess = 6820804546L,
        Completed = 6820804547L,
        Decline = 6820804539L,
        Ignored = 8064650117L,
        Duplicate = 6820804541L,
        Wait = 8743070786L,
        Pause = 8558220235L,
        Upnext = 8550609020L
    }

    private bool _isLoaded;

    public ModLoader.LoaderTask<bool, List<Feedback>> Loader;

    private static readonly Dictionary<string, (Func<PageSetupFeedback, StackPanel> Panel, string Icon)> TagMap = new()
    {
        [((long)TagId.Processing).ToString()] = (p => p.PanListProcessing, "Blocks/CommandBlock.png"),
        [((long)TagId.WaitingProcess).ToString()] = (p => p.PanListWaitingProcess, "Blocks/RedstoneBlock.png"),
        [((long)TagId.Wait).ToString()] = (p => p.PanListWait, "Blocks/Anvil.png"),
        [((long)TagId.Pause).ToString()] = (p => p.PanListPause, "Blocks/RedstoneLampOff.png"),
        [((long)TagId.Upnext).ToString()] = (p => p.PanListUpnext, "Blocks/RedstoneLampOn.png"),
        [((long)TagId.Completed).ToString()] = (p => p.PanListCompleted, "Blocks/Grass.png"),
        [((long)TagId.Decline).ToString()] = (p => p.PanListDecline, "Blocks/CobbleStone.png"),
        [((long)TagId.Ignored).ToString()] = (p => p.PanListIgnored, "Blocks/CobbleStone.png"),
        [((long)TagId.Duplicate).ToString()] = (p => p.PanListDuplicate, "Blocks/CobbleStone.png"),
    };

    public PageSetupFeedback()
    {
        InitializeComponent();
        PanScroll = PanBack; // 不知道为啥不能在 XAML 设置
        Loader = new ModLoader.LoaderTask<bool, List<Feedback>>("FeedbackList", FeedbackListGet);
        Loaded += PageOtherFeedback_Loaded;
    }

    private void PageOtherFeedback_Loaded(object? sender, RoutedEventArgs e)
    {
        PageLoaderInit(Load, PanLoad, PanContent, PanInfo, Loader, _ => RefreshList());
        // 重复加载部分
        PanBack.ScrollToHome();
        // 非重复加载部分
        if (_isLoaded)
            return;
        _isLoaded = true;
    }

    public void FeedbackListGet(ModLoader.LoaderTask<bool, List<Feedback>> task)
    {
        var json = FetchIssuesJson(ShionApiRepoUrl + "/issues?state=all&sort=created&per_page=200"); // 获取近期 200 条数据就够了
        var list = JsonNode.Parse(json) as JsonArray;
        if (list is null)
            throw new Exception(Lang.Text("Setup.Feedback.LoadFailed"));
        var res = new List<Feedback>();
        foreach (var i in list)
        {
            if (i is not JsonObject issue) continue;
            var pullRequestToken = issue["pull_request"];
            if (pullRequestToken is not null && pullRequestToken.GetValueKind() != JsonValueKind.Null) continue;

            var item = issue.Deserialize<Feedback>()!;
            item.User = issue["user"]!["login"]!.ToString();
            item.Id = issue["number"]!.ToString();

            var issueType = Lang.Text("Setup.Feedback.Uncategorized");
            if (issue["type"] is JsonObject typeObject && typeObject["name"] is { } typeNameToken)
                issueType = typeNameToken.ToString().ToLower();

            item.Type = issueType;

            if (issue["labels"] is JsonArray thisTags)
                foreach (var thisTag in thisTags)
                    if (thisTag is JsonObject tagObj)
                        item.Tags.Add(tagObj["id"]!.ToString());

            res.Add(item);
        }

        task.output = res;
    }

    /// <summary>
    ///     请求 GitHub 接口，失败时最多重试 3 次。
    /// </summary>
    private static string FetchIssuesJson(string url)
    {
        Exception? last = null;
        for (var attempt = 0; attempt <= 3; attempt++)
        {
            try
            {
                return App.Services.Engine.Http.GetStringAsync(url).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new Exception(Lang.Text("Setup.Feedback.LoadFailed"), last);
    }

    private MyListItem CreateFeedbackItem(Feedback item, string logo)
    {
        var li = new MyListItem
        {
            Title = item.Title,
            Type = MyListItem.CheckType.Clickable,
            Info = $"{item.User} | {Lang.Date(item.Time)}",
            Logo = "avares://PCL.Shion/Assets/Images/" + logo,
            Tags = item.Type
        };

        li.Click += async (_, _) => await ShowFeedbackDetailAsync(item);

        return li;
    }

    private async Task ShowFeedbackDetailAsync(Feedback item)
    {
        var timeSpanText = Lang.TimeSpan(item.Time - DateTime.Now);
        if (await MsgBox.ShowMarkdownAsync(
                Lang.Text("Setup.Feedback.Item.Submitter", item.User, timeSpanText) + "\n" +
                Lang.Text("Setup.Feedback.Item.Type", item.Type) + "\n\n" +
                item.Content,
                $"#{item.Id} {item.Title}", button2: Lang.Text("Setup.Feedback.Item.ViewDetail")) == 2)
        {
            SystemShell.OpenUrl(item.Url);
        }
    }

    private void SetPanelVisibility(StackPanel panel, MyCard card)
    {
        card.IsVisible = panel.Children.Count != 0;
    }

    public void RefreshList()
    {
        PanListProcessing.Children.Clear();
        PanListWaitingProcess.Children.Clear();
        PanListWait.Children.Clear();
        PanListPause.Children.Clear();
        PanListUpnext.Children.Clear();
        PanListCompleted.Children.Clear();
        PanListDecline.Children.Clear();
        PanListIgnored.Children.Clear();
        PanListDuplicate.Children.Clear();

        foreach (var item in Loader.output ?? new List<Feedback>())
        {
            var tag = item.Tags.Find(TagMap.ContainsKey);
            if (tag is not null)
            {
                var (panel, icon) = TagMap[tag];
                panel(this).Children.Add(CreateFeedbackItem(item, icon));
            }
        }

        SetPanelVisibility(PanListProcessing, PanContentProcessing);
        SetPanelVisibility(PanListWaitingProcess, PanContentWaitingProcess);
        SetPanelVisibility(PanListWait, PanContentWait);
        SetPanelVisibility(PanListPause, PanContentPause);
        SetPanelVisibility(PanListUpnext, PanContentUpnext);
        SetPanelVisibility(PanListCompleted, PanContentCompleted);
        SetPanelVisibility(PanListDecline, PanContentDecline);
        SetPanelVisibility(PanListIgnored, PanContentIgnored);
        SetPanelVisibility(PanListDuplicate, PanContentDuplicate);
    }

    private void Feedback_Click(object? sender, PointerReleasedEventArgs e)
    {
        _ = PageSetupLeft.TryFeedbackAsync();
    }

    public class Feedback
    {
        [JsonIgnore]
        public string User { get; set; } = string.Empty;
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;
        [JsonPropertyName("created_at")]
        public DateTime Time { get; init; }
        [JsonPropertyName("body")]
        public string Content { get; init; } = string.Empty;
        [JsonPropertyName("html_url")]
        public string Url { get; init; } = string.Empty;
        [JsonIgnore]
        public string Id { get; set; } = string.Empty;
        public List<string> Tags { get; } = new();
        public string Type { get; set; } = string.Empty;
    }
}
