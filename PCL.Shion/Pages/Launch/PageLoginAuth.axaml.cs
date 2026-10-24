using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using PCL.Shion.Controls;
using PCL.Shion.Core.Accounts;
using PCL.Shion.Localization;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Launch;

/// <summary>
/// 第三方验证登录页面（authlib-injector）：账号密码登录，或在服务器支持时使用 Yggdrasil Connect 的 OAuth 登录。
/// </summary>
public partial class PageLoginAuth : Grid
{
    /// <summary>从添加档案的弹窗传入的服务器地址。</summary>
    public static string? draggedAuthServer;

    /// <summary>从添加档案的弹窗传入的服务器是否支持 OAuth 登录。</summary>
    public static bool? draggedAuthServerOAuthSupported;

    // 预设服务器
    internal const string DefaultAuthServer = AuthlibAccount.DefaultServer;

    internal static readonly IReadOnlyDictionary<string, string> PredefinedAuthServers = new Dictionary<string, string>
    {
        { Lang.Text("Launch.Account.Auth.Preset.LittleSkin"), DefaultAuthServer },
        { Lang.Text("Common.Option.Customize"), "" }
    };

    private bool _isRegisterMode = true;
    private bool _isOAuthMode;
    private bool? _oauthSupported;
    private bool _hasRegisterLink;
    private bool _loginModeInitialized;
    private string _authServerUrl = "";

    public PageLoginAuth()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
        Loaded += (_, _) => ReloadRegisterButton();
        // Handles
        BtnBack.Click += (_, _) => BtnBack_Click();
        BtnBackOAuth.Click += (_, _) => BtnBack_Click();
        BtnLogin.Click += async (_, _) => await BtnLogin_Click();
        BtnOAuth.Click += async (_, _) => await BtnLogin_Click();
        BtnUsePassword.Click += (_, _) => _SetLoginMode(false);
        BtnUseOAuth.Click += (_, _) => _SetLoginMode(true);
        BtnWebsite.Click += (_, _) => BtnWebsite_Click();
        BtnPasswordWebsite.Click += (_, _) => BtnWebsite_Click();
        BtnLink.Click += (_, _) => Btn_Click();
    }

    /// <summary>检测服务器是否支持 Yggdrasil Connect（OAuth 登录）。</summary>
    internal static async Task<bool> IsOAuthSupportedAsync(string authServerUrl)
    {
        if (Uri.TryCreate(authServerUrl, UriKind.Absolute, out var serverUri) &&
            YggdrasilConnectAccount.TryGetBuiltInClientId(serverUri.Host, out _))
            return true;

        try
        {
            var server = await AuthlibAccount.ResolveApiLocationAsync(ProfileUi.Http, authServerUrl);
            var metadata = await AuthlibAccount.GetMetadataAsync(ProfileUi.Http, server);
            var discovery = AuthlibAccount.GetOpenIdDiscoveryAddress(metadata);
            if (string.IsNullOrWhiteSpace(discovery))
                return false;

            var account = new YggdrasilConnectAccount(ProfileUi.Http, discovery, null, server);
            await account.InitializeAsync();
            return true;
        }
        catch (Exception ex)
        {
            ProfileUi.Info($"检测 Yggdrasil Connect 支持失败：{ex.Message}");
            return false;
        }
    }

    private void Reload()
    {
        _authServerUrl = draggedAuthServer ?? "";
        var knownOAuthSupport = draggedAuthServerOAuthSupported;
        draggedAuthServer = null;
        draggedAuthServerOAuthSupported = null;
        _oauthSupported = knownOAuthSupport;
        _UpdateOAuthEntryVisibility();
        _SetLoginMode(knownOAuthSupport == true);
        if (knownOAuthSupport is null && !string.IsNullOrWhiteSpace(_authServerUrl))
            _ = RefreshOAuthSupportAsync(_authServerUrl);
    }

    private async Task RefreshOAuthSupportAsync(string server)
    {
        var supported = await IsOAuthSupportedAsync(server);
        if (!string.Equals(_authServerUrl, server, StringComparison.OrdinalIgnoreCase))
            return;
        _oauthSupported = supported;
        _UpdateOAuthEntryVisibility();
        _SetLoginMode(supported);
    }

    private void BtnBack_Click()
    {
        _authServerUrl = "";
        TextName.Text = null;
        TextPass.Text = null;
        ProfileUi.ReturnToProfileList();
    }

    private async Task BtnLogin_Click()
    {
        if (string.IsNullOrWhiteSpace(_authServerUrl))
        {
            Hint.Show(Lang.Text("Launch.Account.Auth.EmptyFields"), HintType.Error);
            return;
        }

        if (!IsHttpUrl(_authServerUrl))
        {
            Hint.Show(Lang.Text("Launch.Account.Auth.InvalidServer"), HintType.Error);
            return;
        }

        BtnLogin.IsEnabled = false;
        BtnOAuth.IsEnabled = false;
        BtnBack.IsEnabled = false;
        BtnBackOAuth.IsEnabled = false;
        BtnUsePassword.IsEnabled = false;
        BtnUseOAuth.IsEnabled = false;
        BtnWebsite.IsEnabled = false;
        BtnPasswordWebsite.IsEnabled = false;

        var keepControlsDisabled = false;
        try
        {
            ProfileService.IsCreatingProfile = true;
            if (_isOAuthMode)
            {
                if (!await _TryStartYggdrasilConnectAsync())
                {
                    Hint.Show(Lang.Text("Launch.Account.Auth.LoginFailed"), HintType.Error);
                    return;
                }

                keepControlsDisabled = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(TextName.Text) || string.IsNullOrWhiteSpace(TextPass.Text))
            {
                Hint.Show(Lang.Text("Launch.Account.Auth.EmptyFields"), HintType.Error);
                return;
            }

            var apiLocation = await AuthlibAccount.ResolveApiLocationAsync(ProfileUi.Http, _authServerUrl);
            var result = await AuthlibAccount.AuthenticateAsync(ProfileUi.Http, apiLocation, TextName.Text,
                TextPass.Text, ProfileUi.ChooseProfileAsync);
            ProfileService.ApplyLogin(result, null, select: true);
            ProfileUi.ReturnToProfileList();
        }
        catch (AccountException ex) when (ex.Error == "access_denied")
        {
            Hint.Show(Lang.Text("Launch.Account.Auth.Cancelled"));
        }
        catch (Exception ex)
        {
            if (ex.Message == "$$")
            {
            }
            else if (ex.Message.StartsWith('$'))
            {
                Hint.Show(Lang.Text("Launch.Account.Auth.LoginFailed.WithDetail", ex.Message.TrimStart('$')),
                    HintType.Error);
            }
            else
            {
                ProfileUi.ReportFailure(ex, Lang.Text("Launch.Account.Auth.LoginFailed"),
                    Lang.Text("Launch.Account.Auth.LoginFailed"), showMsgBox: true);
            }
        }
        finally
        {
            if (!keepControlsDisabled)
                _FinishLoginAttempt();
        }
    }

    private void _FinishLoginAttempt()
    {
        ProfileService.IsCreatingProfile = false;
        BtnLogin.IsEnabled = true;
        BtnOAuth.IsEnabled = true;
        BtnBack.IsEnabled = true;
        BtnBackOAuth.IsEnabled = true;
        BtnUsePassword.IsEnabled = true;
        BtnUseOAuth.IsEnabled = true;
        BtnWebsite.IsEnabled = true;
        BtnPasswordWebsite.IsEnabled = true;
        BtnLogin.Text = Lang.Text("Launch.Account.Auth.Login");
    }

    private void _SetLoginMode(bool useOAuth)
    {
        if (_loginModeInitialized && _isOAuthMode == useOAuth)
            return;

        var wasOAuth = _loginModeInitialized && _isOAuthMode;
        _isOAuthMode = useOAuth;
        _loginModeInitialized = true;

        var oldElements = wasOAuth
            ? new Control[] { PanOAuth, BtnUsePassword, BtnWebsite, BtnBackOAuth }
            : new Control[] { TextName, TextNameTitle, TextPass, TextPassTitle, BtnLink, BtnLogin, PanPasswordLinks, BtnBack };
        var newElements = useOAuth
            ? new Control[] { PanOAuth, BtnUsePassword, BtnWebsite, BtnBackOAuth }
            : new Control[] { TextName, TextNameTitle, TextPass, TextPassTitle, BtnLink, BtnLogin, PanPasswordLinks, BtnBack };

        var animations = new List<ModAnimation.AniData>();
        if (wasOAuth || !_loginModeInitialized)
        {
            foreach (var element in oldElements)
            {
                if (element.IsVisible)
                    animations.Add(ModAnimation.AaOpacity(element, -element.Opacity, 90));
            }
        }

        animations.Add(ModAnimation.AaCode(() =>
        {
            foreach (var element in oldElements)
            {
                element.IsVisible = false;
                element.Opacity = 1d;
            }

            foreach (var element in newElements)
            {
                element.IsVisible = !(ReferenceEquals(element, BtnLink) && !_hasRegisterLink);
                element.Opacity = 0d;
            }
        }, after: true));

        foreach (var element in newElements)
        {
            var targetOpacity = ReferenceEquals(element, BtnBackOAuth) || ReferenceEquals(element, PanPasswordLinks)
                ? 0.3d
                : 1d;
            animations.Add(ModAnimation.AaOpacity(element, targetOpacity, 130, 20,
                new ModAnimation.AniEaseInFluent()));
        }

        ModAnimation.AniStart(animations, "Profile Authentication Mode");
    }

    private void _UpdateOAuthEntryVisibility()
    {
        var isOAuthAvailable = _oauthSupported == true;
        BtnUseOAuth.IsVisible = isOAuthAvailable;
        Grid.SetColumnSpan(BtnPasswordWebsite, isOAuthAvailable ? 1 : 3);
    }

    private async Task<bool> _TryStartYggdrasilConnectAsync()
    {
        var server = await AuthlibAccount.ResolveApiLocationAsync(ProfileUi.Http, _authServerUrl);
        var metadata = await AuthlibAccount.GetMetadataAsync(ProfileUi.Http, server);
        var discovery = AuthlibAccount.GetOpenIdDiscoveryAddress(metadata);
        if (string.IsNullOrWhiteSpace(discovery))
            return false;

        var account = new YggdrasilConnectAccount(ProfileUi.Http, discovery, null, server);
        try
        {
            await account.InitializeAsync();
        }
        catch (AccountException)
        {
            return false;
        }

        ProfileUi.Info($"Yggdrasil Connect client id resolved for {new Uri(server).Host}");
        _ = _CompleteYggdrasilConnectLoginAsync(account);
        return true;
    }

    private async Task _CompleteYggdrasilConnectLoginAsync(YggdrasilConnectAccount account)
    {
        try
        {
            var result = await ProfileUi.LoginYggdrasilConnectAsync(account, null);
            var profile = ProfileService.ApplyLogin(result, null, select: true);
            ProfileUi.Info("Yggdrasil Connect 登录成功：" + profile.UserName);
            ProfileUi.ReturnToProfileList();
        }
        catch (OperationCanceledException)
        {
            Hint.Show(Lang.Text("Launch.Account.Auth.Cancelled"));
        }
        catch (Exception ex)
        {
            ProfileUi.ReportFailure(ex, Lang.Text("Launch.Account.Auth.LoginFailed"),
                Lang.Text("Launch.Account.Auth.LoginFailed"), showMsgBox: true);
        }
        finally
        {
            _FinishLoginAttempt();
        }
    }

    // 链接处理
    private void Btn_Click()
    {
        ProfileUi.OpenWebsite(_isRegisterMode
            ? AuthlibAccount.RegisterAddress
            : AuthlibAccount.RegisterAddress.Replace("/auth/register", "/auth/forgot"));
    }

    private void BtnWebsite_Click()
    {
        var websiteUri = new UriBuilder(new Uri(_authServerUrl))
        {
            Path = "/",
            Query = "",
            Fragment = ""
        }.Uri;
        ProfileUi.OpenWebsite(websiteUri.ToString());
    }

    // 切换注册按钮可见性
    private void ReloadRegisterButton()
    {
        _hasRegisterLink = IsHttpUrl(AuthlibAccount.RegisterAddress);
        BtnLink.IsVisible = !_isOAuthMode && _hasRegisterLink;
    }

    private static bool IsHttpUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
