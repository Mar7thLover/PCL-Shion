using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Setup;
using PCL.Shion.Services;

namespace PCL.Shion.Pages.Instance;

/// <summary>
/// 模组被禁用的提示页：告诉用户当前实例没有可用的模组页面，可以前往下载或切换实例。
/// </summary>
public partial class PageInstanceModDisabled : MyPageRight
{
    public PageInstanceModDisabled()
    {
        InitializeComponent();
        BtnDownload.Click += BtnDownload_Click;
        BtnVersion.Click += BtnVersion_Click;
        BtnDownload.Loaded += BtnDownload_Loaded;
    }

    private void BtnDownload_Click(object? sender, PointerReleasedEventArgs e)
    {
        Navigator.Go(PageType.Download, PageSubType.DownloadInstall);
    }

    private void BtnVersion_Click(object? sender, PointerReleasedEventArgs e)
    {
        Navigator.Go(PageType.Launch); // 在实例选择页面选定实例的时候只会返回一层，因此如果不先锚定 Launch，在选择实例后会回退到实例设置的这个页面
        Navigator.Go(PageType.InstanceSelect);
    }

    public void BtnDownload_Loaded(object? sender = null, RoutedEventArgs? e = null)
    {
        // TODO(shion): WPF 版读取 PageSelectRight.frmSelectRight.showHidden（实例选择页的隐藏实例开关）。
        // 当前 Avalonia 版的右侧实例选择页由 PageSelectLeft 私有持有，尚未公开，因此暂时按未开启处理。
        var selectShowHidden = false;
        var newVisibility = !(
            (App.Services.Settings.UiHiddenPageDownload && !PageSetupUI.HiddenForceShow) ||
            selectShowHidden);
        if (BtnDownload.IsVisible != newVisibility)
        {
            BtnDownload.IsVisible = newVisibility;
            PanMain.TriggerForceResize();
        }
    }
}
