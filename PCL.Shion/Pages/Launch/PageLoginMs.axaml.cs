using System;
using System.Security.Authentication;
using System.Threading.Tasks;
using Avalonia.Controls;
using PCL.Shion.Core.Accounts;
using PCL.Shion.Localization;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Launch;

/// <summary>
/// 微软正版登录页面：点击开始后通过设备码完成登录。
/// </summary>
public partial class PageLoginMs : StackPanel
{
    private const string XboxStoreUrl = "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj";
    private const string MinecraftWebsite = "https://www.minecraft.net/zh-hans";

    public PageLoginMs()
    {
        InitializeComponent();
        BtnBack.Click += (_, _) => ProfileUi.ReturnToProfileList();
        BtnLogin.Click += async (_, _) => await BtnLogin_Click();
        BtnPurchase.Click += (_, _) => ProfileUi.OpenWebsite(XboxStoreUrl);
        BtnWebsite.Click += (_, _) => ProfileUi.OpenWebsite(MinecraftWebsite);
    }

    private async Task BtnLogin_Click()
    {
        BtnLogin.IsEnabled = false;
        BtnBack.IsVisible = false;
        BtnLogin.Text = FormatProgress(0d);

        var previousProfile = ProfileService.Current;
        var loginSucceeded = false;
        try
        {
            ProfileService.Select(null);
            var progress = new Progress<double>(value => BtnLogin.Text = FormatProgress(value));
            var result = await ProfileUi.LoginMicrosoftAsync(progress, null);
            ProfileService.ApplyLogin(result, null, select: true);
            loginSucceeded = true;
            ProfileUi.ReturnToProfileList();
        }
        catch (OperationCanceledException)
        {
            if (!loginSucceeded && previousProfile is not null)
                ProfileService.Select(previousProfile);
            Hint.Show(Lang.Text("Launch.Account.LoginCancelled"));
        }
        catch (Exception ex)
        {
            if (!loginSucceeded && previousProfile is not null)
                ProfileService.Select(previousProfile);
            // 已知的账号错误（未购买、未注册 Xbox 等）给出操作指引，其余错误才显示通用提示
            if (!(ex is AccountException account && await ProfileUi.HandleMicrosoftErrorAsync(account)))
                ShowLoginError(ex);
        }
        finally
        {
            ProfileService.IsCreatingProfile = false;
            BtnLogin.IsEnabled = true;
            BtnBack.IsVisible = true;
            BtnLogin.Text = Lang.Text("Launch.Account.Login");
        }
    }

    private static void ShowLoginError(Exception ex)
    {
        if (ex.Message == "$$")
            return;
        if (ex.Message.StartsWith('$'))
        {
            Hint.Show(Lang.Text("Launch.Account.Microsoft.LoginFailed.WithDetail", ex.Message.TrimStart('$')),
                HintType.Error);
            return;
        }

        if (ex is AuthenticationException && ex.Message.Contains("SSL/TLS", StringComparison.Ordinal))
        {
            ProfileUi.ReportFailure(ex,
                $"{Lang.Text("Launch.Account.Microsoft.LoginFailed.Message")}\r\n{ex.Message}",
                Lang.Text("Launch.Account.Microsoft.Error.OperationFailed"), showMsgBox: true);
            return;
        }

        ProfileUi.ReportFailure(ex, Lang.Text("Launch.Account.Microsoft.LoginFailed.Title"),
            Lang.Text("Launch.Account.Microsoft.LoginFailed.Title"), showMsgBox: true);
    }

    private static string FormatProgress(double value) => value.ToString("P0", Lang.Culture);
}
