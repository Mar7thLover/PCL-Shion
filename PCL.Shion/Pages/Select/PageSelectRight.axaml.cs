using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Select;

public partial class PageSelectRight : MyPageRight
{
    private const int normalDelay = 75; // 正常输入延迟0.075秒
    private const int quickDelay = 50; // 清空搜索框延迟0.05秒
    private bool isRefreshing;

    private DateTime lastInputTime = DateTime.MinValue;
    private DispatcherTimer? reloadTimer;

    // 窗口属性
    /// <summary>
    ///     是否显示隐藏的 Minecraft 实例。
    /// </summary>
    public bool showHidden = false;

    public PageSelectRight()
    {
        InitializeComponent();
        PanScroll = PanBack;
        PanVerSearchBox.HintText = Lang.Text("Select.Instance.Search.Hint");
        PanVerSearchBox.TextChanged += PanVerSearchBox_TextChanged;
        Loaded += PageSelectRight_Loaded;
        Unloaded += PageSelectRight_Unloaded;
        LoaderInit();
    }

    // 窗口基础
    private void PageSelectRight_Loaded(object? sender, EventArgs e)
    {
        App.Services.InstancesChanged -= OnInstancesChanged;
        App.Services.InstancesChanged += OnInstancesChanged;
        SelectInstances.Reload(false);
        PanBack.ScrollToHome();
    }

    private void OnInstancesChanged()
    {
        Dispatcher.UIThread.Post(() => SelectInstances.Reload(true));
    }

    private void PanVerSearchBox_TextChanged(object? sender, EventArgs e)
    {
        // 记录最后一次输入时间
        lastInputTime = DateTime.Now;

        isRefreshing = false;

        reloadTimer ??= CreateReloadTimer();

        // 动态调整延迟时间
        if (string.IsNullOrWhiteSpace(PanVerSearchBox.Text))
        {
            if (reloadTimer.Interval.TotalMilliseconds != quickDelay)
                reloadTimer.Interval = TimeSpan.FromMilliseconds(quickDelay);
        }
        else if (reloadTimer.Interval.TotalMilliseconds != normalDelay)
        {
            reloadTimer.Interval = TimeSpan.FromMilliseconds(normalDelay);
        }

        if (!reloadTimer.IsEnabled)
            reloadTimer.Start();
    }

    private DispatcherTimer CreateReloadTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(normalDelay) };
        timer.Tick += ReloadTimer_Tick;
        return timer;
    }

    private void ReloadTimer_Tick(object? sender, EventArgs e)
    {
        if (sender is not DispatcherTimer timer)
            return;

        // 检查是否超过当前设定的延迟时间没有新输入
        var elapsed = (DateTime.Now - lastInputTime).TotalMilliseconds;
        var currentDelay = timer.Interval.TotalMilliseconds;

        if (elapsed >= currentDelay && SelectInstances.Loader.State == LoadState.Finished && !isRefreshing)
        {
            isRefreshing = true;

            // 确保在UI线程执行刷新
            Dispatcher.UIThread.Post(() =>
            {
                McInstanceListUI(SelectInstances.Loader);
                isRefreshing = false;
            });
            timer.Stop();
        }
    }

    private void PageSelectRight_Unloaded(object? sender, EventArgs e)
    {
        App.Services.InstancesChanged -= OnInstancesChanged;

        // 清理计时器
        if (reloadTimer is not null)
        {
            reloadTimer.Stop();
            reloadTimer.Tick -= ReloadTimer_Tick;
            reloadTimer = null;
        }
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanAllBack, null, SelectInstances.Loader,
            a => McInstanceListUI((ModLoader.LoaderTask<string, int>)a),
            autoRun: false);
        Load.Click += (_, _) => Load_Click();
        BtnEmptyDownload.Click += (_, _) => BtnDownload_Click();
    }

    private void Load_Click()
    {
        if (SelectInstances.Loader.State == LoadState.Failed)
            SelectInstances.Reload(true);
    }

    #region 结果 UI 化

    private void McInstanceListUI(ModLoader.LoaderTask<string, int> loader)
    {
        try
        {
            // 加载 UI
            PanMain.Children.Clear();

            var hasVisibleFolders = false;
            var searchText = PanVerSearchBox.Text.Trim().ToLower(); // 获取搜索框文本
            var hasAnyResults = false;
            var originalHasInstances = SelectInstances.Cards.Any(card => card.Items.Count > 0);

            // 搜索无结果时显示 PanEmptySearch
            PanEmptySearch.IsVisible = false; // 默认隐藏

            foreach (var card in SelectInstances.Cards)
            {
                if ((card.Kind == SelectCardKind.Hidden) ^ showHidden)
                    continue;
                var filteredInstances = card.Items.Where(entry =>
                {
                    if (string.IsNullOrEmpty(searchText))
                        return true;
                    return entry.Name.ToLower().Contains(searchText) ||
                           entry.Desc.ToLower().Contains(searchText) ||
                           entry.Desc.Replace(",", "").ToLower().Trim().Contains(searchText);
                }).ToList();
                if (filteredInstances.Count == 0)
                    continue;

                hasVisibleFolders = true;
                hasAnyResults = true;

                #region 确认卡片名称

                var cardName = card.Kind switch
                {
                    SelectCardKind.OriginalLike => Lang.Text("Select.Instance.Card.Regular"),
                    SelectCardKind.Api => ApiCardName(card.Items),
                    SelectCardKind.Error => Lang.Text("Select.Instance.Card.Error"),
                    SelectCardKind.Hidden => Lang.Text("Select.Instance.Card.Hidden"),
                    SelectCardKind.Star => Lang.Text("Select.Instance.Card.Favorites"),
                    _ => throw new ArgumentException($"未知的卡片种类（{(int)card.Kind}）")
                };

                #endregion

                // 建立控件
                var cardTitle =
                    $"{cardName}{(card.Kind == SelectCardKind.Star ? "" : $" ({Lang.Number(filteredInstances.Count, "N0")})")}";
                var newCard = new MyCard { Title = cardTitle, Margin = new Thickness(0d, 0d, 0d, 15d) };
                var newStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top,
                    RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = filteredInstances
                };
                newCard.Children.Add(newStack);
                newCard.SwapControl = newStack;
                PanMain.Children.Add(newCard);

                // 确定卡片是否展开
                void PutMethod(StackPanel stack)
                {
                    foreach (var item in (IEnumerable<SelectInstanceEntry>)stack.Tag!)
                        stack.Children.Add(McVersionListItem(item));
                }

                if (card.Kind == SelectCardKind.Error)
                {
                    newCard.IsSwapped = true;
                    newCard.InstallMethod = PutMethod;
                }
                else
                {
                    MyCard.StackInstall(ref newStack, PutMethod);
                }
            }

            // 若只有一个卡片，则强制展开
            if (PanMain.Children.Count == 1 && ((MyCard)PanMain.Children[0]!).IsSwapped)
                ((MyCard)PanMain.Children[0]!).IsSwapped = false;

            PanVerSearchBox.IsVisible = hasVisibleFolders;

            // 判断应该显示哪一个页面
            if (!hasAnyResults)
            {
                if (!originalHasInstances)
                {
                    // 完全没有实例的情况
                    PanEmpty.IsVisible = true;
                    PanBack.IsVisible = false;
                    if (showHidden)
                    {
                        LabEmptyTitle.Text = Lang.Text("Select.Instance.Hidden.EmptyTitle");
                        LabEmptyContent.Text = Lang.Text("Select.Instance.Hidden.EmptyMessage");
                        BtnEmptyDownload.IsVisible = false;
                    }
                    else
                    {
                        LabEmptyTitle.Text = Lang.Text("Select.Instance.Empty.Title");
                        LabEmptyContent.Text = Lang.Text("Select.Instance.Empty.Message");
                        BtnEmptyDownload.IsVisible = true;
                    }
                }
                // 有实例但搜索无结果的情况
                else if (showHidden && SelectInstances.Cards.Any(card =>
                             card.Kind == SelectCardKind.Hidden && card.Items.Count > 0))
                {
                    // 有隐藏实例但搜索无结果 - 显示搜索无结果提示
                    PanVerSearchBox.IsVisible = true;
                    PanEmpty.IsVisible = false;
                    PanBack.IsVisible = true;
                    PanEmptySearch.IsVisible = true;
                    LabEmptySearchTitle.Text = Lang.Text("Select.Instance.Hidden.EmptySearchTitle");
                    LabEmptySearchContent.Text = string.IsNullOrWhiteSpace(searchText)
                        ? Lang.Text("Select.Instance.Search.EmptyInput")
                        : Lang.Text("Select.Instance.Search.NoHiddenResult", searchText);
                }
                else if (showHidden)
                {
                    // 无隐藏实例 - 显示"无隐藏实例"提示
                    PanEmpty.IsVisible = true;
                    PanBack.IsVisible = false;
                    LabEmptyTitle.Text = Lang.Text("Select.Instance.Hidden.EmptyTitle");
                    LabEmptyContent.Text = Lang.Text("Select.Instance.Hidden.EmptyMessage");
                    BtnEmptyDownload.IsVisible = false;
                    PanVerSearchBox.IsVisible = false;
                }
                else
                {
                    // 普通模式下的搜索无结果
                    PanVerSearchBox.IsVisible = true;
                    PanEmpty.IsVisible = false;
                    PanBack.IsVisible = true;
                    PanEmptySearch.IsVisible = true;
                    LabEmptySearchTitle.Text = Lang.Text("Select.Instance.EmptySearch.Title");
                    LabEmptySearchContent.Text = string.IsNullOrWhiteSpace(searchText)
                        ? Lang.Text("Select.Instance.Search.EmptyInput")
                        : Lang.Text("Select.Instance.Search.NoResult", searchText);
                }
            }
            else
            {
                PanBack.IsVisible = true;
                PanEmpty.IsVisible = false;
                PanEmptySearch.IsVisible = false;
            } // 有结果时隐藏
        }
        catch (Exception ex)
        {
            SelectLog.Feedback(Lang.Text("Select.Instance.Error.UiUpdate"), ex);
        }
    }

    private static string ApiCardName(IEnumerable<SelectInstanceEntry> entries)
    {
        var loaders = entries.Select(entry => entry.Instance.Loader?.ToLowerInvariant()).ToHashSet();
        if (loaders.Count(loader => loader is not null) > 1)
            return Lang.Text("Select.Instance.Card.Modable");
        if (loaders.Contains("forge"))
            return Lang.Text("Select.Instance.Card.Forge");
        if (loaders.Contains("neoforge"))
            return Lang.Text("Select.Instance.Card.NeoForge");
        if (loaders.Contains("quilt"))
            return Lang.Text("Select.Instance.Card.Quilt");
        return Lang.Text("Select.Instance.Card.Fabric");
    }

    internal static MyListItem McVersionListItem(SelectInstanceEntry entry)
    {
        var instance = entry.Instance;
        var newItem = new MyListItem
        {
            Title = instance.Name,
            Info = entry.Desc,
            Height = 42d,
            Tag = entry,
            Type = MyListItem.CheckType.Clickable
        };
        var tags = new List<string>();
        if (instance.MinecraftVersion is not null)
            tags.Add(instance.MinecraftVersion);
        if (instance.Loader is not null)
            tags.Add(FormatLoader(instance.Loader));
        newItem.Tags = tags;
        newItem.Logo = GetLogo(instance);

        newItem.ContentHandler = McVersionListContent;
        return newItem;
    }

    private static string FormatLoader(string loader) => loader.ToLowerInvariant() switch
    {
        "forge" => "Forge",
        "neoforge" => "NeoForge",
        "fabric" => "Fabric",
        "quilt" => "Quilt",
        _ => loader,
    };

    private static string GetLogo(GameInstance instance)
    {
        if (instance.Error is not null)
            return "pack://application:,,,/images/Blocks/RedstoneBlock.png";
        var customLogo = Path.Combine(instance.Directory, "PCL", "Logo.png");
        if (File.Exists(customLogo))
            return customLogo;
        var block = instance.Loader?.ToLowerInvariant() switch
        {
            "forge" => "Anvil.png",
            "neoforge" => "NeoForge.png",
            "fabric" => "Fabric.png",
            "quilt" => "Quilt.png",
            _ => "Grass.png",
        };
        return "pack://application:,,,/images/Blocks/" + block;
    }

    private static void McVersionListContent(MyListItem sender, EventArgs e)
    {
        var entry = (SelectInstanceEntry)sender.Tag!;
        var instance = entry.Instance;
        // 注册点击事件
        sender.Click += (_, _) => Item_Click(sender);
        // 图标按钮
        var btnStar = new MyIconButton();
        if (entry.IsStar)
        {
            SetIconTip(btnStar, Lang.Text("Select.Instance.Unfavorite"));
            btnStar.LogoScale = 1.1d;
            btnStar.SvgIcon = "lucide/heart-filled";
        }
        else
        {
            SetIconTip(btnStar, Lang.Text("Select.Instance.Favorite"));
            btnStar.LogoScale = 1.1d;
            btnStar.SvgIcon = "lucide/heart";
        }

        btnStar.Click += (_, _) =>
        {
            SelectInstances.SetStarred(instance.Directory, !entry.IsStar);
            SelectInstances.Reload(true);
        };
        var btnOpenFolder = new MyIconButton { LogoScale = 1.1d, SvgIcon = "lucide/folder-open" };
        SetIconTip(btnOpenFolder, Lang.Text("Select.Instance.OpenFolder"));
        btnOpenFolder.Click += (_, _) => SystemShell.OpenFolder(instance.Directory);
        var btnDel = new MyIconButton { LogoScale = 1.1d, SvgIcon = "lucide/trash-2" };
        SetIconTip(btnDel, Lang.Text("Common.Action.Delete"));
        btnDel.Click += (_, pointer) => DeleteVersion(entry, pointer.KeyModifiers.HasFlag(KeyModifiers.Shift));
        if (instance.Error is null)
        {
            var btnCont = new MyIconButton { LogoScale = 1.1d, SvgIcon = "lucide/settings" };
            SetIconTip(btnCont, Lang.Text("Select.Instance.Settings"));
            btnCont.Click += (_, _) => App.Services.RequestOpenInstance(instance.Name);
            sender.PointerReleased += (_, pointer) =>
            {
                if (pointer.InitialPressMouseButton == MouseButton.Right)
                    App.Services.RequestOpenInstance(instance.Name);
            };
            sender.Buttons = new[] { btnStar, btnOpenFolder, btnDel, btnCont };
        }
        else
        {
            var btnCont = new MyIconButton { LogoScale = 1.15d, SvgIcon = "lucide/folder-open" };
            SetIconTip(btnCont, Lang.Text("Common.Action.OpenFolder"));
            btnCont.Click += (_, _) => SystemShell.OpenFolder(instance.Directory);
            sender.PointerReleased += (_, pointer) =>
            {
                if (pointer.InitialPressMouseButton == MouseButton.Right)
                    SystemShell.OpenFolder(instance.Directory);
            };
            sender.Buttons = new[] { btnStar, btnOpenFolder, btnDel, btnCont };
        }
    }

    private static void SetIconTip(MyIconButton button, string tip)
    {
        ToolTip.SetTip(button, tip);
        ToolTip.SetPlacement(button, PlacementMode.Center);
        ToolTip.SetVerticalOffset(button, 30d);
        ToolTip.SetHorizontalOffset(button, 2d);
    }

    #endregion

    #region 页面事件

    // 点击选项
    internal static void Item_Click(MyListItem sender)
    {
        var entry = (SelectInstanceEntry)sender.Tag!;
        var instance = entry.Instance;
        if (instance.Error is null)
        {
            // 正常实例
            var settings = App.Services.Settings;
            settings.SelectedInstance = instance.Name;
            settings.Save();
            Navigator.Back();
        }
        else
        {
            // 错误实例
            SystemShell.OpenFolder(instance.Directory);
        }
    }

    private void BtnDownload_Click()
    {
        Navigator.Go(PageType.Download, PageSubType.DownloadInstall);
    }

    // 修改此代码时，同时修改 PageInstanceOverall 中的代码
    internal static async void DeleteVersion(SelectInstanceEntry entry, bool isShiftPressed)
    {
        var instance = entry.Instance;
        try
        {
            var isHintIndie = instance.Error is null && instance.IsIsolated;
            var confirmMsg = isShiftPressed
                ? Lang.Text("Select.Instance.Delete.ConfirmPermanentMessage", instance.Name)
                : Lang.Text("Select.Instance.Delete.ConfirmMessage", instance.Name);
            var confirmFullMsg = confirmMsg +
                                 (isHintIndie ? "\r\n" + Lang.Text("Select.Instance.Delete.IsolatedWarning") : "");
            var root = App.Services.Settings.MinecraftRoot;
            switch (await MsgBox.ShowAsync(confirmFullMsg, Lang.Text("Select.Instance.Delete.ConfirmTitle"),
                        button2: Lang.Text("Common.Action.Cancel"), isWarn: true))
            {
                case 1:
                {
                    if (isShiftPressed)
                    {
                        Directory.Delete(instance.Directory, true);
                        Hint.Show(Lang.Text("Select.Instance.Delete.PermanentSuccess", instance.Name),
                            HintType.Success);
                    }
                    else
                    {
                        InstanceManager.Delete(root, instance.Name);
                        Hint.Show(Lang.Text("Select.Instance.Delete.RecycleBinSuccess", instance.Name),
                            HintType.Success);
                    }

                    break;
                }
                case 2:
                {
                    return;
                }
            }

            // 删除当前实例就更改选择
            var settings = App.Services.Settings;
            SelectInstances.Forget(instance.Directory);
            if (settings.SelectedInstance == instance.Name)
            {
                settings.SelectedInstance = null;
                settings.Save();
            }

            SelectInstances.Reload(true);
        }
        catch (Exception ex)
        {
            SelectLog.Feedback(Lang.Text("Select.Instance.Error.Delete", instance.Name), ex);
        }
    }

    public void BtnEmptyDownload_Loaded()
    {
        var newVisible = !showHidden;
        if (BtnEmptyDownload.IsVisible != newVisible)
        {
            BtnEmptyDownload.IsVisible = newVisible;
            PanLoad.TriggerForceResize();
        }
    }

    #endregion
}
