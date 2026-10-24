using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using PCL.Shion.Controls;
using PCL.Shion.Core.Accounts;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;
using SkiaSharp;

namespace PCL.Shion.Pages.Launch;

public partial class MySkin : Grid
{
    public delegate void ClickEventHandler(object? sender, PointerReleasedEventArgs e);

    // 皮肤储存
    private bool isChanging;

    // 点击
    private bool isSkinMouseDown;
    public ModLoader.LoaderTask<PageLaunchLeft.SkinInput, string> loader = null!;

    private string address = "";

    public MySkin()
    {
        InitializeComponent();
        PointerEntered += PanSkin_MouseEnter;
        PointerExited += PanSkin_MouseLeave;
        PointerPressed += PanSkin_MouseLeftButtonDown;
        PointerReleased += PanSkin_MouseLeftButtonUp;
        // Handles
        BtnSkinSave.Click += BtnSkinSave_Click;
        BtnSkinRefresh.Click += RefreshClick;
        BtnSkinCape.Click += BtnSkinCape_Click;
        // 右键菜单打开时刷新“另存为”的可用状态，对应 WPF 中 BtnSkinSave.Checked
        if (ContextMenu is { } menu)
            menu.Opened += (_, _) => BtnSkinSave_Checked(BtnSkinSave, new RoutedEventArgs());
    }

    public string Address
    {
        get => address;
        set
        {
            address = value;
            ToolTip.SetTip(this, string.IsNullOrEmpty(address)
                ? Lang.Text("Common.State.Loading")
                : Lang.Text("Launch.Skin.Change.ToolTip"));
        }
    }

    // 披风
    public bool HasCape
    {
        get => !BtnSkinCape.IsVisible;
        set => BtnSkinCape.IsVisible = value;
    }

    // 事件
    public event ClickEventHandler? Click;

    // 阴影效果，对应 XAML 中 Grid.Effect 里的 DropShadowEffect
    private DropShadowEffect ShadowSkin => (DropShadowEffect)Effect!;

    // 控件动画
    private void PanSkin_MouseEnter(object? sender, PointerEventArgs e)
    {
        ModAnimation.AniStart(ModAnimation.AaOpacity(ShadowSkin, 0.8d - ShadowSkin.Opacity, 200, 100), "Skin Shadow");
    }

    private void PanSkin_MouseLeave(object? sender, PointerEventArgs e)
    {
        ModAnimation.AniStart(ModAnimation.AaOpacity(ShadowSkin, 0.2d - ShadowSkin.Opacity, 200), "Skin Shadow");
        isSkinMouseDown = false;
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(this, 1d - ((ScaleTransform)RenderTransform!).ScaleX, 60,
                ease: new ModAnimation.AniEaseOutFluent()), "Skin Scale");
    }

    private void PanSkin_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        isSkinMouseDown = true;
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(this, 0.9d - ((ScaleTransform)RenderTransform!).ScaleX, 60,
                ease: new ModAnimation.AniEaseOutFluent()), "Skin Scale");
    }

    private void PanSkin_MouseLeftButtonUp(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(this, 1d - ((ScaleTransform)RenderTransform!).ScaleX, 60,
                ease: new ModAnimation.AniEaseOutFluent()), "Skin Scale");
        if (!isSkinMouseDown) return;
        isSkinMouseDown = false;
        Click?.Invoke(sender, e);
    }

    // 保存皮肤
    public void BtnSkinSave_Click(object? sender, RoutedEventArgs e)
    {
        _ = SaveAsync(loader);
    }

    public static async Task SaveAsync(ModLoader.LoaderTask<PageLaunchLeft.SkinInput, string> loader)
    {
        var address = loader.output ?? "";
        if (loader.State != LoadState.Finished)
        {
            HintService.Hint(Lang.Text("Launch.Skin.Fetching"), HintType.Error);
            if (loader.State != LoadState.Loading)
                loader.Start();
            return;
        }

        try
        {
            var top = TopLevel.GetTopLevel(MainWindow.Current);
            if (top is null)
                return;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Lang.Text("Launch.Skin.SaveDialog.Title"),
                SuggestedFileName = Path.GetFileName(address),
                FileTypeChoices = [FilePickerFileTypes.ImagePng]
            });
            var fileAddress = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(fileAddress)) return;
            File.Delete(fileAddress);
            if (address.StartsWith("avares://"))
            {
                using var image = new SkinBitmap(address);
                image.Save(fileAddress);
            }
            else
            {
                File.Copy(address, fileAddress);
            }

            HintService.Hint(Lang.Text("Launch.Skin.SaveSuccess"), HintType.Success);
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Launch.Skin.Save.Error"), HintType.Error);
        }
    }

    private void BtnSkinSave_Checked(object? sender, RoutedEventArgs e)
    {
        ((MenuItem)sender!).IsEnabled = string.IsNullOrEmpty(Address);
    }

    /// <summary>
    ///     载入皮肤。
    /// </summary>
    public void Load()
    {
        try
        {
            // 检查文件存在
            Address = loader.output ?? "";
            if (string.IsNullOrEmpty(Address))
                throw new InvalidDataException("皮肤加载器 " + loader.name + " 没有输出");
            if (!Address.StartsWith("avares://") && !File.Exists(Address))
                throw new FileNotFoundException("皮肤文件未找到", Address);
            // 加载
            SkinBitmap image;
            try
            {
                image = new SkinBitmap(Address);
            }
            catch (Exception) // #2272
            {
                HintService.Hint(Lang.Text("Launch.Skin.Load.Error.Corrupted", Address), HintType.Error);
                if (!Address.StartsWith("avares://"))
                    File.Delete(Address);
                return;
            }

            using (image)
            {
                ImgBack.Tag = Address;
                // 大小检查
                var scale = (int)Math.Round(image.Width / 64d);
                if (image.Width < 32 || image.Height < 32)
                {
                    ImgFore.Source = null;
                    ImgBack.Source = null;
                    throw new InvalidDataException("图片大小不足，长为 " + image.Height + "，宽为 " + image.Width);
                }

                // 头发层（附加层）
                if (image.Width >= 64 && image.Height >= 32)
                {
                    var head = image.Pixel(scale * 41, scale * 9);
                    if (image.Pixel(1, 1).A == 0 ||
                        image.Pixel(image.Width - 1, image.Height - 1).A == 0 ||
                        image.Pixel(image.Width - 2, (int)Math.Round(image.Height / 2d - 2d)).A == 0 ||
                        (image.Pixel(1, 1) != head &&
                         image.Pixel(image.Width - 1, image.Height - 1) != head &&
                         image.Pixel(image.Width - 2, (int)Math.Round(image.Height / 2d - 2d)) != head)) // 如果图片中有任何透明像素（避免纯色白底）
                        // 或是头部颜色和透明区均不一样
                    {
                        ImgFore.Source = image.Clip(scale * 40, scale * 8, scale * 8, scale * 8);
                    }
                    else
                    {
                        ImgFore.Source = null;
                    }
                }
                else
                {
                    ImgFore.Source = null;
                }

                // 脸层
                ImgBack.Source = image.Clip(scale * 8, scale * 8, scale * 8, scale * 8);
                // 用于显示档案列表头像的图片
                var skinHeadId = Between(Address, Address.Contains("Images/Skins/") ? "Skins/" : @"Skin\", ".png");
                var cachePath = Path.Combine(TempRoot, "Cache", "Skin", "Head", $"{skinHeadId}.png");
                using var completeHead = new SKBitmap(56, 56);
                using (var canvas = new SKCanvas(completeHead))
                {
                    canvas.Clear(SKColors.Transparent);
                    using var paint = new SKPaint { FilterQuality = SKFilterQuality.None };
                    using (var faceBitmap = image.CropSk(scale * 8, scale * 8, scale * 8, scale * 8))
                        canvas.DrawBitmap(faceBitmap, new SKRect(4, 4, 52, 52), paint);

                    if (ImgFore.Source is not null)
                    {
                        using var hairBitmap = image.CropSk(scale * 40, scale * 8, scale * 8, scale * 8);
                        canvas.DrawBitmap(hairBitmap, new SKRect(0, 0, 56, 56), paint);
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                SkinBitmap.SaveTo(completeHead, cachePath);
            }
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Launch.Skin.Load.Error.Avatar", $"{(Address ?? "null")},{loader.name}"),
                HintType.Error);
        }
    }

    /// <summary>
    ///     清空皮肤。
    /// </summary>
    public void Clear()
    {
        Address = "";
        ImgFore.Source = null;
        ImgBack.Source = null;
    }

    // 刷新缓存
    public void RefreshClick(object? sender, RoutedEventArgs e)
    {
        RefreshCache(loader);
    }

    /// <summary>
    ///     刷新皮肤缓存。
    /// </summary>
    public static void RefreshCache(ModLoader.LoaderTask<PageLaunchLeft.SkinInput, string>? sender = null)
    {
        var hasLoaderRunning =
            PageLaunchLeft.skinLoaders.Any(skinLoader => skinLoader.State == LoadState.Loading);

        if (hasLoaderRunning)
        {
            // 由于 Abort 不是实时的，暂时不会释放文件，会导致删除报错，故只能取消执行
            HintService.Hint(Lang.Text("Launch.Skin.Refresh.Busy"));
            return;
        }

        // 清空缓存
        // 刷新控件
        _ = Task.Run(() =>
        {
            try
            {
                HintService.Hint(Lang.Text("Launch.Skin.Refreshing"));
                SkinNetwork.ClearCache();
                foreach (var skinLoader in sender is not null
                             ? new[] { sender }
                             : new[] { PageLaunchLeft.skinLegacy, PageLaunchLeft.skinMs })
                    skinLoader.WaitForExit(isForceRestart: true);
                HintService.Hint(Lang.Text("Launch.Skin.RefreshSuccess"), HintType.Success);
            }
            catch (Exception)
            {
                HintService.Hint(Lang.Text("Launch.Skin.Refresh.Error"), HintType.Error);
            }
        });
    }

    /// <summary>
    ///     在更换正版皮肤后，刷新正版皮肤。
    /// </summary>
    /// <param name="skinAddress">新的正版皮肤完整地址。</param>
    /// <param name="uuid">玩家 UUID。</param>
    public static void ReloadCache(string skinAddress, string uuid)
    {
        // 更新缓存
        // 刷新控件
        // 完成提示
        _ = Task.Run(() =>
        {
            try
            {
                SkinNetwork.RecordSkinAddress(uuid, skinAddress);
                PageLaunchLeft.skinMs.WaitForExit(isForceRestart: true);
                HintService.Hint(Lang.Text("Launch.Skin.ChangeSuccess"), HintType.Success);
            }
            catch (Exception)
            {
                HintService.Hint(Lang.Text("Launch.Skin.Change.Error.MsRefresh"), HintType.Error);
            }
        });
    }

    public void BtnSkinCape_Click(object? sender, RoutedEventArgs e)
    {
        // 检查条件，获取新披风
        if (isChanging)
        {
            HintService.Hint(Lang.Text("Launch.Skin.Cape.Changing"));
            return;
        }

        if (ProfileService.Current is not { } profile)
        {
            HintService.Hint(Lang.Text("Launch.Skin.Cape.LoginFailed"), HintType.Error);
            return;
        }

        HintService.Hint(Lang.Text("Launch.Skin.Cape.FetchingList"));
        isChanging = true;
        // 开始实际获取
        _ = ChangeCapeAsync(profile);
    }

    private async Task ChangeCapeAsync(McProfile profile)
    {
        try
        {
            // 获取登录信息与全部披风
            var session = await SkinNetwork.LoadCapeSessionAsync(profile);

            // 缓存披风图片，并裁出正面
            foreach (var cape in session.Capes)
            {
                var localFile = Path.Combine(TempRoot, "Cache", "Capes", $"{cape.Alias}.png");
                var capeFrontFile = Path.Combine(TempRoot, "Cache", "Capes", $"{cape.Alias}-front.png");
                if (!(File.Exists(localFile) && File.Exists(capeFrontFile)))
                {
                    await SkinNetwork.DownloadFileAsync(cape.Url, localFile);
                    SkinBitmap.SaveCrop(localFile, 1, 0, 11, 17, capeFrontFile);
                }

                cape.FrontFile = capeFrontFile;
            }

            // 获取玩家的所有披风
            var items = new List<IMyRadio>
            {
                new MyListItem
                {
                    Title = Lang.Text("Launch.Skin.Cape.None"),
                    Info = "Null"
                }
            };
            items.AddRange(session.Capes.Select(cape => (IMyRadio)new MyListItem
            {
                Title = CapeDisplayName(cape.Alias),
                Info = cape.Alias,
                Checked = cape.IsActive,
                Type = MyListItem.CheckType.RadioBox,
                Logo = cape.FrontFile,
                LogoScale = 0.8d
            }));

            var selId = await MsgBox.SelectAsync(items, Lang.Text("Launch.Skin.Cape.SelectTitle"));
            if (selId is null)
                return;
            // 发送请求
            var error = await SkinNetwork.SetCapeAsync(session.AccessToken,
                selId is 0 ? null : session.Capes[(int)selId - 1].Id);
            if (error is not null)
                HintService.Hint(Lang.Text("Launch.Skin.Cape.ChangeFailedWithReason", error), HintType.Error);
            else
                HintService.Hint(Lang.Text("Launch.Skin.Cape.ChangeSuccess"), HintType.Success);
        }
        catch (Exception)
        {
            HintService.Hint(Lang.Text("Launch.Skin.Cape.ChangeFailed"), HintType.Error);
        }
        finally
        {
            isChanging = false;
        }
    }

    private static string CapeDisplayName(string capeAlias)
    {
        var safeName = capeAlias
            .Replace("-", "")
            .Replace(" ", "")
            .Replace("'", "");
        var key = $"Launch.Skin.Cape.Name.{safeName}";
        var name = Lang.Text(key);
        if (name == $"!{key}!" || name == key)
            return capeAlias;
        return name;
    }

    /// <summary>应用临时文件夹，对应 WPF 的 ModBase.pathTemp。</summary>
    private static string TempRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCL Shion", "Temp");

    /// <summary>取 <paramref name="text" /> 中位于 <paramref name="start" /> 之后、<paramref name="end" /> 之前的部分。</summary>
    private static string Between(string text, string start, string end)
    {
        var begin = text.IndexOf(start, StringComparison.Ordinal);
        if (begin < 0)
            return "";
        begin += start.Length;
        var finish = text.IndexOf(end, begin, StringComparison.Ordinal);
        return finish < 0 ? text[begin..] : text[begin..finish];
    }

    /// <summary>皮肤与披风图片的读取、裁剪与保存。</summary>
    private sealed class SkinBitmap : IDisposable
    {
        private readonly SKBitmap _pic;

        public SkinBitmap(string source)
        {
            using var stream = source.StartsWith("avares://", StringComparison.Ordinal)
                ? AssetLoader.Open(new Uri(source))
                : File.OpenRead(source);
            _pic = SKBitmap.Decode(stream) ?? throw new InvalidDataException("图片无法解码：" + source);
        }

        public int Width => _pic.Width;

        public int Height => _pic.Height;

        public SkinColor Pixel(int x, int y)
        {
            var color = _pic.GetPixel(x, y);
            return new SkinColor(color.Alpha, color.Red, color.Green, color.Blue);
        }

        /// <summary>裁剪为 SkiaSharp 位图，供后续合成使用。</summary>
        public SKBitmap CropSk(int x, int y, int width, int height)
        {
            var result = new SKBitmap(width, height);
            using var canvas = new SKCanvas(result);
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.None };
            canvas.DrawBitmap(_pic, new SKRect(x, y, x + width, y + height), new SKRect(0, 0, width, height), paint);
            return result;
        }

        /// <summary>裁剪为界面使用的图片。</summary>
        public Bitmap Clip(int x, int y, int width, int height)
        {
            using var cropped = CropSk(x, y, width, height);
            return ToBitmap(cropped);
        }

        public void Save(string path) => SaveTo(_pic, path);

        public static void SaveTo(SKBitmap bitmap, string path)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var file = File.Create(path);
            data.SaveTo(file);
        }

        public static void SaveCrop(string source, int x, int y, int width, int height, string target)
        {
            using var image = new SkinBitmap(source);
            using var cropped = image.CropSk(x, y, width, height);
            SaveTo(cropped, target);
        }

        private static Bitmap ToBitmap(SKBitmap bitmap)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            return new Bitmap(stream);
        }

        public void Dispose() => _pic.Dispose();
    }

    /// <summary>单个像素的颜色，按值比较。</summary>
    private readonly record struct SkinColor(byte A, byte R, byte G, byte B);
}

/// <summary>一件披风。</summary>
internal sealed record CapeEntry(string Id, string Alias, string Url, bool IsActive)
{
    /// <summary>披风正面的本地图片。</summary>
    public string FrontFile { get; set; } = "";
}

/// <summary>账户的披风列表与用于修改披风的令牌。</summary>
internal sealed record CapeSession(string AccessToken, IReadOnlyList<CapeEntry> Capes);

/// <summary>
///     皮肤与披风的网络请求和本地缓存，按 Mojang 会话服务、Authlib-Injector 与 Minecraft 服务的公开协议实现。
/// </summary>
internal static class SkinNetwork
{
    private static readonly HttpClient Http = new();

    private static string CacheRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCL Shion", "Temp", "Cache");

    /// <summary>
    ///     取得玩家皮肤的地址。已记录过的皮肤直接返回记录的地址；否则查询会话服务，未设置自定义皮肤时抛出异常。
    /// </summary>
    /// <param name="uuid">玩家 UUID。</param>
    /// <param name="authServer">第三方验证服务器地址；为 null 时使用正版会话服务。</param>
    public static async Task<string> GetSkinUrlAsync(string uuid, string? authServer)
    {
        var index = Path.Combine(CacheRoot, "Skin", authServer is null ? "IndexMs.ini" : "IndexAuth.ini");
        if (ReadIndex(index).TryGetValue(uuid, out var recorded))
            return recorded;

        var profileUrl = authServer is null
            ? "https://sessionserver.mojang.com/session/minecraft/profile/" + uuid
            : await AuthlibAccount.ResolveApiLocationAsync(Http, authServer) + "/sessionserver/session/minecraft/profile/" + uuid;
        using var response = await Http.GetAsync(profileUrl);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("429 Too Many Requests");
        var profile = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var textures = profile?["properties"]?[0]?["value"]?.GetValue<string>();
        if (textures is null)
            throw new InvalidDataException("未设置自定义皮肤");
        var skin = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(textures)))?["textures"]?["SKIN"]?["url"]
            ?.GetValue<string>();
        return skin ?? throw new InvalidDataException("未设置自定义皮肤");
    }

    /// <summary>下载皮肤到缓存；已是本地文件的地址直接返回。</summary>
    public static async Task<string> DownloadSkinAsync(string address)
    {
        if (File.Exists(address))
            return address;
        var target = Path.Combine(CacheRoot, "Skin", Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(address))) + ".png");
        if (!File.Exists(target))
            await DownloadFileAsync(address, target);
        return target;
    }

    /// <summary>把远程文件下载到本地路径。</summary>
    public static async Task DownloadFileAsync(string url, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllBytesAsync(target, await Http.GetByteArrayAsync(url));
    }

    /// <summary>记录玩家的皮肤地址，之后 <see cref="GetSkinUrlAsync" /> 直接使用它。</summary>
    public static void RecordSkinAddress(string uuid, string address)
    {
        var index = Path.Combine(CacheRoot, "Skin", "IndexMs.ini");
        var entries = ReadIndex(index);
        entries[uuid] = address;
        Directory.CreateDirectory(Path.GetDirectoryName(index)!);
        File.WriteAllLines(index, entries.Select(pair => pair.Key + "=" + pair.Value));
    }

    /// <summary>清空皮肤与 UUID 的缓存。</summary>
    public static void ClearCache()
    {
        foreach (var folder in new[] { Path.Combine(CacheRoot, "Skin"), Path.Combine(CacheRoot, "Uuid") })
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
    }

    /// <summary>读取正版账户的披风列表；令牌过期时先刷新。</summary>
    public static async Task<CapeSession> LoadCapeSessionAsync(McProfile profile)
    {
        var account = await PageLaunchLeft.CreateLaunchAccountAsync(profile, CancellationToken.None);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        using var response = await Http.SendAsync(request);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var capes = (body?["capes"]?.AsArray() ?? new JsonArray())
            .Where(cape => cape?["url"] is not null)
            .Select(cape => new CapeEntry(
                cape!["id"]!.GetValue<string>(),
                cape["alias"]!.GetValue<string>(),
                cape["url"]!.GetValue<string>(),
                string.Equals(cape["state"]?.GetValue<string>(), "ACTIVE", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return new CapeSession(account.AccessToken, capes);
    }

    /// <summary>设置当前披风，<paramref name="capeId" /> 为 null 时卸下披风；失败时返回错误信息。</summary>
    public static async Task<string?> SetCapeAsync(string accessToken, string? capeId)
    {
        using var request = new HttpRequestMessage(
            capeId is null ? HttpMethod.Delete : HttpMethod.Put,
            "https://api.minecraftservices.com/minecraft/profile/capes/active");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (capeId is not null)
            request.Content = new StringContent(new JsonObject { ["capeId"] = capeId }.ToJsonString(),
                Encoding.UTF8, "application/json");
        using var response = await Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!body.Contains("\"errorMessage\"", StringComparison.Ordinal))
            return null;
        return JsonNode.Parse(body)?["errorMessage"]?.ToString() ?? body;
    }

    private static Dictionary<string, string> ReadIndex(string path)
    {
        var entries = new Dictionary<string, string>();
        if (!File.Exists(path))
            return entries;
        foreach (var line in File.ReadAllLines(path))
        {
            var split = line.IndexOf('=');
            if (split > 0)
                entries[line[..split]] = line[(split + 1)..];
        }

        return entries;
    }
}
