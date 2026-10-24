using System;
using System.Globalization;
using System.IO;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Shion.Controls;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Online;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Setup;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Instance;

public partial class PageInstanceLeft : MyPageLeft, IRefreshable
{
    /// <summary>
    ///     当前显示设置的 MC 实例。
    /// </summary>
    public static GameInstance? McInstance = null;

    /// <summary>
    ///     左侧页面中的次级页面缓存，与 WPF ModMain 中的同名字段对应。
    /// </summary>
    public static PageInstanceOverall? frmInstanceOverall;
    public static PageInstanceSetup? frmInstanceSetup;
    public static PageInstanceSaves? frmInstanceSaves;
    public static PageInstanceScreenshot? frmInstanceScreenshot;
    public static PageInstanceCompResource? frmInstanceMod;
    public static PageInstanceModDisabled? frmInstanceModDisabled;
    public static PageInstanceCompResource? frmInstanceResourcePack;
    public static PageInstanceCompResource? frmInstanceShader;
    public static PageInstanceCompResource? frmInstanceSchematic;
    public static PageInstanceInstall? frmInstanceInstall;
    public static PageInstanceExport? frmInstanceExport;
    public static PageInstanceServer? frmInstanceServer;
    public static PageInstanceCompJarInJar? frmInstanceModJarInJar;

    /// <summary>
    ///     左侧实例设置栏的当前实例，与 WPF ModMain.frmInstanceLeft 对应。
    /// </summary>
    public static PageInstanceLeft? frmInstanceLeft => Navigator.PageLeft as PageInstanceLeft;

    public PageInstanceLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;
        Loaded += (_, _) => RefreshModDisabled();
    }

    public void Refresh()
    {
        Refresh(Navigator.CurrentSub);
    }

    public void RefreshModDisabled()
    {
        var hide = App.Services.Settings.Config.Preference.Hide;

        if (McInstance is not null && McInstance.Modable)
        {
            ItemMod.IsVisible = !(!PageSetupUI.HiddenForceShow && hide.InstanceMod);
            ItemModDisabled.IsVisible = false;
        }
        else
        {
            ItemMod.IsVisible = false;
            ItemModDisabled.IsVisible = !(!PageSetupUI.HiddenForceShow && hide.InstanceMod);
        }

        // 功能隐藏
        if (!PageSetupUI.HiddenForceShow)
        {
            var disableCount = 0;
            if (hide.InstanceSave)
                disableCount += 1;
            if (hide.InstanceScreenshot)
                disableCount += 1;
            if (hide.InstanceMod)
                disableCount += 1;
            if (hide.InstanceResourcePack)
                disableCount += 1;
            if (hide.InstanceShader)
                disableCount += 1;
            if (hide.InstanceSchematic)
                disableCount += 1;
            if (hide.InstanceServer)
                disableCount += 1;
            if (disableCount == 7)
                TextResource.IsVisible = false;
            else
                TextResource.IsVisible = true;
        }
        else
        {
            TextResource.IsVisible = true;
        }

        ItemInstall.IsVisible = !(!PageSetupUI.HiddenForceShow && hide.InstanceEdit);
        ItemExport.IsVisible = !(!PageSetupUI.HiddenForceShow && hide.InstanceExport);
        ItemWorld.IsVisible = !(!PageSetupUI.HiddenForceShow && hide.InstanceSave);
        ItemScreenshot.IsVisible = !(!PageSetupUI.HiddenForceShow && hide.InstanceScreenshot);
        ItemResourcePack.IsVisible = !(!PageSetupUI.HiddenForceShow && hide.InstanceResourcePack);
        ItemShader.IsVisible = !(!PageSetupUI.HiddenForceShow && hide.InstanceShader);
        ItemSchematic.IsVisible = !(!PageSetupUI.HiddenForceShow && hide.InstanceSchematic);
        ItemServer.IsVisible = !(!PageSetupUI.HiddenForceShow && hide.InstanceServer);
    }

    private void RefreshButton_Click(object? sender, PointerReleasedEventArgs e) // 由边栏按钮匿名调用
    {
        Refresh((PageSubType)TagValue(((MyIconButton)sender!).Tag));
    }

    public void Refresh(PageSubType subType)
    {
        switch (subType)
        {
            case PageSubType.VersionMod:
            {
                PageInstanceCompResource.Refresh(CompType.Mod);
                break;
            }
            case PageSubType.VersionScreenshot:
            {
                _ = PageInstanceScreenshot.RefreshAsync();
                break;
            }
            case PageSubType.VersionWorld:
            {
                PageInstanceSaves.Refresh();
                break;
            }
            case PageSubType.VersionResourcePack:
            {
                PageInstanceCompResource.Refresh(CompType.ResourcePack);
                break;
            }
            case PageSubType.VersionShader:
            {
                PageInstanceCompResource.Refresh(CompType.Shader);
                break;
            }
            case PageSubType.VersionSchematic:
            {
                PageInstanceCompResource.Refresh(CompType.Schematic);
                break;
            }
            case PageSubType.VersionInstall:
            {
                ItemInstall.Checked = true;
                frmInstanceInstall?.GetCurrentInfo();
                break;
            }
            case PageSubType.VersionExport:
            {
                frmInstanceExport?.RefreshAll();
                ItemExport.Checked = true;
                break;
            }
            case PageSubType.VersionServer:
            {
                frmInstanceServer?.RefreshServers();
                ItemServer.Checked = true;
                break;
            }
        }
    }

    private async void Reset(object? sender, PointerReleasedEventArgs e)
    {
        if (await MsgBox.ShowAsync(
                Lang.Text("Instance.Left.InitializeSettings.ConfirmMessage"),
                Lang.Text("Instance.Left.InitializeSettings.ConfirmTitle"),
                button2: Lang.Text("Common.Action.Cancel"),
                isWarn: true)
            == 1)
        {
            frmInstanceSetup ??= new PageInstanceSetup();
            frmInstanceSetup.Reset();
            ItemSetup.Checked = true;
        }
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。从 0 开始计算。
    /// </summary>
    public PageSubType pageID = PageSubType.Default;

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object sender, RouteEventArgs e)
    {
        if (sender is MyListItem item && item.Tag is not null)
            PageChange((PageSubType)TagValue(item.Tag));
    }

    private static int TagValue(object? tag) =>
        int.Parse(tag?.ToString() ?? "0", CultureInfo.InvariantCulture);

    public MyPageRight PageGet(PageSubType id)
    {
        if ((int)id == -1)
            id = pageID;
        switch (id)
        {
            case PageSubType.VersionOverall:
            {
                frmInstanceOverall ??= new PageInstanceOverall();
                return frmInstanceOverall;
            }
            case PageSubType.VersionMod:
            {
                frmInstanceMod ??= new PageInstanceCompResource(CompType.Mod);
                return frmInstanceMod;
            }
            case PageSubType.VersionModDisabled:
            {
                frmInstanceModDisabled ??= new PageInstanceModDisabled();
                return frmInstanceModDisabled;
            }
            case PageSubType.VersionModJarInJar:
            {
                frmInstanceModJarInJar ??= new PageInstanceCompJarInJar();
                return frmInstanceModJarInJar;
            }
            case PageSubType.VersionSetup:
            {
                frmInstanceSetup ??= new PageInstanceSetup();
                return frmInstanceSetup;
            }
            case PageSubType.VersionWorld:
            {
                frmInstanceSaves ??= new PageInstanceSaves();
                return frmInstanceSaves;
            }
            case PageSubType.VersionScreenshot:
            {
                frmInstanceScreenshot ??= new PageInstanceScreenshot();
                return frmInstanceScreenshot;
            }
            case PageSubType.VersionResourcePack:
            {
                frmInstanceResourcePack ??= new PageInstanceCompResource(CompType.ResourcePack);
                return frmInstanceResourcePack;
            }
            case PageSubType.VersionShader:
            {
                frmInstanceShader ??= new PageInstanceCompResource(CompType.Shader);
                return frmInstanceShader;
            }
            case PageSubType.VersionSchematic:
            {
                frmInstanceSchematic ??= new PageInstanceCompResource(CompType.Schematic);
                return frmInstanceSchematic;
            }
            case PageSubType.VersionInstall:
            {
                frmInstanceInstall ??= new PageInstanceInstall();
                return frmInstanceInstall;
            }
            case PageSubType.VersionExport:
            {
                frmInstanceExport ??= new PageInstanceExport();
                return frmInstanceExport;
            }
            case PageSubType.VersionServer:
            {
                frmInstanceServer ??= new PageInstanceServer();
                return frmInstanceServer;
            }

            default:
            {
                throw new InvalidOperationException("未知的实例设置子页面种类：" + (int)id);
            }
        }
    }

    /// <summary>
    ///     切换现有页面。
    /// </summary>
    public void PageChange(PageSubType id)
    {
        if (pageID == id)
            return;
        ModAnimation.AniControlEnabled += 1;
        try
        {
            PageChangeRun(PageGet(id));
            pageID = id;
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Instance.Error.OperationFailed"), HintType.Error);
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private static void PageChangeRun(MyPageRight target)
    {
        Navigator.ShowRight(target);
    }

    #endregion
}

/// <summary>
///     实例的路径与状态，与 WPF 版 McInstance 的同名属性对应。
/// </summary>
public static class McInstanceExtensions
{
    extension(GameInstance instance)
    {
        /// <summary>
        ///     实例目录，以路径分隔符结尾。
        /// </summary>
        public string PathInstance =>
            Path.TrimEndingDirectorySeparator(instance.Directory) + Path.DirectorySeparatorChar;

        /// <summary>
        ///     游戏目录，以路径分隔符结尾。
        /// </summary>
        public string PathIndie
        {
            get
            {
                var root = App.Services.Settings.MinecraftRoot;
                var settings = InstanceStore.Load(root, instance.Name);
                return Path.TrimEndingDirectorySeparator(InstanceManager.GetGameDirectory(root, instance, settings)) +
                       Path.DirectorySeparatorChar;
            }
        }

        /// <summary>
        ///     实例是否可以安装 Mod。
        /// </summary>
        public bool Modable => instance.Error is null;

        /// <summary>
        ///     实例是否已收藏。
        /// </summary>
        public bool IsStar => App.Services.Settings.States.Instance.Starred[instance.PathInstance];
    }
}
