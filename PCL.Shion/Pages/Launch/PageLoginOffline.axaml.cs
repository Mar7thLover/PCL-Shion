using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using PCL.Shion.Core.Accounts;
using PCL.Shion.Localization;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Launch;

/// <summary>
/// 离线档案的创建页面：输入玩家 ID，选择 UUID 生成方式。
/// </summary>
public partial class PageLoginOffline : Grid
{
    private static readonly Regex UsernamePattern = new("^[A-z0-9_]{3,16}$");
    private static readonly Regex UuidPattern = new("^[a-fA-F0-9]{32}$");

    public PageLoginOffline()
    {
        InitializeComponent();
        BtnBack.Click += (_, _) => BtnBack_Click();
        RadioUuidCustom.Check += (_, _) => RadioUuid_Checked();
        RadioUuidStandard.Check += (_, _) => RadioUuid_Checked();
        RadioUuidLegacy.Check += (_, _) => RadioUuid_Checked();
        BtnLogin.Click += async (_, _) => await BtnLogin_Click();
    }

    private void BtnBack_Click()
    {
        ProfileUi.ReturnToProfileList();
    }

    private void RadioUuid_Checked()
    {
        var custom = RadioUuidCustom.Checked;
        TextUuidTitle.IsVisible = custom;
        TextUuid.IsVisible = custom;
    }

    private async Task BtnLogin_Click()
    {
        // 玩家 ID 输入检查
        var username = TextName.Text ?? string.Empty;
        if (!UsernamePattern.IsMatch(username))
        {
            if (await MsgBox.ShowAsync(Lang.Text("Launch.Account.Offline.InvalidPlayerId.Message"),
                    Lang.Text("Launch.Account.Offline.InvalidPlayerId.Title"), Lang.Text("Common.Action.Continue"),
                    Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                return;
        }

        // UUID
        string userUuid;
        if (RadioUuidCustom.Checked)
        {
            // 自定义输入检查
            var uuidInput = (TextUuid.Text ?? string.Empty).Replace("-", "");
            if (!UuidPattern.IsMatch(uuidInput))
            {
                Hint.Show(Lang.Text("Launch.Account.Offline.InvalidUuid",
                    Lang.Text("Launch.Account.Profile.Uuid.InvalidChars")), HintType.Error);
                return;
            }

            userUuid = uuidInput;
        }
        else if (RadioUuidLegacy.Checked)
        {
            userUuid = OfflineUuid.Legacy(username);
        }
        else
        {
            userUuid = OfflineUuid.Standard(username);
        }

        // 创建档案
        var newProfile = new McProfile
        {
            ProfileType = ProfileType.Offline,
            Uuid = userUuid,
            UserName = username,
            Description = "",
            AccessToken = userUuid,
            ClientToken = userUuid,
            ProfileId = Guid.NewGuid().ToString("N")
        };
        ProfileService.Add(newProfile);
        Hint.Show(Lang.Text("Launch.Account.Profile.Created"), HintType.Success);
        ProfileUi.ReturnToProfileList();
    }
}
