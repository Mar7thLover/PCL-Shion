using PCL.Core.App;

namespace PCL;

/// <summary>
///     主页预设：可选的预设列表，以及读写用户勾选的预设（首页以选项卡切换显示）。
/// </summary>
public static class HomepagePresets
{
    /// <summary>
    ///     一个主页预设。<see cref="Id" /> 与旧版单选设置 UiCustomPreset 的取值一致。
    /// </summary>
    /// <param name="Id">预设编号。</param>
    /// <param name="NameKey">名称的语言键。</param>
    /// <param name="Url">联网主页地址；内置预设为 null。</param>
    public sealed record Preset(int Id, string NameKey, string? Url);

    public const int Trivia = 0;
    public const int McOfficialFeed = 15;

    /// <summary>
    ///     所有可选的预设，按设置页的显示顺序排列。已移除的回声洞（1）不在其中。
    /// </summary>
    public static readonly IReadOnlyList<Preset> All =
    [
        new(Trivia, "Setup.Ui.Homepage.Preset.Trivia", null),
        new(2, "Setup.Ui.Homepage.Preset.McNews", "https://news.bugjump.net"),
        new(3, "Setup.Ui.Homepage.Preset.DailyModpack", "https://pclsub.sodamc.com/"),
        new(4, "Setup.Ui.Homepage.Preset.McSkin", "https://forgepixel.com/pcl_sub_file"),
        new(5, "Setup.Ui.Homepage.Preset.OpenBmclapi", "https://pcl-bmcl.milu.ink/"),
        new(6, "Setup.Ui.Homepage.Preset.PclManual", "https://raw.gitcode.com/WForst-Breeze/WhatsNewPCL/raw/main/Custom.xaml"),
        new(7, "Setup.Ui.Homepage.Preset.Magazine", "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Ext1nguisher/Custom.xaml"),
        new(8, "Setup.Ui.Homepage.Preset.GitHub", "https://ddf.pcl-community.org/Custom.xaml"),
        new(9, "Setup.Ui.Homepage.Preset.McUpdateSummary", "https://raw.gitcode.com/ENC_Euphony/PCL-AI-Summary-HomePage/raw/master/Custom.xaml"),
        new(10, "Setup.Ui.Homepage.Preset.NewsToday", "https://pcl.wyc-w.top/index.xaml"),
        new(11, "Setup.Ui.Homepage.Preset.MinecraftKnowledge", "https://www.xxag.top/mkss"),
        new(12, "Setup.Ui.Homepage.Preset.ModpackRecommendation", "https://qawsedrftgyhujiko.fun/pcl2/Custom.xaml"),
        new(13, "Setup.Ui.Homepage.Preset.Bangumi", "https://bangumi.p.kaphia.qzz.io"),
        new(14, "Setup.Ui.Homepage.Preset.PclAnnouncement", "https://s3.pysio.online/pcl2-ce/apiv2/pages/announce.xaml"),
        new(McOfficialFeed, "Setup.Ui.Homepage.Preset.McOfficialFeed", null)
    ];

    public static Preset? Find(int id) => All.FirstOrDefault(p => p.Id == id);

    /// <summary>
    ///     读取用户勾选的预设编号，按 <see cref="All" /> 的顺序排列，至少有一个。
    ///     尚未使用多选设置时，沿用旧版的单选预设。
    /// </summary>
    public static List<int> GetSelected()
    {
        var homepage = Config.Preference.Homepage;
        var ids = homepage.SelectedPresets
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : -1)
            .ToHashSet();
        var selected = All.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToList();
        if (selected.Count == 0)
            selected.Add(Find(homepage.SelectedPreset)?.Id ?? Trivia);
        return selected;
    }

    /// <summary>
    ///     保存用户勾选的预设。同时把第一个写入旧版的单选设置，便于降级后仍能显示。
    /// </summary>
    public static void SetSelected(IEnumerable<int> ids)
    {
        var set = ids.ToHashSet();
        var selected = All.Where(p => set.Contains(p.Id)).Select(p => p.Id).ToList();
        if (selected.Count == 0)
            return;
        var homepage = Config.Preference.Homepage;
        homepage.SelectedPresets = string.Join(",", selected);
        homepage.SelectedPreset = selected[0];
    }
}
