using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Online;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Instance.Saves;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Instance;

public partial class PageInstanceSaves : MyPageRight, IRefreshable
{
    private readonly DispatcherTimer fileSystemRefreshTimer;
    private readonly DispatcherTimer searchTimer;
    private FileSystemWatcher? fileSystemWatcher;
    private bool isLoad;

    private bool quickPlayFeature;

    private List<string> saveFolders = new();
    private string worldPath = "";

    public PageInstanceSaves()
    {
        InitializeComponent();
        PanScroll = PanBack;
        fileSystemRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100d) };
        searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100d) };
        Loaded += PageSetupLaunch_Loaded;
        Unloaded += Page_Unloaded;
        fileSystemRefreshTimer.Tick += FileSystemRefreshTimer_Tick;
        searchTimer.Tick += SearchTimer_Tick;
        SearchBox.TextChanged += SearchRun;
    }

    void IRefreshable.Refresh()
    {
        RefreshSelf();
    }

    private void RefreshSelf()
    {
        Refresh();
        CheckQuickPlay();
    }

    public static void Refresh()
    {
        PageInstanceLeft.frmInstanceSaves?.Reload();
        if (PageInstanceLeft.frmInstanceLeft is { } left)
            left.ItemWorld.Checked = true;
        HintService.Hint(Lang.Text("Instance.Saves.Status.Refreshing"), log: false);
    }

    private void PageSetupLaunch_Loaded(object? sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        Dispatcher.UIThread.Post(SyncQuickActionWidths, DispatcherPriority.Loaded);
        var instance = PageInstanceLeft.McInstance;
        if (instance is null)
            return;
        worldPath = Path.Combine(instance.PathIndie, "saves") + Path.DirectorySeparatorChar;
        if (!Directory.Exists(worldPath))
            Directory.CreateDirectory(worldPath);
        Reload();

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;
        CheckQuickPlay();

        // 初始化文件系统监视器和排序按钮
        SetupFileSystemWatcher();
        BtnSort.Click += BtnSortClick;
        SetSortMethod(_currentSortMethod);
    }

    /// <summary>
    ///     与 WPF 的 SharedSizeGroup 对应：快捷操作的三个按钮宽度取最宽者。
    /// </summary>
    private void SyncQuickActionWidths()
    {
        MyButton[] buttons = [BtnQuickOpenFolder, BtnQuickDownloadNew, BtnQuickPaste];
        var widest = buttons.Max(b => b.DesiredSize.Width);
        foreach (var button in buttons)
            button.MinWidth = widest;
    }

    private string GetFolderNameFromPath(string fullPath)
    {
        return string.IsNullOrEmpty(fullPath) ? "" :
            fullPath.EndsWith(Path.DirectorySeparatorChar) ? new DirectoryInfo(fullPath).Parent?.Name ?? "" : new DirectoryInfo(fullPath).Name;
    }

    private string GetFileNameFromPath(string fullPath)
    {
        return Path.GetFileName(fullPath);
    }

    private void SetupFileSystemWatcher()
    {
        fileSystemWatcher?.Dispose();

        // 确保目录存在
        if (!Directory.Exists(worldPath))
            Directory.CreateDirectory(worldPath);

        fileSystemWatcher = new FileSystemWatcher
        {
            Path = worldPath,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
        };

        fileSystemWatcher.Created += OnFileSystemChanged;
        fileSystemWatcher.Deleted += OnFileSystemChanged;
        fileSystemWatcher.Renamed += OnFileSystemChanged;

        fileSystemWatcher.EnableRaisingEvents = true;
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            fileSystemRefreshTimer.Stop();
            fileSystemRefreshTimer.Start();
        });
    }

    private void FileSystemRefreshTimer_Tick(object? sender, EventArgs e)
    {
        fileSystemRefreshTimer.Stop();
        Dispatcher.UIThread.Post(Reload);
    }

    private void Page_Unloaded(object? sender, RoutedEventArgs e)
    {
        if (fileSystemWatcher is not null)
        {
            fileSystemWatcher.Created -= OnFileSystemChanged;
            fileSystemWatcher.Deleted -= OnFileSystemChanged;
            fileSystemWatcher.Renamed -= OnFileSystemChanged;
            fileSystemWatcher.Dispose();
            fileSystemWatcher = null;
        }

        fileSystemRefreshTimer.Stop();
        searchTimer.Stop();
    }

    /// <summary>
    ///     确保当前页面上的信息已正确显示。
    /// </summary>
    public void Reload()
    {
        ModAnimation.AniControlEnabled += 1;
        PanBack.ScrollToHome();
        LoadFileList();
        ModAnimation.AniControlEnabled -= 1;
    }

    private void RefreshUI()
    {
        try
        {
            if (IsSearching)
            {
                var resultCount = _searchResult?.Count ?? 0;
                PanListBack.Title = Lang.Text("Instance.Saves.SearchResultTitle", resultCount.ToString());
            }
            else
            {
                PanListBack.Title = Lang.Text("Instance.Saves.SaveListTitle", saveFolders.Count.ToString());
            }

            if (saveFolders.Count == 0)
            {
                PanNoWorld.IsVisible = true;
                PanContent.IsVisible = false;
            }
            else
            {
                PanNoWorld.IsVisible = false;
                PanContent.IsVisible = true;

                var showingSaves = (IsSearching ? _searchResult ?? new List<string>() : saveFolders).ToList();

                if (showingSaves.Count > 0)
                {
                    var sortMethod = GetSortMethod(_currentSortMethod);
                    showingSaves.Sort((a, b) => sortMethod(a, b));
                }

                ModAnimation.AniControlEnabled += 1;
                PanList.Children.Clear();

                foreach (var curFolder in showingSaves)
                {
                    // 检查文件夹是否仍然存在
                    if (!Directory.Exists(curFolder)) continue;

                    var saveLogo = Path.Combine(curFolder, "icon.png");
                    var tmpCurFolder = curFolder;
                    if (File.Exists(saveLogo))
                    {
                        var target = Path.Combine(PageInstanceLeft.McInstance!.PathInstance, "PCL", "ImgCache",
                            SavesSupport.GetStringMd5(saveLogo) + ".png");
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        if (!File.Exists(target))
                            File.Copy(saveLogo, target, true);
                        saveLogo = target;
                    }
                    else
                    {
                        saveLogo = "avares://PCL.Shion/Assets/Images/Icons/NoIcon.png";
                    }

                    var worldItem = new MyListItem
                    {
                        Logo = saveLogo,
                        Title = GetFolderNameFromPath(curFolder),
                        Info =
                            Lang.Text("Instance.Saves.CreationTime", Lang.Date(Directory.GetCreationTime(curFolder), "d"), Lang.Date(Directory.GetLastWriteTime(curFolder), "d")),
                        Type = MyListItem.CheckType.Clickable
                    };
                    worldItem.Click += (_, _) => OpenSaveDetail(tmpCurFolder);

                    var btnOpen = new MyIconButton
                    {
                        SvgIcon = "lucide/folder-open",
                    };
                    ToolTip.SetTip(btnOpen, Lang.Text("Common.Action.Open"));
                    btnOpen.Click += (_, _) => SystemShell.OpenFolder(tmpCurFolder);

                    var btnDelete = new MyIconButton
                    {
                        SvgIcon = "lucide/trash-2",
                    };
                    ToolTip.SetTip(btnDelete, Lang.Text("Common.Action.Delete"));
                    btnDelete.Click += (_, _) =>
                    {
                        worldItem.IsEnabled = false;
                        worldItem.Info = Lang.Text("Instance.Saves.Deleting");
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                Trash.MoveToTrash(tmpCurFolder);
                                Dispatcher.UIThread.Post(() => HintService.Hint(Lang.Text("Instance.Saves.DeletedToRecycleBin")));
                                Dispatcher.UIThread.Post(() => RemoveItem(worldItem));
                            }
                            catch (Exception ex)
                            {
                                var message = Lang.Text("Instance.Saves.DeleteFailed");
                                SavesSupport.LogFailure(ex, message, message);
                                Dispatcher.UIThread.Post(Reload);
                            }
                        });
                    };

                    var btnCopy = new MyIconButton
                    {
                        SvgIcon = "lucide/copy",
                    };
                    ToolTip.SetTip(btnCopy, Lang.Text("Common.Action.Copy"));
                    btnCopy.Click += (_, _) => _ = CopyFolderAsync(tmpCurFolder);

                    var btnInfo = new MyIconButton
                    {
                        SvgIcon = "lucide/info",
                    };
                    ToolTip.SetTip(btnInfo, Lang.Text("Instance.Saves.Details"));
                    btnInfo.Click += (_, _) => OpenSaveDetail(tmpCurFolder);

                    var btnLaunch = new MyIconButton
                    {
                        SvgIcon = "lucide/play",
                    };
                    ToolTip.SetTip(btnLaunch, Lang.Text("Instance.Saves.QuickPlay"));
                    btnLaunch.Click += (_, _) => SavesSupport.QuickPlay(GetFileNameFromPath(tmpCurFolder));

                    worldItem.Buttons = quickPlayFeature
                        ? new[] { btnOpen, btnDelete, btnCopy, btnInfo, btnLaunch }
                        : new[] { btnOpen, btnDelete, btnCopy, btnInfo };

                    PanList.Children.Add(worldItem);
                }

                ModAnimation.AniControlEnabled -= 1;
            }
        }
        catch (Exception ex)
        {
            var message = Lang.Text("Instance.Saves.RefreshUiFailed");
            SavesSupport.LogFailure(ex, message, message);
        }
    }

    /// <summary>
    ///     打开存档的详情页（左侧为存档页面，默认显示信息子页面）。
    /// </summary>
    private static void OpenSaveDetail(string folder)
    {
        PageInstanceSavesLeft.currentSave = folder;
        if (Navigator.Current == PageType.VersionSaves && Navigator.PageLeft is PageInstanceSavesLeft left)
            left.OnNavigated(PageSubType.VersionSavesInfo);
        else
            Navigator.Go(PageType.VersionSaves, PageSubType.VersionSavesInfo);
    }

    /// <summary>
    ///     把存档文件夹作为文件复制到剪贴板，便于粘贴到其他位置。
    /// </summary>
    private async Task CopyFolderAsync(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                HintService.Hint(Lang.Text("Instance.Saves.FolderNotFound"));
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            var clipboard = topLevel?.Clipboard;
            var folderItem = topLevel is null
                ? null
                : await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(folder));
            if (clipboard is null || folderItem is null)
                throw new InvalidOperationException("Clipboard or storage provider is unavailable.");

            await clipboard.SetFileAsync(folderItem);

            HintService.Hint(Lang.Text("Instance.Saves.CopiedToClipboard"));
            HintService.Hint(Lang.Text("Instance.Saves.CopyPasteWarning"));
        }
        catch (Exception ex)
        {
            var message = Lang.Text("Instance.Saves.CopyFailed");
            SavesSupport.LogFailure(ex, message, message);
        }
    }

    private void CheckQuickPlay()
    {
        try
        {
            var instance = PageInstanceLeft.McInstance;
            quickPlayFeature = instance is not null &&
                               VersionResolver.Resolve(App.Services.Settings.MinecraftRoot, instance.Name)
                                   .GameArguments.Any(argument => argument.Values.Contains("--quickPlaySingleplayer"));
        }
        catch (Exception ex)
        {
            SavesSupport.LogFailure(ex, "检查存档快捷启动失败", Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    private void LoadFileList()
    {
        try
        {
            saveFolders = Directory.Exists(worldPath)
                ? Directory.EnumerateDirectories(worldPath).ToList()
                : new List<string>();
            PanList.Children.Clear();
            CheckQuickPlay();

            RefreshUI(); // 确保UI刷新
        }
        catch (Exception ex)
        {
            var message = Lang.Text("Instance.Saves.LoadListFailed");
            SavesSupport.LogFailure(ex, message, message);
        }
    }

    private void RemoveItem(MyListItem item)
    {
        if (PanList.Children.IndexOf(item) == -1)
            return;
        PanList.Children.Remove(item);
        RefreshUI();
    }

    private void BtnOpenFolder_Click(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenFolder(worldPath);
    }

    private void BtnPaste_Click(object? sender, PointerReleasedEventArgs e)
    {
        _ = PasteFoldersAsync();
    }

    /// <summary>
    ///     把剪贴板中的存档文件夹复制到当前实例的存档目录中。
    /// </summary>
    private async Task PasteFoldersAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        var storageItems = clipboard is null ? null : await clipboard.TryGetFilesAsync();
        var files = (storageItems ?? Array.Empty<IStorageItem>())
            .Select(item => item.TryGetLocalPath())
            .OfType<string>()
            .ToList();

        var loaders = new List<ModLoader.LoaderBase>();
        loaders.Add(new ModLoader.LoaderTask<int, int>("Copy saves", _ =>
        {
            var copied = 0;
            foreach (var i in files)
                try
                {
                    if (Directory.Exists(i))
                    {
                        if (Directory.Exists(worldPath + GetFolderNameFromPath(i)))
                        {
                            HintService.Hint(Lang.Text("Instance.Saves.DuplicateFolder", GetFolderNameFromPath(i)));
                        }
                        else
                        {
                            SavesSupport.CopyDirectory(i, worldPath + GetFolderNameFromPath(i));
                            copied += 1;
                        }
                    }
                    else
                    {
                        HintService.Hint(Lang.Text("Instance.Saves.SourceNotFolder"));
                    }
                }
                catch (Exception ex)
                {
                    var message = Lang.Text("Instance.Saves.PasteFolderFailed");
                    SavesSupport.LogFailure(ex, message, message);
                }

            if (copied > 0)
                HintService.Hint(Lang.Text("Instance.Saves.PastedCount", copied.ToString()), HintType.Success);
            Dispatcher.UIThread.Post(Reload);
        }));
        var loader = new ModLoader.LoaderCombo<int>($"{PageInstanceLeft.McInstance?.Name} - {Lang.Text("Instance.Saves.CopySave")}", loaders);
        loader.Start(1);
        (MainWindow.Current as IPageHost)?.RefreshExtraButtons();
    }

    private void BtnDownloadNew_Click(object? sender, PointerReleasedEventArgs e)
    {
        Navigator.Go(PageType.Download, PageSubType.DownloadWorld);
    }

    #region 搜索和排序

    private SortMethod _currentSortMethod = SortMethod.FileName;
    private List<string>? _searchResult;

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchBox.Text);

    private enum SortMethod
    {
        FileName,
        CreateTime,
        ModifyTime
    }

    private string GetSortName(SortMethod method)
    {
        switch (method)
        {
            case SortMethod.FileName:
            {
                return Lang.Text("Instance.Saves.SortFileName");
            }
            case SortMethod.CreateTime:
            {
                return Lang.Text("Instance.Saves.SortCreateTime");
            }
            case SortMethod.ModifyTime:
            {
                return Lang.Text("Instance.Saves.SortModifyTime");
            }

            default:
            {
                return Lang.Text("Instance.Saves.SortFileName");
            }
        }
    }

    private void SetSortMethod(SortMethod target)
    {
        _currentSortMethod = target;
        BtnSort.Text = Lang.Text("Instance.Saves.SortBy", GetSortName(target));
        RefreshUI();
    }

    private void BtnSortClick(object? sender, RouteEventArgs e)
    {
        var body = new ContextMenu();
        foreach (SortMethod i in Enum.GetValues<SortMethod>())
        {
            var item = new MyMenuItem();
            item.Header = GetSortName(i);
            item.Click += (_, _) => SetSortMethod(i);
            body.Items.Add(item);
        }

        if (sender is Control anchor)
            body.Open(anchor);
    }

    private void SearchRun(object? sender, EventArgs e)
    {
        searchTimer.Stop();
        searchTimer.Start();
    }

    private void SearchTimer_Tick(object? sender, EventArgs e)
    {
        searchTimer.Stop();
        PerformSearch();
    }

    private void PerformSearch()
    {
        try
        {
            if (IsSearching)
            {
                var queryList = new List<CompSearchEntry<string>>();
                foreach (var saveFolder in saveFolders)
                {
                    var folderName = GetFolderNameFromPath(saveFolder);
                    queryList.Add(new CompSearchEntry<string>(saveFolder, [new CompSearchSource(folderName, 1d)]));
                }

                _searchResult = CompSimilarity.Search(queryList, SearchBox.Text, 25, 0.35d)
                    .Select(r => r.Item).ToList();
            }
            else
            {
                _searchResult = null;
            }

            RefreshUI();
        }
        catch (Exception ex)
        {
            SavesSupport.LogFailure(ex, Lang.Text("Instance.Saves.SearchError"));
        }
    }

    private Func<string, string, int> GetSortMethod(SortMethod method)
    {
        switch (method)
        {
            case SortMethod.FileName:
            {
                return (a, b) => string.Compare(GetFolderNameFromPath(a), GetFolderNameFromPath(b),
                    StringComparison.OrdinalIgnoreCase);
            }
            case SortMethod.CreateTime:
            {
                return (a, b) => Directory.GetCreationTime(b).CompareTo(Directory.GetCreationTime(a));
            }
            case SortMethod.ModifyTime:
            {
                return (a, b) => Directory.GetLastWriteTime(b).CompareTo(Directory.GetLastWriteTime(a));
            }

            default:
            {
                return (a, b) => string.Compare(GetFolderNameFromPath(a), GetFolderNameFromPath(b),
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    #endregion
}
