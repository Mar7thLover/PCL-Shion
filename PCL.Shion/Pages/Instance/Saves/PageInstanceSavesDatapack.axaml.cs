using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.Online;
using PCL.Shion.Core.Platform;
using PCL.Shion.Core.Saves;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Instance.Saves;

public partial class PageInstanceSavesDatapack : MyPageRight, IRefreshable
{
    // Avalonia 不支持给 Transform 命名，从 CardSelect 的渲染变换取用
    private TranslateTransform TransSelect => (TranslateTransform)CardSelect.RenderTransform!;

    /// <summary>
    ///     当前存档的数据包列表（扫描结果）。
    /// </summary>
    private IReadOnlyList<DatapackEntry> loadedEntries = [];

    /// <summary>
    ///     已加载的数据包 UI 缓存。Key 为数据包的 RawPath。
    /// </summary>
    private readonly Dictionary<string, MyListItem> datapackItems = new();

    /// <summary>
    ///     选择的数据包的路径（RawPath）。
    /// </summary>
    private readonly HashSet<string> selectedDatapacks = new();

    private List<DatapackEntry>? searchResult;

    private FilterType filter = FilterType.All;
    private SortMethod currentSortMethod = SortMethod.CompName;
    private int bottomBarShownCount;
    private int reloadVersion;
    private bool isLoad;

    public PageInstanceSavesDatapack()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Loaded += (_, _) => PageOther_Loaded();
        PageExit += UnselectedAllWithAnimation;
        KeyDown += PageKeyDown;

        BtnManageOpen.Click += BtnManageOpen_Click;
        BtnHintOpen.Click += BtnManageOpen_Click;
        BtnManageSelectAll.Click += BtnManageSelectAll_Click;
        BtnManageInstall.Click += BtnManageInstall_Click;
        BtnHintInstall.Click += BtnManageInstall_Click;
        BtnManageDownload.Click += BtnManageDownload_Click;
        BtnHintDownload.Click += BtnManageDownload_Click;
        BtnManageInfoExport.Click += BtnManageInfoExport_Click;
        SearchBox.TextChanged += SearchRun;
        BtnFilterAll.Check += ChangeFilter;
        BtnFilterEnabled.Check += ChangeFilter;
        BtnFilterDisabled.Check += ChangeFilter;
        BtnFilterError.Check += ChangeFilter;
        BtnSort.Click += BtnSortClick;
        BtnSelectEnable.Click += BtnSelectEnable_Click;
        BtnSelectDisable.Click += BtnSelectDisable_Click;
        BtnSelectDelete.Click += BtnSelectDelete_Click;
        BtnSelectCancel.Click += BtnSelectCancel_Click;
    }

    /// <summary>
    ///     当前存档的 datapacks 目录。
    /// </summary>
    private static string DatapackFolder => Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");

    /// <summary>
    ///     底部操作栏的位移变换，即 XAML 中 CardSelect 的 RenderTransform。
    /// </summary>
    private TranslateTransform CardSelectTransform => (TranslateTransform)CardSelect.RenderTransform!;

    void IRefreshable.Refresh()
    {
        Refresh();
    }

    public void Refresh()
    {
        ReloadDatapackFileList();
    }

    public void PageOther_Loaded()
    {
        if (Navigator.Last != PageType.CompDetail)
            PanBack.ScrollToHome();
        ModAnimation.AniControlEnabled += 1;
        selectedDatapacks.Clear();
        ReloadDatapackFileList();
        ChangeAllSelected(false);
        ModAnimation.AniControlEnabled -= 1;

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;
    }

    /// <summary>
    ///     重新扫描数据包列表并刷新 UI。
    /// </summary>
    public void ReloadDatapackFileList()
    {
        _ = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var version = Interlocked.Increment(ref reloadVersion);
        PanLoad.IsVisible = true;

        var folder = DatapackFolder;
        IReadOnlyList<DatapackEntry> entries;
        try
        {
            entries = await Task.Run(() => DatapackScanner.Scan(folder));
        }
        catch (Exception ex)
        {
            SavesSupport.LogFailure(ex, "加载数据包列表 UI 失败", Lang.Text("Instance.Saves.Error.OperationFailed"));
            entries = [];
        }

        // 期间又发起了新的刷新，丢弃这一次的结果
        if (version != reloadVersion)
            return;

        loadedEntries = entries;
        selectedDatapacks.RemoveWhere(key => entries.All(entry => entry.RawPath != key));
        LoadUIFromLoaderOutput();
        PanLoad.IsVisible = false;

        Filter = FilterType.All;
        PanBack.ScrollToHome();
        SearchBox.Text = ""; // 这会触发结果刷新，所以需要在数据包列表更新之后
    }

    /// <summary>
    ///     将扫描结果加载为 UI。
    /// </summary>
    private void LoadUIFromLoaderOutput()
    {
        if (loadedEntries.Count == 0)
        {
            PanEmpty.IsVisible = true;
            PanBack.IsVisible = false;
            return;
        }

        PanBack.IsVisible = true;
        PanEmpty.IsVisible = false;

        datapackItems.Clear();
        foreach (var entry in loadedEntries)
            datapackItems[entry.RawPath] = BuildLocalCompItem(entry);

        RefreshUI();
    }

    private MyListItem BuildLocalCompItem(DatapackEntry entry)
    {
        ModAnimation.AniControlEnabled += 1;
        var newItem = new MyListItem
        {
            Logo = "avares://PCL.Shion/Assets/Images/Icons/NoIcon.png",
            Title = entry.Name,
            Info = entry.UnavailableReason ?? entry.Description ?? entry.FileName,
            Type = MyListItem.CheckType.CheckBox,
        };
        newItem.Checked = selectedDatapacks.Contains(entry.RawPath);
        ModAnimation.AniControlEnabled -= 1;

        newItem.Changed += (_, _) => CheckChanged(entry.RawPath, newItem.Checked);
        newItem.Click += (_, _) => newItem.Checked = !newItem.Checked;
        newItem.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Right)
                ShowInfo(entry);
        };

        var btnInfo = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/info" };
        ToolTip.SetTip(btnInfo, Lang.Text("Instance.Saves.Detail"));
        btnInfo.Click += (_, _) => ShowInfo(entry);

        var btnOpen = new MyIconButton { LogoScale = 1.05d, SvgIcon = "lucide/folder-open" };
        ToolTip.SetTip(btnOpen, Lang.Text("Instance.Saves.OpenFileLocation"));
        btnOpen.Click += (_, _) => SystemShell.OpenFolder(entry.Path);

        var btnDelete = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/trash-2" };
        ToolTip.SetTip(btnDelete, Lang.Text("Common.Action.Delete"));
        btnDelete.Click += (_, _) => DeleteDatapacks([entry]);

        if (entry.State == DatapackState.Enabled)
        {
            var btnDisable = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/circle-minus" };
            ToolTip.SetTip(btnDisable, Lang.Text("Instance.Resource.Disable"));
            btnDisable.Click += (_, _) => ToggleDatapacks([entry], false);
            newItem.Buttons = new[] { btnInfo, btnOpen, btnDisable, btnDelete };
        }
        else if (entry.State == DatapackState.Disabled)
        {
            var btnEnable = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/circle-check" };
            ToolTip.SetTip(btnEnable, Lang.Text("Instance.Resource.Enable"));
            btnEnable.Click += (_, _) => ToggleDatapacks([entry], true);
            newItem.Buttons = new[] { btnInfo, btnOpen, btnEnable, btnDelete };
        }
        else
        {
            newItem.Buttons = new[] { btnInfo, btnOpen, btnDelete };
        }

        return newItem;
    }

    /// <summary>
    ///     刷新整个 UI。
    /// </summary>
    public void RefreshUI()
    {
        var showingDatapacks = (IsSearching ? searchResult ?? new List<DatapackEntry>() : loadedEntries.ToList())
            .Where(CanPassFilter).ToList();

        if (showingDatapacks.Count > 0)
        {
            var sortMethod = GetSortMethod(currentSortMethod);
            showingDatapacks.Sort((a, b) => sortMethod(a, b));
        }

        // 重新列出列表
        ModAnimation.AniControlEnabled += 1;
        if (showingDatapacks.Count > 0)
        {
            PanList.IsVisible = true;
            PanList.Children.Clear();
            foreach (var target in showingDatapacks)
            {
                if (!datapackItems.TryGetValue(target.RawPath, out var item))
                    continue;

                // 确保元素没有父容器，避免重复添加异常
                if (item.Parent is Panel parent)
                    parent.Children.Remove(item);

                item.Checked = selectedDatapacks.Contains(target.RawPath); // 更新选中状态
                PanList.Children.Add(item);
            }
        }
        else
        {
            PanList.IsVisible = false;
        }

        ModAnimation.AniControlEnabled -= 1;
        selectedDatapacks.RemoveWhere(key => showingDatapacks.All(s => s.RawPath != key));
        RefreshBars();
    }

    /// <summary>
    ///     刷新顶栏和底栏显示。
    /// </summary>
    public void RefreshBars()
    {
        // -----------------
        // 顶部栏
        // -----------------
        var itemSource = IsSearching ? searchResult ?? new List<DatapackEntry>() : loadedEntries.ToList();
        var anyCount = itemSource.Count;
        var enabledCount = itemSource.Count(i => i.State == DatapackState.Enabled);
        var disabledCount = itemSource.Count(i => i.State == DatapackState.Disabled);
        var unavailableCount = itemSource.Count(i => i.State == DatapackState.Unavailable);

        BtnFilterAll.Text = IsSearching
            ? Lang.Text("Instance.Resource.Filter.SearchResult")
            : Lang.Text("Instance.Resource.Filter.AllWithCount", anyCount);
        BtnFilterEnabled.Text = Lang.Text("Instance.Resource.Filter.EnabledWithCount", enabledCount);
        BtnFilterEnabled.IsVisible = filter == FilterType.Enabled || (enabledCount > 0 && enabledCount < anyCount);
        BtnFilterDisabled.Text = Lang.Text("Instance.Resource.Filter.DisabledWithCount", disabledCount);
        BtnFilterDisabled.IsVisible = filter == FilterType.Disabled || disabledCount > 0;
        BtnFilterError.Text = Lang.Text("Instance.Resource.Filter.ErrorWithCount", unavailableCount);
        BtnFilterError.IsVisible = filter == FilterType.Unavailable || unavailableCount > 0;

        // -----------------
        // 底部栏
        // -----------------
        var newCount = selectedDatapacks.Count;
        var selected = newCount > 0;
        if (selected)
        {
            LabSelect.Text = Lang.Text("Instance.Resource.SelectedCount", newCount);
            BtnSelectDisable.IsEnabled = loadedEntries.Any(m =>
                selectedDatapacks.Contains(m.RawPath) && m.State == DatapackState.Enabled);
            BtnSelectEnable.IsEnabled = loadedEntries.Any(m =>
                selectedDatapacks.Contains(m.RawPath) && m.State == DatapackState.Disabled);
        }

        // 更新显示状态
        if (ModAnimation.AniControlEnabled == 0)
        {
            PanListBack.Margin = new Thickness(0d, 0d, 0d, selected ? 95 : 15);
            if (selected)
            {
                // 仅在数量增加时播放出现/跳跃动画
                if (bottomBarShownCount >= newCount)
                {
                    bottomBarShownCount = newCount;
                    return;
                }

                bottomBarShownCount = newCount;
                // 出现/跳跃动画
                CardSelect.IsVisible = true;
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(CardSelect, 1d - CardSelect.Opacity, 60),
                        ModAnimation.AaTranslateY(CardSelect, -27 - CardSelectTransform.Y, 120,
                            ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaTranslateY(CardSelect, 3d, 150, 120,
                            new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaTranslateY(CardSelect, -1, 90, 270,
                            new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak))
                    }, "Datapack Sidebar");
            }
            else
            {
                // 不重复播放隐藏动画
                if (bottomBarShownCount == 0)
                    return;
                bottomBarShownCount = 0;
                // 隐藏动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(CardSelect, -CardSelect.Opacity, 90),
                        ModAnimation.AaTranslateY(CardSelect, -10 - CardSelectTransform.Y, 90,
                            ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaCode(() => CardSelect.IsVisible = false, after: true)
                    }, "Datapack Sidebar");
            }
        }
        else
        {
            ModAnimation.AniStop("Datapack Sidebar");
            bottomBarShownCount = newCount;
            if (selected)
            {
                CardSelect.IsVisible = true;
                CardSelect.Opacity = 1d;
                CardSelectTransform.Y = -25;
            }
            else
            {
                CardSelect.IsVisible = false;
                CardSelect.Opacity = 0d;
                CardSelectTransform.Y = -10;
            }
        }
    }

    #region 管理

    /// <summary>
    ///     打开 datapacks 文件夹。
    /// </summary>
    private void BtnManageOpen_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(DatapackFolder);
            SystemShell.OpenFolder(DatapackFolder);
        }
        catch (Exception ex)
        {
            SavesSupport.LogFailure(ex, "打开 datapacks 文件夹失败", Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     全选。
    /// </summary>
    private void BtnManageSelectAll_Click(object? sender, PointerReleasedEventArgs e)
    {
        ChangeAllSelected(selectedDatapacks.Count < PanList.Children.Count);
    }

    /// <summary>
    ///     安装数据包。
    /// </summary>
    private async void BtnManageInstall_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
                return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Lang.Text("Instance.Saves.Datapack.Install.FileDialog.Title"),
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType(Lang.Text("Instance.Saves.Datapack.Install.FileDialog.Filter"))
                    {
                        Patterns = ["*.zip"],
                    },
                ],
            });
            var paths = files.Select(file => file.TryGetLocalPath()).OfType<string>().ToList();
            if (paths.Count == 0)
                return;
            await InstallDatapackFiles(paths);
            Refresh();
        }
        catch (Exception ex)
        {
            SavesSupport.LogFailure(ex, "复制数据包文件失败", Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     安装数据包文件。
    /// </summary>
    private async Task InstallDatapackFiles(IReadOnlyList<string> filePathList)
    {
        if (filePathList.Count == 0)
            return;

        var extension = Path.GetExtension(filePathList[0]).TrimStart('.').ToLowerInvariant();

        // 检查文件扩展名
        if (extension != "zip")
        {
            HintService.Hint(Lang.Text("Instance.Resource.Install.UnsupportedFormat", extension, Lang.Text("Download.Comp.Type.DataPack"), "zip"), HintType.Error);
            return;
        }

        // 检查回收站
        if (filePathList[0].Contains(@"$RECYCLE.BIN"))
        {
            HintService.Hint(Lang.Text("Instance.Resource.Install.RestoreFromRecycleBin"), HintType.Error);
            return;
        }

        // 执行安装
        try
        {
            var datapackFolder = DatapackFolder;
            Directory.CreateDirectory(datapackFolder);

            foreach (var filePath in filePathList)
            {
                var newFileName = Path.GetFileName(filePath);
                var destFile = Path.Combine(datapackFolder, newFileName);

                if (File.Exists(destFile) &&
                    await MsgBox.ShowAsync(
                        Lang.Text("Instance.Resource.Install.OverwriteConfirm.Message", newFileName),
                        Lang.Text("Instance.Resource.Install.OverwriteConfirm.Title"),
                        Lang.Text("Common.Action.Overwrite"),
                        Lang.Text("Common.Action.Cancel")) != 1)
                    continue;

                File.Copy(filePath, destFile, true);
            }

            if (filePathList.Count == 1)
                HintService.Hint(Lang.Text("Instance.Resource.Install.SuccessSingle", Path.GetFileName(filePathList[0])), HintType.Success);
            else
                HintService.Hint(Lang.Text("Instance.Resource.Install.SuccessMultiple", filePathList.Count, Lang.Text("Download.Comp.Type.DataPack")), HintType.Success);

            ReloadDatapackFileList();
        }
        catch (Exception ex)
        {
            SavesSupport.LogFailure(ex, "复制数据包文件失败", Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     下载数据包。
    /// </summary>
    private void BtnManageDownload_Click(object? sender, PointerReleasedEventArgs e)
    {
        Directory.CreateDirectory(DatapackFolder);
        Navigator.Go(PageType.Download, PageSubType.DownloadDataPack);
    }

    /// <summary>
    ///     导出信息。
    /// </summary>
    private async void BtnManageInfoExport_Click(object? sender, PointerReleasedEventArgs e)
    {
        var choice = await MsgBox.ShowAsync(
            Lang.Text("Instance.Saves.Datapack.Export.Mode.Message"),
            Lang.Text("Instance.Resource.Export.Mode.Title"),
            Lang.Text("Instance.Resource.Export.Mode.Txt"),
            Lang.Text("Instance.Resource.Export.Mode.Csv"),
            Lang.Text("Common.Action.Cancel"));

        var saveName = Path.GetFileName(PageInstanceSavesLeft.currentSave.TrimEnd(Path.DirectorySeparatorChar));
        switch (choice)
        {
            case 1: // TXT
            {
                var exportContent = loadedEntries.Select(entry => entry.FileName).ToList();
                await ExportText(string.Join("\r\n", exportContent), saveName + "的数据包信息.txt", "txt");
                break;
            }

            case 2: // CSV
            {
                var exportContent = new List<string>
                {
                    "文件名,数据包名称,数据包版本,文件大小（字节）,文件路径",
                };
                exportContent.AddRange(loadedEntries.Select(entry =>
                    $"{entry.FileName},{entry.Name},{entry.PackFormat},{entry.Length},{entry.Path}"));
                await ExportText(string.Join("\r\n", exportContent), saveName + "的数据包信息.csv", "csv");
                break;
            }
        }
    }

    private async Task ExportText(string content, string fileName, string extension)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
                return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Lang.Text("Instance.Resource.Export.SelectSaveLocation"),
                SuggestedFileName = fileName,
                FileTypeChoices =
                [
                    new FilePickerFileType(Lang.Text("Instance.Resource.Export.FilesFilter"))
                    {
                        Patterns = ["*." + extension],
                    },
                ],
            });
            var savePath = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(savePath))
                return;
            await File.WriteAllTextAsync(savePath, content, Encoding.UTF8);
            SystemShell.OpenFolder(savePath);
        }
        catch (Exception ex)
        {
            SavesSupport.LogFailure(ex, "导出数据包信息失败", Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    #endregion

    #region 选择

    // 单项切换选择状态
    private void CheckChanged(string rawPath, bool isChecked)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        // 更新选择了的内容
        if (isChecked)
            selectedDatapacks.Add(rawPath);
        else
            selectedDatapacks.Remove(rawPath);
        RefreshBars();
    }

    // 切换所有项的选择状态
    private void ChangeAllSelected(bool value)
    {
        ModAnimation.AniControlEnabled += 1;
        selectedDatapacks.Clear();
        foreach (var (rawPath, item) in datapackItems)
        {
            var shouldSelected = value && PanList.Children.Contains(item);
            item.Checked = shouldSelected;
            if (shouldSelected)
                selectedDatapacks.Add(rawPath);
        }

        ModAnimation.AniControlEnabled -= 1;
        RefreshBars();
    }

    private void UnselectedAllWithAnimation()
    {
        var cacheAniControlEnabled = ModAnimation.AniControlEnabled;
        ModAnimation.AniControlEnabled = 0;
        ChangeAllSelected(false);
        ModAnimation.AniControlEnabled += cacheAniControlEnabled;
    }

    private void PageKeyDown(object? sender, KeyEventArgs e)
    {
        if (!ReferenceEquals(Navigator.PageRight, this))
            return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.A && string.IsNullOrEmpty(SearchBox.Text))
        {
            ChangeAllSelected(true);
            e.Handled = true;
        }
    }

    #endregion

    #region 筛选

    public FilterType Filter
    {
        get => filter;
        set
        {
            if (filter == value)
                return;
            filter = value;
            switch (value)
            {
                case FilterType.All:
                {
                    BtnFilterAll.Checked = true;
                    break;
                }
                case FilterType.Enabled:
                {
                    BtnFilterEnabled.Checked = true;
                    break;
                }
                case FilterType.Disabled:
                {
                    BtnFilterDisabled.Checked = true;
                    break;
                }

                default:
                {
                    BtnFilterError.Checked = true;
                    break;
                }
            }

            RefreshUI();
        }
    }

    public enum FilterType
    {
        All = 0,
        Enabled = 1,
        Disabled = 2,
        Unavailable = 3
    }

    /// <summary>
    ///     检查该数据包项是否符合当前筛选的类别。
    /// </summary>
    private bool CanPassFilter(DatapackEntry checkingDatapack)
    {
        return Filter switch
        {
            FilterType.All => true,
            FilterType.Enabled => checkingDatapack.State == DatapackState.Enabled,
            FilterType.Disabled => checkingDatapack.State == DatapackState.Disabled,
            FilterType.Unavailable => checkingDatapack.State == DatapackState.Unavailable,
            _ => false,
        };
    }

    // 点击筛选项触发的改变
    private void ChangeFilter(MyRadioButton sender, bool raiseByMouse)
    {
        Filter = (FilterType)Convert.ToInt32(sender.Tag);
        RefreshUI();
    }

    #endregion

    #region 排序

    private void SetSortMethod(SortMethod target)
    {
        currentSortMethod = target;
        BtnSort.Text = Lang.Text("Instance.Resource.Sort.Text", GetSortName(target));
        RefreshUI();
    }

    private enum SortMethod
    {
        FileName,
        CompName,
        CreateTime,
        DatapackFileSize
    }

    private string GetSortName(SortMethod method)
    {
        return method switch
        {
            SortMethod.FileName => Lang.Text("Instance.Resource.Sort.FileName"),
            SortMethod.CompName => Lang.Text("Instance.Resource.Sort.ResourceName"),
            SortMethod.CreateTime => Lang.Text("Instance.Resource.Sort.AddTime"),
            SortMethod.DatapackFileSize => Lang.Text("Instance.Resource.Sort.FileSize"),
            _ => Lang.Text("Instance.Resource.Sort.ResourceName"),
        };
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

    private Func<DatapackEntry, DatapackEntry, int> GetSortMethod(SortMethod method)
    {
        return method switch
        {
            SortMethod.FileName => (a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase),
            SortMethod.CreateTime => (a, b) => b.CreationTime.CompareTo(a.CreationTime),
            SortMethod.DatapackFileSize => (a, b) => b.Length.CompareTo(a.Length),
            _ => (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
        };
    }

    #endregion

    #region 下边栏

    private void BtnSelectEnable_Click(object? sender, RouteEventArgs e)
    {
        ToggleDatapacks(loadedEntries.Where(m => selectedDatapacks.Contains(m.RawPath)).ToList(), true);
        ChangeAllSelected(false);
    }

    private void BtnSelectDisable_Click(object? sender, RouteEventArgs e)
    {
        ToggleDatapacks(loadedEntries.Where(m => selectedDatapacks.Contains(m.RawPath)).ToList(), false);
        ChangeAllSelected(false);
    }

    /// <summary>
    ///     启用/禁用数据包（通过在文件名后添加或移除 .disabled 后缀）。
    /// </summary>
    private void ToggleDatapacks(IReadOnlyList<DatapackEntry> datapackList, bool isEnable)
    {
        var isSuccessful = true;
        foreach (var datapackEntity in datapackList)
        {
            string newPath;
            if (datapackEntity.State == DatapackState.Enabled && !isEnable)
                // 禁用 - 添加 .disabled 后缀
                newPath = datapackEntity.RawPath + ".disabled";
            else if (datapackEntity.State == DatapackState.Disabled && isEnable)
                // 启用 - 移除 .disabled 后缀
                newPath = datapackEntity.RawPath;
            else
                continue;

            // 重命名
            try
            {
                if (File.Exists(newPath) || Directory.Exists(newPath))
                {
                    _ = MsgBox.ShowAsync(Lang.Text("Instance.Saves.Datapack.Replace.FileNameConflict", Path.GetFileName(newPath)));
                    continue;
                }

                if (Directory.Exists(datapackEntity.Path))
                    Directory.Move(datapackEntity.Path, newPath);
                else
                    File.Move(datapackEntity.Path, newPath);
            }
            catch (FileNotFoundException ex)
            {
                SavesSupport.LogFailure(ex, $"未找到需要重命名的数据包（{datapackEntity.Path}）",
                    Lang.Text("Instance.Saves.Error.OperationFailed"));
                ReloadDatapackFileList();
                return;
            }
            catch (Exception ex)
            {
                SavesSupport.LogFailure(ex, $"重命名数据包失败（{datapackEntity.Path}）");
                isSuccessful = false;
            }
        }

        if (isSuccessful)
        {
            RefreshBars();
        }
        else
        {
            HintService.Hint(Lang.Text("Instance.Saves.Datapack.ToggleWarning"), HintType.Error);
        }

        ReloadDatapackFileList();
    }

    private void BtnSelectDelete_Click(object? sender, RouteEventArgs e)
    {
        DeleteDatapacks(loadedEntries.Where(m => selectedDatapacks.Contains(m.RawPath)).ToList());
        ChangeAllSelected(false);
    }

    /// <summary>
    ///     把数据包移入回收站。
    /// </summary>
    private void DeleteDatapacks(IReadOnlyList<DatapackEntry> datapackList)
    {
        var isSuccessful = true;
        var deleted = new List<DatapackEntry>();
        foreach (var datapackEntity in datapackList)
        {
            try
            {
                Trash.MoveToTrash(datapackEntity.Path);
                deleted.Add(datapackEntity);
            }
            catch (Exception ex)
            {
                SavesSupport.LogFailure(ex, $"删除数据包失败（{datapackEntity.Path}）",
                    Lang.Text("Instance.Saves.Error.OperationFailed"));
                isSuccessful = false;
            }

            // 取消选中
            selectedDatapacks.Remove(datapackEntity.RawPath);
        }

        if (!isSuccessful)
        {
            HintService.Hint(Lang.Text("Instance.Saves.Datapack.Delete.FileOccupied"), HintType.Error);
        }
        else if (deleted.Count == 1)
        {
            HintService.Hint(Lang.Text("Instance.Saves.Datapack.Delete.RecycleSingle", deleted[0].FileName), HintType.Success);
        }
        else if (deleted.Count > 1)
        {
            HintService.Hint(Lang.Text("Instance.Saves.Datapack.Delete.RecycleMultiple", deleted.Count), HintType.Success);
        }

        ReloadDatapackFileList();
    }

    private void BtnSelectCancel_Click(object? sender, RouteEventArgs e)
    {
        ChangeAllSelected(false);
    }

    #endregion

    #region 单个资源项

    /// <summary>
    ///     显示数据包的详情。
    /// </summary>
    private void ShowInfo(DatapackEntry datapackEntry)
    {
        try
        {
            // 加载失败信息
            if (datapackEntry.State == DatapackState.Unavailable)
            {
                _ = MsgBox.ShowAsync(
                    Lang.Text("Instance.Saves.Datapack.Info.ReadFailed.WithDetail", datapackEntry.UnavailableReason ?? ""),
                    Lang.Text("Instance.Saves.Datapack.Info.ReadFailedTitle"));
                return;
            }

            // 获取信息
            var contentLines = new List<string>();

            if (datapackEntry.Description is not null)
                contentLines.Add(datapackEntry.Description + "\r\n");
            contentLines.Add(Lang.Text("Instance.Saves.Datapack.Info.File") + datapackEntry.FileName + "（" +
                             Lang.FileSize(datapackEntry.Length) + "）");
            if (datapackEntry.PackFormat is { } packFormat)
                contentLines.Add(Lang.Text("Instance.Saves.Datapack.Info.Version") + packFormat);

            // 显示详情信息
            _ = MsgBox.ShowAsync(string.Join("\r\n", contentLines), datapackEntry.Name);
        }
        catch (Exception ex)
        {
            SavesSupport.LogFailure(ex, "获取数据包详情失败", Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    #endregion

    #region 搜索

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchBox.Text);

    private void SearchRun(object? sender, EventArgs e)
    {
        try
        {
            if (IsSearching)
            {
                // 构造请求
                var queryList = new List<CompSearchEntry<DatapackEntry>>();
                foreach (var entry in loadedEntries)
                {
                    var searchSource = new List<CompSearchSource>
                    {
                        new CompSearchSource(entry.Name, 1d),
                        new CompSearchSource(entry.FileName, 1d),
                    };
                    if (!string.IsNullOrEmpty(entry.Description))
                        searchSource.Add(new CompSearchSource(entry.Description, 0.4d));

                    queryList.Add(new CompSearchEntry<DatapackEntry>(entry, searchSource));
                }

                // 进行搜索
                searchResult = CompSimilarity.Search(queryList, SearchBox.Text, 25, 0.35d)
                    .Select(r => r.Item).ToList();
            }

            RefreshUI();
        }
        catch (Exception ex)
        {
            SavesSupport.LogFailure(ex, Lang.Text("Instance.Saves.SearchError"));
        }
    }

    #endregion
}
