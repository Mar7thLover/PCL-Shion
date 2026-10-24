using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using PCL.Shion.Controls;

namespace PCL.Shion.Pages.Download;

/// <summary>
/// 版本下载列表中的列表项与操作按钮，对应 WPF 的 ModDownloadLib 中各个“下载菜单”区域。
/// </summary>
internal static class DownloadListItems
{
    /// <summary>
    /// 创建一个版本列表项。点击整行执行 <paramref name="onClick"/>；展开菜单时执行 <paramref name="buttons"/> 创建右侧按钮。
    /// </summary>
    public static MyListItem Create(string title, string? info, string logo, object tag, Action onClick, Func<IEnumerable<MyIconButton>> buttons)
    {
        var item = new MyListItem
        {
            Title = title,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = tag,
            Info = info,
            Logo = logo
        };
        item.Click += (_, _) => onClick();
        item.ContentHandler = (sender, _) => sender.Buttons = buttons();
        return item;
    }

    /// <summary>
    /// 创建一个列表项右侧的图标按钮，悬停时显示提示。
    /// </summary>
    public static MyIconButton IconButton(string svgIcon, string tip, Action onClick, double logoScale = 1d)
    {
        var button = new MyIconButton { SvgIcon = svgIcon, LogoScale = logoScale };
        ToolTip.SetTip(button, tip);
        ToolTip.SetPlacement(button, PlacementMode.Center);
        ToolTip.SetVerticalOffset(button, 30d);
        ToolTip.SetHorizontalOffset(button, 2d);
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>
    /// 返回方块图标的地址，如 Blocks/Grass 对应 Grass.png。
    /// </summary>
    public static string BlockLogo(string name) => $"avares://PCL.Shion/Assets/Images/Blocks/{name}.png";
}
