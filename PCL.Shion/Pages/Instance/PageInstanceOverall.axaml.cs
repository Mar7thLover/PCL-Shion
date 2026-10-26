using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Shion.Controls;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Select;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Instance;

public partial class PageInstanceOverall : MyPageRight
{
    /// <summary>
    ///     实例分类中“隐藏”项的下标，对应 WPF 版 McInstanceCardType.Hidden。
    /// </summary>
    private const int CardTypeHidden = 1;

    private bool isLoad;

    public MyListItem itemVersion = null!;

    public PageInstanceOverall()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Loaded += PageSetupLaunch_Loaded;
        LabInfoLoading.Text = Lang.Text("Instance.Overall.Info.Loading");
        // Handles
        ComboDisplayType.SelectionChanged += ComboDisplayType_SelectionChanged;
        BtnDisplayDesc.Click += BtnDisplayDesc_Click;
        BtnDisplayRename.Click += BtnDisplayRename_Click;
        ComboDisplayLogo.SelectionChanged += ComboDisplayLogo_SelectionChanged;
        BtnDisplayStar.Click += BtnDisplayStar_Click;
        BtnFolderVersion.Click += BtnFolderVersion_Click;
        BtnFolderSaves.Click += BtnFolderSaves_Click;
        BtnFolderMods.Click += BtnFolderMods_Click;
        BtnManageCheck.Click += BtnManageCheck_Click;
        BtnManageRestore.Click += BtnManageRestore_Click;
        BtnManageDelete.Click += BtnManageDelete_Click;
        BtnManagePatch.Click += BtnManagePatch_Click;
    }

    private static AppSettings Settings => App.Services.Settings;

    private static string McRoot => Settings.MinecraftRoot;

    private void PageSetupLaunch_Loaded(object? sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 更新设置
        ItemDisplayLogoCustom.Tag = Path.Combine("PCL", "Logo.png");
        Reload();

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;
        PanDisplay.TriggerForceResize();
    }

    /// <summary>
    ///     确保当前页面上的信息已正确显示。
    /// </summary>
    private void Reload()
    {
        ModAnimation.AniControlEnabled += 1;

        var instance = PageInstanceLeft.McInstance!;
        // 刷新设置项目
        ComboDisplayType.SelectedIndex = Settings.States.Instance.CardType[instance.PathInstance];
        BtnDisplayStar.Text = instance.IsStar ? Lang.Text("Instance.Overall.Unfavorite") : Lang.Text("Instance.Overall.Favorite");
        BtnFolderMods.IsVisible = instance.Modable;
        // 刷新实例显示
        PanDisplayItem.Children.Clear();
        var hiddenInstances = new HashSet<string>(Settings.HiddenInstances, StringComparer.OrdinalIgnoreCase);
        var entry = new SelectInstanceEntry(instance, instance.IsStar, hiddenInstances.Contains(instance.Directory));
        itemVersion = PageSelectRight.McVersionListItem(entry);
        itemVersion.IsHitTestVisible = false;
        PanDisplayItem.Children.Add(itemVersion);
        Navigator.RefreshTitle();
        // 刷新实例信息
        GetInstanceInfo();
        // 刷新实例图标
        ComboDisplayLogo.SelectedIndex = 0;
        var logo = Settings.States.Instance.LogoPath[instance.PathInstance];
        var logoCustom = Settings.States.Instance.IsLogoCustom[instance.PathInstance];
        if (logoCustom)
            foreach (MyComboBoxItem selection in ComboDisplayLogo.Items)
                if (Equals(selection.Tag, logo) ||
                    (Equals(selection.Tag, Path.Combine("PCL", "Logo.png")) &&
                     logo.EndsWith(Path.Combine("PCL", "Logo.png"), StringComparison.Ordinal)))
                {
                    ComboDisplayLogo.SelectedItem = selection;
                    break;
                }

        ModAnimation.AniControlEnabled -= 1;
    }

    private void GetInstanceInfo()
    {
        PanInfo.Children.Clear();
        PanInfo.Children.Add(new MyLoading { Text = Lang.Text("Instance.Overall.Info.Loading"), Margin = new Thickness(0d, 0d, 0d, 10d) });

        var instance = PageInstanceLeft.McInstance!;
        List<MyListItem> items = [];
        var launchCount = Settings.States.Instance.LaunchCount[instance.PathInstance];
        if (launchCount == 0)
            items.Add(new MyListItem
            {
                Title = Lang.Text("Instance.Overall.Info.LaunchCount.Title"), Info = Lang.Text("Instance.Overall.Info.LaunchCount.Never"), Logo = "avares://PCL.Shion/Assets/Images/Blocks/RedstoneLampOff.png"
            });
        else
            items.Add(new MyListItem
            {
                Title = Lang.Text("Instance.Overall.Info.LaunchCount.Title"),
                Info = Lang.Text("Instance.Overall.Info.LaunchCount.Count", launchCount),
                Logo = "avares://PCL.Shion/Assets/Images/Blocks/RedstoneLampOn.png"
            });
        var modpackVersion = Settings.States.Instance.ModpackVersion[instance.PathInstance];
        if (!string.IsNullOrWhiteSpace(modpackVersion))
            items.Add(new MyListItem
            {
                Title = Lang.Text("Instance.Overall.Info.ModpackVersion"), Info = modpackVersion,
                Logo = "avares://PCL.Shion/Assets/Images/Blocks/CommandBlock.png"
            });
        items.Add(new MyListItem
        {
            Title = "Minecraft", Info = instance.MinecraftVersion ?? "",
            Logo = "avares://PCL.Shion/Assets/Images/Blocks/Grass.png"
        });
        if (instance.Loader is { } loader)
        {
            var (name, logo) = loader.ToLowerInvariant() switch
            {
                "forge" => ("Forge", "avares://PCL.Shion/Assets/Images/Blocks/Anvil.png"),
                "neoforge" => ("NeoForge", "avares://PCL.Shion/Assets/Images/Blocks/NeoForge.png"),
                "fabric" => ("Fabric", "avares://PCL.Shion/Assets/Images/Blocks/Fabric.png"),
                "quilt" => ("Quilt", "avares://PCL.Shion/Assets/Images/Blocks/Quilt.png"),
                _ => (loader, "avares://PCL.Shion/Assets/Images/Blocks/Egg.png"),
            };
            items.Add(new MyListItem { Title = name, Info = instance.MinecraftVersion ?? "", Logo = logo });
        }

        var wrapPanel = new WrapPanel { Margin = new Thickness(0, -5, -20, 7) };
        foreach (var item in items)
        {
            wrapPanel.Children.Add(item);
            wrapPanel.Children.Add(new TextBlock { Width = 2d });
        }

        PanInfo.Children.Clear();
        PanInfo.Children.Add(wrapPanel);
    }

    #region 卡片：个性化

    // 实例分类
    private async void ComboDisplayType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!(isLoad && ModAnimation.AniControlEnabled == 0))
            return;
        var instance = PageInstanceLeft.McInstance!;
        if (ComboDisplayType.SelectedIndex != 1)
        {
            // 改为不隐藏
            try
            {
                // 若设置分类为可安装 Mod，则显示正常的 Mod 管理页面
                Settings.States.Instance.CardType[instance.PathInstance] = ComboDisplayType.SelectedIndex;
                Settings.Save();
                App.Services.NotifyInstancesChanged();
            }
            catch (Exception)
            {
                HintService.Hint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
            }

            Reload(); // 更新 “打开 Mod 文件夹” 按钮
        }
        else
        {
            // 改为隐藏
            try
            {
                if (!Settings.States.Hint.HideGameInstance)
                {
                    if (await MsgBox.ShowAsync(
                            Lang.Text("Instance.Overall.Hide.ConfirmMessage"), Lang.Text("Instance.Overall.Hide.ConfirmTitle"), button2: Lang.Text("Common.Action.Cancel")) != 1)
                    {
                        ComboDisplayType.SelectedIndex = 0;
                        return;
                    }

                    Settings.States.Hint.HideGameInstance = true;
                }

                Settings.States.Instance.CardType[instance.PathInstance] = CardTypeHidden;
                Settings.Save();
                App.Services.NotifyInstancesChanged();
            }
            catch (Exception)
            {
                HintService.Hint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
            }
        }
    }

    // 更改描述
    private async void BtnDisplayDesc_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            var instance = PageInstanceLeft.McInstance!;
            var oldInfo = Settings.States.Instance.CustomInfo[instance.PathInstance];
            var newInfo = await MsgBox.InputAsync(Lang.Text("Instance.Overall.Description.EditTitle"), Lang.Text("Instance.Overall.Description.EditMessage"), oldInfo ?? "");
            if (newInfo is not null && (oldInfo ?? "") != (newInfo ?? ""))
            {
                Settings.States.Instance.CustomInfo[instance.PathInstance] = newInfo;
                Settings.Save();
            }

            PageInstanceLeft.McInstance = ReloadInstance(instance.Name);
            Reload();
            App.Services.NotifyInstancesChanged();
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
        }
    }

    // 重命名实例
    private async void BtnDisplayRename_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            // 确认输入的新名称
            var oldName = PageInstanceLeft.McInstance!.Name;
            var newName = await MsgBox.InputAsync(Lang.Text("Instance.Overall.Name.EditTitle"), "", oldName,
                name => IsNewInstanceNameValid(name, oldName) ? null : Lang.Text("Instance.Overall.Name.EditTitle"));
            if (string.IsNullOrWhiteSpace(newName))
                return;
            // 更改已选中的实例
            InstanceManager.Rename(McRoot, oldName, newName);
            if (Settings.SelectedInstance == oldName)
            {
                Settings.SelectedInstance = newName;
                Settings.Save();
            }

            // 刷新与提示
            HintService.Hint(Lang.Text("Instance.Overall.Name.RenameSuccess"), HintType.Success);
            PageInstanceLeft.McInstance = ReloadInstance(newName);
            Reload();
            App.Services.NotifyInstancesChanged();
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
        }
    }

    private static bool IsNewInstanceNameValid(string name, string oldName) =>
        InstanceManager.IsValidName(name) &&
        (string.Equals(name, oldName, StringComparison.OrdinalIgnoreCase) ||
         !Directory.Exists(Path.Combine(McRoot, "versions", name)));

    // 实例图标
    private async void ComboDisplayLogo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!(isLoad && ModAnimation.AniControlEnabled == 0))
            return;
        var instance = PageInstanceLeft.McInstance!;
        var logoPath = Path.Combine(instance.PathInstance, "PCL", "Logo.png");
        // 选择 自定义 时修改图片
        try
        {
            if (ReferenceEquals(ComboDisplayLogo.SelectedItem, ItemDisplayLogoCustom))
            {
                var fileName = await SystemDialogs.SelectFileAsync(Lang.Text("Instance.Overall.Icon.SelectFile.Filter"), Lang.Text("Instance.Overall.Icon.SelectFile.Title"));
                if (string.IsNullOrEmpty(fileName))
                {
                    Reload(); // 还原选项
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(logoPath)!);
                File.Copy(fileName, logoPath, overwrite: true);
            }
            else
            {
                File.Delete(logoPath);
            }
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
            Reload();
            return;
        }

        // 进行更改
        try
        {
            var newLogo = ComboDisplayLogo.SelectedItem is MyComboBoxItem selected ? selected.Tag?.ToString() : null;
            Settings.States.Instance.LogoPath[instance.PathInstance] = newLogo;
            Settings.States.Instance.IsLogoCustom[instance.PathInstance] = !string.IsNullOrEmpty(newLogo);
            // 刷新显示
            Settings.Save();
            PageInstanceLeft.McInstance = ReloadInstance(instance.Name);
            Reload();
            App.Services.NotifyInstancesChanged();
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
        }
    }

    // 收藏夹
    private void BtnDisplayStar_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            var instance = PageInstanceLeft.McInstance!;
            Settings.States.Instance.Starred[instance.PathInstance] = !instance.IsStar;
            Settings.Save();
            PageInstanceLeft.McInstance = ReloadInstance(instance.Name);
            Reload();
            App.Services.NotifyInstancesChanged();
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
        }
    }

    /// <summary>
    ///     从磁盘重新读取指定名称的实例。
    /// </summary>
    private static GameInstance? ReloadInstance(string name) =>
        InstanceScanner.Scan(McRoot).FirstOrDefault(instance => instance.Name == name);

    #endregion

    #region 卡片：快捷方式

    // 实例文件夹
    private void BtnFolderVersion_Click(object? sender, PointerReleasedEventArgs e)
    {
        OpenVersionFolder(PageInstanceLeft.McInstance!);
    }

    public static void OpenVersionFolder(GameInstance version)
    {
        SystemShell.OpenFolder(version.PathInstance);
    }

    // 存档文件夹
    private void BtnFolderSaves_Click(object? sender, PointerReleasedEventArgs e)
    {
        var folderPath = Path.Combine(PageInstanceLeft.McInstance!.PathIndie, "saves");
        Directory.CreateDirectory(folderPath);
        SystemShell.OpenFolder(folderPath);
    }

    // Mod 文件夹
    private void BtnFolderMods_Click(object? sender, PointerReleasedEventArgs e)
    {
        var folderPath = Path.Combine(PageInstanceLeft.McInstance!.PathIndie, "mods");
        Directory.CreateDirectory(folderPath);
        SystemShell.OpenFolder(folderPath);
    }

    #endregion

    #region 卡片：管理

    // 补全文件
    private void BtnManageCheck_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            var instance = PageInstanceLeft.McInstance!;
            // 忽略文件检查提示
            if (Settings.Config.Instance.DisableAssetVerifyV2[instance.PathInstance])
            {
                HintService.Hint(Lang.Text("Instance.Overall.Repair.DisableVerificationHint"));
                return;
            }

            // 重复任务检查
            var taskName = instance.Name + " " + Lang.Text("Instance.Overall.Repair.TaskName");
            App.Services.StartRepair(instance, taskName);
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
        }
    }

    // 重置
    private async void BtnManageRestore_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            var instance = PageInstanceLeft.McInstance!;
            if (instance.Loader is "quilt")
            {
                HintService.Hint(Lang.Text("Instance.Overall.Reset.QuiltUnsupported"));
                return;
            }

            // 确认操作
            if (await MsgBox.ShowAsync(
                    Lang.Text("Instance.Overall.Reset.ConfirmMessage", instance.Name), Lang.Text("Instance.Overall.Reset.ConfirmTitle"), Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) == 2)
                return;

            // 提交安装申请
            App.Services.RequestReinstall(instance);
            Navigator.Go(PageType.Launch);
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
        }
    }

    // 删除实例
    // 修改此代码时，同时修改 PageSelectRight 中的代码
    private async void BtnManageDelete_Click(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            var instance = PageInstanceLeft.McInstance!;
            var isShiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            var isIsolatedInstance =
                instance.Error is null &&
                !string.Equals(instance.PathIndie,
                    Path.TrimEndingDirectorySeparator(McRoot) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);

            var confirmMessageKey = (isIsolatedInstance, isShiftPressed) switch
            {
                (true, true) => "Instance.Overall.Delete.ConfirmMessageIsolatedPermanent",
                (true, false) => "Instance.Overall.Delete.ConfirmMessageIsolated",
                (false, true) => "Instance.Overall.Delete.ConfirmMessagePermanent",
                (false, false) => "Instance.Overall.Delete.ConfirmMessage"
            };

            var confirmResult = await MsgBox.ShowAsync(
                Lang.Text(confirmMessageKey, instance.Name),
                Lang.Text("Instance.Overall.Delete.ConfirmTitle"),
                button2: Lang.Text("Common.Action.Cancel"),
                isWarn: isIsolatedInstance || isShiftPressed
            );

            switch (confirmResult)
            {
                case 1:
                {
                    var instancePath = instance.PathInstance;
                    var instanceName = instance.Name;
                    if (isShiftPressed)
                    {
                        Directory.Delete(instancePath, recursive: true);
                        HintService.Hint(Lang.Text("Instance.Overall.Delete.PermanentSuccess", instanceName),
                            HintType.Success);
                    }
                    else
                    {
                        Trash.MoveToTrash(instancePath);
                        HintService.Hint(Lang.Text("Instance.Overall.Delete.RecycleBinSuccess", instanceName),
                            HintType.Success);
                    }

                    break;
                }
                case 2:
                {
                    return;
                }
            }

            App.Services.NotifyInstancesChanged();
            Navigator.Back();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
        }
    }

    // 修补核心
    private async void BtnManagePatch_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (!BtnManagePatch.IsEnabled)
            return;
        BtnManagePatch.IsEnabled = false;
        try
        {
            var instance = PageInstanceLeft.McInstance!;
            if (await MsgBox.ShowAsync(
                    Lang.Text("Instance.Overall.Patch.ConfirmMessage", instance.Name),
                    Lang.Text("Instance.Overall.Patch.ConfirmTitle"), button2: Lang.Text("Common.Action.Cancel")) != 1)
                return;
            var userInput = await SystemDialogs.SelectFileAsync(Lang.Text("Instance.Overall.Patch.SelectFile.Filter"),
                Lang.Text("Instance.Overall.Patch.SelectFile.Title"));
            if (string.IsNullOrWhiteSpace(userInput))
                return;
            HintService.Hint(Lang.Text("Instance.Overall.Patch.Patching"));
            if (await App.Services.PatchCore(instance, userInput))
            {
                Settings.Config.Instance.DisableAssetVerifyV2[instance.PathInstance] = true;
                Settings.Save();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Instance.Overall.Error.OperationFailed"), HintType.Error);
        }
        finally
        {
            BtnManagePatch.IsEnabled = true;
        }
    }

    #endregion
}
