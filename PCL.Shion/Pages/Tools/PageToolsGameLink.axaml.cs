using PCL.Shion.Controls;
using PCL.Shion.Localization;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Tools;

/// <summary>
///     工具页：联机（EasyTier）的占位页面。
///     TODO(shion): 联机功能依赖 PCL.Core.Link（EasyTier，仅 Windows/WPF），在 Shion 中尚未移植，
///     当前仅显示占位界面，所有操作按钮保持禁用。
/// </summary>
public partial class PageToolsGameLink : MyPageRight
{
    private const string UnavailableText = "联机功能暂未在 Shion 中提供";

    public PageToolsGameLink()
    {
        InitializeComponent();

        HintUnavailable.Text = LocalizedOrFallback("Tools.GameLink.Unavailable", UnavailableText);
        TextJoinDescription.Text = LocalizedOrFallback("Tools.GameLink.Join.Description", UnavailableText);
        TextCreateDescription.Text = LocalizedOrFallback("Tools.GameLink.Create.Description", UnavailableText);
        BtnJoin.Text = LocalizedOrFallback("Tools.GameLink.Join.Action", "加入");
        BtnPaste.Text = LocalizedOrFallback("Tools.GameLink.Join.Paste", "粘贴");
        BtnClearLobbyId.Text = LocalizedOrFallback("Tools.GameLink.Join.Clear", "清空");
        BtnCreate.Text = LocalizedOrFallback("Tools.GameLink.Create.Action", "创建");
        BtnRefresh.Text = LocalizedOrFallback("Common.Action.Refresh", "刷新");
        BtnInputPort.Text = LocalizedOrFallback("Tools.GameLink.Create.ManualInput", "手动输入");
    }

    /// <summary>
    ///     与 WPF 版保持同样的公开入口。占位页面没有需要刷新的状态，这里不做任何事。
    /// </summary>
    // TODO(shion): 移植联机功能后，在此重新读取大厅与账号状态。
    public void Reload()
    {
    }

    /// <summary>
    ///     取得本地化文本；键缺失时返回给定的中文兜底文本，避免直接显示键名。
    /// </summary>
    private static string LocalizedOrFallback(string key, string fallback) =>
        LocalizationService.FindText(key) ?? fallback;
}
