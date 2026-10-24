using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PCL.Shion.Controls;
using PCL.Shion.Core.Accounts;
using PCL.Shion.Localization;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Launch;

/// <summary>
/// 当前档案的详情页：显示皮肤与玩家信息，悬停时显示皮肤、账号与切换档案等操作。
/// </summary>
public partial class PageLoginProfileSkin : Grid
{
    public PageLoginProfileSkin()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
        // Handles
        PanData.PointerEntered += (_, _) => ShowPanel();
        PanData.PointerExited += (_, _) => HidePanel();
        BtnSkin.Click += (_, _) => BtnSkin_Click();
        BtnEdit.Click += (_, _) => BtnEdit_Click();
        BtnSelect.Click += (_, _) => ChangeProfile();
        BtnSkin.ContextMenu!.Closed += (_, _) => MenuAccountOptions_Closed();
        BtnEdit.ContextMenu!.Closed += (_, _) => MenuAccountOptions_Closed();
        MenuChangeSkin.Click += (_, _) => Skin_Click();
        MenuSaveSkin.Click += (sender, _) => BtnSkinSave_Click(sender);
        MenuRefreshSkin.Click += (sender, _) => BtnSkinRefresh_Click(sender);
        MenuChangeCape.Click += (sender, _) => BtnSkinCape_Click(sender);
        MenuEditPassword.Click += (_, _) => BtnEditPassword_Click();
        MenuEditName.Click += (_, _) => BtnEditName_Click();
    }

    /// <summary>刷新页面显示的所有信息。</summary>
    public void Reload()
    {
        ProfileUi.Info("[Profile] 刷新档案界面");
        Skin.Clear();
        var profile = ProfileService.Current ?? throw new InvalidOperationException("No profile selected.");
        if (profile.ProfileType == ProfileType.Microsoft)
        {
            BtnEdit.IsVisible = true;
            ProfileUi.Info("[Profile] 使用正版皮肤加载器");
            Skin.loader = PageLaunchLeft.skinMs;
        }
        else if (profile.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect)
        {
            BtnEdit.IsVisible = true;
            ProfileUi.Info("[Profile] 使用 Authlib 皮肤加载器");
            Skin.loader = PageLaunchLeft.skinAuth;
        }
        else
        {
            BtnEdit.IsVisible = false;
            ProfileUi.Info("[Profile] 使用离线皮肤加载器");
            Skin.loader = PageLaunchLeft.skinLegacy;
        }

        Skin.loader.Start(isForceRestart: true);
        TextName.Text = profile.UserName;
        TextType.Text = ProfileUi.GetProfileInfo(profile);
    }

    #region 控制与编辑

    // 显示 / 隐藏控制
    private void ShowPanel()
    {
        ModAnimation.AniStart(ModAnimation.AaOpacity(PanButtons, 1d - PanButtons.Opacity, 120),
            "PageLoginProfileSkin Button");
    }

    private void HidePanel()
    {
        if (BtnEdit.ContextMenu!.IsOpen || BtnSkin.ContextMenu!.IsOpen || PanData.IsPointerOver)
            return;
        ModAnimation.AniStart(ModAnimation.AaOpacity(PanButtons, -PanButtons.Opacity, 120),
            "PageLoginProfileSkin Button");
    }

    private void MenuAccountOptions_Closed()
    {
        HidePanel();
    }

    // 皮肤与披风子菜单
    private void BtnSkin_Click()
    {
        BtnSkin.ContextMenu!.Open(BtnSkin);
    }

    // 账号信息子菜单
    private void BtnEdit_Click()
    {
        BtnEdit.ContextMenu!.Open(BtnEdit);
    }

    // 修改密码
    private void BtnEditPassword_Click()
    {
        var profile = ProfileService.Current;
        if (profile?.ProfileType == ProfileType.Microsoft)
        {
            ProfileUi.OpenWebsite("https://account.live.com/password/Change");
        }
        else if ((profile?.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect) &&
                 !string.IsNullOrWhiteSpace(profile.Server))
        {
            var server = profile.Server;
            ProfileUi.OpenWebsite(server.Replace("/api/yggdrasil/authserver" +
                                                 (server.EndsWith("/", StringComparison.Ordinal) ? "/" : ""),
                "/user/profile"));
        }
        else
        {
            Hint.Show(Lang.Text("Launch.Account.ProfileSkin.PasswordUnsupported"));
        }
    }

    // 修改 ID
    private void BtnEditName_Click()
    {
        _ = ProfileUi.EditProfileIdAsync();
    }

    // 选择档案
    private void ChangeProfile()
    {
        ProfileService.Select(null);
        ProfileUi.RefreshLaunchPage();
    }

    // 修改皮肤
    private void Skin_Click()
    {
        var profile = ProfileService.Current;
        if (profile?.ProfileType == ProfileType.Microsoft)
            _ = ProfileUi.ChangeSkinMsAsync();
        else if ((profile?.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect) &&
                 !string.IsNullOrWhiteSpace(profile.Server))
            ProfileUi.OpenWebsite(BeforeFirst(profile.Server, "api/yggdrasil/authserver") + "user/closet");
        else
            Hint.Show(Lang.Text("Launch.Account.ProfileSkin.SkinUnsupported"));
    }

    // 保存皮肤
    private void BtnSkinSave_Click(object? sender)
    {
        Skin.BtnSkinSave_Click(sender, new RoutedEventArgs());
    }

    // 刷新皮肤
    private void BtnSkinRefresh_Click(object? sender)
    {
        Skin.RefreshClick(sender, new RoutedEventArgs());
    }

    // 修改披风
    private void BtnSkinCape_Click(object? sender)
    {
        var profile = ProfileService.Current;
        if (profile?.ProfileType == ProfileType.Microsoft)
            Skin.BtnSkinCape_Click(sender, new RoutedEventArgs());
        else if ((profile?.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect) &&
                 !string.IsNullOrWhiteSpace(profile.Server))
            ProfileUi.OpenWebsite(BeforeFirst(profile.Server, "api/yggdrasil/authserver") + "user/closet");
        else
            Hint.Show(Lang.Text("Launch.Account.ProfileSkin.CapeUnsupported"));
    }

    /// <summary>取得 <paramref name="marker"/> 之前的部分；找不到时返回整个字符串。</summary>
    private static string BeforeFirst(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? text : text[..index];
    }

    #endregion
}
