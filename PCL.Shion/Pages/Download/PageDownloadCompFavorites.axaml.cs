using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using PCL.Shion.Controls;
using PCL.Shion.Core.Online;
using PCL.Shion.Navigation;
using PCL.Shion.Pages;
using PCL.Shion.Pages.Download.Comp;

namespace PCL.Shion.Pages.Download;

/// <summary>
/// 资源收藏页：按收藏夹分组展示已收藏的工程。
/// </summary>
public partial class PageDownloadCompFavorites : MyPageRight
{
    private readonly ModLoader.LoaderTask<int, List<CompProject>> _loader;
    private bool _entered;

    public PageDownloadCompFavorites()
    {
        _loader = new ModLoader.LoaderTask<int, List<CompProject>>("Comp Favorites", task =>
        {
            var ids = CompFavoriteStore.Folders.SelectMany(f => f.Favs).Distinct().ToList();
            task.output = ids.Count == 0
                ? new List<CompProject>()
                : CompServices.CreateApi().GetProjectsByIdsAsync(ids, task.AbortedToken).GetAwaiter().GetResult();
        });
        PageEnter += OnEnter;
        InitializeComponent();
        PanScroll = PanBack;
        PageLoaderInit(Load, PanLoad, PanContent, null, _loader, _ => Load_OnFinish());
    }

    private void OnEnter()
    {
        if (!_entered)
        {
            _entered = true;
            return;
        }

        PageLoaderRestart(isForceRestart: true);
    }

    private void Load_OnFinish()
    {
        PanFolders.Children.Clear();
        var projects = (_loader.output ?? new List<CompProject>())
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var isFirst = true;
        foreach (var folder in CompFavoriteStore.Folders)
        {
            var items = folder.Favs.Where(projects.ContainsKey).Select(id => projects[id]).ToList();
            if (items.Count == 0)
                continue;

            var card = new MyCard
            {
                Title = $"{folder.Name} ({items.Count})",
                Margin = new Thickness(0d, 0d, 0d, 15d)
            };
            var stack = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };
            card.SwapControl = stack;
            card.InstallMethod = target =>
            {
                foreach (var project in items)
                    target.Children.Add(CompDisplay.CreateItem(project, true, true, true));
            };
            PanFolders.Children.Add(card);

            if (isFirst)
                card.StackInstall();
            else
                card.IsSwapped = true;
            isFirst = false;
        }
    }
}
