using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Select;

public partial class PageSelectLeft : MyPageLeft, IRefreshable
{
    private static readonly DataFormat<string> FolderDataFormat =
        DataFormat.CreateStringApplicationFormat("PCL.Shion.Select.Folder");

    private readonly PageSelectRight right = new();
    private bool isFirstLoad = true;
    private List<string>? mcFolderListLast;
    private bool isDragging;

    public PageSelectLeft()
    {
        InitializeComponent();
        AnimatedControl = PanList;
        Loaded += PageSelectLeft_Loaded;
        App.Services.InstancesChanged += OnInstancesChanged;
    }

    public override void OnNavigated(PageSubType sub)
    {
        Navigator.ShowRight(right);
    }

    void IRefreshable.Refresh()
    {
        RefreshCurrent();
    }

    private void OnInstancesChanged()
    {
        Dispatcher.UIThread.Post(McFolderListUI);
    }

    private void PageSelectLeft_Loaded(object? sender, EventArgs e)
    {
        if (isFirstLoad)
            McFolderListUI(); // 若已经执行完成，触发首次加载
        isFirstLoad = false;
    }

    private void McFolderListUI()
    {
        try
        {
            var folders = App.Services.Settings.MinecraftFolders.ToList();

            // 确认数据有变化
            var snapshot = folders.Select(folder => folder + "\n" + SelectFolders.GetName(folder)).ToList();
            if (mcFolderListLast is not null && mcFolderListLast.SequenceEqual(snapshot))
                return;

            mcFolderListLast = snapshot;

            // 创建 UI
            PanList.Children.Clear();

            // 文件夹列表标题
            PanList.Children.Add(new TextBlock
            {
                Text = Lang.Text("Select.Folder.ListTitle"),
                Margin = new Thickness(13, 18, 5, 4),
                Opacity = 0.6,
                FontSize = 12
            });

            for (var i = 0; i < folders.Count; i++)
            {
                var folder = folders[i];
                var isOfficial = SelectFolders.IsOfficial(folder);
                var isRenamed = SelectFolders.HasCustomName(folder);

                // 创建 ContextMenu
                var contMenu = new ContextMenu();

                // 添加菜单项
                void AddMenuItem(string name, string header, string svgIcon, Thickness? padding = null,
                    Action? clickHandler = null)
                {
                    var item = new MyMenuItem
                    {
                        Name = name,
                        Header = header,
                        SvgIcon = svgIcon,
                        Padding = padding ?? new Thickness(0)
                    };
                    if (clickHandler is not null)
                        item.Click += (_, _) => clickHandler();
                    contMenu.Items.Add(item);
                }

                const string iconRestore = "lucide/rotate-ccw";
                const string iconRename = "lucide/pencil";
                const string iconMoveup = "lucide/move-up";
                const string iconMovedown = "lucide/move-down";
                const string iconOpen = "lucide/folder-open";
                const string iconRefresh = "lucide/refresh-cw";
                const string iconDelete = "lucide/trash-2";
                const string iconRemove = "lucide/list-x";

                if (isOfficial && !isRenamed)
                {
                    AddMenuItem("Rename", Lang.Text("Select.Folder.Rename"), iconRename, new Thickness(0, 2, 0, 0),
                        () => Rename_Click(folder));
                    AddMenuItem("MoveUp", Lang.Text("Select.Folder.MoveUp"), iconMoveup, null,
                        () => Move_Click(folder, -1));
                    AddMenuItem("MoveDown", Lang.Text("Select.Folder.MoveDown"), iconMovedown, null,
                        () => Move_Click(folder, 1));
                    AddMenuItem("Open", Lang.Text("Common.Action.Open"), iconOpen, null,
                        () => Open_Click(folder));
                    AddMenuItem("Refresh", Lang.Text("Common.Action.Refresh"), iconRefresh, null,
                        () => Refresh_Click(folder));
                    AddMenuItem("Delete",
                        folders.Count == 1 && isOfficial
                            ? Lang.Text("Select.Folder.Clear")
                            : Lang.Text("Common.Action.Delete"), iconDelete, new Thickness(0, 0, 0, 2),
                        () => Delete_Click(folder));
                }
                else if (isOfficial)
                {
                    AddMenuItem("Restore", Lang.Text("Select.Folder.RestoreName"), iconRestore,
                        new Thickness(0, 2, 0, 0),
                        () => Restore_Click(folder));
                    AddMenuItem("Rename", Lang.Text("Select.Folder.Rename"), iconRename, null,
                        () => Rename_Click(folder));
                    AddMenuItem("MoveUp", Lang.Text("Select.Folder.MoveUp"), iconMoveup, null,
                        () => Move_Click(folder, -1));
                    AddMenuItem("MoveDown", Lang.Text("Select.Folder.MoveDown"), iconMovedown, null,
                        () => Move_Click(folder, 1));
                    AddMenuItem("Open", Lang.Text("Common.Action.Open"), iconOpen, null,
                        () => Open_Click(folder));
                    AddMenuItem("Refresh", Lang.Text("Common.Action.Refresh"), iconRefresh, null,
                        () => Refresh_Click(folder));
                    AddMenuItem("Delete", Lang.Text("Common.Action.Delete"), iconDelete, new Thickness(0, 0, 0, 2),
                        () => Delete_Click(folder));
                }
                else
                {
                    AddMenuItem("Rename", Lang.Text("Select.Folder.Rename"), iconRename, new Thickness(0, 2, 0, 0),
                        () => Rename_Click(folder));
                    AddMenuItem("MoveUp", Lang.Text("Select.Folder.MoveUp"), iconMoveup, null,
                        () => Move_Click(folder, -1));
                    AddMenuItem("MoveDown", Lang.Text("Select.Folder.MoveDown"), iconMovedown, null,
                        () => Move_Click(folder, 1));
                    AddMenuItem("Open", Lang.Text("Common.Action.Open"), iconOpen, null,
                        () => Open_Click(folder));
                    AddMenuItem("Refresh", Lang.Text("Common.Action.Refresh"), iconRefresh, null,
                        () => Refresh_Click(folder));
                    AddMenuItem("Remove", Lang.Text("Select.Folder.RemoveFromList"), iconRemove,
                        null, () => Remove_Click(folder));
                    AddMenuItem("Delete", Lang.Text("Common.Action.Delete"), iconDelete, new Thickness(0, 0, 0, 2),
                        () => Delete_Click(folder));
                }

                // 控制上移下移显示
                var moveUpItem = contMenu.Items.OfType<MyMenuItem>().FirstOrDefault(x => x.Name == "MoveUp");
                var moveDownItem = contMenu.Items.OfType<MyMenuItem>().FirstOrDefault(x => x.Name == "MoveDown");

                // 如果是第一个项目，隐藏上移按钮
                if (i == 0 && moveUpItem is not null)
                    moveUpItem.IsVisible = false;

                // 如果是最后一个项目，隐藏下移按钮
                if (i == folders.Count - 1 && moveDownItem is not null)
                    moveDownItem.IsVisible = false;

                // 构建列表项
                var newItem = new MyListItem
                {
                    IsScaleAnimationEnabled = false,
                    Type = MyListItem.CheckType.RadioBox,
                    MinPaddingRight = 30,
                    Title = SelectFolders.GetName(folder),
                    Info = folder,
                    Height = 40,
                    ContextMenu = contMenu,
                    Tag = folder
                };

                newItem.Changed += (a, b) => Folder_Change((MyListItem)a, b);

                // 拖拽
                DragDrop.SetAllowDrop(newItem, true);
                newItem.PointerPressed += Item_PointerPressed;
                newItem.AddHandler(DragDrop.DragEnterEvent, Item_DragEnter);
                newItem.AddHandler(DragDrop.DragOverEvent, Item_DragOver);
                newItem.AddHandler(DragDrop.DragLeaveEvent, Item_DragLeave);
                newItem.AddHandler(DragDrop.DropEvent, Item_Drop);

                // 图标按钮
                var newIconButton = new MyIconButton
                {
                    SvgIcon = "lucide/settings",
                    LogoScale = 1.1
                };
                newIconButton.Click += (_, _) => contMenu.Open(newItem);
                newItem.Buttons = new[] { newIconButton };

                PanList.Children.Add(newItem);

                SelectLog.Info($"有效的 Minecraft 文件夹：{SelectFolders.GetName(folder)} > {folder}");
            }

            // 标题文本
            PanList.Children.Add(new TextBlock
            {
                Text = Lang.Text("Select.Folder.AddOrImport"),
                Margin = new Thickness(13, 18, 5, 4),
                Opacity = 0.6,
                FontSize = 12
            });

            // 创建新文件夹按钮
            if (!Directory.Exists(MinecraftFolder.GetDefault()))
            {
                var itemCreate = new MyListItem
                {
                    IsScaleAnimationEnabled = false,
                    Type = MyListItem.CheckType.Clickable,
                    Title = Lang.Text("Select.Folder.CreateNew.Title"),
                    Height = 34,
                    LogoScale = 0.9,
                    SvgIcon = "lucide/folder-plus"
                };
                ToolTip.SetTip(itemCreate, Lang.Text("Select.Folder.CreateNew.ToolTip"));
                ToolTip.SetPlacement(itemCreate, PlacementMode.Right);
                itemCreate.Click += (_, _) => Create_Click();
                PanList.Children.Add(itemCreate);
            }

            // 添加按钮
            var itemAdd = new MyListItem
            {
                IsScaleAnimationEnabled = false,
                Type = MyListItem.CheckType.Clickable,
                Title = Lang.Text("Select.Folder.AddExisting.Title"),
                Height = 34,
                SvgIcon = "lucide/folder-input"
            };
            ToolTip.SetTip(itemAdd, Lang.Text("Select.Folder.AddExisting.ToolTip"));
            ToolTip.SetPlacement(itemAdd, PlacementMode.Right);
            itemAdd.Click += (_, _) => Add_Click();
            PanList.Children.Add(itemAdd);

            // 导入整合包
            var itemInstall = new MyListItem
            {
                IsScaleAnimationEnabled = false,
                Type = MyListItem.CheckType.Clickable,
                Title = Lang.Text("Select.Folder.ImportModpack.Title"),
                Height = 34,
                SvgIcon = "lucide/package-plus"
            };
            ToolTip.SetTip(itemInstall, Lang.Text("Select.Folder.ImportModpack.ToolTip"));
            ToolTip.SetPlacement(itemInstall, PlacementMode.Right);
            itemInstall.Click += (_, _) => ModpackInstall();
            PanList.Children.Add(itemInstall);

            // 边距
            PanList.Children.Add(new Control { Height = 10, IsHitTestVisible = false });

            // 确认勾选状态，列表第一项即当前使用的文件夹
            if (folders.Count > 0)
                ((MyListItem)PanList.Children[1]).Checked = true; // 去掉第一个标题
        }
        catch (Exception ex)
        {
            SelectLog.Error("构建 Minecraft 文件夹列表 UI 出错", ex);
        }
        finally
        {
            SelectInstances.Reload(false);
        }
    }

    private void Move_Click(string folder, int offset)
    {
        SelectFolders.Move(folder, offset);
        McFolderListUI();
    }

    private void Restore_Click(string folder)
    {
        SelectFolders.RestoreName(folder);
        McFolderListUI();
    }

    // 添加文件夹
    private async void Add_Click()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
                return;
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Lang.Text("Select.Folder.AddExisting.Title")
            });
            if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } newFolder)
                return;
            if (newFolder.Contains('!') || newFolder.Contains(';'))
            {
                Hint.Show(Lang.Text("Select.Folder.InvalidPathChars"), HintType.Error);
                return;
            }

            SelectFolders.AddFolder(newFolder, true);
        }
        catch (Exception ex)
        {
            SelectLog.Feedback(Lang.Text("Select.Folder.Error.Add"), ex);
        }
    }

    private static void ModpackInstall()
    {
        Navigator.Go(PageType.Download, PageSubType.DownloadInstall);
    }

    // 创建文件夹
    public void Create_Click()
    {
        SelectFolders.CreateNew();
        McFolderListUI();
    }

    // 右键菜单
    private async void Remove_Click(string folder)
    {
        try
        {
            switch (await MsgBox.ShowAsync(
                        Lang.Text("Select.Folder.Cleanup.Message"),
                        Lang.Text("Select.Folder.Cleanup.Title"), Lang.Text("Common.Action.Delete"),
                        Lang.Text("Select.Folder.Cleanup.Keep"), Lang.Text("Common.Action.Cancel")))
            {
                case 1:
                {
                    // 删除配置文件
                    SelectFolders.CleanupConfig(folder);
                    break;
                }
                case 2:
                {
                    break;
                }
                // 不删除
                case 3:
                {
                    // 取消
                    return;
                }
            }

            // 获取并删除列表项
            var name = SelectFolders.GetName(folder);
            SelectFolders.RemoveFolder(folder);
            McFolderListUI();
            Hint.Show(Lang.Text("Select.Folder.RemoveSuccess", name), HintType.Success);
        }
        catch (Exception ex)
        {
            SelectLog.Feedback(Lang.Text("Select.Folder.Error.Remove"), ex);
        }
    }

    private async void Delete_Click(string folder)
    {
        try
        {
            var name = SelectFolders.GetName(folder);
            var isClearing = SelectFolders.IsOfficial(folder) && App.Services.Settings.MinecraftFolders.Count == 1;

            var deleteText = Lang.Text(isClearing ? "Select.Folder.Clear" : "Common.Action.Delete");
            var firstWarning = Lang.Text(
                isClearing ? "Select.Folder.Clear.FirstWarning" : "Select.Folder.Delete.FirstWarning", folder);
            var finalWarning = Lang.Text(
                isClearing ? "Select.Folder.Clear.FinalWarning" : "Select.Folder.Delete.FinalWarning", folder);
            var confirmTitle = Lang.Text(isClearing ? "Select.Folder.Clear.Confirm" : "Select.Folder.Delete.Confirm");
            var inProgress = Lang.Text(
                isClearing ? "Select.Folder.Clear.InProgress" : "Select.Folder.Delete.InProgress", name);
            var success = Lang.Text(isClearing ? "Select.Folder.Clear.Success" : "Select.Folder.Delete.Success", name);

            if (await MsgBox.ShowAsync(firstWarning, Lang.Text("Select.Folder.Delete.WarningTitle"),
                    Lang.Text("Common.Action.Cancel"), Lang.Text("Common.Action.Confirm"),
                    Lang.Text("Common.Action.Cancel")) != 2)
                return;

            if (await MsgBox.ShowAsync(finalWarning, Lang.Text("Select.Folder.Delete.WarningTitle"),
                    confirmTitle, Lang.Text("Common.Action.Cancel"), isWarn: true) != 1)
                return;

            // 清空官方文件夹时保留列表项，只清除自定义名称
            if (isClearing)
                SelectFolders.RestoreName(folder);
            else
                SelectFolders.RemoveFolder(folder);
            McFolderListUI();

            await Task.Run(() =>
            {
                try
                {
                    Hint.Show(inProgress);
                    if (Directory.Exists(folder))
                        Directory.Delete(folder, true);
                    if (isClearing)
                        Directory.CreateDirectory(folder);
                    Hint.Show(success, HintType.Success);
                }
                catch (Exception ex)
                {
                    SelectLog.Error(Lang.Text("Select.Folder.Error.Operate", deleteText, name), ex);
                    Hint.Show(Lang.Text("Select.Folder.Error.Operate", deleteText, name), HintType.Error);
                }
                finally
                {
                    App.Services.NotifyInstancesChanged();
                }
            });
        }
        catch (Exception ex)
        {
            SelectLog.Feedback(Lang.Text("Select.Folder.Error.Operate", Lang.Text("Common.Action.Delete"), folder), ex);
        }
    }

    private void Open_Click(string folder)
    {
        SystemShell.OpenFolder(folder);
    }

    private void Refresh_Click(string folder)
    {
        RefreshCurrent(folder);
    }

    public void RefreshCurrent()
    {
        RefreshCurrent(App.Services.Settings.MinecraftRoot);
    }

    public static void RefreshCurrent(string folder)
    {
        if (SelectFolders.IsSamePath(folder, App.Services.Settings.MinecraftRoot))
            SelectInstances.Reload(true);
    }

    private async void Rename_Click(string folder)
    {
        try
        {
            // 获取输入
            var currentName = SelectFolders.GetName(folder);
            var newName = await MsgBox.InputAsync(Lang.Text("Select.Folder.Rename.Title"), "", currentName,
                ValidateFolderName);
            if (string.IsNullOrWhiteSpace(newName))
                return;
            // 名称未修改
            if (newName == currentName)
                return;
            // 修改自定义名
            SelectFolders.Rename(folder, newName);
            Hint.Show(Lang.Text("Select.Folder.NameUpdated", newName), HintType.Success);
            McFolderListUI();
        }
        catch (Exception ex)
        {
            SelectLog.Feedback(Lang.Text("Select.Folder.Error.Rename"), ex);
        }
    }

    private static string? ValidateFolderName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "输入内容不能为空！";
        if (input.Length > 30)
            return "长度最长为 30 个字符！";
        foreach (var item in new[] { ">", "|" })
            if (input.Contains(item))
                return $"输入内容不能包含 {item}！";
        return null;
    }

    // 点击选项
    private void Folder_Change(MyListItem sender, RouteEventArgs e)
    {
        if (!e.raiseByMouse || !sender.Checked)
            return;
        // 更换
        SelectFolders.MakeCurrent((string)sender.Tag!);
        Dispatcher.UIThread.Post(McFolderListUI);
    }

    #region 拖拽排序功能

    // 拖拽开始：Avalonia 的 DoDragDropAsync 只能由 PointerPressed 事件发起（对应 WPF 的 MouseMove 中按住左键时调用 DragDrop.DoDragDrop）
    private void Item_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var item = (MyListItem)sender!;
        // 当按住鼠标左键时开始拖拽操作
        if (!e.GetCurrentPoint(item).Properties.IsLeftButtonPressed || isDragging)
            return;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(FolderDataFormat, (string)item.Tag!));
        _ = StartDragAsync(e, data);
    }

    private async Task StartDragAsync(PointerPressedEventArgs e, IDataTransfer data)
    {
        isDragging = true;
        try
        {
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            SelectLog.Error("开始拖拽操作失败", ex);
        }
        finally
        {
            isDragging = false;
        }
    }

    // 拖拽进入时的处理
    private void Item_DragEnter(object? sender, DragEventArgs e)
    {
        try
        {
            if (e.DataTransfer.Contains(FolderDataFormat))
            {
                e.DragEffects = DragDropEffects.Move;
                // 添加视觉反馈
                var item = (MyListItem)sender!;
                item.Opacity = 0.7d;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        }
        catch (Exception)
        {
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    // 拖拽悬停时的处理
    private void Item_DragOver(object? sender, DragEventArgs e)
    {
        try
        {
            e.DragEffects = e.DataTransfer.Contains(FolderDataFormat)
                ? DragDropEffects.Move
                : DragDropEffects.None;
        }
        catch (Exception)
        {
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    // 拖拽离开时的处理
    private void Item_DragLeave(object? sender, DragEventArgs e)
    {
        try
        {
            // 恢复视觉状态
            var item = (MyListItem)sender!;
            item.Opacity = 1.0d;
        }
        catch (Exception ex)
        {
            SelectLog.Error("拖拽离开处理失败", ex);
        }

        e.Handled = true;
    }

    // 拖拽放下时的处理
    private void Item_Drop(object? sender, DragEventArgs e)
    {
        try
        {
            var targetItem = (MyListItem)sender!;
            var targetFolder = (string)targetItem.Tag!;

            // 恢复视觉状态
            targetItem.Opacity = 1.0d;

            // 检查数据有效性
            if (!e.DataTransfer.Contains(FolderDataFormat))
            {
                e.Handled = true;
                return;
            }

            var sourceFolder = e.DataTransfer.TryGetValue(FolderDataFormat)!;

            // 检查是否为有效的拖拽操作
            if (sourceFolder == targetFolder)
            {
                e.Handled = true;
                return;
            }

            // 执行移动操作，并更新文件夹顺序并刷新UI
            SelectFolders.Reorder(sourceFolder, targetFolder);
            McFolderListUI();
        }
        catch (Exception ex)
        {
            SelectLog.Feedback(Lang.Text("Select.Folder.Error.DragDrop"), ex);
        }
        finally
        {
            e.Handled = true;
        }
    }

    #endregion
}
