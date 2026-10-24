using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using PCL.Shion.Controls;
using PCL.Shion.Core.Accounts;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Launch;

/// <summary>
/// 档案相关的界面流程：新建档案、登录弹窗、编辑档案、皮肤上传与错误提示。
/// 账号协议与令牌处理全部在 <c>PCL.Shion.Core.Accounts</c> 中完成，这里只负责交互。
/// </summary>
internal static class ProfileUi
{
    private const string MinecraftServicesEndpoint = "https://api.minecraftservices.com/minecraft/profile";
    private const string SkinUploadEndpoint = "https://api.minecraftservices.com/minecraft/profile/skins";
    private const string XboxStoreUrl = "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj";

    private static int _isMsSkinChanging;

    /// <summary>网络客户端，与下载共用同一个配置。</summary>
    public static HttpClient Http => App.Services.Engine.Http;

    /// <summary>完成或取消档案相关的操作后，回到启动页的档案列表。</summary>
    public static void ReturnToProfileList()
    {
        ProfileService.IsCreatingProfile = false;
        RefreshLaunchPage();
    }

    /// <summary>
    /// 刷新启动页左侧的内容，与 WPF 版 <c>ModMain.frmLaunchLeft.RefreshPage</c> 相同。
    /// <paramref name="loginType"/> 不为空时显示对应方式的登录页面。
    /// </summary>
    public static void RefreshLaunchPage(LoginType? loginType = null)
    {
        if (Navigator.Current == PageType.Launch && Navigator.PageLeft is PageLaunchLeft launch)
            launch.RefreshPage(true, loginType);
        else
            Navigator.Go(PageType.Launch);
    }

    /// <summary>记录日志；需要时显示提示或弹窗。</summary>
    public static void ReportFailure(Exception ex, string logMessage, string userSummary, bool showMsgBox)
    {
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(typeof(ProfileUi), "{Message}\n{Exception}", logMessage, ex);
        if (showMsgBox)
            _ = MsgBox.ShowAsync(userSummary, isWarn: true);
        else
            Hint.Show(userSummary, HintType.Error);
    }

    public static void Info(string message)
    {
        Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(typeof(ProfileUi), "{Message}", message);
    }

    public static void OpenWebsite(string url)
    {
        try
        {
            SystemShell.OpenUrl(url);
        }
        catch (Exception e) when (e is ArgumentException or Win32Exception or InvalidOperationException)
        {
            Hint.Show(Lang.Text("Application.Control.MessageBox.Error.OperationFailed"), HintType.Error);
        }
    }

    public static void CopyText(string text)
    {
        if (MainWindow.Current?.Clipboard is { } clipboard)
            _ = clipboard.SetTextAsync(text);
    }

    /// <summary>
    /// 第三方服务器的档案选择列表，选中的档案返回；取消时返回 null。
    /// </summary>
    public static async Task<AccountCandidate?> ChooseProfileAsync(IReadOnlyList<AccountCandidate> candidates)
    {
        if (candidates.Count == 0)
            return null;
        var controls = candidates.Select(candidate => (IMyRadio)new MyRadioBox { Text = candidate.Name }).ToList();
        var index = await MsgBox.SelectAsync(controls, Lang.Text("Launch.Account.Auth.ChangeProfile"));
        return index is { } selected && selected >= 0 && selected < candidates.Count ? candidates[selected] : null;
    }

    /// <summary>
    /// 处理微软登录中的账号类错误（未注册 Xbox、被封禁、地区限制、年龄限制、未购买、未创建档案），给出对应的操作指引。
    /// </summary>
    /// <returns>错误是否属于这些情况并已被处理。</returns>
    public static async Task<bool> HandleMicrosoftErrorAsync(AccountException ex)
    {
        switch (ex.Error.ToLowerInvariant())
        {
            case "xsts_2148916227":
                await MsgBox.ShowAsync(Lang.Text("Minecraft.Launch.Login.Microsoft.Banned"),
                    Lang.Text("Minecraft.Launch.Login.Failed"), Lang.Text("Minecraft.Launch.Login.IKnow"), isWarn: true);
                return true;
            case "xsts_2148916233":
                if (await MsgBox.ShowAsync(Lang.Text("Minecraft.Launch.Login.Microsoft.XboxNotRegistered"),
                        Lang.Text("Minecraft.Launch.Login.Hint"), Lang.Text("Minecraft.Launch.Login.Register"),
                        Lang.Text("Common.Action.Cancel")) == 1)
                    OpenWebsite("https://signup.live.com/signup");
                return true;
            case "xsts_2148916235":
                await MsgBox.ShowAsync(Lang.Text("Minecraft.Launch.Login.Microsoft.RegionBlocked"),
                    Lang.Text("Minecraft.Launch.Login.Failed"), Lang.Text("Minecraft.Launch.Login.IKnow"));
                return true;
            case "xsts_2148916238":
                if (await MsgBox.ShowAsync(Lang.Text("Minecraft.Launch.Login.Microsoft.Underage.Message"),
                        Lang.Text("Minecraft.Launch.Login.Hint"),
                        Lang.Text("Minecraft.Launch.Login.Microsoft.Underage.AgeOver13"),
                        Lang.Text("Minecraft.Launch.Login.Microsoft.Underage.AgeUnder13"),
                        Lang.Text("Common.Option.IDontKnow")) == 1)
                {
                    OpenWebsite("https://account.live.com/editprof.aspx");
                    await MsgBox.ShowAsync(Lang.Text("Minecraft.Launch.Login.Microsoft.ChangeBirthDate.Message"),
                        Lang.Text("Minecraft.Launch.Login.Hint"));
                }
                else
                {
                    OpenWebsite(
                        "https://support.microsoft.com/zh-cn/account-billing/如何更改-microsoft-帐户上的出生日期-837badbc-999e-54d2-2617-d19206b9540a");
                    await MsgBox.ShowAsync(Lang.Text("Minecraft.Launch.Login.Microsoft.ChangeBirthDate.SupportMessage"),
                        Lang.Text("Minecraft.Launch.Login.Hint"));
                }

                return true;
            case "minecraft_not_owned":
                if (await MsgBox.ShowAsync(Lang.Text("Minecraft.Launch.Login.Microsoft.NotPurchased"),
                        Lang.Text("Minecraft.Launch.Login.Failed"),
                        Lang.Text("Minecraft.Launch.Login.Microsoft.PurchaseMinecraft"),
                        Lang.Text("Common.Action.Cancel")) == 1)
                    OpenWebsite(XboxStoreUrl);
                return true;
            case "profile_not_created":
                if (await MsgBox.ShowAsync(Lang.Text("Minecraft.Launch.Login.Microsoft.CreateProfile.Message"),
                        Lang.Text("Minecraft.Launch.Login.Failed"),
                        Lang.Text("Minecraft.Launch.Login.Microsoft.CreateProfile.Button"),
                        Lang.Text("Common.Action.Cancel")) == 1)
                    OpenWebsite("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile");
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 显示设备码登录弹窗，并在弹窗中轮询授权结果。授权成功返回令牌；用户取消时抛出 <see cref="OperationCanceledException"/>。
    /// </summary>
    public static async Task<TokenResponse> ShowDeviceCodeLoginAsync(DeviceCodeInfo device,
        Func<CancellationToken, Task<TokenResponse?>> pollAsync)
    {
        var window = MainWindow.Current ?? throw new InvalidOperationException("主窗口不可用。");
        // 与 WPF 的 ForceWait 相同：等待当前弹窗关闭后再显示
        while (window.PanMsg.Children.Count > 0)
            await Task.Delay(100);

        var completion = new TaskCompletionSource<TokenResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var converter = new MyMsgLoginConverter
        {
            Device = device,
            DeviceCodePoll = pollAsync,
            CompletionHandler = result =>
            {
                switch (result)
                {
                    case TokenResponse token:
                        completion.TrySetResult(token);
                        break;
                    case OperationCanceledException:
                        completion.TrySetCanceled();
                        break;
                    case Exception exception:
                        completion.TrySetException(exception);
                        break;
                }
            }
        };
        if (window.PanMsgBackground is { } background)
            background.IsVisible = true;
        window.PanMsg.Children.Add(new MyMsgLogin(converter));
        return await completion.Task;
    }

    /// <summary>
    /// 微软登录。已有刷新令牌时先尝试刷新；刷新失败或没有令牌时走设备码流程。
    /// </summary>
    public static async Task<AccountLoginResult> LoginMicrosoftAsync(IProgress<double>? progress, McProfile? existing,
        CancellationToken cancellationToken = default)
    {
        var http = Http;
        if (existing?.RefreshToken is { Length: > 0 } refreshToken)
        {
            TokenResponse? refreshed = null;
            try
            {
                refreshed = await MicrosoftAccount.RefreshAsync(http, refreshToken, cancellationToken);
            }
            catch (AccountException e) when (!e.Error.StartsWith("xsts_", StringComparison.OrdinalIgnoreCase))
            {
                Info($"刷新微软令牌失败，将重新授权：{e.Error}");
            }

            if (refreshed is not null)
                return await MicrosoftAccount.CompleteAsync(http, refreshed, progress, cancellationToken);
        }

        var device = await MicrosoftAccount.RequestDeviceCodeAsync(http, cancellationToken);
        var oauth = await ShowDeviceCodeLoginAsync(device, token => MicrosoftAccount.PollDeviceCodeAsync(http, device, token));
        return await MicrosoftAccount.CompleteAsync(http, oauth, progress, cancellationToken);
    }

    /// <summary>
    /// Yggdrasil Connect 登录。已有刷新令牌时先尝试刷新，失败则走设备码流程。
    /// </summary>
    public static async Task<AccountLoginResult> LoginYggdrasilConnectAsync(YggdrasilConnectAccount account,
        McProfile? existing, CancellationToken cancellationToken = default)
    {
        await account.InitializeAsync(cancellationToken);
        if (existing?.RefreshToken is { Length: > 0 } refreshToken)
        {
            try
            {
                var refreshed = await account.RefreshAsync(refreshToken, cancellationToken);
                return await account.CompleteAsync(refreshed, ChooseProfileAsync, existing.Uuid, cancellationToken);
            }
            catch (AccountException e)
            {
                Info($"刷新 Yggdrasil Connect 令牌失败，将重新授权：{e.Error}");
            }
        }

        var device = await account.RequestDeviceCodeAsync(cancellationToken);
        var oauth = await ShowDeviceCodeLoginAsync(device,
            token => account.PollDeviceCodeAsync(device.DeviceCode, token));
        return await account.CompleteAsync(oauth, ChooseProfileAsync, existing?.Uuid, cancellationToken);
    }

    /// <summary>
    /// 新建档案：选择登录方式，第三方服务器需要先选择或输入服务器，然后切换到对应的登录页面。
    /// </summary>
    public static async Task CreateProfileAsync()
    {
        var includeOthers = CanCreateOtherProfile();
        var selected = await MsgBox.SelectAsync(GetAvailableProfileSelection(includeOthers),
            Lang.Text("Launch.Account.Profile.Create.SelectAuthType.Title"));
        if (selected is not { } choice)
            return;

        ProfileService.IsCreatingProfile = true;
        var type = choice switch
        {
            0 => LoginType.Microsoft,
            1 => LoginType.Authlib,
            _ => LoginType.Offline
        };
        if (type == LoginType.Authlib)
        {
            var server = await MsgBox.AuthServerAsync(AuthlibAccount.DefaultServer, PageLoginAuth.PredefinedAuthServers);
            if (string.IsNullOrWhiteSpace(server))
            {
                ProfileService.IsCreatingProfile = false;
                return;
            }

            PageLoginAuth.draggedAuthServerOAuthSupported = await PageLoginAuth.IsOAuthSupportedAsync(server);
            PageLoginAuth.draggedAuthServer = server;
        }

        RefreshLaunchPage(type);
    }

    public static string GetProfileInfo(McProfile profile)
    {
        var info = profile.ProfileType switch
        {
            ProfileType.Authlib => Lang.Text("Launch.Account.Type.ThirdParty") +
                                   (string.IsNullOrWhiteSpace(profile.ServerName) ? "" : $" / {profile.ServerName}"),
            ProfileType.YggdrasilConnect => Lang.Text("Launch.Account.Type.ThirdParty") +
                                            (string.IsNullOrWhiteSpace(profile.ServerName)
                                                ? " / Yggdrasil Connect"
                                                : $" / {profile.ServerName}"),
            ProfileType.Microsoft => Lang.Text("Launch.Account.Type.Microsoft"),
            _ => Lang.Text("Launch.Account.Type.Offline")
        };
        return string.IsNullOrWhiteSpace(profile.Description) ? info : $"{info}，{profile.Description}";
    }

    /// <summary>编辑玩家 ID（微软账号）或离线档案的 UUID 与名称。</summary>
    public static async Task EditProfileIdAsync()
    {
        var profile = ProfileService.Current;
        if (profile is null)
            return;

        if (profile.ProfileType == ProfileType.Microsoft)
        {
            var username = await MsgBox.InputAsync(
                Lang.Text("Launch.Account.Profile.EditPlayerId.Title"),
                Lang.Text("Launch.Account.Profile.EditPlayerId.MicrosoftWarning"),
                profile.UserName, ValidatePlayerName);
            if (string.IsNullOrWhiteSpace(username))
                return;
            if (await MsgBox.ShowAsync(Lang.Text("Launch.Account.Profile.EditPlayerId.Confirm.Message"),
                    Lang.Text("Launch.Account.Profile.EditPlayerId.Confirm.Title"), Lang.Text("Common.Action.Continue"),
                    Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                return;
            await ChangeMicrosoftPlayerIdAsync(profile, username);
            return;
        }

        if (profile.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect)
        {
            if (!string.IsNullOrWhiteSpace(profile.Server))
                OpenWebsite(profile.Server.Replace("/api/yggdrasil/authserver", "/user/profile"));
            return;
        }

        var newName = await MsgBox.InputAsync(Lang.Text("Launch.Account.Profile.EditPlayerId.Title"),
            Lang.Text("Launch.Account.Profile.EditPlayerId.Hint"), profile.UserName, ValidatePlayerName);
        if (!string.IsNullOrWhiteSpace(newName))
            await EditOfflineUuidAsync(profile, OfflineUuid.Standard(newName));
    }

    /// <summary>修改离线档案的 UUID。<paramref name="uuid"/> 为 null 时询问用户。</summary>
    public static async Task EditOfflineUuidAsync(McProfile profile, string? uuid = null)
    {
        var current = ProfileService.Profiles.FirstOrDefault(item => item.ProfileId == profile.ProfileId);
        if (current is null)
            return;

        var newUuid = uuid;
        if (newUuid is null)
        {
            var type = await MsgBox.SelectAsync(
            [
                new MyRadioBox { Text = Lang.Text("Launch.Account.Profile.Uuid.Standard") },
                new MyRadioBox { Text = Lang.Text("Launch.Account.Profile.Uuid.Legacy") },
                new MyRadioBox { Text = Lang.Text("Common.Option.Customize") }
            ], Lang.Text("Launch.Account.Profile.Uuid.SelectType.Title"));
            if (type is not { } choice)
                return;

            newUuid = choice switch
            {
                0 => OfflineUuid.Standard(current.UserName),
                1 => OfflineUuid.Legacy(current.UserName),
                _ => await MsgBox.InputAsync(
                    Lang.Text("Launch.Account.Profile.Uuid.ChangeTitle", current.UserName),
                    Lang.Text("Launch.Account.Profile.Uuid.Hint"), current.Uuid, ValidateUuid)
            };
        }

        if (string.IsNullOrWhiteSpace(newUuid))
            return;
        var updated = current.Clone();
        updated.Uuid = newUuid;
        ProfileService.Update(current, updated);
        ProfileService.Select(updated);
        Hint.Show(Lang.Text("Launch.Account.Profile.Saved"), HintType.Success);
    }

    public static void EditAuthServerName(McProfile profile, string serverName)
    {
        var current = ProfileService.Profiles.FirstOrDefault(item => item.ProfileId == profile.ProfileId);
        if (current is null)
            return;
        var updated = current.Clone();
        updated.ServerName = serverName;
        ProfileService.Update(current, updated);
        if (ProfileService.Current?.ProfileId == current.ProfileId)
            ProfileService.Select(updated);
        Hint.Show(Lang.Text("Launch.Account.Profile.Saved"), HintType.Success);
    }

    public static void RemoveProfile(McProfile profile)
    {
        ProfileService.Remove(profile);
        Hint.Show(Lang.Text("Launch.Account.Profile.Deleted"), HintType.Success);
    }

    /// <summary>修改微软正版档案的皮肤：选择文件、选择皮肤类型并上传，成功后刷新皮肤缓存。</summary>
    public static async Task ChangeSkinMsAsync()
    {
        if (Interlocked.Exchange(ref _isMsSkinChanging, 1) != 0)
        {
            Hint.Show(Lang.Text("Launch.Skin.Change.Busy"));
            return;
        }

        try
        {
            var profile = ProfileService.Current;
            if (profile is null)
                return;
            var skin = await SelectSkinAsync();
            if (skin is null)
                return;

            Hint.Show(Lang.Text("Launch.Skin.Change.Starting"));
            var profileId = profile.ProfileId;
            var latest = await EnsureMicrosoftLoginAsync(profileId);
            var hasRetriedAuthentication = false;
            while (true)
            {
                var (needsAuthentication, skinUrl) = await UploadSkinAsync(latest, skin);
                if (needsAuthentication)
                {
                    if (hasRetriedAuthentication)
                        throw new AccountException("invalid_token", "Microsoft authentication remained invalid.");
                    hasRetriedAuthentication = true;
                    Hint.Show(Lang.Text("Launch.Skin.Change.Reauthenticating"));
                    latest = await EnsureMicrosoftLoginAsync(profileId);
                    continue;
                }

                if (string.IsNullOrEmpty(skinUrl))
                    throw new InvalidDataException("皮肤上传没有返回可用的皮肤地址。");
                MySkin.ReloadCache(skinUrl, latest.Uuid);
                break;
            }
        }
        catch (Exception ex)
        {
            ReportFailure(ex, "皮肤上传失败", Lang.Text("Launch.Account.Profile.Error.ChangeSkin"), showMsgBox: false);
        }
        finally
        {
            Interlocked.Exchange(ref _isMsSkinChanging, 0);
        }
    }

    /// <summary>重新登录当前的微软档案，并把结果写回档案列表；返回更新后的档案。</summary>
    private static async Task<McProfile> EnsureMicrosoftLoginAsync(string profileId)
    {
        var existing = ProfileService.Profiles.FirstOrDefault(item => item.ProfileId == profileId)
                       ?? throw new InvalidOperationException("Microsoft profile no longer exists.");
        var result = await LoginMicrosoftAsync(null, existing);
        return ProfileService.ApplyLogin(result, existing, select: true);
    }

    private static async Task ChangeMicrosoftPlayerIdAsync(McProfile profile, string username)
    {
        try
        {
            var check = await SendMinecraftServicesAsync(HttpMethod.Get,
                $"{MinecraftServicesEndpoint}/name/{username}/available", profile.AccessToken);
            var status = JsonString(check.Json, "status");
            if (status is "DUPLICATE" or "NOT_ALLOWED")
            {
                await MsgBox.ShowAsync(status == "DUPLICATE"
                        ? Lang.Text("Launch.Account.Profile.EditPlayerId.Duplicate")
                        : Lang.Text("Launch.Account.Profile.EditPlayerId.NotAllowed"),
                    Lang.Text("Launch.Account.Profile.EditPlayerId.Failed.Title"), Lang.Text("Common.Action.Confirm"),
                    isWarn: true);
                return;
            }

            var result = await SendMinecraftServicesAsync(HttpMethod.Put,
                $"{MinecraftServicesEndpoint}/name/{username}", profile.AccessToken);
            if (result.Status == HttpStatusCode.Forbidden)
            {
                await MsgBox.ShowAsync(Lang.Text("Launch.Account.Profile.EditPlayerId.Cooldown"),
                    Lang.Text("Launch.Account.Profile.EditPlayerId.Failed.Title"), Lang.Text("Common.Action.Confirm"));
                return;
            }

            if (!result.IsSuccess)
                throw new HttpRequestException($"改名失败（HTTP {(int)result.Status}）。", null, result.Status);

            var updated = profile.Clone();
            updated.UserName = JsonString(result.Json, "name") ?? username;
            ProfileService.Update(profile, updated);
            ProfileService.Select(updated);
            Hint.Show(Lang.Text("Launch.Account.Profile.EditPlayerId.Success", updated.UserName), HintType.Success);
        }
        catch (Exception ex)
        {
            ReportFailure(ex, "修改玩家 ID 失败", Lang.Text("Launch.Account.Profile.Error.ChangeId"), showMsgBox: true);
        }
    }

    private sealed record McServiceResponse(HttpStatusCode Status, JsonNode? Json)
    {
        public bool IsSuccess => (int)Status is >= 200 and < 300;
    }

    private static async Task<McServiceResponse> SendMinecraftServicesAsync(HttpMethod method, string url, string accessToken)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return new McServiceResponse(response.StatusCode, JsonParseOrNull(body));
    }

    private static JsonNode? JsonParseOrNull(string body)
    {
        try
        {
            return JsonNode.Parse(body);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string? JsonString(JsonNode? node, string name)
    {
        return node?[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;
    }

    private static async Task<(bool NeedsAuthentication, string? SkinUrl)> UploadSkinAsync(McProfile profile, SkinSelection skin)
    {
        using var contents = new MultipartFormDataContent
        {
            { new StringContent(skin.IsSlim ? "slim" : "classic"), "variant" },
            { new ByteArrayContent(await File.ReadAllBytesAsync(skin.FilePath)), "file", Path.GetFileName(skin.FilePath) }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, SkinUploadEndpoint) { Content = contents };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.AccessToken);
        using var response = await Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (body.Contains("request requires user authentication", StringComparison.OrdinalIgnoreCase))
            return (true, null);

        var json = JsonParseOrNull(body);
        var active = json?["skins"] is JsonArray skins
            ? skins.FirstOrDefault(item => JsonString(item, "state") == "ACTIVE")
            : null;
        var url = JsonString(active, "url");
        if (string.IsNullOrEmpty(url))
            throw new InvalidDataException(JsonString(json, "errorMessage") ?? body);
        return (false, url);
    }

    private sealed record SkinSelection(string FilePath, bool IsSlim);

    private static async Task<SkinSelection?> SelectSkinAsync()
    {
        var window = MainWindow.Current;
        if (window is null)
            return null;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Lang.Text("Launch.Skin.FileDialog.Title"),
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(Lang.Text("Launch.Skin.FileDialog.Filter")) { Patterns = ["*.png"] }]
        });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (string.IsNullOrEmpty(path))
            return null;

        try
        {
            using (var bitmap = new Bitmap(path))
            {
                if (bitmap.PixelSize.Width != 64 || bitmap.PixelSize.Height is not (32 or 64))
                {
                    Hint.Show(Lang.Text("Launch.Skin.InvalidSize"), HintType.Error);
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            ReportFailure(ex, "无法读取皮肤文件", Lang.Text("Launch.Skin.File.Error"), showMsgBox: false);
            return null;
        }

        var size = new FileInfo(path).Length;
        if (size > 24 * 1024)
        {
            Hint.Show(Lang.Text("Launch.Skin.FileTooLarge", (size / 1024d).ToString("N2", Lang.Culture)), HintType.Error);
            return null;
        }

        var model = await MsgBox.ShowAsync(Lang.Text("Launch.Skin.Model.SelectMessage"),
            Lang.Text("Launch.Skin.Model.SelectTitle"), Lang.Text("Launch.Skin.Model.Steve"),
            Lang.Text("Launch.Skin.Model.Alex"), Lang.Text("Common.Option.IDontKnow"));
        if (model == 3)
        {
            Hint.Show(Lang.Text("Launch.Skin.Model.UnknownHint"));
            return null;
        }

        return new SkinSelection(path, model == 2);
    }

    private static IReadOnlyList<IMyRadio> GetAvailableProfileSelection(bool includeOthers)
    {
        var items = new List<IMyRadio>
        {
            new MyListItem { Title = Lang.Text("Launch.Account.Type.Microsoft"), Type = MyListItem.CheckType.RadioBox, SvgIcon = "lucide/shield-check" }
        };
        if (includeOthers)
        {
            items.Add(new MyListItem { Title = Lang.Text("Launch.Account.Type.ThirdParty"), Type = MyListItem.CheckType.RadioBox, SvgIcon = "lucide/network" });
            items.Add(new MyListItem { Title = Lang.Text("Launch.Account.Type.Offline"), Type = MyListItem.CheckType.RadioBox, SvgIcon = "lucide/link-2-off" });
        }

        return items;
    }

    private static bool CanCreateOtherProfile()
    {
        return ProfileService.HasMicrosoftProfile || ProfileService.Profiles.Count > 0 || !NetworkInterface.GetIsNetworkAvailable();
    }

    private static string? ValidatePlayerName(string name)
    {
        return name.Length is >= 3 and <= 16 && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
            ? null
            : Lang.Text("Launch.Account.Profile.EditPlayerId.Hint");
    }

    private static string? ValidateUuid(string uuid)
    {
        return uuid.Length == 32 && uuid.All(char.IsAsciiHexDigit)
            ? null
            : Lang.Text("Launch.Account.Profile.Uuid.InvalidChars");
    }
}
