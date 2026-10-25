using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Online;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Download.Comp;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

using LocalFile = PCL.Shion.ModLocalComp.LocalCompFile;
using LocalFileStatus = PCL.Shion.ModLocalComp.LocalCompFile.LocalFileStatus;

namespace PCL.Shion.Pages.Instance;

/// <summary>
///     实例的资源管理页，支持 Mod、资源包、光影包与投影原理图。与 WPF 版 PageInstanceCompResource 对应。
/// </summary>
public partial class PageInstanceCompResource : MyPageRight, IRefreshable
{
    #region 模组信息缓存

    // 模组信息缓存 - 解决排序时重复创建 FileInfo 导致的性能问题
    private readonly Dictionary<string, (DateTime CreationTime, long Length)> modFileInfoCache = new();

    private (DateTime CreationTime, long Length) GetModFileInfo(string path)
    {
        if (modFileInfoCache.TryGetValue(path, out var cacheItem))
            return cacheItem;

        try
        {
            var fileInfo = new FileInfo(path);
            var newItem = (fileInfo.CreationTime, fileInfo.Length);
            modFileInfoCache.TryAdd(path, newItem);
            return newItem;
        }
        catch (Exception ex)
        {
            LogError(ex, "获取模组信息失败: " + path);
            return (DateTime.MinValue, 0L);
        }
    }

    private void Page_Unloaded(object? sender, RoutedEventArgs e)
    {
        modFileInfoCache.Clear();
    }

    #endregion

    #region 初始化

    private readonly CompType currentCompType = CompType.Mod;
    private bool isLoad;

    // TODO(shion): WPF 版的 States.Hint.SchematicFirstTime / UpdateMod 需要持久化到 AppSettings，当前仅在本次会话内生效。
    private static bool schematicFirstTimeShown;
    private static bool updateModConfirmed;

    /// <summary>
    ///     当前显示的文件夹路径。空字符串表示根目录。
    /// </summary>
    public string CurrentFolderPath { get; set; } = "";

    public PageInstanceCompResource(CompType loadCompType)
    {
        currentCompType = loadCompType;
        CurrentFolderPath = ""; // 确保文件夹路径被重置为根目录

        InitializeComponent();
        PanScroll = PanBack;

        if (currentCompType is CompType.Shader or CompType.ResourcePack or CompType.Schematic)
        {
            BtnSelectEnable.IsVisible = false;
            BtnSelectDisable.IsVisible = false;
        }

        // 投影文件管理页隐藏下载按钮
        if (currentCompType == CompType.Schematic)
        {
            BtnManageDownload.IsVisible = false;
            BtnHintDownload.IsVisible = false;
        }

        if (currentCompType == CompType.Mod)
            BtnManageJarInJar.Click += (_, _) => Navigator.Go(PageType.InstanceSetup, PageSubType.VersionModJarInJar);
        else
            BtnManageJarInJar.IsVisible = false;

        Unloaded += Page_Unloaded;
        Loaded += (_, _) => PageOther_Loaded();
        LoaderInit();
        PageExit += UnselectedAllWithAnimation;

        // 事件绑定
        BtnManageBack.Click += (_, _) => GoBackToParentFolder();
        BtnHintBack.Click += (_, _) => GoBackToParentFolder();
        BtnManageOpen.Click += (_, _) => BtnManageOpen_Click();
        BtnHintOpen.Click += (_, _) => BtnManageOpen_Click();
        BtnManageSelectAll.Click += (_, _) => ChangeAllSelected(selectedMods.Count < PanList.Children.Count);
        BtnManageInstall.Click += (_, _) => _ = BtnManageInstall_ClickAsync();
        BtnHintInstall.Click += (_, _) => _ = BtnManageInstall_ClickAsync();
        BtnManageDownload.Click += (_, _) => BtnManageDownload_Click();
        BtnHintDownload.Click += (_, _) => BtnManageDownload_Click();
        BtnManageInfoExport.Click += (_, _) => _ = BtnManageInfoExport_ClickAsync();
        BtnSchematicDownloadMod.Click += (_, _) => BtnSchematicDownloadMod_Click();
        BtnSchematicVersionSelect.Click += (_, _) => BtnSchematicVersionSelect_Click();
        SearchBox.TextChanged += SearchRun;
        BtnFilterAll.Check += ChangeFilter;
        BtnFilterCanUpdate.Check += ChangeFilter;
        BtnFilterDisabled.Check += ChangeFilter;
        BtnFilterEnabled.Check += ChangeFilter;
        BtnFilterError.Check += ChangeFilter;
        BtnFilterDuplicate.Check += ChangeFilter;
        BtnSort.Click += BtnSortClick;
        BtnSelectEnable.Click += (s, ev) => _ = BtnSelectED_ClickAsync(s);
        BtnSelectDisable.Click += (s, ev) => _ = BtnSelectED_ClickAsync(s);
        BtnSelectUpdate.Click += (_, _) => BtnSelectUpdate_Click();
        BtnSelectDelete.Click += (s, ev) => _ = BtnSelectDelete_ClickAsync(ev.KeyModifiers.HasFlag(KeyModifiers.Shift));
        BtnSelectCancel.Click += (_, _) => ChangeAllSelected(false);
        BtnSelectFavorites.Click += (s, _) => BtnSelectFavorites_Click(s);
        BtnSelectShare.Click += (_, _) => BtnSelectShare_Click();
    }

    private static string GetCompRootPath(CompType type)
    {
        var instance = PageInstanceLeft.McInstance!;
        return instance.PathIndie +
               (instance.Info.HasLabyMod
                   ? Path.Combine("labymod-neo", "fabric", instance.Info.VanillaName)
                   : "") + ModLocalComp.GetPathNameByCompType(type) + Path.DirectorySeparatorChar;
    }

    private ModLocalComp.CompLocalLoaderData GetRequireLoaderData()
    {
        var res = new ModLocalComp.CompLocalLoaderData
        {
            gameVersion = PageInstanceLeft.McInstance,
            frm = this,
            compPath = GetCompRootPath(currentCompType),
            compType = currentCompType
        };

        List<CompLoaderType> requireLoaders;
        switch (currentCompType)
        {
            case CompType.Mod:
            {
                requireLoaders = ModLocalComp.GetCurrentVersionModLoader();
                break;
            }
            case CompType.ResourcePack:
            {
                requireLoaders = [CompLoaderType.Minecraft];
                break;
            }
            case CompType.Shader:
            {
                requireLoaders = [CompLoaderType.OptiFine, CompLoaderType.Iris, CompLoaderType.Vanilla, CompLoaderType.Canvas];
                break;
            }
            case CompType.Schematic:
            {
                requireLoaders = [CompLoaderType.Minecraft];
                break;
            }
            default:
            {
                requireLoaders = [];
                break;
            }
        }

        res.loaders = requireLoaders;
        return res;
    }

    private void PageOther_Loaded()
    {
        CurrentFolderPath = string.Empty;

        PanBack.ScrollToHome();
        ModAnimation.AniControlEnabled += 1;
        selectedMods.Clear();
        ReloadCompFileList();
        ChangeAllSelected(false);
        ModAnimation.AniControlEnabled -= 1;

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;

        // 检查是否为原理图管理界面且首次打开
        if (currentCompType == CompType.Schematic && !schematicFirstTimeShown)
        {
            schematicFirstTimeShown = true;
            _ = MsgBox.ShowAsync(Lang.Text("Instance.Saves.Folder.DoubleClickHint.Message"),
                Lang.Text("Instance.Saves.Folder.DoubleClickHint.Title"), Lang.Text("Common.Action.GotIt"));
        }
    }

    /// <summary>
    ///     刷新 Mod 列表。
    /// </summary>
    public void ReloadCompFileList(bool forceReload = false)
    {
        if (LoaderRun(forceReload
                ? ModLoader.LoaderFolderRunType.ForceRun
                : ModLoader.LoaderFolderRunType.RunOnUpdated))
        {
            Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, $"[System] 已刷新 {currentCompType} 列表");
            modFileInfoCache.Clear();

            Dispatcher.UIThread.Post(() =>
            {
                Filter = FilterType.All;
                PanBack.ScrollToHome();
                SearchBox.Text = "";
            });
        }
    }

    // 强制刷新
    private void RefreshSelf()
    {
        Refresh(currentCompType);
    }

    void IRefreshable.Refresh()
    {
        RefreshSelf();
    }

    public static void Refresh(CompType whichPage)
    {
        // 强制刷新
        try
        {
            CompProject.Cache.Clear();
            CompApi.FilesCache.Clear();
            // TODO(shion): WPF 版会删除 ModBase.pathTemp 下的 Cache\LocalComp.json，Shion 尚未提供对应的缓存路径。
            Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(null, "[CompResource] 由于点击刷新按钮，清理本地工程信息缓存");
        }
        catch (Exception ex)
        {
            LogError(ex, "强制刷新时清理本地工程信息缓存失败");
        }

        switch (whichPage)
        {
            case CompType.Mod:
            {
                PageInstanceLeft.frmInstanceMod?.ReloadCompFileList(true);
                if (PageInstanceLeft.frmInstanceLeft is { } leftMod)
                    leftMod.ItemMod.Checked = true;
                break;
            }
            case CompType.ResourcePack:
            {
                PageInstanceLeft.frmInstanceResourcePack?.ReloadCompFileList(true);
                if (PageInstanceLeft.frmInstanceLeft is { } leftPack)
                    leftPack.ItemResourcePack.Checked = true;
                break;
            }
            case CompType.Shader:
            {
                PageInstanceLeft.frmInstanceShader?.ReloadCompFileList(true);
                if (PageInstanceLeft.frmInstanceLeft is { } leftShader)
                    leftShader.ItemShader.Checked = true;
                break;
            }
            case CompType.Schematic:
            {
                PageInstanceLeft.frmInstanceSchematic?.ReloadCompFileList(true);
                if (PageInstanceLeft.frmInstanceLeft is { } leftSchematic)
                    leftSchematic.ItemSchematic.Checked = true;
                break;
            }
        }

        HintService.Hint(Lang.Text("Instance.Left.Refreshing"), log: false);
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanAllBack, null, ModLocalComp.compResourceListLoader,
            _ => LoadUIFromLoaderOutput(), () => currentCompType, false);
    }

    /// <summary>
    ///     以指定的模式运行列表加载器，刷新当前文件夹的内容。
    /// </summary>
    public bool LoaderRun(ModLoader.LoaderFolderRunType type)
    {
        var loadPath = string.IsNullOrEmpty(CurrentFolderPath) ? GetCompRootPath(currentCompType) : CurrentFolderPath;
        return ModLoader.LoaderFolderRun(ModLocalComp.compResourceListLoader, loadPath, type,
            loaderInput: GetRequireLoaderData());
    }

    #endregion

    #region 文件夹导航

    /// <summary>
    ///     进入指定的文件夹。
    /// </summary>
    private void EnterFolder(string folderPath)
    {
        try
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                HintService.Hint(Lang.Text("Instance.Saves.Folder.NotFound"), HintType.Error);
                return;
            }

            CurrentFolderPath = folderPath;
            Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, $"[原理图] 进入文件夹：{folderPath}");

            ModLoader.LoaderFolderRun(ModLocalComp.compResourceListLoader, folderPath,
                ModLoader.LoaderFolderRunType.ForceRun, loaderInput: GetRequireLoaderData());
        }
        catch (Exception ex)
        {
            LogFailure(ex, "进入文件夹失败", Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     返回上级文件夹。
    /// </summary>
    private void GoBackToParentFolder()
    {
        if (string.IsNullOrEmpty(CurrentFolderPath))
            return;

        try
        {
            // 获取根路径
            var rootPath = Path.GetFullPath(GetCompRootPath(currentCompType).TrimEnd(Path.DirectorySeparatorChar));

            // 获取父级路径
            var parentPath = Directory.GetParent(CurrentFolderPath)?.FullName;

            // 如果父级路径就是根路径或者父级路径不在根路径范围内，则返回根目录
            if (parentPath is null || parentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase) ||
                !parentPath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                CurrentFolderPath = "";
            else
                CurrentFolderPath = parentPath;
        }
        catch (Exception ex)
        {
            LogError(ex, "路径处理失败");
            // 发生错误时直接返回根目录
            CurrentFolderPath = "";
        }

        Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this,
            $"[原理图] 返回上级文件夹：{(string.IsNullOrEmpty(CurrentFolderPath) ? "根目录" : CurrentFolderPath)}");

        // 重新加载当前文件夹的内容
        var loadPath = string.IsNullOrEmpty(CurrentFolderPath) ? GetCompRootPath(currentCompType) : CurrentFolderPath;

        // 确保按钮状态正确
        BtnManageBack.IsVisible = !string.IsNullOrEmpty(CurrentFolderPath);
        BtnHintBack.IsVisible = !string.IsNullOrEmpty(CurrentFolderPath);

        // 延迟一帧后再加载，确保 UI 状态已更新
        Dispatcher.UIThread.Post(() =>
            ModLoader.LoaderFolderRun(ModLocalComp.compResourceListLoader, loadPath,
                ModLoader.LoaderFolderRunType.ForceRun, loaderInput: GetRequireLoaderData()), DispatcherPriority.Loaded);
    }

    #endregion

    #region UI 化

    /// <summary>
    ///     已加载的 Mod UI 缓存，不确保按显示顺序排列。Key 为 Mod 的 RawPath。
    /// </summary>
    public Dictionary<string, MyListItem> modItems = new();

    /// <summary>
    ///     将加载器结果的 Mod 列表加载为 UI。
    /// </summary>
    private void LoadUIFromLoaderOutput()
    {
        try
        {
            var output = ModLocalComp.compResourceListLoader.output ?? new List<LocalFile>();

            // 判断应该显示哪一个页面
            if (output.Count > 0)
            {
                PanBack.IsVisible = true;
                PanEmpty.IsVisible = false;
                PanSchematicEmpty.IsVisible = false;
            }
            else
            {
                // 检查是否为投影文件类型且 schematics 文件夹不存在
                if (currentCompType == CompType.Schematic)
                {
                    var schematicsPath = PageInstanceLeft.McInstance!.PathIndie + "schematics" + Path.DirectorySeparatorChar;
                    if (!Directory.Exists(schematicsPath))
                    {
                        PanSchematicEmpty.IsVisible = true;
                        PanEmpty.IsVisible = false;
                        PanBack.IsVisible = false;
                        return;
                    }
                }

                // 根据组件类型设置空状态提示的文本
                if (currentCompType == CompType.Schematic)
                {
                    if (!string.IsNullOrEmpty(CurrentFolderPath))
                    {
                        TxtEmptyTitle.Text = Lang.Text("Instance.Resource.EmptyFolder.Title");
                        TxtEmptyDescription.Text = Lang.Text("Instance.Resource.EmptyFolder.Description");
                    }
                    else
                    {
                        TxtEmptyTitle.Text = Lang.Text("Instance.Resource.Empty.Title");
                        TxtEmptyDescription.Text = Lang.Text("Instance.Resource.Empty.Description");
                    }
                }
                else
                {
                    TxtEmptyTitle.Text = Lang.Text("Instance.Resource.Empty.Title");
                    TxtEmptyDescription.Text = Lang.Text("Instance.Resource.Empty.DescriptionWithDownload");
                }

                // 如果当前在子文件夹中，显示返回上一级按钮
                BtnHintBack.IsVisible = !string.IsNullOrEmpty(CurrentFolderPath);

                PanEmpty.IsVisible = true;
                PanBack.IsVisible = false;
                PanSchematicEmpty.IsVisible = false;
                return;
            }

            // 修改缓存
            modItems.Clear();
            var rootPath = Path.GetFullPath(GetCompRootPath(currentCompType).TrimEnd(Path.DirectorySeparatorChar));

            var itemsToShow = output.Where(item =>
            {
                var itemPath = item.IsFolder ? item.ActualPath : item.path;
                var parentDir = Directory.GetParent(itemPath)?.FullName ?? "";
                return string.IsNullOrEmpty(CurrentFolderPath)
                    ? parentDir.Equals(rootPath, StringComparison.OrdinalIgnoreCase)
                    : parentDir.Equals(CurrentFolderPath, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            foreach (var entry in itemsToShow)
                modItems[entry.RawPath] = BuildLocalCompItem(entry);

            // 显示结果
            Dispatcher.UIThread.Post(() =>
            {
                Filter = FilterType.All;
                SearchBox.Text = ""; // 这会触发结果刷新，所以需要在 modItems 更新之后
                RefreshUI();
                SetSortMethod(SortMethod.CompName);
            });
        }
        catch (Exception ex)
        {
            LogFailure(ex, $"加载 {currentCompType} 列表 UI 失败", Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    private MyListItem BuildLocalCompItem(LocalFile entry)
    {
        try
        {
            ModAnimation.AniControlEnabled += 1;
            var newItem = new MyListItem
            {
                Type = MyListItem.CheckType.CheckBox,
                Tag = entry,
                Title = DisplayName(entry),
                Info = BuildItemInfo(entry),
                Logo = GetItemLogo(entry),
                Checked = selectedMods.Contains(entry.RawPath)
            };
            // TODO(shion): WPF 版的 MyLocalCompItem 会用 MinecraftFormatter 为标题与信息着色，Shion 暂未移植该格式化器。
            newItem.Tags = entry.Tags;
            newItem.Changed += (s, _) => CheckChanged((MyListItem)s!, new RouteEventArgs());
            var lastClickTime = DateTime.MinValue;
            newItem.Click += (sss, _) =>
            {
                var item = (MyListItem)sss!;
                if (entry.IsFolder)
                {
                    // 文件夹项：300ms 内双击进入文件夹，单击切换选中状态
                    var now = DateTime.Now;
                    if ((now - lastClickTime).TotalMilliseconds <= 300d)
                        EnterFolder(entry.ActualPath);
                    else
                        item.Checked = !item.Checked;
                    lastClickTime = now;
                }
                else
                {
                    item.Checked = !item.Checked;
                }
            };
            newItem.PointerReleased += (sss, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Right)
                    Info_Click(entry);
            };
            entry.OnCompUpdate += _ => Dispatcher.UIThread.Post(() => RefreshItem(entry));
            newItem.Buttons = BuildItemButtons(entry);
            return newItem;
        }
        catch (Exception ex)
        {
            LogError(ex, $"创建 UI 项失败：{entry.RawPath}");
            throw;
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private void RefreshItem(LocalFile entry)
    {
        if (!modItems.TryGetValue(entry.RawPath, out var item))
            return;
        item.Title = DisplayName(entry);
        item.Info = BuildItemInfo(entry);
        item.Buttons = BuildItemButtons(entry);
    }

    private IEnumerable<MyIconButton> BuildItemButtons(LocalFile entry)
    {
        var btnOpen = new MyIconButton { LogoScale = 1.05d, SvgIcon = "lucide/folder-open" };
        ToolTip.SetTip(btnOpen, Lang.Text("Instance.Saves.OpenFileLocation"));
        btnOpen.Click += (_, _) => Open_Click(entry);

        var btnCont = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/info" };
        ToolTip.SetTip(btnCont, Lang.Text("Instance.Saves.Detail"));
        btnCont.Click += (_, _) => Info_Click(entry);

        var btnDelete = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/trash-2" };
        // 删除按钮的 Shift 检测使用点击事件携带的修饰键
        ToolTip.SetTip(btnDelete, Lang.Text("Common.Action.Delete"));
        btnDelete.Click += (s, ev) => _ = DeleteModsAsync([entry], ev.KeyModifiers.HasFlag(KeyModifiers.Shift));

        if (currentCompType != CompType.Mod || entry.State == LocalFileStatus.Unavailable)
            return [btnCont, btnOpen, btnDelete];

        var isEnabled = entry.State == LocalFileStatus.Fine;
        var btnED = new MyIconButton
        {
            LogoScale = 1d,
            SvgIcon = isEnabled ? "lucide/circle-minus" : "lucide/circle-check"
        };
        ToolTip.SetTip(btnED, Lang.Text(isEnabled ? "Instance.Resource.Disable" : "Instance.Resource.Enable"));
        btnED.Click += (_, _) => _ = EDModsAsync([entry], !isEnabled);
        return [btnCont, btnOpen, btnED, btnDelete];
    }

    private static string GetItemLogo(LocalFile entry) =>
        entry.Comp?.LogoUrl is { Length: > 0 } url ? url : "avares://PCL.Shion/Assets/Images/Icons/NoIcon.png";

    private static string BuildItemInfo(LocalFile entry)
    {
        var parts = new List<string>();
        var version = CleanPlaceholder(entry.Version);
        if (!string.IsNullOrEmpty(version))
            parts.Add(version);
        parts.Add(entry.FileName);
        if (entry.State == LocalFileStatus.Disabled)
            parts.Add(Lang.Text("Instance.Resource.Disabled"));
        else if (entry.State == LocalFileStatus.Unavailable)
            parts.Add(Lang.Text("Instance.Resource.Error.Unavailable"));
        return string.Join("  |  ", parts);
    }

    /// <summary>
    ///     刷新整个 UI。
    /// </summary>
    /// <summary>
    ///     找出重复的资源：同一 Mod ID（或同一工程）有两个及以上未禁用的文件。
    /// </summary>
    private static List<LocalFile> DuplicateEntries(IEnumerable<LocalFile> entries) => entries
        .Where(m => !m.IsFolder && m.State != LocalFileStatus.Disabled)
        .Select(m => (Entry: m, Key: DuplicateKey(m)))
        .Where(item => item.Key is not null)
        .GroupBy(item => item.Key!, StringComparer.OrdinalIgnoreCase)
        .Where(g => g.Count() > 1)
        .SelectMany(g => g.Select(item => item.Entry))
        .ToList();

    private static string? DuplicateKey(LocalFile entry) =>
        !string.IsNullOrEmpty(entry.ModId) ? "mod:" + entry.ModId :
        entry.Comp is not null ? "comp:" + entry.Comp.Id : null;

    public void RefreshUI()
    {
        if (PanList is null)
            return;
        var showingMods = (IsSearching ? searchResult ?? new List<LocalFile>() : modItems.Values
                .Select(i => (LocalFile)i.Tag!)
                .ToList())
            .Where(CanPassFilter).ToList();

        // 对显示的资源进行排序，确保文件夹置顶
        if (showingMods.Count > 0)
        {
            var sortMethod = GetSortMethod(currentSortMethod);
            showingMods.Sort((a, b) => sortMethod(a, b));
        }

        // 重新列出列表
        ModAnimation.AniControlEnabled += 1;
        PanList.Children.Clear();
        if (showingMods.Count > 0)
        {
            PanList.IsVisible = true;
            foreach (var target in showingMods)
            {
                if (!modItems.TryGetValue(target.RawPath, out var item))
                    continue;
                item.Checked = selectedMods.Contains(target.RawPath); // 更新选中状态
                PanList.Children.Add(item);
            }
        }
        else
        {
            PanList.IsVisible = false;
        }

        ModAnimation.AniControlEnabled -= 1;
        selectedMods = new HashSet<string>(selectedMods.Where(m =>
            showingMods.Any(s => (s.RawPath ?? "") == (m ?? ""))));
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
        var itemSource = (IsSearching ? searchResult ?? new List<LocalFile>() : AllEntries()).ToList();
        var anyCount = itemSource.Count;
        var updateCount = itemSource.Count(item => item.CanUpdate);
        var enabledCount = itemSource.Count(item => item.State == LocalFileStatus.Fine);
        var disabledCount = itemSource.Count(item => item.State == LocalFileStatus.Disabled);
        var unavailableCount = itemSource.Count(item => item.State == LocalFileStatus.Unavailable);

        // 显示
        BtnFilterAll.Text = IsSearching
            ? Lang.Text("Instance.Resource.Filter.SearchResult")
            : Lang.Text("Instance.Resource.Filter.AllWithCount", anyCount);
        BtnFilterCanUpdate.Text = Lang.Text("Instance.Resource.Filter.UpdatableWithCount", updateCount);
        BtnFilterCanUpdate.IsVisible = Filter == FilterType.CanUpdate || updateCount > 0;
        BtnFilterEnabled.Text = Lang.Text("Instance.Resource.Filter.EnabledWithCount", enabledCount);
        BtnFilterEnabled.IsVisible = Filter == FilterType.Enabled || (enabledCount > 0 && enabledCount < anyCount);
        BtnFilterDisabled.Text = Lang.Text("Instance.Resource.Filter.DisabledWithCount", disabledCount);
        BtnFilterDisabled.IsVisible = Filter == FilterType.Disabled || disabledCount > 0;
        BtnFilterError.Text = Lang.Text("Instance.Resource.Filter.ErrorWithCount", unavailableCount);
        BtnFilterError.IsVisible = Filter == FilterType.Unavailable || unavailableCount > 0;

        // 查找重复项目：优先按文件里读到的 Mod ID 判断，读不到时按联网获取的工程判断，已禁用的不算重复
        var duplicateItems = DuplicateEntries(itemSource);
        BtnFilterDuplicate.Text = Lang.Text("Instance.Resource.Filter.DuplicateWithCount", duplicateItems.Count);
        BtnFilterDuplicate.IsVisible = Filter == FilterType.Duplicate || duplicateItems.Count > 0;

        // 返回按钮显示控制（在子文件夹中时显示）
        BtnManageBack.IsVisible = !string.IsNullOrEmpty(CurrentFolderPath);

        // -----------------
        // 底部栏
        // -----------------
        var newCount = selectedMods.Count;
        var selected = newCount > 0;
        if (selected)
            LabSelect.Text = Lang.Text("Instance.Resource.SelectedCount", newCount);

        if (selected)
        {
            var selectedEntries = AllEntries().Where(m => selectedMods.Contains(m.RawPath)).ToList();
            var hasUpdate = selectedEntries.Any(m => m.CanUpdate);
            var hasEnabled = selectedEntries.Any(m => m.State == LocalFileStatus.Fine);
            var hasDisabled = selectedEntries.Any(m => m.State == LocalFileStatus.Disabled);
            // 是否所有选中的资源都有有效的工程信息（即已完成联网更新）
            var canFavoriteAndShare = selectedEntries.All(m => m.Comp is not null && !string.IsNullOrEmpty(m.Comp.Id));

            BtnSelectDisable.IsEnabled = hasEnabled;
            BtnSelectEnable.IsEnabled = hasDisabled;
            BtnSelectUpdate.IsEnabled = hasUpdate;

            // 针对投影原理图隐藏分享、更新、收藏按钮
            var isSchematic = currentCompType == CompType.Schematic;
            BtnSelectUpdate.IsVisible = !isSchematic;
            BtnSelectFavorites.IsVisible = !isSchematic;
            BtnSelectShare.IsVisible = !isSchematic;
            if (!isSchematic)
            {
                BtnSelectFavorites.IsEnabled = canFavoriteAndShare;
                BtnSelectShare.IsEnabled = canFavoriteAndShare;
            }
        }

        // 更新显示状态
        // TODO(shion): WPF 版在这里播放底栏的出现与隐藏动画，Shion 版暂以直接显示与隐藏代替。
        PanListBack.Margin = new Thickness(0d, 0d, 0d, selected ? 95 : 15);
        CardSelect.IsVisible = selected;
        CardSelect.Opacity = selected ? 1d : 0d;
        bottomBarShownCount = newCount;
    }

    private int bottomBarShownCount;

    /// <summary>
    ///     所有已加载的资源（不含搜索过滤）。
    /// </summary>
    private static IEnumerable<LocalFile> AllEntries() =>
        ModLocalComp.compResourceListLoader.output ?? new List<LocalFile>();

    #endregion

    #region 管理

    private void BtnManageOpen_Click()
    {
        try
        {
            var compFilePath = string.IsNullOrEmpty(CurrentFolderPath)
                ? GetCompRootPath(currentCompType)
                : CurrentFolderPath.EndsWith(Path.DirectorySeparatorChar) ? CurrentFolderPath : CurrentFolderPath + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(compFilePath);
            SystemShell.OpenFolder(compFilePath);
        }
        catch (Exception ex)
        {
            LogFailure(ex, "打开 Mods 文件夹失败", Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     安装文件（Mod、资源包、光影包、原理图）。
    /// </summary>
    private async Task BtnManageInstall_ClickAsync()
    {
        var fileList = await SelectFilesAsync(currentCompType);
        if (fileList is null || fileList.Count == 0)
            return;
        await InstallCompFilesAsync(fileList, currentCompType, CurrentFolderPath);
    }

    private async Task<List<string>?> SelectFilesAsync(CompType type)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return null;

        var (filter, title, patterns) = type switch
        {
            CompType.Mod => ("Instance.Resource.Install.FileDialog.Mod.Filter", "Instance.Resource.Install.FileDialog.Mod.Title", new[] { "*.jar", "*.litemod", "*.disabled", "*.old" }),
            CompType.ResourcePack => ("Instance.Resource.Install.FileDialog.ResourcePack.Filter", "Instance.Resource.Install.FileDialog.ResourcePack.Title", new[] { "*.zip" }),
            CompType.Shader => ("Instance.Resource.Install.FileDialog.Shader.Filter", "Instance.Resource.Install.FileDialog.Shader.Title", new[] { "*.zip" }),
            _ => ("Instance.Resource.Install.FileDialog.Schematic.Filter", "Instance.Resource.Install.FileDialog.Schematic.Title", new[] { "*.litematic", "*.nbt", "*.schematic", "*.schem" })
        };

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Lang.Text(title),
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType(Lang.Text(filter)) { Patterns = patterns }]
        });
        return files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
    }

    /// <summary>
    ///     尝试安装 Mod。返回输入的文件是否为一个 Mod 文件，仅用于判断拖拽行为。
    /// </summary>
    public static async Task<bool> InstallMods(IEnumerable<string> filePathList)
    {
        var list = filePathList.ToList();
        if (list.Count == 0) return false;

        // 1. 检查文件扩展名
        var firstFile = list[0];
        var extension = firstFile.Split('.').LastOrDefault()?.ToLowerInvariant();
        string[] allowedExtensions = ["jar", "litemod", "disabled", "old"];
        if (!allowedExtensions.Contains(extension)) return false;

        Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(null, "[System] 文件格式为 jar/litemod，尝试安装为 Mod");

        // 2. 回收站中的文件有错误的文件名
        if (firstFile.Contains(@":\$RECYCLE.BIN\", StringComparison.OrdinalIgnoreCase))
        {
            HintService.Hint(Lang.Text("Instance.Resource.Install.RestoreFromRecycleBin"), HintType.Error);
            return true;
        }

        // 3. 确定目标实例
        var targetInstance = PageInstanceLeft.McInstance;

        // 4. 检查实例状态
        if (targetInstance is null || !targetInstance.Modable)
        {
            HintService.Hint(Lang.Text("Instance.Resource.Install.SelectModableInstance"));
            return true;
        }

        // 5. 需要用户确认
        var confirmed = await MsgBox.ShowAsync(
            Lang.Text("Instance.Resource.Install.ModConfirm.Message", targetInstance.Name),
            Lang.Text("Instance.Resource.Install.ModConfirm.Title"), Lang.Text("Common.Action.Confirm"),
            Lang.Text("Common.Action.Cancel"));
        if (confirmed != 1)
            return true;

        // 6. 执行安装
        ExecuteModInstallation(targetInstance, list, true);
        return true;
    }

    private static void ExecuteModInstallation(GameInstance targetMcInstance, IReadOnlyList<string> filePathList,
        bool refreshList)
    {
        // 路径解析
        var modPathSuffix = targetMcInstance.Info.HasLabyMod
            ? Path.Combine("labymod-neo", "fabric", targetMcInstance.Info.VanillaName) + Path.DirectorySeparatorChar
            : "";
        var modFolder = targetMcInstance.PathIndie + modPathSuffix + "mods" + Path.DirectorySeparatorChar;

        try
        {
            Directory.CreateDirectory(modFolder);
            foreach (var modFile in filePathList)
            {
                var fileName = Path.GetFileName(modFile).Replace(".disabled", "").Replace(".old", "");
                if (!fileName.Contains('.')) fileName += ".jar"; // #4227
                File.Copy(modFile, Path.Combine(modFolder, fileName), true);
            }

            // 成功提示
            if (filePathList.Count == 1)
            {
                var installedName = Path.GetFileName(filePathList[0]).Replace(".disabled", "").Replace(".old", "");
                HintService.Hint(Lang.Text("Instance.Resource.Install.SuccessSingle", installedName), HintType.Success);
            }
            else
            {
                HintService.Hint(Lang.Text("Instance.Resource.Install.SuccessMultiple", filePathList.Count,
                    Lang.Text("Download.Comp.Type.Mod")), HintType.Success);
            }

            // 刷新列表
            if (refreshList)
                ModLoader.LoaderFolderRun(ModLocalComp.compResourceListLoader, modFolder,
                    ModLoader.LoaderFolderRunType.ForceRun,
                    loaderInput: PageInstanceLeft.frmInstanceMod?.GetRequireLoaderData());
        }
        catch (Exception ex)
        {
            LogError(ex, "拷贝文件失败");
        }
    }

    /// <summary>
    ///     安装组件文件（Mod、资源包、光影包、投影文件等）。
    /// </summary>
    public static async Task InstallCompFilesAsync(IReadOnlyList<string> filePathList, CompType compType,
        string targetFolderPath = "")
    {
        if (filePathList.Count == 0)
            return;

        var extension = Path.GetExtension(filePathList[0]).TrimStart('.').ToLowerInvariant();
        string[] validExtensions;
        var compTypeName = "";
        string compFolder;

        // 回收站中的文件有错误的文件名
        if (filePathList[0].Contains(@":\$RECYCLE.BIN\", StringComparison.OrdinalIgnoreCase))
        {
            HintService.Hint(Lang.Text("Instance.Resource.Install.RestoreFromRecycleBin"), HintType.Error);
            return;
        }

        // 获取并检查目标实例
        var targetInstance = PageInstanceLeft.McInstance!;

        // 根据组件类型设置相关参数
        switch (compType)
        {
            case CompType.Mod:
            {
                validExtensions = ["jar", "litemod", "disabled", "old"];
                compTypeName = "Mod";
                compFolder = string.IsNullOrEmpty(targetFolderPath) ? GetCompRootPath(CompType.Mod) : targetFolderPath;
                break;
            }
            case CompType.ResourcePack:
            {
                validExtensions = ["zip"];
                compTypeName = Lang.Text("Download.Comp.Type.ResourcePack");
                compFolder = string.IsNullOrEmpty(targetFolderPath) ? GetCompRootPath(CompType.ResourcePack) : targetFolderPath;
                break;
            }
            case CompType.Shader:
            {
                validExtensions = ["zip"];
                compTypeName = Lang.Text("Download.Comp.Type.Shader");
                compFolder = string.IsNullOrEmpty(targetFolderPath) ? GetCompRootPath(CompType.Shader) : targetFolderPath;
                break;
            }
            default:
            {
                validExtensions = ["litematic", "nbt", "schematic", "schem"];
                compTypeName = Lang.Text("Download.Comp.Type.Schematic");
                compFolder = string.IsNullOrEmpty(targetFolderPath) ? GetCompRootPath(CompType.Schematic) : targetFolderPath;
                break;
            }
        }

        // 检查文件扩展名
        if (!validExtensions.Contains(extension))
        {
            HintService.Hint(Lang.Text("Instance.Resource.Install.UnsupportedFormat", extension, compTypeName,
                string.Join(", ", validExtensions)), HintType.Error);
            return;
        }

        Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(null, $"[System] 文件为 {extension} 格式，尝试作为{compTypeName}安装");

        // 检查实例兼容性
        if (compType == CompType.Mod && !targetInstance.Modable)
        {
            HintService.Hint(Lang.Text("Instance.Resource.Install.SelectModableInstance"));
            return;
        }

        // 确认安装
        var currentPage = compType switch
        {
            CompType.ResourcePack => PageSubType.VersionResourcePack,
            CompType.Shader => PageSubType.VersionShader,
            CompType.Schematic => PageSubType.VersionSchematic,
            _ => PageSubType.VersionMod
        };
        var isCurrentPage = PageInstanceLeft.frmInstanceLeft is not null && Navigator.Current == PageType.InstanceSetup &&
                            Navigator.CurrentSub == currentPage;
        if (!isCurrentPage)
        {
            var confirmed = await MsgBox.ShowAsync(
                Lang.Text("Instance.Resource.Install.GenericConfirm.Message", compTypeName, targetInstance.Name),
                Lang.Text("Instance.Resource.Install.GenericConfirm.Title", compTypeName),
                Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel"));
            if (confirmed != 1)
                return;
        }

        // 执行安装
        try
        {
            Directory.CreateDirectory(compFolder);
            foreach (var filePath in filePathList)
            {
                var newFileName = Path.GetFileName(filePath);
                if (compType == CompType.Mod)
                {
                    newFileName = newFileName.Replace(".disabled", "").Replace(".old", "");
                    if (!newFileName.Contains('.'))
                        newFileName += ".jar";
                }

                var destFile = Path.Combine(compFolder, newFileName);
                if (File.Exists(destFile))
                {
                    var overwrite = await MsgBox.ShowAsync(
                        Lang.Text("Instance.Resource.Install.OverwriteConfirm.Message", newFileName),
                        Lang.Text("Instance.Resource.Install.OverwriteConfirm.Title"),
                        Lang.Text("Common.Action.Overwrite"), Lang.Text("Common.Action.Cancel"));
                    if (overwrite != 1)
                        continue;
                }

                File.Copy(filePath, destFile, true);
            }

            if (filePathList.Count == 1)
                HintService.Hint(Lang.Text("Instance.Resource.Install.SuccessSingle", Path.GetFileName(filePathList[0])),
                    HintType.Success);
            else
                HintService.Hint(Lang.Text("Instance.Resource.Install.SuccessMultiple", filePathList.Count, compTypeName),
                    HintType.Success);

            // 刷新列表
            if (isCurrentPage)
            {
                if (compType == CompType.Mod)
                {
                    if (PageInstanceLeft.frmInstanceMod is not null)
                        ModLoader.LoaderFolderRun(ModLocalComp.compResourceListLoader, compFolder,
                            ModLoader.LoaderFolderRunType.ForceRun,
                            loaderInput: PageInstanceLeft.frmInstanceMod.GetRequireLoaderData());
                }
                else
                {
                    var currentForm = GetCurrentCompResourceForm();
                    if (currentForm is not null)
                        Dispatcher.UIThread.Post(() => currentForm.ReloadCompFileList(true));
                }
            }
        }
        catch (Exception ex)
        {
            LogFailure(ex, $"复制{compTypeName}文件失败", Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     获取当前的组件资源管理页面。
    /// </summary>
    private static PageInstanceCompResource? GetCurrentCompResourceForm()
    {
        return Navigator.CurrentSub switch
        {
            PageSubType.VersionMod => PageInstanceLeft.frmInstanceMod,
            PageSubType.VersionResourcePack => PageInstanceLeft.frmInstanceResourcePack,
            PageSubType.VersionShader => PageInstanceLeft.frmInstanceShader,
            PageSubType.VersionSchematic => PageInstanceLeft.frmInstanceSchematic,
            _ => null
        };
    }

    private async Task BtnManageInfoExport_ClickAsync()
    {
        var choice = await MsgBox.ShowAsync(
            Lang.Text("Instance.Resource.Export.Mode.Message"), Lang.Text("Instance.Resource.Export.Mode.Title"),
            Lang.Text("Instance.Resource.Export.Mode.Txt"), Lang.Text("Instance.Resource.Export.Mode.Csv"),
            Lang.Text("Common.Action.Cancel"));

        switch (choice)
        {
            case 1: // TXT
            {
                var exportContent = new List<string>();
                foreach (var entry in AllEntries())
                {
                    exportContent.Add(entry.FileName);
                    AppendEmbeddedForExport(exportContent, entry.EmbeddedMods, 1);
                }

                await ExportTextAsync(string.Join("\r\n", exportContent),
                    PageInstanceLeft.McInstance!.Name + "已安装的资源信息.txt");
                break;
            }
            case 2: // CSV
            {
                var exportContent = new List<string>
                {
                    "文件名,资源名称,资源版本,此版本更新时间,Mod ID,对应平台工程 ID,文件大小（字节）,文件路径,内嵌模组"
                };
                foreach (var entry in AllEntries())
                    exportContent.Add(
                        $"{entry.FileName},{entry.Comp?.TranslatedName},{entry.Version},{entry.compFile?.ReleaseDate},{entry.ModId},{entry.Comp?.Id},{GetModFileInfo(entry.path).Length},{entry.path},{string.Join(";", FlattenEmbeddedNames(entry.EmbeddedMods))}");
                await ExportTextAsync(string.Join("\r\n", exportContent),
                    PageInstanceLeft.McInstance!.Name + "已安装的资源信息.csv");
                break;
            }
        }
    }

    private async Task ExportTextAsync(string content, string fileName)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top is null)
                return;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Lang.Text("Instance.Resource.Export.SelectSaveLocation"),
                SuggestedFileName = fileName,
                FileTypeChoices = [new FilePickerFileType(Lang.Text("Instance.Resource.Export.FilesFilter"))]
            });
            var savePath = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(savePath))
                return;
            await File.WriteAllTextAsync(savePath, content, Encoding.UTF8);
            SystemShell.OpenFolder(savePath);
        }
        catch (Exception ex)
        {
            LogFailure(ex, "导出资源信息失败", Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    private static void AppendEmbeddedForExport(List<string> lines, List<LocalFile>? mods, int depth)
    {
        if (mods is null)
            return;
        var indent = new string('\t', depth);
        foreach (var mod in mods)
        {
            var line = indent + "└ " + (mod.Name ?? mod.ModId ?? mod.FileName);
            if (!string.IsNullOrWhiteSpace(mod.Version))
                line += $" ({mod.Version})";
            lines.Add(line);
            if (mod.EmbeddedMods is { Count: > 0 })
                AppendEmbeddedForExport(lines, mod.EmbeddedMods, depth + 1);
        }
    }

    private static IEnumerable<string> FlattenEmbeddedNames(List<LocalFile>? mods)
    {
        if (mods is null)
            yield break;
        foreach (var mod in mods)
        {
            yield return mod.Name ?? mod.ModId ?? mod.FileName;
            if (mod.EmbeddedMods is { Count: > 0 })
                foreach (var child in FlattenEmbeddedNames(mod.EmbeddedMods))
                    yield return child;
        }
    }

    /// <summary>
    ///     下载资源：跳转到对应类型的下载页面。
    /// </summary>
    private void BtnManageDownload_Click()
    {
        switch (currentCompType)
        {
            case CompType.Mod:
                Navigator.Go(PageType.Download, PageSubType.DownloadMod);
                break;
            case CompType.ResourcePack:
                Navigator.Go(PageType.Download, PageSubType.DownloadResourcePack);
                break;
            case CompType.Shader:
                Navigator.Go(PageType.Download, PageSubType.DownloadShader);
                break;
        }

        // 将当前实例设置为筛选器
        if (PageInstanceLeft.McInstance is { } instance)
            PageComp.TargetVersion = CreateTargetVersion(instance);
    }

    /// <summary>
    ///     下载投影 Mod 按钮点击事件。
    /// </summary>
    private void BtnSchematicDownloadMod_Click()
    {
        Navigator.Go(PageType.Download, PageSubType.DownloadMod);
        if (PageInstanceLeft.McInstance is { } instance)
            PageComp.TargetVersion = CreateTargetVersion(instance); // 将当前实例设置为筛选器
    }

    /// <summary>
    ///     根据实例的游戏版本与加载器生成下载页面的筛选条件。
    /// </summary>
    private static CompTargetVersion CreateTargetVersion(GameInstance instance)
    {
        var loaders = new List<CompLoaderType>();
        if (instance.Info.HasForge)
            loaders.Add(CompLoaderType.Forge);
        if (instance.Info.HasNeoForge)
            loaders.Add(CompLoaderType.NeoForge);
        if (instance.Info.HasFabric)
            loaders.Add(CompLoaderType.Fabric);
        return new CompTargetVersion(instance.Info.VanillaName, loaders);
    }

    /// <summary>
    ///     实例选择按钮点击事件。
    /// </summary>
    private void BtnSchematicVersionSelect_Click()
    {
        Navigator.Go(PageType.Launch);
        Navigator.Go(PageType.InstanceSelect);
    }

    #endregion

    #region 选择

    /// <summary>
    ///     选择的 Mod 的路径（不含 .disabled 和 .old）。
    /// </summary>
    public HashSet<string> selectedMods = new();

    // 单项切换选择状态
    public void CheckChanged(MyListItem sender, RouteEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (sender.Tag is not LocalFile entry)
            return;
        // 更新选择了的内容
        if (sender.Checked)
            selectedMods.Add(entry.RawPath);
        else
            selectedMods.Remove(entry.RawPath);
        RefreshBars();
    }

    // 切换所有项的选择状态
    private void ChangeAllSelected(bool value)
    {
        ModAnimation.AniControlEnabled += 1;
        selectedMods.Clear();
        foreach (var item in modItems.Values)
        {
            // #4992，Mod 从过滤器看可能不应在列表中，但因为刚切换状态所以依然保留在列表中，所以应该从列表 UI 判断，而非从过滤器判断
            var shouldSelected = value && PanList.Children.Contains(item);
            item.Checked = shouldSelected;
            if (shouldSelected && item.Tag is LocalFile entry)
                selectedMods.Add(entry.RawPath);
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Ctrl + A 全选（搜索框有内容时保留给文本编辑）
        if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !IsSearching)
        {
            ChangeAllSelected(true);
            e.Handled = true;
        }
    }

    #endregion

    #region 筛选

    private FilterType filter = FilterType.All;

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
                    BtnFilterAll.Checked = true;
                    break;
                case FilterType.Enabled:
                    BtnFilterEnabled.Checked = true;
                    break;
                case FilterType.Disabled:
                    BtnFilterDisabled.Checked = true;
                    break;
                case FilterType.CanUpdate:
                    BtnFilterCanUpdate.Checked = true;
                    break;
                case FilterType.Duplicate:
                    BtnFilterDuplicate.Checked = true;
                    break;
                default:
                    BtnFilterError.Checked = true;
                    break;
            }

            RefreshUI();
        }
    }

    public enum FilterType
    {
        All = 0,
        Enabled = 1,
        Disabled = 2,
        CanUpdate = 3,
        Unavailable = 4,
        Duplicate = 5
    }

    /// <summary>
    ///     检查该 Mod 项是否符合当前筛选的类别。
    /// </summary>
    private bool CanPassFilter(LocalFile checkingMod)
    {
        switch (Filter)
        {
            case FilterType.All:
                return true;
            case FilterType.Enabled:
                return checkingMod.State == LocalFileStatus.Fine;
            case FilterType.Disabled:
                return checkingMod.State == LocalFileStatus.Disabled;
            case FilterType.CanUpdate:
                return checkingMod.CanUpdate;
            case FilterType.Unavailable:
                return checkingMod.State == LocalFileStatus.Unavailable;
            case FilterType.Duplicate:
            {
                var itemSource = IsSearching
                    ? searchResult ?? new List<LocalFile>()
                    : AllEntries().ToList();
                return DuplicateEntries(itemSource).Contains(checkingMod);
            }
            default:
                return false;
        }
    }

    // 点击筛选项触发的改变
    private void ChangeFilter(MyRadioButton sender, bool raiseByMouse)
    {
        Filter = (FilterType)Convert.ToInt32(sender.Tag);
        RefreshUI();
        DoSort();
    }

    #endregion

    #region 排序

    private SortMethod currentSortMethod = SortMethod.CompName;

    private void SetSortMethod(SortMethod target)
    {
        currentSortMethod = target;
        BtnSort.Text = Lang.Text("Instance.Resource.Sort.Text", GetSortName(target));
        DoSort();
    }

    private enum SortMethod
    {
        FileName,
        CompName,
        TagNums,
        CreateTime,
        ModFileSize
    }

    private string GetSortName(SortMethod method) => method switch
    {
        SortMethod.FileName => Lang.Text("Instance.Resource.Sort.FileName"),
        SortMethod.CompName => Lang.Text("Instance.Resource.Sort.ResourceName"),
        SortMethod.TagNums => Lang.Text("Instance.Resource.Sort.TagCount"),
        SortMethod.CreateTime => Lang.Text("Instance.Resource.Sort.AddTime"),
        SortMethod.ModFileSize => Lang.Text("Instance.Resource.Sort.FileSize"),
        _ => Lang.Text("Instance.Resource.Sort.ResourceName")
    };

    private void BtnSortClick(object? sender, RouteEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var method in Enum.GetValues<SortMethod>())
        {
            var item = new MyMenuItem { Header = GetSortName(method) };
            item.Click += (_, _) => SetSortMethod(method);
            menu.Items.Add(item);
        }

        if (sender is Control anchor)
            menu.Open(anchor);
    }

    private readonly object sortLock = new();

    private void DoSort()
    {
        lock (sortLock)
        {
            try
            {
                if (PanList is null || PanList.Children.Count < 2)
                    return;

                // 将子元素转换为可排序的列表
                var items = PanList.Children.OfType<MyListItem>().ToList();
                var method = GetSortMethod(currentSortMethod);

                // 分离有效和无效项（保持原始相对顺序）
                var invalid = items.Where(i =>
                    i.Tag is not LocalFile entry ||
                    (currentSortMethod == SortMethod.TagNums && entry.Comp is null && !entry.IsFolder)).ToList();
                var valid = items.Except(invalid).ToList();
                valid.Sort((x, y) => method((LocalFile)x.Tag!, (LocalFile)y.Tag!));
                // 合并保持无效项的原始顺序
                items = valid.Concat(invalid).ToList();

                // 批量更新 UI 元素
                PanList.Children.Clear();
                foreach (var item in items)
                    PanList.Children.Add(item);
            }
            catch (Exception ex)
            {
                LogFailure(ex, "执行排序时出错", Lang.Text("Instance.Resource.Error.OperationFailed"));
            }
        }
    }

    private Comparison<LocalFile> GetSortMethod(SortMethod method)
    {
        // 通用的文件夹置顶比较函数
        static int FolderFirstCompare(LocalFile a, LocalFile b)
        {
            if (a.IsFolder && !b.IsFolder)
                return -1;
            if (!a.IsFolder && b.IsFolder)
                return 1;
            return 0; // 相同类型，需要进一步比较
        }

        switch (method)
        {
            case SortMethod.FileName:
                return (a, b) =>
                {
                    var folderResult = FolderFirstCompare(a, b);
                    return folderResult != 0
                        ? folderResult
                        : string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
                };
            case SortMethod.CompName:
                return (a, b) =>
                {
                    var folderResult = FolderFirstCompare(a, b);
                    return folderResult != 0
                        ? folderResult
                        : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                };
            case SortMethod.TagNums:
                return (a, b) =>
                {
                    var folderResult = FolderFirstCompare(a, b);
                    if (folderResult != 0)
                        return folderResult;
                    if (a.IsFolder && b.IsFolder)
                        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    if (!a.IsFolder && !b.IsFolder)
                    {
                        var aTagCount = a.Comp?.TagKeys.Count ?? 0;
                        var bTagCount = b.Comp?.TagKeys.Count ?? 0;
                        return bTagCount.CompareTo(aTagCount);
                    }

                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                };
            case SortMethod.CreateTime:
                return (a, b) =>
                {
                    var folderResult = FolderFirstCompare(a, b);
                    if (folderResult != 0)
                        return folderResult;
                    var aPath = a.IsFolder ? a.ActualPath : a.path;
                    var bPath = b.IsFolder ? b.ActualPath : b.path;
                    var aDate = GetModFileInfo(aPath).CreationTime;
                    var bDate = GetModFileInfo(bPath).CreationTime;
                    if (aDate == DateTime.MinValue && bDate == DateTime.MinValue)
                        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    if (aDate == DateTime.MinValue) return 1; // 出错的文件排在后面
                    if (bDate == DateTime.MinValue) return -1;
                    return bDate.CompareTo(aDate);
                };
            case SortMethod.ModFileSize:
                return (a, b) =>
                {
                    var folderResult = FolderFirstCompare(a, b);
                    if (folderResult != 0)
                        return folderResult;
                    if (a.IsFolder && b.IsFolder)
                        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    if (!a.IsFolder && !b.IsFolder)
                    {
                        var aSize = GetModFileInfo(a.ActualPath).Length;
                        var bSize = GetModFileInfo(b.ActualPath).Length;
                        if (aSize == 0L && bSize == 0L)
                            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                        if (aSize == 0L) return 1;
                        if (bSize == 0L) return -1;
                        return bSize.CompareTo(aSize);
                    }

                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                };
            default:
                return (a, b) =>
                {
                    var folderResult = FolderFirstCompare(a, b);
                    return folderResult != 0
                        ? folderResult
                        : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                };
        }
    }

    #endregion

    #region 下边栏

    // 启用 / 禁用
    private async Task BtnSelectED_ClickAsync(object? sender)
    {
        var isEnable = !ReferenceEquals(sender, BtnSelectDisable);
        var targets = AllEntries().Where(m => selectedMods.Contains(m.RawPath)).ToList();
        await EDModsAsync(targets, isEnable);
        ChangeAllSelected(false);
    }

    private async Task EDModsAsync(IReadOnlyList<LocalFile> modList, bool isEnable)
    {
        var list = modList.ToList();
        if (!isEnable)
        {
            if (!await ConfirmCascadeReadyAsync()) return;
            var affected = JijFindAffected(list);
            if (affected.Count > 0)
            {
                var choice = await AskJijCascadeAsync(affected, false);
                if (choice is null) return;
                if (choice == 1) list = list.Concat(affected).ToList();
            }
        }

        EDModsCore(list, isEnable);
    }

    private async Task DeleteModsAsync(IReadOnlyList<LocalFile> modList, bool isShiftPressed)
    {
        if (!await ConfirmCascadeReadyAsync()) return;
        var list = modList.ToList();
        var affected = JijFindAffected(list);
        if (affected.Count > 0)
        {
            var choice = await AskJijCascadeAsync(affected, true);
            if (choice is null) return;
            switch (choice)
            {
                case 1:
                    if (!EDModsCore(affected, false)) return;
                    break;
                case 2:
                    // 先禁用依赖者；若后续删除部分失败，也不会留下启用但缺少前置的 Mod
                    if (!EDModsCore(affected, false)) return;
                    list = list.Concat(affected).ToList();
                    break;
            }
        }

        DeleteModsCore(list, isShiftPressed);
    }

    private async Task BtnSelectDelete_ClickAsync(bool isShiftPressed)
    {
        var targets = AllEntries().Where(m => selectedMods.Contains(m.RawPath)).ToList();
        await DeleteModsAsync(targets, isShiftPressed);
        ChangeAllSelected(false);
    }

    /// <summary>
    ///     实际删除文件（默认移入回收站，按住 Shift 时永久删除），并同步更新列表与 UI。
    /// </summary>
    private void DeleteModsCore(IReadOnlyList<LocalFile> modList, bool isShiftPressed)
    {
        try
        {
            var isSuccessful = true;

            // 确认需要删除的文件：文件夹只需要删除自身
            var paths = modList.SelectMany(target =>
                {
                    if (target.IsFolder) return new[] { target.path };
                    if (target.State == LocalFileStatus.Fine)
                        return new[] { target.path, target.path + (File.Exists(target.path + ".old") ? ".old" : ".disabled") };
                    return new[] { target.path, target.RawPath };
                })
                .Distinct()
                .Where(m => m.EndsWith(Path.DirectorySeparatorChar + "__FOLDER__", StringComparison.OrdinalIgnoreCase)
                    ? Directory.Exists(m.Replace(Path.DirectorySeparatorChar + "__FOLDER__", ""))
                    : File.Exists(m))
                .ToList();
            var targets = paths.Select(m => new LocalFile(m)).ToList();

            // 实际删除文件
            foreach (var entry in targets)
            {
                try
                {
                    if (entry.IsFolder)
                    {
                        if (isShiftPressed)
                            Directory.Delete(entry.ActualPath, true);
                        else
                            Trash.MoveToTrash(entry.ActualPath);
                    }
                    else if (isShiftPressed)
                    {
                        File.Delete(entry.path);
                    }
                    else
                    {
                        Trash.MoveToTrash(entry.path);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    LogError(ex, "删除资源被主动取消");
                    ReloadCompFileList(true);
                    return;
                }
                catch (Exception ex)
                {
                    LogFailure(ex, $"删除资源失败（{entry.path}）", Lang.Text("Instance.Resource.Error.OperationFailed"));
                    isSuccessful = false;
                    continue;
                }

                // 取消选中
                selectedMods.Remove(entry.RawPath);
                // 更改 Loader 和 UI 中的列表
                ModLocalComp.compResourceListLoader.output.Remove(entry);
                searchResult?.Remove(entry);
                modItems.Remove(entry.RawPath);
                var indexOfUi = PanList.Children.ToList().FindIndex(c =>
                    c is MyListItem item && item.Tag is LocalFile listed && listed.Equals(entry));
                if (indexOfUi >= 0)
                    PanList.Children.RemoveAt(indexOfUi);
            }

            RefreshBars();
            if (!isSuccessful)
            {
                HintService.Hint(Lang.Text("Instance.Resource.Delete.Failed"), HintType.Error);
                ReloadCompFileList(true);
            }
            else if (PanList.Children.Count == 0)
            {
                ReloadCompFileList(true); // 删除了全部项目
            }
            else
            {
                RefreshBars();
            }

            // 显示结果提示
            if (!isSuccessful)
                return;
            if (isShiftPressed)
            {
                if (targets.Count == 1)
                    HintService.Hint(Lang.Text("Instance.Resource.Delete.PermanentSingle", targets[0].FileName), HintType.Success);
                else
                    HintService.Hint(Lang.Text("Instance.Resource.Delete.PermanentMultiple", targets.Count), HintType.Success);
            }
            else if (targets.Count == 1)
            {
                HintService.Hint(Lang.Text("Instance.Resource.Delete.RecycleSingle", targets[0].FileName), HintType.Success);
            }
            else
            {
                HintService.Hint(Lang.Text("Instance.Resource.Delete.RecycleMultiple", targets.Count), HintType.Success);
            }
        }
        catch (OperationCanceledException ex)
        {
            LogError(ex, "删除资源被主动取消");
            ReloadCompFileList(true);
        }
        catch (Exception ex)
        {
            LogFailure(ex, "删除资源出现未知错误", Lang.Text("Instance.Resource.Error.OperationFailed"));
            ReloadCompFileList(true);
        }

        LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
    }

    // 后台内嵌（Jar-in-Jar）解析未完成时，级联反查可能遗漏内嵌依赖，禁用/删除前提示确认
    private async Task<bool> ConfirmCascadeReadyAsync()
    {
        if (currentCompType != CompType.Mod || ModLocalComp.CompJijResolved) return true;
        var proceed = await MsgBox.ShowAsync(
            Lang.Text("Instance.Resource.Mod.JarInJar.ParsePending.Message"),
            Lang.Text("Instance.Resource.Mod.JarInJar.ParsePending.Title"),
            Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel"), isWarn: true);
        if (proceed != 1)
            return false;

        // “继续”表示先同步补齐整份列表，再重新构建级联索引；不能在残缺 output 上直接计算 affected
        var output = ModLocalComp.compResourceListLoader.output;
        if (output is null) return false;
        var completed = true;
        try
        {
            ModJarInJarCache.UseInstance(PageInstanceLeft.McInstance?.PathInstance);
            ModLocalComp.JijScanContext = new ModJarInJar.ScanContext();
            foreach (var mod in output.Where(m => !m.IsFolder && m.JijPending))
                completed &= mod.ResolveJijNow();
            if (completed)
            {
                ModJarInJarCache.Prune(output.Where(m => !m.IsFolder).Select(m => m.path));
                ModJarInJarCache.Flush();
            }
        }
        finally
        {
            ModLocalComp.JijScanContext = null;
            ModJarInJarCache.UseInstance(null);
        }

        ModLocalComp.CompJijResolved = completed;
        PageInstanceLeft.frmInstanceModJarInJar?.OnJijResolved();
        if (!completed)
            HintService.Hint(Lang.Text("Instance.Resource.Ed.ToggleFailed"), HintType.Error);
        return completed;
    }

    private List<LocalFile> JijFindAffected(List<LocalFile> targets)
    {
        if (currentCompType != CompType.Mod) return new List<LocalFile>();
        var output = ModLocalComp.compResourceListLoader.output;
        if (output is null) return new List<LocalFile>();
        var index = new ModJarInJarIndex(output, PageInstanceLeft.McInstance?.Info?.VanillaName);
        return index.FindAffected(targets);
    }

    // 级联选择弹窗；返回 0=仅此项 / 1=连带禁用 /（删除时）2=连带删除；null=取消
    private static async Task<int?> AskJijCascadeAsync(List<LocalFile> affected, bool isDelete)
    {
        var sels = new List<IMyRadio>
        {
            new MyRadioBox { Text = Lang.Text("Instance.Resource.Mod.JarInJar.Cascade.OnlySelf") },
            new MyRadioBox { Text = Lang.Text("Instance.Resource.Mod.JarInJar.Cascade.WithDisable") }
        };
        if (isDelete)
            sels.Add(new MyRadioBox { Text = Lang.Text("Instance.Resource.Mod.JarInJar.Cascade.WithDelete") });

        // MsgBox.SelectAsync 返回从 0 开始的选项序号，取消时为 null
        return await MsgBox.SelectAsync(sels, Lang.Text(isDelete
            ? "Instance.Resource.Mod.JarInJar.Cascade.DeleteTitle"
            : "Instance.Resource.Mod.JarInJar.Cascade.DisableTitle", affected.Count));
    }

    private bool EDModsCore(IEnumerable<LocalFile> modList, bool isEnable)
    {
        var isSuccessful = true;
        foreach (var modEntity in modList.ToList())
        {
            // 用户可能在 Phase 2 尚未完成时选择继续操作。重命名前先同步补齐该项，
            // 否则后台仍持有旧路径实体，而新实体会复制空树并永久丢失本次会话的 JIJ 状态
            if (currentCompType == CompType.Mod && modEntity.JijPending)
            {
                var resolved = false;
                try
                {
                    ModJarInJarCache.UseInstance(PageInstanceLeft.McInstance?.PathInstance);
                    ModLocalComp.JijScanContext = new ModJarInJar.ScanContext();
                    resolved = modEntity.ResolveJijNow();
                    if (resolved) ModJarInJarCache.Flush();
                }
                finally
                {
                    ModLocalComp.JijScanContext = null;
                    ModJarInJarCache.UseInstance(null);
                }

                if (!resolved)
                {
                    HintService.Hint(Lang.Text("Instance.Resource.Ed.ToggleFailed"), HintType.Error);
                    isSuccessful = false;
                    continue;
                }
            }

            string newPath;
            if (modEntity.State == LocalFileStatus.Fine && !isEnable)
                // 禁用
                newPath = modEntity.path + (File.Exists(modEntity.path + ".old") ? ".old" : ".disabled");
            else if (modEntity.State == LocalFileStatus.Disabled && isEnable)
                // 启用
                newPath = modEntity.RawPath;
            else
                continue;

            // 重命名
            try
            {
                if (File.Exists(newPath))
                {
                    if (File.Exists(modEntity.path))
                    {
                        // 同时存在两个名称的 Mod
                        if (ComputeMd5(modEntity.path) != ComputeMd5(newPath))
                        {
                            _ = MsgBox.ShowAsync(
                                Lang.Text("Instance.Resource.Ed.FileConflict.Message", newPath, modEntity.path),
                                Lang.Text("Instance.Resource.Ed.FileConflict"));
                            isSuccessful = false;
                            continue;
                        }
                    }
                    else
                    {
                        // 已经重命名过了
                        Logger.TryGet(LogEventLevel.Debug, LogArea.Control)?.Log(null, "[Mod] Mod 的状态已被切换");
                        continue;
                    }
                }

                File.Delete(newPath);
                File.Move(modEntity.path, newPath);
            }
            catch (FileNotFoundException ex)
            {
                LogFailure(ex, $"未找到需要重命名的 Mod（{modEntity.path ?? "null"}）",
                    Lang.Text("Instance.Resource.Error.OperationFailed"));
                ReloadCompFileList(true);
                return false;
            }
            catch (Exception ex)
            {
                LogError(ex, $"重命名 Mod 失败（{modEntity.path ?? "null"}）");
                isSuccessful = false;
                continue;
            }

            // 更改 Loader 中的列表
            var newModEntity = new LocalFile(newPath);
            newModEntity.FromJson(modEntity.ToJson());
            newModEntity.CopyLoadedStateFrom(modEntity); // 仅改名未改内容，复用已解析元数据

            var loaderOutput = ModLocalComp.compResourceListLoader.output;
            if (loaderOutput.Contains(modEntity))
            {
                var indexOfLoader = loaderOutput.IndexOf(modEntity);
                loaderOutput.RemoveAt(indexOfLoader);
                loaderOutput.Insert(indexOfLoader, newModEntity);
            }

            if (searchResult is not null && searchResult.Contains(modEntity)) // #4862
            {
                var indexOfResult = searchResult.IndexOf(modEntity);
                searchResult.Remove(modEntity);
                searchResult.Insert(indexOfResult, newModEntity);
            }

            // 更改 UI 中的列表
            try
            {
                var newItem = BuildLocalCompItem(newModEntity);
                modItems[modEntity.RawPath] = newItem;
                var uiItems = PanList.Children.OfType<MyListItem>().ToList();
                var indexOfUi = uiItems.FindIndex(i => ReferenceEquals(i.Tag, modEntity));
                if (indexOfUi == -1)
                    continue; // 因为未知原因 Mod 的状态已经切换完了
                PanList.Children.RemoveAt(indexOfUi);
                PanList.Children.Insert(indexOfUi, newItem);
            }
            catch (Exception ex)
            {
                LogFailure(ex, $"更新 UI 列表项失败：{modEntity.FileName}", Lang.Text("Instance.Resource.Error.OperationFailed"));
            }
        }

        if (isSuccessful)
        {
            RefreshBars();
        }
        else
        {
            HintService.Hint(Lang.Text("Instance.Resource.Ed.ToggleFailed"), HintType.Error);
            ReloadCompFileList(true);
        }

        LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
        return isSuccessful;
    }

    /// <summary>
    ///     记录正在进行资源更新的 mods 文件夹路径。
    /// </summary>
    public static List<string> updatingVersions = new();

    // 更新
    private void BtnSelectUpdate_Click()
    {
        var updateList = AllEntries().Where(m => selectedMods.Contains(m.RawPath) && m.CanUpdate).ToList();
        if (updateList.Count == 0)
            return;
        _ = UpdateResourceAsync(updateList);
        ChangeAllSelected(false);
    }

    /// <summary>
    ///     下载并替换为最新版本的资源文件。
    /// </summary>
    public async Task UpdateResourceAsync(IEnumerable<LocalFile> modList)
    {
        var list = modList.ToList();

        // 更新前警告
        if (currentCompType == CompType.Mod && (!updateModConfirmed || list.Count >= 15))
        {
            var confirm = await MsgBox.ShowAsync(
                Lang.Text("Instance.Resource.Update.Warning.Message"),
                Lang.Text("Instance.Resource.Update.Warning.Title"),
                Lang.Text("Instance.Resource.Update.Warning.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true);
            if (confirm == 1)
                updateModConfirmed = true;
            else
                return;
        }

        try
        {
            // 构造下载信息
            var downloadItems = new List<DownloadItem>();
            var fileCopyList = new Dictionary<string, string>();
            foreach (var entry in list)
            {
                var file = entry.UpdateFile;
                if (file is null || !file.Available)
                    continue;

                // 确认更新后的文件名
                var currentReplaceName = (entry.compFile?.FileName ?? entry.FileName).Replace(".jar", "").Replace(".old", "").Replace(".disabled", "");
                var newestReplaceName = entry.UpdateFile.FileName.Replace(".jar", "").Replace(".old", "").Replace(".disabled", "");
                var currentSegs = currentReplaceName.Split('-').ToList();
                var newestSegs = newestReplaceName.Split('-').ToList();
                var shortened = false;

                // 移除前导相同部分（不能移除所有相同项，见 WPF 版注释）
                while (currentSegs.Count > 0 && newestSegs.Count > 0 && currentSegs[0] == newestSegs[0])
                {
                    currentSegs.RemoveAt(0);
                    newestSegs.RemoveAt(0);
                    shortened = true;
                }

                // 移除后导相同部分
                while (currentSegs.Count > 0 && newestSegs.Count > 0 &&
                       currentSegs[^1] == newestSegs[^1])
                {
                    currentSegs.RemoveAt(currentSegs.Count - 1);
                    newestSegs.RemoveAt(newestSegs.Count - 1);
                    shortened = true;
                }

                if (shortened && currentSegs.Count > 0 && newestSegs.Count > 0)
                {
                    currentReplaceName = string.Join("-", currentSegs);
                    newestReplaceName = string.Join("-", newestSegs);
                }

                // 添加到下载列表
                var newFileName = entry.FileName.Replace(currentReplaceName, newestReplaceName);
                var tempAddress = Path.Combine(GetTempCompDirectory(), newFileName);
                var realAddress = Path.Combine(Path.GetDirectoryName(entry.path) ?? "", newFileName);
                downloadItems.Add(file.ToDownloadItem(tempAddress, DownloadReason.Update));
                fileCopyList[tempAddress] = realAddress;
            }

            var pathMods = GetCompRootPath(currentCompType);
            updatingVersions.Add(pathMods);
            try
            {
                var result = downloadItems.Count == 0
                    ? null
                    : await App.Services.Engine.DownloadAsync(downloadItems, null, CancellationToken.None);
                var finishedFileNames = new List<string>();

                if (result is not null && result.Failed.Count > 0)
                {
                    HintService.Hint(Lang.Text("Instance.Resource.Update.Failed", string.Join("\n",
                        result.Errors.Values.SelectMany(e => e).Select(e => e.ToString()))), HintType.Error);
                }
                else
                {
                    // 替换旧文件：旧文件与新文件都移入回收站，然后把临时文件移到目标位置
                    foreach (var entry in list)
                        if (File.Exists(entry.path))
                            Trash.MoveToTrash(entry.path);
                        else
                            Logger.TryGet(LogEventLevel.Debug, LogArea.Control)?.Log(null,
                                $"[CompUpdate] 未找到更新前的资源文件，跳过对它的删除：{entry.path}");

                    foreach (var (tempFile, realFile) in fileCopyList)
                    {
                        if (!File.Exists(tempFile))
                            continue;
                        if (File.Exists(realFile))
                            Trash.MoveToTrash(realFile);
                        if (Directory.Exists(Path.GetDirectoryName(realFile) ?? ""))
                        {
                            File.Move(tempFile, realFile);
                            finishedFileNames.Add(Path.GetFileName(realFile));
                        }
                    }

                    switch (finishedFileNames.Count)
                    {
                        case 0:
                            Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(null, "[CompUpdate] 没有资源被成功更新");
                            break;
                        case 1:
                            HintService.Hint(Lang.Text("Instance.Resource.Update.SuccessSingle", finishedFileNames[0]), HintType.Success);
                            break;
                        default:
                            HintService.Hint(Lang.Text("Instance.Resource.Update.SuccessMultiple", finishedFileNames.Count), HintType.Success);
                            break;
                    }
                }

                // 清理缓存
                foreach (var tempFile in fileCopyList.Keys.Where(File.Exists))
                    File.Delete(tempFile);
            }
            finally
            {
                updatingVersions.Remove(pathMods);
            }

            ReloadCompFileList(true);
        }
        catch (Exception ex)
        {
            LogError(ex, "初始化资源更新失败");
        }
    }

    private static string GetTempCompDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "PCL", "DownloadedComp");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private void BtnSelectFavorites_Click(object? sender)
    {
        var selected = AllEntries()
            .Where(m => selectedMods.Contains(m.RawPath) && m.Comp is not null)
            .Select(i => i.Comp!)
            .ToList();
        if (sender is not Control anchor || selected.Count == 0)
            return;

        var menu = new ContextMenu();
        foreach (var folder in CompFavoriteStore.Folders)
        {
            var item = new MyMenuItem { MaxWidth = 240d, Header = folder.Name };
            item.Click += (_, _) =>
            {
                foreach (var project in selected)
                    folder.Favs.Add(project.Id);
                CompFavoriteStore.Save();
                HintService.Hint(Lang.Text("Download.Comp.Detail.Favorites.Add", selected.Count, folder.Name),
                    HintType.Success);
            };
            menu.Items.Add(item);
        }

        menu.Open(anchor);
    }

    private void BtnSelectShare_Click()
    {
        var shareList = AllEntries()
            .Where(m => selectedMods.Contains(m.RawPath) && m.Comp is not null)
            .Select(i => i.Comp!.Id)
            .ToHashSet();
        // TODO(shion): 分享码的编码逻辑仍在 WPF 版 ModComp.CompFavorites 中，等待 Compat 层提供。
        var shareCode = ModComp.CompFavorites.GetShareCode(shareList);
        SetClipboardText(shareCode);
        ChangeAllSelected(false);
    }

    private void SetClipboardText(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            _ = clipboard.SetTextAsync(text);
    }

    #endregion

    #region 单个资源项

    /// <summary>
    ///     显示资源详情。
    /// </summary>
    public void Info_Click(LocalFile modEntry)
    {
        _ = InfoAsync(modEntry);
    }

    private async Task InfoAsync(LocalFile modEntry)
    {
        try
        {
            var instance = PageInstanceLeft.McInstance!;
            // 判断该 LabyMod 是否支持安装 Fabric Mod
            var moddedLabyMod = instance.Info.HasLabyMod && instance.Modable;

            // 加载失败信息
            if (modEntry.State == LocalFileStatus.Unavailable)
            {
                _ = MsgBox.ShowAsync(
                    Lang.Text("Instance.Resource.Item.Info.FailedMessage.WithDetail", modEntry.FileUnavailableReason.ToString()),
                    Lang.Text("Instance.Resource.Item.Info.FailedTitle"));
                return;
            }

            if (modEntry.Comp is not null)
            {
                // 跳转到工程详情页面
                var loader = instance.Info.HasForge ? CompLoaderType.Forge :
                    instance.Info.HasNeoForge ? CompLoaderType.NeoForge :
                    instance.Info.HasFabric || moddedLabyMod ? CompLoaderType.Fabric :
                    CompLoaderType.Any;
                PageDownloadCompDetail.Open(new CompDetailArgs(modEntry.Comp, new List<string>(),
                    instance.Info.VanillaName, loader, currentCompType));
                return;
            }

            // 原理图文件使用异步加载，避免 UI 卡顿
            if (modEntry.path.EndsWith(".litematic", StringComparison.OrdinalIgnoreCase) ||
                modEntry.path.EndsWith(".schem", StringComparison.OrdinalIgnoreCase) ||
                modEntry.path.EndsWith(".schematic", StringComparison.OrdinalIgnoreCase) ||
                modEntry.path.EndsWith(".nbt", StringComparison.OrdinalIgnoreCase))
            {
                await ShowSchematicInfoAsync(modEntry);
                return;
            }

            // 获取信息
            var contentLines = new List<string>();

            // 检查是否为文件夹
            if (modEntry.IsFolder)
            {
                var folderPath = modEntry.ActualPath;
                if (Directory.Exists(folderPath))
                {
                    int fileCount;
                    try
                    {
                        fileCount = currentCompType switch
                        {
                            CompType.Schematic => new DirectoryInfo(folderPath)
                                .EnumerateFiles("*", SearchOption.AllDirectories)
                                .Count(f => LocalFile.IsCompFile(f.FullName, CompType.Schematic)),
                            CompType.Mod => new DirectoryInfo(folderPath).EnumerateFiles("*.jar", SearchOption.AllDirectories).Count(),
                            CompType.ResourcePack => new DirectoryInfo(folderPath).EnumerateFiles("*.zip", SearchOption.AllDirectories).Count(),
                            CompType.Shader => new DirectoryInfo(folderPath).EnumerateFiles("*.zip", SearchOption.AllDirectories).Count(),
                            _ => new DirectoryInfo(folderPath).EnumerateFiles("*", SearchOption.AllDirectories).Count()
                        };
                    }
                    catch (Exception)
                    {
                        fileCount = 0;
                    }

                    if (fileCount == 0)
                        contentLines.Add(Lang.Text("Instance.Resource.Item.Info.EmptyFolder") + "\r\n");
                    else if (fileCount == 1)
                        contentLines.Add(Lang.Text("Instance.Resource.Item.Info.ContainsOne") + "\r\n");
                    else
                        contentLines.Add(Lang.Text("Instance.Resource.Item.Info.ContainsMany", fileCount) + "\r\n");
                }
                else
                {
                    contentLines.Add(Lang.Text("Instance.Resource.Item.Info.FolderNotFound") + "\r\n");
                }

                contentLines.Add(Lang.Text("Instance.Resource.Item.Info.Path", folderPath));

                // 文件夹只显示基本信息，不提供搜索功能
                _ = MsgBox.ShowAsync(string.Join("\r\n", contentLines), modEntry.Name,
                    Lang.Text("Instance.Resource.Item.Info.Return"));
                return;
            }

            // 处理普通文件详情
            if (modEntry.Description is not null)
                contentLines.Add(modEntry.Description + "\r\n");
            if (modEntry.Authors is not null)
                contentLines.Add(Lang.Text("Instance.Resource.Item.Info.Author", modEntry.Authors));
            contentLines.Add(Lang.Text("Instance.Resource.Item.Info.File", modEntry.FileName,
                Lang.FileSize(GetModFileInfo(modEntry.path).Length)));
            if (modEntry.Version is not null)
                contentLines.Add(Lang.Text("Instance.Resource.Item.Info.Version", modEntry.Version));

            // 只有普通文件才显示调试信息
            AppendDebugInfo(contentLines, modEntry);

            // 获取用于搜索的 Mod 名称
            var modSearchName = BuildMcModSearchName(modEntry.Name ?? modEntry.FileName);
            var mcModUrl = "https://www.mcmod.cn/s?key=" + modSearchName + "&site=all&filter=0";
            var content = string.Join("\r\n", contentLines);

            // 投影原理图文件或非中文区域不显示百科搜索选项
            if (currentCompType == CompType.Schematic || !Lang.IsChineseMainland)
            {
                if (modEntry.Url is null)
                {
                    _ = MsgBox.ShowAsync(content, modEntry.Name, Lang.Text("Instance.Resource.Item.Info.Return"));
                }
                else
                {
                    var choice = await MsgBox.ShowAsync(content, modEntry.Name,
                        Lang.Text("Instance.Resource.Item.Info.OpenWebsite"), Lang.Text("Instance.Resource.Item.Info.Return"));
                    if (choice == 1)
                        SystemShell.OpenUrl(modEntry.Url);
                }

                return;
            }

            // 其他资源类型保留百科搜索功能
            if (modEntry.Url is null)
            {
                var choice = await MsgBox.ShowAsync(content, modEntry.Name,
                    Lang.Text("Instance.Resource.Item.Info.McMod"), Lang.Text("Instance.Resource.Item.Info.Return"));
                if (choice == 1)
                    SystemShell.OpenUrl(mcModUrl);
            }
            else
            {
                var choice = await MsgBox.ShowAsync(content, modEntry.Name,
                    Lang.Text("Instance.Resource.Item.Info.OpenWebsite"), Lang.Text("Instance.Resource.Item.Info.McMod"),
                    Lang.Text("Instance.Resource.Item.Info.Return"));
                switch (choice)
                {
                    case 1:
                        SystemShell.OpenUrl(modEntry.Url);
                        break;
                    case 2:
                        SystemShell.OpenUrl(mcModUrl);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            LogFailure(ex, "获取资源详情失败", Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     把 Mod 名称拆成适合百科搜索的关键字（大小写边界处插入 +）。
    /// </summary>
    private static string BuildMcModSearchName(string name)
    {
        var original = name.Replace(" ", "+");
        if (original.Length == 0)
            return "";
        var searchName = original[..1];
        for (var i = 1; i < original.Length; i++)
        {
            var isLastLower = original[i - 1].ToString().ToLowerInvariant() == original[i - 1].ToString();
            var isCurrentLower = original[i].ToString().ToLowerInvariant() == original[i].ToString();
            if (isLastLower && !isCurrentLower)
                searchName += "+";
            searchName += original[i];
        }

        return searchName.Replace("++", "+").Replace("pti+Fine", "ptiFine");
    }

    private static void AppendDebugInfo(List<string> contentLines, LocalFile modEntry)
    {
        var debugInfo = new List<string>();
        if (modEntry.ModId is not null)
            debugInfo.Add(Lang.Text("Instance.Resource.Item.Info.ModId", modEntry.ModId));
        if (modEntry.Dependencies.Count > 0)
        {
            debugInfo.Add(Lang.Text("Instance.Resource.Item.Info.Dependency"));
            foreach (var dep in modEntry.Dependencies)
                debugInfo.Add(" - " + (dep.VersionRange is null
                    ? dep.Id
                    : Lang.Text("Instance.Resource.Item.Info.DependencyVersion", dep.Id, dep.VersionRange)));
        }

        if (debugInfo.Count > 0)
        {
            contentLines.Add("");
            contentLines.AddRange(debugInfo);
        }
    }

    // 打开文件所在的位置
    public void Open_Click(LocalFile entry)
    {
        try
        {
            // 对于文件夹使用实际路径，对于文件使用原路径
            var targetPath = entry.IsFolder ? entry.ActualPath : entry.path;
            SystemShell.OpenFolder(targetPath);
        }
        catch (Exception ex)
        {
            LogFailure(ex, "打开资源文件位置失败", Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     异步显示原理图详情信息，避免 UI 卡顿。
    /// </summary>
    private async Task ShowSchematicInfoAsync(LocalFile modEntry)
    {
        // 显示加载提示
        HintService.Hint(Lang.Text("Instance.Resource.Item.Info.LoadingDetail"));

        try
        {
            await Task.Run(() => modEntry.LoadNbtDataIfNeeded());

            var contentLines = new List<string>();
            if (modEntry.Description is not null) contentLines.Add(modEntry.Description + "\r\n");
            if (modEntry.Authors is not null) contentLines.Add(Lang.Text("Instance.Resource.Item.Info.Author", modEntry.Authors));
            contentLines.Add(Lang.Text("Instance.Resource.Item.Info.File", modEntry.FileName,
                Lang.FileSize(GetModFileInfo(modEntry.path).Length)));
            if (modEntry.Version is not null) contentLines.Add(Lang.Text("Instance.Resource.Item.Info.Version", modEntry.Version));

            if (modEntry.path.EndsWith(".litematic", StringComparison.OrdinalIgnoreCase))
                ShowLitematicDetails(contentLines, modEntry);
            else if (modEntry.path.EndsWith(".schem", StringComparison.OrdinalIgnoreCase))
                ShowSchemDetails(contentLines, modEntry);
            else if (modEntry.path.EndsWith(".schematic", StringComparison.OrdinalIgnoreCase))
                ShowSchematicDetails(contentLines, modEntry);
            else if (modEntry.path.EndsWith(".nbt", StringComparison.OrdinalIgnoreCase))
                ShowNbtDetails(contentLines, modEntry);
            AppendDebugInfo(contentLines, modEntry);

            // 投影原理图文件不显示百科搜索选项
            var content = string.Join("\r\n", contentLines);
            if (modEntry.Url is null)
            {
                _ = MsgBox.ShowAsync(content, modEntry.Name, Lang.Text("Instance.Resource.Item.Info.Return"));
            }
            else
            {
                var choice = await MsgBox.ShowAsync(content, modEntry.Name,
                    Lang.Text("Instance.Resource.Item.Info.OpenWebsite"), Lang.Text("Instance.Resource.Item.Info.Return"));
                if (choice == 1)
                    SystemShell.OpenUrl(modEntry.Url);
            }
        }
        catch (Exception ex)
        {
            LogFailure(ex, "加载原理图 NBT 数据失败", Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    #region 原理图文件详细信息显示

    /// <summary>
    ///     显示 Litematic 文件的详细信息。
    /// </summary>
    private static void ShowLitematicDetails(List<string> contentLines, LocalFile modEntry)
    {
        contentLines.Add("");
        contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.DetailInfo"));

        if (modEntry.LitematicOriginalName is not null)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.OriginalName") + modEntry.LitematicOriginalName);
        if (modEntry.LitematicVersion.HasValue)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.Version") + modEntry.LitematicVersion.Value);
        if (modEntry.LitematicEnclosingSize is not null)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.EnclosingSize") + modEntry.LitematicEnclosingSize);
        AddBlockAndVolumeStats(contentLines, modEntry);
        if (modEntry.LitematicRegionCount.HasValue)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.RegionCount") + modEntry.LitematicRegionCount.Value);

        if (modEntry.LitematicTimeCreated.HasValue)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.CreatedTime") +
                             FormatUnixMilliseconds(modEntry.LitematicTimeCreated.Value));
        if (modEntry.LitematicTimeModified.HasValue)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.ModifiedTime") +
                             FormatUnixMilliseconds(modEntry.LitematicTimeModified.Value));
    }

    /// <summary>
    ///     显示 Schem 文件的详细信息。
    /// </summary>
    private static void ShowSchemDetails(List<string> contentLines, LocalFile modEntry)
    {
        contentLines.Add("");
        contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.DetailInfo"));

        if (modEntry.SchemOriginalName is not null)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.OriginalName") + modEntry.SchemOriginalName);
        if (modEntry.StructureGameVersion is not null)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.GameVersion") + modEntry.StructureGameVersion);
        if (modEntry.SpongeVersion.HasValue)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.SpongeVersion") + modEntry.SpongeVersion.Value);
        if (modEntry.StructureDataVersion.HasValue)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.DataVersion") + modEntry.StructureDataVersion.Value);
        if (modEntry.LitematicEnclosingSize is not null)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.EnclosingDimensions") + modEntry.LitematicEnclosingSize);
        AddBlockAndVolumeStats(contentLines, modEntry);
        if (modEntry.LitematicRegionCount.HasValue)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.RegionCount") + modEntry.LitematicRegionCount.Value);

        contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.FileType",
            Lang.Text("Instance.Resource.Item.Schematic.FileType.Sponge")));
    }

    /// <summary>
    ///     显示 Schematic 文件的详细信息。
    /// </summary>
    private static void ShowSchematicDetails(List<string> contentLines, LocalFile modEntry)
    {
        contentLines.Add("");
        contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.DetailInfo"));

        if (modEntry.LitematicEnclosingSize is not null)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.Size") + modEntry.LitematicEnclosingSize);
        AddBlockAndVolumeStats(contentLines, modEntry);

        contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.FileType",
            Lang.Text("Instance.Resource.Item.Schematic.FileType.Mcedit")));
    }

    /// <summary>
    ///     显示 NBT 结构文件的详细信息。
    /// </summary>
    private static void ShowNbtDetails(List<string> contentLines, LocalFile modEntry)
    {
        contentLines.Add("");
        contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.DetailInfo"));

        if (modEntry.StructureAuthor is not null)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Info.Author", modEntry.StructureAuthor));
        if (modEntry.StructureGameVersion is not null)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.GameVersion") + modEntry.StructureGameVersion);
        if (modEntry.StructureDataVersion.HasValue)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.DataVersion") + modEntry.StructureDataVersion.Value);
        if (modEntry.LitematicEnclosingSize is not null)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.EnclosingDimensions") + modEntry.LitematicEnclosingSize);
        AddBlockAndVolumeStats(contentLines, modEntry);
        if (modEntry.LitematicRegionCount.HasValue)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.RegionCount") + modEntry.LitematicRegionCount.Value);

        contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.FileType",
            Lang.Text("Instance.Resource.Item.Schematic.FileType.Nbt")));
    }

    private static void AddBlockAndVolumeStats(List<string> contentLines, LocalFile modEntry)
    {
        if (modEntry.LitematicTotalBlocks.HasValue)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.TotalBlocks") +
                             Lang.Number(modEntry.LitematicTotalBlocks.Value, "N0"));
        if (modEntry.LitematicTotalVolume.HasValue)
            contentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.TotalVolume") +
                             Lang.Number(modEntry.LitematicTotalVolume.Value, "N0"));
    }

    private static string FormatUnixMilliseconds(long milliseconds)
    {
        try
        {
            var time = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToLocalTime().DateTime;
            return Lang.Date(time, "G");
        }
        catch (Exception)
        {
            return milliseconds.ToString();
        }
    }

    #endregion

    #endregion

    #region 搜索

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchBox.Text);
    private List<LocalFile>? searchResult;
    private CancellationTokenSource? _cancelToken;

    private async void SearchRun(object? sender, EventArgs e)
    {
        var curToken = new CancellationTokenSource();
        var oldToken = Interlocked.Exchange(ref _cancelToken, curToken);
        oldToken?.Cancel();
        oldToken?.Dispose();

        try
        {
            await Task.Delay(350, curToken.Token);
            if (IsSearching)
            {
                var searchText = SearchBox.Text;
                var result = await Task.Run(() => GetSearchResult(searchText), curToken.Token);
                if (curToken.IsCancellationRequested) return;
                searchResult = result;
            }

            if (curToken.IsCancellationRequested) return;
            RefreshUI();
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogError(ex, "搜索过程中发生异常");
        }
    }

    private List<LocalFile> GetSearchResult(string query)
    {
        // 构造请求
        var queryList = new List<CompSearchEntry<LocalFile>>();
        foreach (var entry in AllEntries())
        {
            var searchSource = new List<CompSearchSource> { new(entry.Name ?? "", 1d), new(entry.FileName, 1d) };
            if (entry.Version is not null)
                searchSource.Add(new CompSearchSource(entry.Version, 0.2d));
            if (!string.IsNullOrEmpty(entry.Description))
                searchSource.Add(new CompSearchSource(entry.Description, 0.4d));
            if (entry.Comp is not null)
            {
                if ((entry.Comp.RawName ?? "") != (entry.Name ?? ""))
                    searchSource.Add(new CompSearchSource(entry.Comp.RawName, 1d));
                if ((entry.Comp.TranslatedName ?? "") != (entry.Comp.RawName ?? ""))
                    searchSource.Add(new CompSearchSource(entry.Comp.TranslatedName, 1d));
                if ((entry.Comp.Description ?? "") != (entry.Description ?? ""))
                    searchSource.Add(new CompSearchSource(entry.Comp.Description, 0.4d));
                searchSource.Add(new CompSearchSource(string.Join("", entry.Comp.TagKeys), 0.2d));
            }

            queryList.Add(new CompSearchEntry<LocalFile>(entry, searchSource));
        }

        // 进行搜索
        return CompSimilarity.Search(queryList, query, 100, 0.35d).Select(r => r.Item).ToList();
    }

    #endregion

    #region 辅助

    private static string? CleanPlaceholder(string? version) =>
        string.IsNullOrEmpty(version) || version.Contains("${") ? null : version;

    private static string DisplayName(LocalFile m)
    {
        if (m.Comp is not null)
        {
            var title = CompDisplay.GetControlTitle(m.Comp, false);
            return title.Key + title.Value;
        }

        return string.IsNullOrWhiteSpace(m.Name) ? m.FileName : m.Name;
    }

    private static string ComputeMd5(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(MD5.HashData(stream));
    }

    private static void LogError(Exception ex, string message)
    {
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);
    }

    private static void LogFailure(Exception ex, string message, string hint)
    {
        LogError(ex, message);
        HintService.Hint(hint, HintType.Error);
    }

    #endregion
}
