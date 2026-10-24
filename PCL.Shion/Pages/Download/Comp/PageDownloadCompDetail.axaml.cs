using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using PCL.Shion.Controls;
using PCL.Shion.Core.Online;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download.Comp;

/// <summary>
/// 打开资源详情页所需的数据。
/// </summary>
/// <param name="Project">要展示的工程。</param>
/// <param name="ExpandedTitles">打开前已展开的版本卡片标题。</param>
/// <param name="TargetVersion">需要匹配的 MC 版本，为空时不限制。</param>
/// <param name="TargetLoader">需要匹配的加载器，Any 代表不限制。</param>
/// <param name="ResourceType">展示的资源种类，Any 代表任意种类。</param>
public sealed record CompDetailArgs(
    CompProject Project,
    IReadOnlyList<string> ExpandedTitles,
    string? TargetVersion,
    CompLoaderType TargetLoader,
    CompType ResourceType);

/// <summary>
/// 资源详情页：展示工程信息与各版本文件，并提供下载与收藏。
/// </summary>
public partial class PageDownloadCompDetail : MyPageRight
{
    private readonly CompDetailArgs _args;
    private readonly ModLoader.LoaderTask<int, List<CompFile>> _compFileLoader;
    private MyCompItem? _compItem;
    private bool _isFirstInit = true;
    private string? _instanceFilter;
    private string? _modLoaderFilter;
    private bool _groupedDrop;
    private bool _groupedOld;

    public PageDownloadCompDetail(CompDetailArgs args)
    {
        _args = args;
        _compFileLoader = new ModLoader.LoaderTask<int, List<CompFile>>("Comp File", task =>
        {
            task.output = CompServices.CreateApi()
                .GetFilesAsync(_args.Project.Id, _args.Project.FromCurseForge, task.AbortedToken)
                .GetAwaiter()
                .GetResult();
        });
        PageEnter += Init;
        InitializeComponent();
        PanScroll = PanBack;
        Load.StateChanged += (_, _, _) => Load_State();
        BtnIntroWeb.Click += (_, _) => OpenUrl(_args.Project.Website);
        BtnIntroCopy.Click += (_, _) => _ = SetClipboardAsync(_args.Project.TranslatedName);
        BtnIntroLinkCopy.Click += (_, _) => _ = SetClipboardAsync(_args.Project.Website);
        BtnFavorites.Click += (_, _) => CompFavoriteMenu.Show(_args.Project, BtnFavorites, RefreshFavoriteButton);
        PageLoaderInit(Load, PanLoad, PanMain, CardIntro, _compFileLoader, _ => Load_OnFinish());
    }

    /// <summary>
    /// 页面标题为工程名称。
    /// </summary>
    public override string? Title => _args.Project.TranslatedName;

    /// <summary>
    /// 从资源列表中的卡片打开详情页，目标版本与加载器取自当前的筛选项。
    /// </summary>
    public static void OpenFromItem(CompProject project)
    {
        var expandedTitles = new List<string>();
        if (Navigator.PageRight is PageDownloadCompDetail currentDetail)
            expandedTitles.AddRange(currentDetail.ExpandedTitles());

        var targetType = CompType.Any;
        string? targetVersion = null;
        var targetLoader = CompLoaderType.Any;

        if (Navigator.Current == PageType.Download)
        {
            if (Navigator.CurrentSub == PageSubType.DownloadCompFavorites)
            {
                targetVersion = "";
            }
            else
            {
                targetType = Navigator.CurrentSub switch
                {
                    PageSubType.DownloadMod => CompType.Mod,
                    PageSubType.DownloadPack => CompType.ModPack,
                    PageSubType.DownloadDataPack => CompType.DataPack,
                    PageSubType.DownloadResourcePack => CompType.ResourcePack,
                    PageSubType.DownloadShader => CompType.Shader,
                    PageSubType.DownloadWorld => CompType.World,
                    _ => CompType.Any
                };
                if (Navigator.PageRight?.Content is PageComp comp && comp.loader.input is { } input)
                {
                    targetVersion = input.GameVersion;
                    targetLoader = input.ModLoader;
                }
            }
        }
        else if (Navigator.Current == PageType.InstanceSetup)
        {
            targetType = CompType.ModPack;
        }
        else if (Navigator.PageRight is PageDownloadCompDetail detail)
        {
            targetVersion = detail._args.TargetVersion;
            targetLoader = detail._args.TargetLoader;
        }

        Open(new CompDetailArgs(project, expandedTitles, targetVersion, targetLoader, targetType));
    }

    /// <summary>
    /// 打开资源详情页。
    /// </summary>
    public static void Open(CompDetailArgs args)
    {
        Navigator.Go(PageType.CompDetail);
        Navigator.ShowRight(new PageDownloadCompDetail(args));
        Navigator.RefreshTitle();
    }

    /// <summary>
    /// 当前展开的版本卡片标题。
    /// </summary>
    public IReadOnlyList<string> ExpandedTitles() =>
        PanResults.Children.OfType<MyCard>()
            .Where(card => !string.IsNullOrEmpty(card.Title) && !card.IsSwapped)
            .Select(card => card.Title)
            .ToList();

    private void Init()
    {
        PanBack.ScrollToHome();
        if (_isFirstInit)
            _isFirstInit = false;
        else
            PageLoaderRestart(isForceRestart: true);

        if (_compItem is not null)
            PanIntro.Children.Remove(_compItem);
        _compItem = CompDisplay.BuildCard(_args.Project, true, true, false);
        _compItem.CanInteraction = false;
        _compItem.ShowFavoriteBtn = false;
        _compItem.Margin = new Thickness(-7, -7, 0d, 8d);
        PanIntro.Children.Insert(0, _compItem);

        BtnIntroWeb.Text = _args.Project.FromCurseForge ? "CurseForge" : "Modrinth";
        RefreshFavoriteButton();
    }

    private void RefreshFavoriteButton()
    {
        var isFavourite = CompFavoriteStore.IsFavourite(_args.Project.Id);
        BtnFavorites.SvgIcon = isFavourite ? "lucide/heart-filled" : "lucide/heart";
        _compItem?.RefreshFavoriteStatus();
    }

    private void Load_State()
    {
        if (_compFileLoader.State != LoadState.Failed)
            return;
        var errorMessage = _compFileLoader.Error?.Message ?? "";
        if (errorMessage.Contains(Lang.Text("Common.Error.InvalidJson")))
            PageLoaderRestart();
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return;
        SystemShell.OpenUrl(url);
    }

    private async System.Threading.Tasks.Task SetClipboardAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }

    private List<CompFile> GetResults()
    {
        var results = _compFileLoader.output ?? new List<CompFile>();
        var type = _args.ResourceType;
        if (type == CompType.Any)
            return results.Where(r => r.Type != CompType.Plugin).ToList();
        if (type is CompType.Shader or CompType.ResourcePack)
            return results;
        return results.Where(r => r.Type == type).ToList();
    }

    private void Load_OnFinish()
    {
        var results = GetResults();

        _groupedDrop = false;
        _groupedOld = false;
        var (instanceFilters, modLoaderFilters) = ComputeFilters(results);
        if (instanceFilters.Count >= 9)
        {
            _groupedDrop = true;
            (instanceFilters, modLoaderFilters) = ComputeFilters(results);
            if (instanceFilters.Count >= 9)
            {
                _groupedDrop = false;
                _groupedOld = true;
                (instanceFilters, modLoaderFilters) = ComputeFilters(results);
                if (instanceFilters.Count >= 9)
                {
                    _groupedDrop = true;
                    (instanceFilters, modLoaderFilters) = ComputeFilters(results);
                }
            }
        }

        PanInstanceFilter.Children.Clear();
        PanModLoaderFilter.Children.Clear();
        var isMod = _args.ResourceType == CompType.Mod;
        PanInstanceFilter.Margin = isMod ? new Thickness(10d, 10d, 0d, 5d) : new Thickness(10d, 10d, 0d, 10d);
        PanModLoaderFilter.Margin = isMod ? new Thickness(10d, 5d, 0d, 10d) : new Thickness(0d);

        if (instanceFilters.Count < 2)
        {
            CardFilter.IsVisible = false;
            _instanceFilter = null;
        }
        else
        {
            CardFilter.IsVisible = true;
            if (isMod)
            {
                PanInstanceFilter.Children.Add(new TextBlock
                {
                    Text = Lang.Text("Download.Comp.Detail.InstanceFilter"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2d, 0d, 0d, 0d)
                });
                PanModLoaderFilter.Children.Add(new TextBlock
                {
                    Text = Lang.Text("Download.Comp.Detail.LoaderFilter"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2d, 0d, 0d, 0d)
                });
            }

            var allText = Lang.Text("Common.Option.All");
            instanceFilters.Insert(0, allText);
            modLoaderFilters.Insert(0, allText);

            foreach (var version in instanceFilters)
            {
                var button = CreateFilterButton(version);
                button.Check += (sender, _) =>
                {
                    _instanceFilter = sender.Text == allText ? null : sender.Text;
                    UpdateFilterResult();
                };
                PanInstanceFilter.Children.Add(button);
            }

            if (isMod)
                foreach (var loader in modLoaderFilters)
                {
                    var button = CreateFilterButton(loader);
                    button.Check += (sender, _) =>
                    {
                        _modLoaderFilter = sender.Text == allText ? null : sender.Text;
                        UpdateFilterResult();
                    };
                    PanModLoaderFilter.Children.Add(button);
                }

            MyRadioButton? instanceToCheck = null;
            MyRadioButton? modLoaderToCheck = null;
            if (!string.IsNullOrEmpty(_args.TargetVersion))
            {
                var targetFile = results.FirstOrDefault(v => v.GameVersions.Contains(_args.TargetVersion));
                if (targetFile is not null)
                {
                    var targetGroup = GroupedVersionName(_args.TargetVersion, _groupedDrop, _groupedOld);
                    instanceToCheck = PanInstanceFilter.Children.OfType<MyRadioButton>()
                        .FirstOrDefault(b => (b.Text ?? "") == (targetGroup ?? ""));
                }
            }

            if (isMod && _args.TargetLoader != CompLoaderType.Any)
            {
                var targetFile = results.FirstOrDefault(v => v.ModLoaders.Contains(_args.TargetLoader));
                if (targetFile is not null)
                    modLoaderToCheck = PanModLoaderFilter.Children.OfType<MyRadioButton>()
                        .FirstOrDefault(b => (b.Text ?? "") == _args.TargetLoader.ToString());
            }

            instanceToCheck ??= PanInstanceFilter.Children.OfType<MyRadioButton>().ElementAtOrDefault(0);
            if (instanceToCheck is not null)
                instanceToCheck.Checked = true;
            if (isMod)
            {
                modLoaderToCheck ??= PanModLoaderFilter.Children.OfType<MyRadioButton>().ElementAtOrDefault(0);
                if (modLoaderToCheck is not null)
                    modLoaderToCheck.Checked = true;
            }
        }

        UpdateFilterResult();
    }

    private static MyRadioButton CreateFilterButton(string text) => new()
    {
        Text = text,
        Margin = new Thickness(2d, 0d, 2d, 0d),
        ColorType = MyRadioButton.ColorState.Highlight
    };

    private (List<string> Instances, List<string> Loaders) ComputeFilters(List<CompFile> results)
    {
        var instances = results.SelectMany(v => v.GameVersions)
            .Select(v => GroupedVersionName(v, _groupedDrop, _groupedOld))
            .Distinct()
            .OrderBy(s => s, new CardSorter("")) // CardSorter 本身就是新版本在前
            .ToList();
        var loaders = results.SelectMany(v => v.ModLoaders)
            .Select(l => l.ToString())
            .Distinct()
            .OrderByDescending(s => s)
            .ToList();
        return (instances, loaders);
    }

    private void UpdateFilterResult()
    {
        var results = GetResults();
        var targetVersionText = _args.TargetLoader != CompLoaderType.Any ? _args.TargetLoader + " " : "";
        var targetCardName = !string.IsNullOrEmpty(_args.TargetVersion) || _args.TargetLoader != CompLoaderType.Any
            ? Lang.Text("Download.Comp.Detail.SelectedVersion", targetVersionText, _args.TargetVersion ?? "")
            : "";

        var hasMultipleLoaders = _args.Project.ModLoaders.Count > 1;
        var otherKey = Lang.Text("Download.Comp.Detail.VersionGroup.Other");
        var dict = new SortedDictionary<string, List<CompFile>>(new CardSorter(targetCardName))
        {
            [otherKey] = new List<CompFile>()
        };
        var checker = new Dictionary<string, HashSet<CompFile>>();

        foreach (var version in results)
        {
            foreach (var gameVersion in version.GameVersions)
            {
                var currentGroupedName = GroupedVersionName(gameVersion, _groupedDrop, _groupedOld);
                if (_instanceFilter is not null && (currentGroupedName ?? "") != (_instanceFilter ?? ""))
                    continue;
                var verName = GroupedVersionName(gameVersion, false, false);
                var loaders = new List<string>();

                if (hasMultipleLoaders && version.Type == CompType.Mod && CompVersions.IsFormatFit(verName))
                {
                    foreach (var loader in version.ModLoaders)
                    {
                        if (_modLoaderFilter is not null && loader.ToString() != _modLoaderFilter)
                            continue;
                        loaders.Add(loader + " ");
                    }

                    if (loaders.Count == 0 && _modLoaderFilter is not null) continue;
                }

                if (loaders.Count == 0)
                    loaders.Add("");

                foreach (var loaderPrefix in loaders)
                    AddVersionToDict(dict, checker, loaderPrefix + verName, version);
            }

            if (!string.IsNullOrEmpty(targetCardName))
            {
                var targetGroup = GroupedVersionName(_args.TargetVersion ?? "", _groupedDrop, _groupedOld);
                var isMatchFilter = _instanceFilter is null || targetGroup.StartsWith(_instanceFilter, StringComparison.Ordinal);
                if (isMatchFilter && version.GameVersions.Contains(_args.TargetVersion ?? ""))
                    if (_args.TargetLoader == CompLoaderType.Any || version.ModLoaders.Contains(_args.TargetLoader))
                        if (_instanceFilter is null || version.GameVersions.Any(v =>
                                (GroupedVersionName(v, _groupedDrop, _groupedOld) ?? "") == (_instanceFilter ?? "")))
                            AddVersionToDict(dict, checker, targetCardName, version);
            }
        }

        PanResults.Children.Clear();
        var expandedTitles = _args.ExpandedTitles;
        foreach (var pair in dict)
        {
            if (pair.Value.Count == 0)
                continue;

            var files = pair.Value;
            var newStack = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                VerticalAlignment = VerticalAlignment.Top,
                Tag = files
            };
            var newCard = new MyCard
            {
                Title = pair.Key,
                Margin = new Thickness(0d, 0d, 0d, 15d)
            };
            newCard.SwapControl = newStack;
            newCard.InstallMethod = stack =>
            {
                var list = (List<CompFile>)stack.Tag!;
                list.Sort((a, b) => b.ReleaseDate.CompareTo(a.ReleaseDate));
                var distinctCount = list.Select(f => f.DisplayName).Distinct().Count();
                var badDisplayName = distinctCount != list.Count;
                AddDependencyBars(stack, list);
                foreach (var file in list)
                    stack.Children.Add(CreateFileItem(file, badDisplayName));
            };

            PanResults.Children.Add(newCard);

            if ((pair.Key ?? "") == (targetCardName ?? "") || expandedTitles.Contains(newCard.Title))
                newCard.StackInstall();
            else
                newCard.IsSwapped = true;

            if (pair.Key == otherKey)
                newStack.Children.Add(new MyHint
                {
                    Text = Lang.Text("Download.Comp.Detail.VersionRecognitionDelayHint"),
                    Theme = MyHint.Themes.Yellow,
                    Margin = new Thickness(5d, 0d, 0d, 8d)
                });
        }

        if (PanResults.Children.Count == 1)
            ((MyCard)PanResults.Children[0]).IsSwapped = false;
    }

    private static void AddVersionToDict(SortedDictionary<string, List<CompFile>> dict,
        Dictionary<string, HashSet<CompFile>> checker, string key, CompFile version)
    {
        if (!dict.ContainsKey(key))
        {
            dict.Add(key, new List<CompFile>());
            checker.Add(key, new HashSet<CompFile>());
        }

        if (checker[key].Add(version))
            dict[key].Add(version);
    }

    private static string GroupedVersionName(string? name, bool groupedByDrop, bool foldOld)
    {
        if (name is null)
            return Lang.Text("Download.Comp.Detail.VersionGroup.Other");
        if (name.Length == 0)
            return Lang.Text("Download.Comp.Detail.CompItem.UnknownVersion");
        if (name.Contains('w'))
            return Lang.Text("Download.Comp.Detail.VersionGroup.Snapshot");
        if (foldOld && CompVersions.VersionToDrop(name, true) < 120)
            return Lang.Text("Download.Comp.Detail.VersionGroup.Old");
        if (groupedByDrop)
        {
            var drop = CompVersions.VersionToDrop(name, true);
            if (drop >= 0)
                return CompVersions.DropToVersion(drop);
        }

        return name.Replace("-snapshot", Lang.Text("Download.Comp.Detail.CompItem.PreviewSuffix"));
    }

    private MyListItem CreateFileItem(CompFile file, bool badDisplayName)
    {
        var project = _args.Project;
        var title = badDisplayName ? file.FileName : file.DisplayName;
        var baseName = file.FileName.Contains('.') ? file.FileName[..file.FileName.LastIndexOf('.')] : file.FileName;
        var info = new List<string>();
        if (title != baseName)
            info.Add(baseName);
        if (file.Dependencies.Count > 0)
            info.Add(Lang.Text("Download.Comp.Detail.FileList.DependencyCount", file.Dependencies.Count));

        var snapshotKeywords = new[] { "w", "snapshot", "rc", "pre", "experimental", "-" };
        if (file.GameVersions.All(ver => !ver.Contains('.') ||
                                         snapshotKeywords.Any(s => ver.Contains(s, StringComparison.OrdinalIgnoreCase))))
            info.Add(Lang.Text("Download.Comp.Detail.FileList.GameVersion", string.Join("、", file.GameVersions)));

        if (file.DownloadCount > 0)
            info.Add(Lang.Text("Common.Format.DownloadCount", Lang.CompactNumber(file.DownloadCount)));
        info.Add(Lang.Text("Download.Comp.Detail.FileList.Updated", Lang.TimeSpan(file.ReleaseDate - DateTime.Now)));
        if (file.Status != CompFileStatus.Release)
            info.Add(StatusDescription(file.Status));

        var item = new MyListItem
        {
            Title = title,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Tag = file,
            Info = string.Join("  |  ", info),
            Logo = file.Status switch
            {
                CompFileStatus.Release => "avares://PCL.Shion/Assets/Images/Icons/R.png",
                CompFileStatus.Beta => "avares://PCL.Shion/Assets/Images/Icons/B.png",
                _ => "avares://PCL.Shion/Assets/Images/Icons/A.png"
            }
        };
        item.Click += (_, _) => _ = CompFileActions.SaveAsync(this, project, file);

        var btnSave = new MyIconButton
        {
            SvgIcon = "lucide/save",
            Height = 25d,
            Width = 25d
        };
        ToolTip.SetTip(btnSave, Lang.Text("Download.Version.SaveAs"));
        btnSave.Click += (_, _) => _ = CompFileActions.SaveAsync(this, project, file);
        item.Buttons = new[] { btnSave };
        return item;
    }

    private static string StatusDescription(CompFileStatus status) => status switch
    {
        CompFileStatus.Release => Lang.Text("Download.Comp.Detail.FileList.ReleaseType.Release"),
        CompFileStatus.Beta => Lang.Text("Download.Comp.Detail.FileList.ReleaseType.Beta"),
        _ => Lang.Text("Download.Comp.Detail.FileList.ReleaseType.Alpha")
    };

    private static void AddDependencyBars(StackPanel stack, List<CompFile> files)
    {
        var deps = files.SelectMany(f => f.Dependencies).Distinct().ToList();
        var optionalDeps = files.SelectMany(f => f.OptionalDependencies).Distinct().ToList();
        if (deps.Count == 0 && optionalDeps.Count == 0)
            return;

        AddDependencyBar(stack, deps, Lang.Text("Download.Comp.Detail.FileList.RequiredDependencies"), false);
        AddDependencyBar(stack, optionalDeps, Lang.Text("Download.Comp.Detail.FileList.OptionalDependencies"), true);

        stack.Children.Add(new TextBlock
        {
            Text = Lang.Text("Download.Comp.Detail.FileList.VersionList"),
            FontSize = 14d,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(6d, 12d, 0d, 5d)
        });
    }

    private static void AddDependencyBar(StackPanel stack, List<string> depIds, string title, bool collapsed)
    {
        if (depIds.Count == 0)
            return;

        depIds.Sort(StringComparer.Ordinal);
        var projects = depIds
            .Where(CompProject.Cache.ContainsKey)
            .Select(id => CompProject.Cache[id])
            .ToList();
        if (projects.Count == 0)
            return;

        var bar = new MyCollapseBar
        {
            Title = $"{title} ({projects.Count})",
            IsCollapsed = collapsed
        };
        foreach (var project in projects)
            bar.ContentPanel.Children.Add(CompDisplay.CreateItem(project, false, false));
        stack.Children.Add(bar);
    }

    private sealed class CardSorter(string topmost) : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            x ??= "";
            y ??= "";
            if (x == y)
                return 0;
            if (x == topmost)
                return -1;
            if (y == topmost)
                return 1;

            var isXSpecial = !x.Contains('.');
            var isYSpecial = !y.Contains('.');
            if (isXSpecial && isYSpecial)
                return string.CompareOrdinal(x, y);
            if (isXSpecial)
                return 1;
            if (isYSpecial)
                return -1;

            var versionCodeSort = -CompVersions.CompareVersion(
                x.Replace(BeforeFirstSpace(x) + " ", ""),
                y.Replace(BeforeFirstSpace(y) + " ", ""));
            if (versionCodeSort != 0)
                return versionCodeSort;
            return -CompVersions.CompareVersion(x, y);
        }

        private static string BeforeFirstSpace(string text)
        {
            var index = text.IndexOf(' ');
            return index < 0 ? text : text[..index];
        }
    }
}
