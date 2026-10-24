using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PCL.Shion.Controls;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Online;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Download.Comp;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Download;

/// <summary>
/// 资源收藏页：按收藏夹展示已收藏的工程，支持搜索、备注、多选批量操作与收藏夹管理。
/// </summary>
public partial class PageDownloadCompFavorites : MyPageRight
{
    private const string NoIconLogo = "avares://PCL.Shion/Assets/Images/Icons/NoIcon.png";
    private const string BarAnimationName = "CompFavorites Sidebar";

    private readonly ModLoader.LoaderTask<List<string>, List<CompProject>> _loader;

    /// <summary>当前收藏夹的全部列表项（与 WPF 的 compItemList 对应）。</summary>
    private readonly List<MyListItem> _itemList = new();

    /// <summary>搜索结果列表项。</summary>
    private List<MyListItem> _searchResult = new();

    /// <summary>已勾选的列表项。</summary>
    private List<MyListItem> _selectedItems = new();

    /// <summary>被用户折叠过的卡片分类，键为工程类型（搜索结果为 -1）。</summary>
    private readonly HashSet<int> _collapsedTypes = new();

    private bool _entered;
    private bool _allowSearch = true;
    private bool _refreshingFolders;
    private int _bottomBarShownCount;

    public PageDownloadCompFavorites()
    {
        _loader = new ModLoader.LoaderTask<List<string>, List<CompProject>>("Comp Favorites", FetchProjects,
            LoaderInput);
        _loader.OnStateChangedUi += (_, state, _) =>
        {
            // 收藏夹 JSON 损坏时自动重试，与 WPF 版一致
            if (state != LoadState.Failed)
                return;
            if ((_loader.Error?.Message ?? "").Contains(Lang.Text("Common.Error.InvalidJson")))
            {
                ModBase.Log("[Download] 下载的工程列表 JSON 文件损坏，已自动重试", ModBase.LogLevel.Debug);
                PageLoaderRestart();
            }
        };
        PageEnter += OnEnter;
        InitializeComponent();
        PanScroll = PanBack;

        // 收藏夹管理按钮：WPF 中通过代码设置图标
        Btn_ManageTargetFav.SvgIcon = "lucide/settings";
        ToolTip.SetTip(Btn_ManageTargetFav, Lang.Text("Download.Comp.Favorites.Manage"));
        Btn_ManageTargetFav.Click += (sender, _) => ShowManageMenu(Btn_ManageTargetFav);

        // 底部操作栏的位移由 RenderTransform 承担，与 WPF 的 TransSelect 一致
        CardSelect.RenderTransform = new TranslateTransform(0d, -10d);

        Btn_FavoritesCancel.Click += (_, _) => Btn_FavoritesCancel_Clicked();
        Btn_SelectCancel.Click += (_, _) => Items_SetSelectAll(false);
        Btn_FavoritesShare.Click += (_, _) => Btn_FavoritesShare_Clicked();
        Btn_FavoritesDownload.Click += (_, _) => Btn_FavoritesDownload_Clicked();
        HintGetFail.Click += (_, _) => _ = ShowGetFailDialogAsync();
        PanSearchBox.TextChanged += (_, _) => SearchRun();
        KeyDown += Page_KeyDown;

        RefreshFavTargets();
        PageLoaderInit(Load, PanLoad, PanContent, null, _loader, _ => Load_OnFinish(), () => LoaderInput());
        ComboTargetFav.SelectionChanged += ComboTargetFav_Selected;
    }

    #region 收藏夹与加载器

    /// <summary>当前选中的收藏夹，若选择异常则回退到第一个收藏夹。</summary>
    private CompFavoriteFolder CurrentFolder
    {
        get
        {
            var folders = CompFavoriteStore.Folders;
            var id = (ComboTargetFav.SelectedItem as MyComboBoxItem)?.Tag as string;
            if (id is null)
                ModBase.Log("[Favorites] 异常：未选择收藏夹");
            return folders.FirstOrDefault(f => f.Id == id) ?? folders[0];
        }
    }

    private List<string> LoaderInput()
    {
        try
        {
            return CurrentFolder.Favs.Distinct().ToList();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Favorites] 加载收藏夹列表时出错");
            return new List<string>();
        }
    }

    private static void FetchProjects(ModLoader.LoaderTask<List<string>, List<CompProject>> task)
    {
        var ids = task.input ?? new List<string>();
        if (ids.Count == 0)
        {
            task.output = new List<CompProject>();
            return;
        }

        task.output = CompServices.CreateApi().GetProjectsByIdsAsync(ids, task.AbortedToken).GetAwaiter().GetResult();
    }

    private void OnEnter()
    {
        if (!_entered)
        {
            _entered = true;
            return;
        }

        // 其它页面可能修改了收藏夹，重新读取列表与标题
        RefreshFavTargets();
        PageLoaderRestart(isForceRestart: true);
    }

    /// <summary>
    /// 刷新收藏夹下拉框，保留当前选中的收藏夹。刷新过程中不触发加载。
    /// </summary>
    private void RefreshFavTargets()
    {
        var currentId = (ComboTargetFav.SelectedItem as MyComboBoxItem)?.Tag as string;
        _refreshingFolders = true;
        try
        {
            ComboTargetFav.Items.Clear();
            var folders = CompFavoriteStore.Folders;
            foreach (var folder in folders)
                ComboTargetFav.Items.Add(new MyComboBoxItem { Content = folder.Name, Tag = folder.Id });
            var index = folders.FindIndex(f => f.Id == currentId);
            ComboTargetFav.SelectedIndex = index >= 0 ? index : 0;
        }
        finally
        {
            _refreshingFolders = false;
        }
    }

    /// <summary>
    /// 切换到指定位置的收藏夹并重新加载。若已经选中该位置，则直接重新加载。
    /// </summary>
    private void SelectFolderIndex(int index)
    {
        if (ComboTargetFav.SelectedIndex == index)
        {
            Items_SetSelectAll(false);
            PageLoaderRestart(isForceRestart: true);
        }
        else
        {
            // 选择变化会触发 ComboTargetFav_Selected，由它负责重新加载
            ComboTargetFav.SelectedIndex = index;
        }
    }

    private void ComboTargetFav_Selected(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingFolders || ComboTargetFav.SelectedItem is null)
            return;
        Items_SetSelectAll(false);
        PageLoaderRestart(isForceRestart: true);
    }

    #endregion

    #region 结果 UI 化

    private void Load_OnFinish()
    {
        _itemList.Clear();
        try
        {
            _allowSearch = false;
            PanSearchBox.Text = string.Empty;
            _allowSearch = true;
            _selectedItems = new List<MyListItem>();

            var input = _loader.input ?? new List<string>();
            var output = _loader.output ?? new List<CompProject>();
            HintGetFail.IsVisible = input.Count != output.Count;

            foreach (var project in output)
                _itemList.Add(BuildItem(project));

            if (_itemList.Count > 0) // 有收藏
            {
                if (!IsSearching)
                {
                    PanSearchBox.IsVisible = true;
                    PanContentList.IsVisible = true;
                    CardNoContent.IsVisible = false;
                }
            }
            else // 没有收藏
            {
                PanSearchBox.IsVisible = false;
                PanContentList.IsVisible = false;
                CardNoContent.IsVisible = true;
            }

            RefreshContent();
            RefreshBar();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化收藏夹列表出错", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    /// <summary>
    /// 为工程创建可勾选的列表项，包括备注、修改备注与取消收藏按钮，以及右键查看详情。
    /// </summary>
    private MyListItem BuildItem(CompProject project)
    {
        var folder = CurrentFolder;
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.CheckBox,
            Tag = project,
            Title = project.TranslatedName,
            Info = project.Description.Replace("\r", "").Replace("\n", ""),
            Logo = string.IsNullOrEmpty(project.LogoUrl) ? NoIconLogo : project.LogoUrl,
            LogoCornerRadius = new CornerRadius(6d),
            Tags = project.TagKeys.Select(key => Lang.Text(key)).Distinct().ToList()
        };

        // ----备注----
        var noteRun = new Run { Foreground = new SolidColorBrush(Color.FromRgb(0, 184, 148)) };
        if (folder.Notes.TryGetValue(project.Id, out var notes) && !string.IsNullOrWhiteSpace(notes))
            noteRun.Text = $" ({notes})";
        item.Inlines.Add(noteRun);

        // ----按钮----
        var btnEditNote = new MyIconButton { SvgIcon = "lucide/pencil" };
        ToolTip.SetTip(btnEditNote, Lang.Text("Download.Comp.Favorites.EditNote"));
        btnEditNote.Click += (_, _) => _ = EditNoteAsync(folder, project, noteRun);

        var btnDelete = new MyIconButton { SvgIcon = "lucide/heart-filled" };
        ToolTip.SetTip(btnDelete, Lang.Text("Download.Comp.Favorites.Action.Unfavorite"));
        btnDelete.Click += (_, _) =>
        {
            CancelFavorite(folder, item);
            if (_itemList.Count > 0)
                RefreshContent();
            RefreshBar();
        };
        item.Buttons = new[] { btnEditNote, btnDelete };

        // ----操作逻辑----
        item.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Right)
                PageDownloadCompDetail.OpenFromItem(project);
        };
        item.Changed += (_, _) => ItemCheckStatusChanged(item);
        return item;
    }

    private async Task EditNoteAsync(CompFavoriteFolder folder, CompProject project, Run noteRun)
    {
        try
        {
            folder.Notes.TryGetValue(project.Id, out var notes);
            var desiredNote = await MsgBox.InputAsync(Lang.Text("Download.Comp.Favorites.EditNote"),
                defaultInput: notes ?? "");
            // 只有在用户确认时才更新备注，避免取消时清空原有备注
            if (desiredNote is null)
                return;
            folder.Notes[project.Id] = desiredNote;
            noteRun.Text = string.IsNullOrWhiteSpace(desiredNote) ? "" : $" ({desiredNote})";
            CompFavoriteStore.Save();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "修改收藏备注时发生错误", ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    /// <summary>
    /// 按工程类型分组生成卡片。搜索时只显示一张搜索结果卡片。
    /// </summary>
    private void RefreshContent()
    {
        // 先把列表项从旧的父容器中移除，否则 Avalonia 不允许它们被添加到新容器
        foreach (var item in _itemList.Concat(_searchResult))
            if (item.Parent is Panel panel)
                panel.Children.Remove(item);
        PanContentList.Children.Clear();

        var searching = IsSearching;
        var source = searching ? _searchResult : _itemList;
        var groups = new List<KeyValuePair<int, List<MyListItem>>>();
        foreach (var item in source)
        {
            var type = searching ? -1 : (int)((CompProject)item.Tag!).Type;
            var index = groups.FindIndex(g => g.Key == type);
            if (index < 0)
            {
                groups.Add(new KeyValuePair<int, List<MyListItem>>(type, new List<MyListItem>()));
                index = groups.Count - 1;
            }

            groups[index].Value.Add(item);
        }

        foreach (var group in groups)
        {
            var type = group.Key;
            var items = group.Value;
            var card = new MyCard
            {
                Title = string.Format(GetCategoryTitleFormat(type), items.Count),
                CanSwap = true,
                Margin = new Thickness(0d, 0d, 0d, 15d)
            };
            var stack = new StackPanel
            {
                Margin = new Thickness(12d, MyCard.SwapedHeight, 12d, 12d),
                VerticalAlignment = VerticalAlignment.Top,
                Tag = items // MyCard 会在展开时才安装内容，Tag 非空表示有待安装的内容
            };
            card.Children.Add(stack); // 折叠区需要先加入卡片，MyCard 不会自动添加
            card.SwapControl = stack;
            card.InstallMethod = target =>
            {
                foreach (var item in items)
                    target.Children.Add(item);
            };
            PanContentList.Children.Add(card);

            if (_collapsedTypes.Contains(type))
            {
                card.IsSwapped = true;
            }
            else
            {
                card.StackInstall();
            }

            card.Swap += (sender, _) =>
            {
                if (sender is not MyCard swapped)
                    return;
                if (swapped.IsSwapped)
                    _collapsedTypes.Add(type);
                else
                    _collapsedTypes.Remove(type);
            };
        }
    }

    private static string GetCategoryTitleFormat(int type) => type switch
    {
        -1 => Lang.Text("Download.Comp.Favorites.SearchResults.Title"),
        (int)CompType.Mod => Lang.Text("Download.Comp.Favorites.Category.Mod"),
        (int)CompType.ModPack => Lang.Text("Download.Comp.Type.Modpack") + " ({0})",
        (int)CompType.ResourcePack => Lang.Text("Download.Comp.Type.ResourcePack") + " ({0})",
        (int)CompType.Shader => Lang.Text("Download.Comp.Type.Shader") + " ({0})",
        (int)CompType.DataPack => Lang.Text("Download.Comp.Type.DataPack") + " ({0})",
        (int)CompType.Plugin => Lang.Text("Download.Comp.Type.Plugin") + " ({0})",
        (int)CompType.World => Lang.Text("Download.Comp.Type.World") + " ({0})",
        _ => Lang.Text("Download.Comp.Favorites.UnknownType") + " ({0})"
    };

    #endregion

    #region 选择操作

    private bool IsSearching => !string.IsNullOrWhiteSpace(PanSearchBox.Text);

    private double TransSelectY => CardSelect.RenderTransform is TranslateTransform transform ? transform.Y : 0d;

    private void SetTransSelectY(double value)
    {
        if (CardSelect.RenderTransform is TranslateTransform transform)
            transform.Y = value;
    }

    /// <summary>
    /// 根据选中数量显示或隐藏底部操作栏，数量增加时播放出现动画，减少到 0 时播放隐藏动画。
    /// </summary>
    private void RefreshBar()
    {
        var newCount = _selectedItems.Count;
        var selected = newCount > 0;
        if (selected)
            LabSelect.Text = Lang.Text("Download.Comp.Favorites.Hint.SelectedCount", newCount);

        if (ModAnimation.AniControlEnabled == 0)
        {
            PanContentList.Margin = new Thickness(0d, 0d, 0d, selected ? 80 : 0);
            if (selected)
            {
                // 仅在数量增加时播放出现/跳跃动画
                if (_bottomBarShownCount >= newCount)
                {
                    _bottomBarShownCount = newCount;
                    return;
                }

                _bottomBarShownCount = newCount;
                CardSelect.IsVisible = true;
                ModAnimation.AniStart(new List<ModAnimation.AniData>
                {
                    ModAnimation.AaOpacity(CardSelect, 1d - CardSelect.Opacity, 60),
                    ModAnimation.AaTranslateY(CardSelect, -27 - TransSelectY, 120,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaTranslateY(CardSelect, 3d, 150, 120,
                        new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaTranslateY(CardSelect, -1, 90, 270,
                        new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak))
                }, BarAnimationName);
            }
            else
            {
                // 不重复播放隐藏动画
                if (_bottomBarShownCount == 0)
                    return;
                _bottomBarShownCount = 0;
                ModAnimation.AniStart(new List<ModAnimation.AniData>
                {
                    ModAnimation.AaOpacity(CardSelect, -CardSelect.Opacity, 90),
                    ModAnimation.AaTranslateY(CardSelect, -10 - TransSelectY, 90,
                        ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaCode(() => CardSelect.IsVisible = false, after: true)
                }, BarAnimationName);
            }
        }
        else
        {
            ModAnimation.AniStop(BarAnimationName);
            _bottomBarShownCount = newCount;
            if (selected)
            {
                CardSelect.IsVisible = true;
                CardSelect.Opacity = 1d;
                SetTransSelectY(-25d);
            }
            else
            {
                CardSelect.IsVisible = false;
                CardSelect.Opacity = 0d;
                SetTransSelectY(-10d);
            }
        }
    }

    private void ItemCheckStatusChanged(MyListItem item)
    {
        _selectedItems.Remove(item);
        if (item.Checked)
            _selectedItems.Add(item);
        RefreshBar();
    }

    /// <summary>
    /// 全选或取消全选。搜索时只作用于搜索结果，与 WPF 版一致。
    /// </summary>
    private void Items_SetSelectAll(bool targetStatus)
    {
        foreach (var item in IsSearching ? _searchResult : _itemList)
            item.Checked = targetStatus;
        _selectedItems = _itemList.Where(e => e.Checked).ToList();
    }

    /// <summary>
    /// 从收藏夹中移除工程，列表为空时重新加载以显示空状态。
    /// </summary>
    private void CancelFavorite(CompFavoriteFolder folder, MyListItem item)
    {
        try
        {
            _itemList.Remove(item);
            _selectedItems.Remove(item);
            _searchResult.Remove(item);
            folder.Favs.Remove(((CompProject)item.Tag!).Id);
            CompFavoriteStore.Save();
            if (_itemList.Count == 0)
                PageLoaderRestart(isForceRestart: true);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[CompFavourites] 移除收藏时发生错误");
        }
    }

    private void Btn_FavoritesCancel_Clicked()
    {
        var folder = CurrentFolder;
        foreach (var item in _selectedItems.ToList())
            CancelFavorite(folder, item);
        if (_itemList.Count > 0)
            RefreshContent();
        RefreshBar();
    }

    private void Btn_FavoritesShare_Clicked()
    {
        try
        {
            var ids = _selectedItems.Select(i => ((CompProject)i.Tag!).Id).ToHashSet();
            SetClipboardText(ModComp.CompFavorites.GetShareCode(ids));
            Items_SetSelectAll(false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[CompFavourites] 分享收藏时发生错误", ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    private void Btn_FavoritesDownload_Clicked()
    {
        _ = BatchDownloadAsync();
    }

    private void SetClipboardText(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            _ = clipboard.SetTextAsync(text);
    }

    /// <summary>
    /// 批量下载所选工程：选择共同支持的加载器与游戏版本，再把每个工程的最新兼容文件下载到所选文件夹。
    /// </summary>
    private async Task BatchDownloadAsync()
    {
        try
        {
            var projects = _selectedItems.Select(i => (CompProject)i.Tag!).ToList();
            var confirm = await MsgBox.ShowAsync(
                Lang.Text("Download.Comp.Favorites.Dialog.BulkDownload.Content"),
                Lang.Text("Download.Comp.Favorites.Dialog.BulkDownload.Title"),
                Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel"), isWarn: true);
            if (confirm != 1)
                return;

            // 获取共同支持的 ModLoader
            var supportedLoaders = new List<CompLoaderType>();
            var loaderFirstSet = true;
            var hasMod = false;
            foreach (var project in projects)
            {
                if (project.Type != CompType.Mod)
                    continue;
                hasMod = true;
                if (loaderFirstSet)
                {
                    loaderFirstSet = false;
                    supportedLoaders = project.ModLoaders.ToList();
                }
                else
                {
                    supportedLoaders = supportedLoaders.Intersect(project.ModLoaders).ToList();
                }
            }

            if (hasMod && supportedLoaders.Count == 0)
            {
                Hint.Show(Lang.Text("Download.Comp.Favorites.Hint.SelectLoader"), HintType.Error);
                return;
            }

            // 要求选择加载器
            var desiredLoader = CompLoaderType.Any;
            if (hasMod)
            {
                var loaderOptions = supportedLoaders.Select(l => (IMyRadio)new MyRadioBox { Text = l.ToString() })
                    .ToList();
                var loaderIndex = await MsgBox.SelectAsync(loaderOptions,
                    Lang.Text("Download.Comp.Favorites.Dialog.SelectLoader.Title"));
                if (loaderIndex is null)
                    return;
                desiredLoader = supportedLoaders[loaderIndex.Value];
            }

            Hint.Show(Lang.Text("Download.Comp.Favorites.Hint.LoadingVersions"));
            var api = CompServices.CreateApi();
            var fetched = await Task.WhenAll(projects.Select(async project =>
            {
                try
                {
                    var files = await api.GetFilesAsync(project.Id, project.FromCurseForge);
                    var suitable = files
                        .Where(f => f.Available && (f.Type != CompType.Mod || f.ModLoaders.Contains(desiredLoader)))
                        .ToList();
                    return (Project: project, Files: (List<CompFile>?)suitable);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"获取 {project.Id} 的下载信息失败", ModBase.LogLevel.Hint,
                        userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
                    return (Project: project, Files: (List<CompFile>?)null);
                }
            }));
            var available = fetched.Where(f => f.Files is not null)
                .Select(f => (f.Project, Files: f.Files!))
                .ToList();

            // 求取共同支持的游戏版本
            List<string>? suitVersions = null;
            foreach (var (_, files) in available)
            {
                var current = files.SelectMany(f => f.GameVersions).Distinct().ToList();
                suitVersions = suitVersions is null ? current : suitVersions.Intersect(current).ToList();
                if (suitVersions.Count == 0)
                {
                    Hint.Show(Lang.Text("Download.Comp.Favorites.Hint.NoResource"), HintType.Error);
                    return;
                }
            }

            if (suitVersions is null || suitVersions.Count == 0)
            {
                Hint.Show(Lang.Text("Download.Comp.Favorites.Hint.NoResource"), HintType.Error);
                return;
            }

            // 要求用户选择希望下载的版本
            var versionOptions = suitVersions.Select(v => (IMyRadio)new MyRadioBox { Text = v }).ToList();
            var versionIndex = await MsgBox.SelectAsync(versionOptions,
                Lang.Text("Download.Comp.Favorites.Dialog.SelectVersion.Title"));
            if (versionIndex is null)
                return;
            var selectedVersion = suitVersions[versionIndex.Value];
            Hint.Show(Lang.Text("Download.Comp.Favorites.Hint.SelectSaveLocation", selectedVersion));

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
                return;
            IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = Lang.Text("Download.Version.SelectSaveLocation"),
                    AllowMultiple = false
                });
            var saveFolder = folders.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(saveFolder))
                return;

            // 每个工程选择目标版本中发布日期最新的文件
            var items = new List<DownloadItem>();
            foreach (var (project, files) in available)
            {
                var file = files
                    .Where(f => f.GameVersions.Contains(selectedVersion))
                    .OrderByDescending(f => f.ReleaseDate)
                    .FirstOrDefault();
                if (file is null)
                    continue;
                var fileName = CompNaming.GetFileName(project, file);
                items.Add(file.ToDownloadItem(Path.Combine(saveFolder, fileName)));
            }

            Items_SetSelectAll(false);
            RefreshBar();
            if (items.Count == 0)
            {
                Hint.Show(Lang.Text("Download.Comp.Favorites.Hint.NoResource"), HintType.Error);
                return;
            }

            // TODO(shion): WPF 版通过 ModLoader.LoaderTaskbarAdd 与 BtnExtraDownload 显示进度，这里尚未接入，只在后台执行。
            var result = await App.Services.Engine.DownloadAsync(items, null, CancellationToken.None);
            if (result.Failed.Count > 0)
                Hint.Show(Lang.Text("Download.Comp.Favorites.Error.OperationFailed"), HintType.Error);
            else
                Hint.Show(Lang.Text("Download.Comp.QuickDownload.Hint.DownloadStarted",
                    $"{selectedVersion} ({items.Count})"), HintType.Success);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "批量下载收藏时发生错误", ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    #endregion

    #region 收藏夹管理菜单

    /// <summary>
    /// 弹出收藏夹管理菜单：分享、导入、新建、重命名与删除收藏夹。
    /// </summary>
    private void ShowManageMenu(Control anchor)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem(Lang.Text("Download.Comp.Favorites.Menu.Share"), "lucide/share-2",
            ShareCurrentFolder));
        menu.Items.Add(CreateMenuItem(Lang.Text("Download.Comp.Favorites.Menu.Import"), "lucide/circle-plus",
            () => _ = ImportFavoritesAsync()));
        menu.Items.Add(CreateMenuItem(Lang.Text("Download.Comp.Favorites.Menu.New"), "lucide/folder-plus",
            () => _ = CreateFolderAsync()));
        menu.Items.Add(CreateMenuItem(Lang.Text("Download.Comp.Favorites.Menu.Rename"), "lucide/pencil",
            () => _ = RenameFolderAsync()));
        menu.Items.Add(CreateMenuItem(Lang.Text("Download.Comp.Favorites.Menu.Delete"), "lucide/trash-2",
            () => _ = DeleteFolderAsync()));

        menu.PlacementTarget = anchor;
        menu.Placement = PlacementMode.Bottom;
        menu.Open(anchor);
    }

    private static MyMenuItem CreateMenuItem(string header, string svgIcon, Action onClick)
    {
        var item = new MyMenuItem
        {
            Header = header,
            SvgIcon = svgIcon
        };
        item.Click += (_, _) => onClick();
        return item;
    }

    private void ShareCurrentFolder()
    {
        try
        {
            var folder = CurrentFolder;
            if (folder.Favs.Count == 0)
            {
                Hint.Show(Lang.Text("Download.Comp.Favorites.Hint.NothingShared"));
                return;
            }

            SetClipboardText(ModComp.CompFavorites.GetShareCode(folder.Favs));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Favourites] 分享收藏时发生错误", ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    private async Task ImportFavoritesAsync()
    {
        try
        {
            var clipData = await MsgBox.InputAsync(Lang.Text("Download.Comp.Favorites.Dialog.Import.Input"),
                Lang.Text("Download.Comp.Favorites.Dialog.Import.Hint"));
            if (string.IsNullOrWhiteSpace(clipData))
                return;
            var newFavs = ModComp.CompFavorites.GetIdsByShareCode(clipData);
            if (newFavs.Count == 0)
            {
                Hint.Show(Lang.Text("Download.Comp.Favorites.Hint.NothingShared"));
                return;
            }

            var userWant = await MsgBox.ShowAsync(Lang.Text("Download.Comp.Favorites.Dialog.Import.Type"),
                button1: Lang.Text("Download.Comp.Favorites.Dialog.Import.NewFolder"),
                button2: Lang.Text("Download.Comp.Favorites.Dialog.Import.CurrentFolder"));
            switch (userWant)
            {
                case 1:
                {
                    var newFavName = await MsgBox.InputAsync(Lang.Text("Download.Comp.Favorites.Dialog.Import.New"),
                        Lang.Text("Download.Comp.Favorites.Dialog.NewPrompt"));
                    if (string.IsNullOrWhiteSpace(newFavName))
                        return;
                    AddFolder(newFavName, newFavs);
                    break;
                }
                case 2:
                {
                    var folder = CurrentFolder;
                    foreach (var id in newFavs)
                        folder.Favs.Add(id);
                    CompFavoriteStore.Save();
                    PageLoaderRestart(isForceRestart: true);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "解析分享数据失败", ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    private async Task CreateFolderAsync()
    {
        var newFavName = await MsgBox.InputAsync(Lang.Text("Download.Comp.Favorites.Menu.New"),
            Lang.Text("Download.Comp.Favorites.Dialog.NewPrompt"));
        if (string.IsNullOrWhiteSpace(newFavName))
            return;
        AddFolder(newFavName, null);
    }

    /// <summary>
    /// 添加收藏夹并切换到它。
    /// </summary>
    private void AddFolder(string name, IEnumerable<string>? favs)
    {
        var folder = new CompFavoriteFolder { Name = name };
        if (favs is not null)
            folder.Favs = favs.ToHashSet();
        CompFavoriteStore.Folders.Add(folder);
        CompFavoriteStore.Save();
        RefreshFavTargets();
        SelectFolderIndex(ComboTargetFav.Items.Count - 1);
    }

    private async Task RenameFolderAsync()
    {
        var folder = CurrentFolder;
        var newName = await MsgBox.InputAsync(Lang.Text("Download.Comp.Favorites.Dialog.Rename.Title"),
            defaultInput: folder.Name);
        if (string.IsNullOrWhiteSpace(newName) || folder.Name == newName)
            return;
        folder.Name = newName;
        CompFavoriteStore.Save();
        RefreshFavTargets();
    }

    private async Task DeleteFolderAsync()
    {
        if (CompFavoriteStore.Folders.Count == 1)
        {
            Hint.Show(Lang.Text("Download.Comp.Favorites.Hint.LastCollection"));
            return;
        }

        var folder = CurrentFolder;
        var content = Lang.Text("Download.Comp.Favorites.Dialog.Delete.Confirm", folder.Name, folder.Favs.Count,
            folder.Id);
        var res = await MsgBox.ShowAsync(content, Lang.Text("Download.Comp.Favorites.Dialog.Delete.Title"),
            button1: Lang.Text("Common.Option.No"), button2: Lang.Text("Common.Option.Yes"), isWarn: true);
        if (res != 2)
            return;

        CompFavoriteStore.Folders.Remove(folder);
        CompFavoriteStore.Save();
        Hint.Show(Lang.Text("Download.Comp.Favorites.Hint.Deleted"), HintType.Success);
        RefreshFavTargets();
        SelectFolderIndex(0);
    }

    /// <summary>
    /// 获取失败的工程：可以复制 ID 或从收藏夹中移除。
    /// </summary>
    private async Task ShowGetFailDialogAsync()
    {
        try
        {
            var input = _loader.input ?? new List<string>();
            var output = _loader.output ?? new List<CompProject>();
            var failIds = input.Except(output.Select(i => i.Id)).ToList();

            var content = Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Content") + "\r\n" + "\r\n";
            foreach (var id in failIds)
                content += $" - {id}" + "\r\n";
            var choice = await MsgBox.ShowAsync(content,
                Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Title"),
                button2: Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.CopyIds"),
                button3: Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Remove"));
            if (choice == 2)
            {
                SetClipboardText(string.Join("\r\n", failIds));
            }
            else if (choice == 3)
            {
                var folder = CurrentFolder;
                foreach (var id in failIds)
                    folder.Favs.Remove(id);
                CompFavoriteStore.Save();
                Hint.Show(Lang.Text("Download.Comp.Favorites.Hint.Removed"), HintType.Success);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "处理获取失败的收藏项时发生错误", ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    #endregion

    #region 搜索与键盘

    /// <summary>
    /// 按名称、描述与标签进行模糊搜索，结果显示在单张搜索卡片中。
    /// </summary>
    private void SearchRun()
    {
        if (!_allowSearch)
            return;
        if (IsSearching)
        {
            var queryList = new List<CompSearchEntry<MyListItem>>();
            foreach (var item in _itemList)
            {
                if (item.Tag is not CompProject project)
                    continue;
                var searchSource = new List<CompSearchSource> { new(project.RawName, 1d) };
                if (!string.IsNullOrEmpty(project.Description))
                    searchSource.Add(new CompSearchSource(project.Description, 0.4d));
                searchSource.Add(new CompSearchSource(
                    string.Join("", project.TagKeys.Select(key => Lang.Text(key))), 0.2d));
                queryList.Add(new CompSearchEntry<MyListItem>(item, searchSource));
            }

            _searchResult = CompSimilarity.Search(queryList, PanSearchBox.Text, 25, 0.35d)
                .Select(r => r.Item).ToList();
        }

        RefreshContent();
    }

    private void Page_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && (e.KeyModifiers & KeyModifiers.Control) != 0)
            Items_SetSelectAll(true);
    }

    #endregion
}
