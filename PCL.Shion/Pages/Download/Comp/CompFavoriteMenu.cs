using System;
using Avalonia.Controls;
using PCL.Shion.Controls;
using PCL.Shion.Core.Online;
using PCL.Shion.Localization;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download.Comp;

/// <summary>
/// 收藏菜单：点击收藏夹即可把工程加入或移出该收藏夹。
/// </summary>
public static class CompFavoriteMenu
{
    /// <summary>
    /// 在锚点控件旁显示收藏菜单。
    /// </summary>
    /// <param name="project">工程。</param>
    /// <param name="anchor">菜单的锚点。</param>
    /// <param name="onChanged">收藏状态改变后的回调。</param>
    public static void Show(CompProject project, Control anchor, Action? onChanged = null)
    {
        var menu = new ContextMenu();
        foreach (var folder in CompFavoriteStore.Folders)
        {
            var contains = folder.Favs.Contains(project.Id);
            var item = new MyMenuItem
            {
                MaxWidth = 240d,
                Header = Lang.Text(contains
                    ? "Download.Comp.Detail.Favorites.UnfavoriteContextMenu"
                    : "Download.Comp.Detail.Favorites.FavoriteContextMenu", folder.Name),
                SvgIcon = contains ? "lucide/heart-filled" : "lucide/heart"
            };
            item.Click += (_, _) =>
            {
                if (contains)
                {
                    folder.Favs.Remove(project.Id);
                    Hint.Show(Lang.Text("Download.Comp.Detail.Favorites.Remove", project.TranslatedName, folder.Name),
                        HintType.Success);
                }
                else
                {
                    folder.Favs.Add(project.Id);
                    Hint.Show(Lang.Text("Download.Comp.Detail.Favorites.Add", project.TranslatedName, folder.Name),
                        HintType.Success);
                }

                CompFavoriteStore.Save();
                onChanged?.Invoke();
            };
            menu.Items.Add(item);
        }

        menu.PlacementTarget = anchor;
        menu.Placement = PlacementMode.Bottom;
        menu.Open(anchor);
    }
}
