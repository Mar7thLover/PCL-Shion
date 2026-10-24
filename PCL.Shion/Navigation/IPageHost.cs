using Avalonia.Controls;
using PCL.Shion.Pages;

namespace PCL.Shion.Navigation;

/// <summary>
///     承载主页面的窗口需要实现的接口。由 <see cref="MainWindow" /> 实现，<see cref="Navigator" /> 通过它驱动页面容器与标题栏。
/// </summary>
internal interface IPageHost
{
    /// <summary>
    ///     左侧页面容器中当前的页面。设置时应当把旧页面从视觉树摘下，并把容器背景设为空。
    /// </summary>
    MyPageLeft? PageLeft { get; set; }

    /// <summary>
    ///     右侧页面容器中当前的页面。设置时应当把旧页面从视觉树摘下，并把容器背景设为空。
    /// </summary>
    MyPageRight? PageRight { get; set; }

    /// <summary>
    ///     左侧页面容器的实际宽度。
    /// </summary>
    double PageLeftWidth { get; }

    /// <summary>
    ///     按新的宽度调整左侧栏背景与阴影，对应 WPF 的 PanMainLeft_Resize。
    /// </summary>
    void ResizePageLeft(double newWidth);

    /// <summary>
    ///     标题栏中的主页面区域，对应 WPF 的 PanTitleMain。
    /// </summary>
    Control PanTitleMain { get; }

    /// <summary>
    ///     标题栏中的子页面区域，对应 WPF 的 PanTitleInner。
    /// </summary>
    Control PanTitleInner { get; }

    /// <summary>
    ///     子页面标题文本，对应 WPF 的 LabTitleInner。
    /// </summary>
    TextBlock LabTitleInner { get; }

    /// <summary>
    ///     设置顶部页签的选中状态，对应 WPF 中 <c>((MyRadioButton)PanTitleSelect.Children[(int)page]).SetChecked(true, raiseByMouse, anime)</c>。
    /// </summary>
    void SetTitleTabChecked(PageType page, bool raiseByMouse, bool anime);

    /// <summary>
    ///     刷新右上角的附加按钮，对应 WPF 的 BtnExtraDownload.ShowRefresh 与 BtnExtraApril.ShowRefresh。
    /// </summary>
    void RefreshExtraButtons();

    /// <summary>
    ///     刷新“返回顶部”按钮，对应 WPF 的 BtnExtraBack.ShowRefresh。
    /// </summary>
    void RefreshBackButton();
}
