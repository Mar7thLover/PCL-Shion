using System;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Logging;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Instance.Saves;

public partial class PageInstanceSavesLeft : MyPageLeft, IRefreshable
{
    /// <summary>
    ///     当前打开的存档文件夹。由存档列表在导航到存档详情前设置。
    /// </summary>
    public static string currentSave = "";

    /// <summary>
    ///     存档详情与数据包页面的缓存，与 WPF ModMain 中的同名字段对应。
    /// </summary>
    public static PageInstanceSavesInfo? frmInstanceSavesInfo;
    public static PageInstanceSavesDatapack? frmInstanceSavesDatapack;

    /// <summary>
    ///     左侧存档页面，与 WPF ModMain.frmInstanceSavesLeft 对应。
    /// </summary>
    public static PageInstanceSavesLeft? frmInstanceSavesLeft => Navigator.PageLeft as PageInstanceSavesLeft;

    // 初始化
    private bool isLoad;

    // 右侧页面缓存对应的存档文件夹
    private string? shownSave;

    public PageInstanceSavesLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;
        Loaded += Page_Loaded;
        ItemInfo.Check += PageCheck;
        ItemDatapack.Check += PageCheck;
        BtnOpenFolder.Click += BtnOpenFolder_Click;
    }

    private void Page_Loaded(object? sender, RoutedEventArgs e)
    {
        if (isLoad)
            return;
        isLoad = true;
    }

    private void BtnOpenFolder_Click(object? sender, PointerReleasedEventArgs e)
    {
        e.Handled = true;
        SystemShell.OpenFolder(currentSave);
    }

    #region 龙猫牌 页面管理

    /// <summary>
    ///     当前页面的编号。从 0 开始计算。
    /// </summary>
    public PageSubType pageID = PageSubType.Default;

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object sender, RouteEventArgs e)
    {
        if (sender is MyListItem { Tag: not null } item)
            PageChange((PageSubType)Convert.ToInt32(item.Tag));
    }

    public MyPageRight PageGet(PageSubType id = PageSubType.Default)
    {
        if ((int)id == -1)
            id = pageID;
        switch (id)
        {
            case PageSubType.VersionSavesInfo:
            {
                frmInstanceSavesInfo ??= new PageInstanceSavesInfo();
                return frmInstanceSavesInfo;
            }
            case PageSubType.VersionSavesDatapack:
            {
                frmInstanceSavesDatapack ??= new PageInstanceSavesDatapack();
                return frmInstanceSavesDatapack;
            }

            default:
            {
                throw new InvalidOperationException(Lang.Text("Instance.Saves.Left.UnknownSubPage", (int)id));
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
            Navigator.ShowRight(PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            Logger.TryGet(Avalonia.Logging.LogEventLevel.Error, LogArea.Control)?.Log(this,
                "{Message}：{Error}", Lang.Text("Instance.Saves.Left.SwitchFailed", (int)id), ex);
            HintService.Hint(Lang.Text("Instance.Saves.Left.SwitchFailed", (int)id), HintType.Error);
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    /// <summary>
    ///     导航到存档页面时调用，按子页面显示右侧内容。
    /// </summary>
    public override void OnNavigated(PageSubType sub)
    {
        // 换了存档后，丢弃旧存档的右侧页面，使内容按新存档重新加载
        if (shownSave != currentSave)
        {
            shownSave = currentSave;
            frmInstanceSavesInfo = null;
            frmInstanceSavesDatapack = null;
        }

        var id = sub == PageSubType.VersionSavesDatapack
            ? PageSubType.VersionSavesDatapack
            : PageSubType.VersionSavesInfo;
        pageID = id;
        ItemInfo.Checked = id == PageSubType.VersionSavesInfo;
        ItemDatapack.Checked = id == PageSubType.VersionSavesDatapack;
        Navigator.ShowRight(PageGet(id));
    }

    public void RefreshButton_Click(object? sender, PointerReleasedEventArgs e) // 由边栏按钮匿名调用
    {
        Refresh((PageSubType)Convert.ToInt32(((MyIconButton)sender!).Tag));
    }

    public void Refresh()
    {
        Refresh(Navigator.CurrentSub);
    }

    public void Refresh(PageSubType subType)
    {
        switch (subType)
        {
            case PageSubType.VersionSavesDatapack:
            {
                frmInstanceSavesDatapack ??= new PageInstanceSavesDatapack();
                if (ItemDatapack.Checked)
                    frmInstanceSavesDatapack.Refresh();
                else
                    ItemDatapack.Checked = true;

                break;
            }
        }

        HintService.Hint(Lang.Text("Instance.Saves.Left.Refreshing"));
    }

    #endregion
}
