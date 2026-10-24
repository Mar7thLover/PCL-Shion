using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using PCL.Shion.Controls;
using PCL.Shion.Core.Accounts;
using PCL.Shion.Localization;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Launch;

/// <summary>
/// 档案列表页面：列出所有档案，选择档案、编辑 UUID 或删除档案，并进入新建档案的流程。
/// </summary>
public partial class PageLoginProfile : Grid
{
    public PageLoginProfile()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
        BtnNew.Click += async (_, _) => await BtnNew_Click();
    }

    /// <summary>刷新页面显示的所有信息。</summary>
    public void Reload()
    {
        RefreshProfileList();
        PageLaunchLeft.frmLoginProfileSkin = null;
    }

    /// <summary>刷新档案列表。</summary>
    public void RefreshProfileList()
    {
        ProfileUi.Info("[Profile] 刷新档案列表");
        PanList.Children.Clear();
        ProfileService.Load();
        try
        {
            var profiles = ProfileService.Profiles;
            foreach (var profile in profiles)
                PanList.Children.Add(CreateProfileItem(profile));
            HintMicrosoft.IsVisible = profiles.Count == 0;
            ProfileUi.Info("[Profile] 档案列表刷新完成");
        }
        catch (Exception ex)
        {
            ProfileUi.ReportFailure(ex, "读取档案列表失败", Lang.Text("Launch.Account.Profile.Error.Read"), showMsgBox: true);
        }

        HintCreate.IsVisible = !ProfileService.Profiles.Any();
    }

    private MyListItem CreateProfileItem(McProfile profile)
    {
        var item = new MyListItem
        {
            Margin = new Thickness(8, 2, 10, 2),
            Title = profile.UserName,
            Info = ProfileUi.GetProfileInfo(profile),
            Type = MyListItem.CheckType.Clickable,
            Tag = profile
        };

        var logoPath = Path.Combine(ProfileService.DataDirectory, "Cache", "Skin", "Head", $"{profile.SkinHeadId}.png");
        if (File.Exists(logoPath) && new FileInfo(logoPath).Length != 0L)
        {
            item.Logo = logoPath;
            item.SvgIcon = string.Empty;
        }
        else
        {
            item.Logo = string.Empty;
            item.SvgIcon = "lucide/user";
        }

        item.Click += (_, _) => SelectProfile(profile);
        ProfileContMenuBuild(item, profile);
        return item;
    }

    private void SelectProfile(McProfile profile)
    {
        ProfileService.Select(profile);
        ProfileUi.Info($"[Profile] 选定档案: {profile.UserName}, 以 {profile.ProfileType} 方式验证");
        ProfileUi.RefreshLaunchPage();
    }

    /// <summary>为档案列表项创建右侧的操作按钮：离线档案可修改 UUID，其他档案可复制 UUID；都可以删除。</summary>
    private void ProfileContMenuBuild(MyListItem sender, McProfile profile)
    {
        // 更改 UUID
        var btnEditUuid = CreateActionButton("lucide/pencil", Lang.Text("Launch.Account.Profile.ChangeUuid"));
        btnEditUuid.Click += async (_, _) => await ProfileUi.EditOfflineUuidAsync(profile);
        // 复制 UUID
        var btnCopyUuid = CreateActionButton("lucide/copy", Lang.Text("Launch.Account.Profile.CopyUuid"));
        btnCopyUuid.Click += (_, _) => ProfileUi.CopyText(profile.Uuid);
        // 删除档案
        var btnDelete = CreateActionButton("lucide/trash-2", Lang.Text("Launch.Account.Profile.Delete"));
        btnDelete.Click += async (_, _) => await DeleteProfile(profile);

        // 根据档案类型显示不同的菜单项
        sender.Buttons = profile.ProfileType == ProfileType.Offline
            ? new[] { btnEditUuid, btnDelete }
            : new[] { btnCopyUuid, btnDelete };
    }

    private static MyIconButton CreateActionButton(string svgIcon, string tooltip)
    {
        var button = new MyIconButton { SvgIcon = svgIcon };
        ToolTip.SetTip(button, tooltip);
        ToolTip.SetPlacement(button, PlacementMode.Center);
        ToolTip.SetVerticalOffset(button, 30d);
        ToolTip.SetHorizontalOffset(button, 2d);
        return button;
    }

    private async Task BtnNew_Click()
    {
        await ProfileUi.CreateProfileAsync();
        RefreshProfileList();
    }

    private async Task DeleteProfile(McProfile profile)
    {
        if (await MsgBox.ShowAsync(Lang.Text("Launch.Account.Profile.DeleteConfirm.Message"),
                Lang.Text("Launch.Account.Profile.DeleteConfirm.Title"), Lang.Text("Common.Action.Continue"),
                Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
            return;
        ProfileUi.RemoveProfile(profile);
        RefreshProfileList();
    }
}
