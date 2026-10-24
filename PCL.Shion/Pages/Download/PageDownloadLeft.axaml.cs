using System;
using Avalonia.Controls;
using Avalonia.Input;
using PCL.Shion.Controls;
using PCL.Shion.Core.Online;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Download.Comp;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Download;

public partial class PageDownloadLeft : MyPageLeft, IRefreshable
{
    // 各右侧页面的缓存实例，与 WPF 中 ModMain 下的 frmDownload* 字段对应
    private PageDownloadInstall? frmDownloadInstall;
    private PageDownloadClient? frmDownloadClient;
    private PageDownloadOptiFine? frmDownloadOptiFine;
    private PageDownloadForge? frmDownloadForge;
    private PageDownloadNeoForge? frmDownloadNeoForge;
    private PageDownloadCleanroom? frmDownloadCleanroom;
    private PageDownloadFabric? frmDownloadFabric;
    private PageDownloadLegacyFabric? frmDownloadLegacyFabric;
    private PageDownloadLiteLoader? frmDownloadLiteLoader;
    private PageDownloadLabyMod? frmDownloadLabyMod;
    private PageDownloadMod? frmDownloadMod;
    private PageDownloadPack? frmDownloadPack;
    private PageDownloadDataPack? frmDownloadDataPack;
    private PageDownloadResourcePack? frmDownloadResourcePack;
    private PageDownloadShader? frmDownloadShader;
    private PageDownloadWorld? frmDownloadWorld;
    private PageDownloadCompFavorites? frmDownloadCompFavorites;

    public void Refresh()
    {
        Refresh(Navigator.CurrentSub);
    }

    // 强制刷新
    public void RefreshButton_Click(object? sender, PointerReleasedEventArgs e) // 由边栏按钮匿名调用
    {
        Refresh((PageSubType)Convert.ToInt32(((MyIconButton)sender!).Tag));
    }

    public void Refresh(PageSubType subType)
    {
        switch (subType)
        {
            case PageSubType.DownloadInstall:
            {
                ModDownload.dlClientListLoader.Start(isForceRestart: true);
                ModDownload.dlOptiFineListLoader.Start(isForceRestart: true);
                ModDownload.dlForgeListLoader.Start(isForceRestart: true);
                ModDownload.dlNeoForgeListLoader.Start(isForceRestart: true);
                ModDownload.dlCleanroomListLoader.Start(isForceRestart: true);
                ModDownload.dlLiteLoaderListLoader.Start(isForceRestart: true);
                ModDownload.dlFabricListLoader.Start(isForceRestart: true);
                ModDownload.dlLegacyFabricListLoader.Start(isForceRestart: true);
                ModDownload.dlFabricApiLoader.Start(isForceRestart: true);
                ModDownload.dlLegacyFabricApiLoader.Start(isForceRestart: true);
                ModDownload.dlOptiFabricLoader.Start(isForceRestart: true);
                ModDownload.dlLabyModListLoader.Start(isForceRestart: true);
                ItemInstall.Checked = true;
                break;
            }
            case PageSubType.DownloadMod:
            {
                ModComp.compProjectCache.Clear();
                ModComp.compFilesCache.Clear();
                if (frmDownloadMod is not null)
                {
                    ((PageComp)frmDownloadMod.Content!).storage = new CompProjectStorage();
                    ((PageComp)frmDownloadMod.Content!).page = 0;
                    frmDownloadMod.PageLoaderRestart();
                }

                ItemMod.Checked = true;
                break;
            }
            case PageSubType.DownloadPack:
            {
                ModComp.compProjectCache.Clear();
                ModComp.compFilesCache.Clear();
                if (frmDownloadPack is not null)
                {
                    ((PageComp)frmDownloadPack.Content!).storage = new CompProjectStorage();
                    ((PageComp)frmDownloadPack.Content!).page = 0;
                    frmDownloadPack.PageLoaderRestart();
                }

                ItemPack.Checked = true;
                break;
            }
            case PageSubType.DownloadDataPack:
            {
                ModComp.compProjectCache.Clear();
                ModComp.compFilesCache.Clear();
                if (frmDownloadDataPack is not null)
                {
                    ((PageComp)frmDownloadDataPack.Content!).storage = new CompProjectStorage();
                    ((PageComp)frmDownloadDataPack.Content!).page = 0;
                    frmDownloadDataPack.PageLoaderRestart();
                }

                ItemDataPack.Checked = true;
                break;
            }
            case PageSubType.DownloadResourcePack:
            {
                ModComp.compProjectCache.Clear();
                ModComp.compFilesCache.Clear();
                if (frmDownloadResourcePack is not null)
                {
                    ((PageComp)frmDownloadResourcePack.Content!).storage = new CompProjectStorage();
                    ((PageComp)frmDownloadResourcePack.Content!).page = 0;
                    frmDownloadResourcePack.PageLoaderRestart();
                }

                ItemResourcePack.Checked = true;
                break;
            }
            case PageSubType.DownloadShader:
            {
                ModComp.compProjectCache.Clear();
                ModComp.compFilesCache.Clear();
                if (frmDownloadShader is not null)
                {
                    ((PageComp)frmDownloadShader.Content!).storage = new CompProjectStorage();
                    ((PageComp)frmDownloadShader.Content!).page = 0;
                    frmDownloadShader.PageLoaderRestart();
                }

                ItemShader.Checked = true;
                break;
            }
            case PageSubType.DownloadWorld:
            {
                ModComp.compProjectCache.Clear();
                ModComp.compFilesCache.Clear();
                if (frmDownloadWorld is not null)
                {
                    ((PageComp)frmDownloadWorld.Content!).storage = new CompProjectStorage();
                    ((PageComp)frmDownloadWorld.Content!).page = 0;
                    frmDownloadWorld.PageLoaderRestart();
                }

                ItemWorld.Checked = true;
                break;
            }
            case PageSubType.DownloadClient:
            {
                ModDownload.dlClientListLoader.Start(isForceRestart: true);
                ItemClient.Checked = true;
                break;
            }
            case PageSubType.DownloadOptiFine:
            {
                ModDownload.dlOptiFineListLoader.Start(isForceRestart: true);
                ItemOptiFine.Checked = true;
                break;
            }
            case PageSubType.DownloadForge:
            {
                ModDownload.dlForgeListLoader.Start(isForceRestart: true);
                ItemForge.Checked = true;
                break;
            }
            case PageSubType.DownloadNeoForge:
            {
                ModDownload.dlNeoForgeListLoader.Start(isForceRestart: true);
                ItemNeoForge.Checked = true;
                break;
            }
            case PageSubType.DownloadCleanroom:
            {
                ModDownload.dlCleanroomListLoader.Start(isForceRestart: true);
                ItemCleanroom.Checked = true;
                break;
            }
            case PageSubType.DownloadLiteLoader:
            {
                ModDownload.dlLiteLoaderListLoader.Start(isForceRestart: true);
                ItemLiteLoader.Checked = true;
                break;
            }
            case PageSubType.DownloadFabric:
            {
                ModDownload.dlFabricListLoader.Start(isForceRestart: true);
                ItemFabric.Checked = true;
                break;
            }
            case PageSubType.DownloadLabyMod:
            {
                ModDownload.dlLabyModListLoader.Start(isForceRestart: true);
                ItemLabyMod.Checked = true;
                break;
            }
            case PageSubType.DownloadLegacyFabric:
            {
                ModDownload.dlLegacyFabricListLoader.Start(isForceRestart: true);
                ItemLegacyFabric.Checked = true;
                break;
            }
            case PageSubType.DownloadCompFavorites:
            {
                if (frmDownloadCompFavorites is not null)
                    frmDownloadCompFavorites.PageLoaderRestart();
                ItemFavorites.Checked = true;
                break;
            }
        }

        Hint.Show(Lang.Text("Download.Left.Hint.Refreshing"));
    }

    // 点击返回
    private void ItemInstall_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (!ItemInstall.Checked)
            return;
        frmDownloadInstall?.ExitSelectPage();
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。
    /// </summary>
    public PageSubType pageID = PageSubType.DownloadInstall;

    public PageDownloadLeft()
    {
        InitializeComponent();
        ItemInstall.Check += PageCheck;
        ItemMod.Check += PageCheck;
        ItemPack.Check += PageCheck;
        ItemDataPack.Check += PageCheck;
        ItemResourcePack.Check += PageCheck;
        ItemShader.Check += PageCheck;
        ItemWorld.Check += PageCheck;
        ItemFavorites.Check += PageCheck;
        ItemClient.Check += PageCheck;
        ItemOptiFine.Check += PageCheck;
        ItemForge.Check += PageCheck;
        ItemNeoForge.Check += PageCheck;
        ItemLiteLoader.Check += PageCheck;
        ItemFabric.Check += PageCheck;
        ItemLegacyFabric.Check += PageCheck;
        ItemLabyMod.Check += PageCheck;

        foreach (var item in new[]
                 {
                     ItemInstall, ItemMod, ItemPack, ItemDataPack, ItemResourcePack, ItemShader, ItemWorld,
                     ItemFavorites, ItemClient, ItemOptiFine, ItemForge, ItemNeoForge, ItemCleanroom, ItemFabric,
                     ItemLegacyFabric, ItemLabyMod, ItemLiteLoader
                 })
            foreach (var button in item.Buttons)
                button.Click += RefreshButton_Click;

        AnimatedControl = PanItem;
    }

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object sender, RouteEventArgs e)
    {
        if (sender is MyListItem { Tag: { } tag })
            PageChange((PageSubType)Convert.ToInt32(tag));
    }

    public MyPageRight PageGet(PageSubType id)
    {
        if (id == default)
            id = pageID;
        switch (id)
        {
            case PageSubType.DownloadInstall:
            {
                frmDownloadInstall ??= new PageDownloadInstall();
                return frmDownloadInstall;
            }
            case PageSubType.DownloadMod:
            {
                frmDownloadMod ??= new PageDownloadMod();
                return frmDownloadMod;
            }
            case PageSubType.DownloadPack:
            {
                frmDownloadPack ??= new PageDownloadPack();
                return frmDownloadPack;
            }
            case PageSubType.DownloadDataPack:
            {
                frmDownloadDataPack ??= new PageDownloadDataPack();
                return frmDownloadDataPack;
            }
            case PageSubType.DownloadResourcePack:
            {
                frmDownloadResourcePack ??= new PageDownloadResourcePack();
                return frmDownloadResourcePack;
            }
            case PageSubType.DownloadShader:
            {
                frmDownloadShader ??= new PageDownloadShader();
                return frmDownloadShader;
            }
            case PageSubType.DownloadWorld:
            {
                frmDownloadWorld ??= new PageDownloadWorld();
                return frmDownloadWorld;
            }
            case PageSubType.DownloadCompFavorites:
            {
                frmDownloadCompFavorites ??= new PageDownloadCompFavorites();
                return frmDownloadCompFavorites;
            }
            case PageSubType.DownloadClient:
            {
                frmDownloadClient ??= new PageDownloadClient();
                return frmDownloadClient;
            }
            case PageSubType.DownloadOptiFine:
            {
                frmDownloadOptiFine ??= new PageDownloadOptiFine();
                return frmDownloadOptiFine;
            }
            case PageSubType.DownloadForge:
            {
                frmDownloadForge ??= new PageDownloadForge();
                return frmDownloadForge;
            }
            case PageSubType.DownloadNeoForge:
            {
                frmDownloadNeoForge ??= new PageDownloadNeoForge();
                return frmDownloadNeoForge;
            }
            case PageSubType.DownloadCleanroom:
            {
                frmDownloadCleanroom ??= new PageDownloadCleanroom();
                return frmDownloadCleanroom;
            }
            case PageSubType.DownloadLiteLoader:
            {
                frmDownloadLiteLoader ??= new PageDownloadLiteLoader();
                return frmDownloadLiteLoader;
            }
            case PageSubType.DownloadFabric:
            {
                frmDownloadFabric ??= new PageDownloadFabric();
                return frmDownloadFabric;
            }
            case PageSubType.DownloadLabyMod:
            {
                frmDownloadLabyMod ??= new PageDownloadLabyMod();
                return frmDownloadLabyMod;
            }
            case PageSubType.DownloadLegacyFabric:
            {
                frmDownloadLegacyFabric ??= new PageDownloadLegacyFabric();
                return frmDownloadLegacyFabric;
            }

            default:
            {
                throw new Exception(Lang.Text("Download.Left.Error.UnknownSubPageType", (int)id));
            }
        }
    }

    private static void Log(Exception ex, string message) => LogWrapper.Error(ex, message);

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
            Log(ex, "切换分页面失败（ID " + (int)id + "）");
            Hint.Show(Lang.Text("Download.Error.OperationFailed"), HintType.Error);
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    #endregion
}
