using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Shion.Core.ServerQuery;
using PCL.Shion.Core.ServerQuery.Model;
using PCL.Shion.Localization;

namespace PCL.Shion.Controls;

public partial class MinecraftServer : Grid
{
    private static readonly Uri FallbackImageUri = new("avares://PCL.Shion/Assets/Images/Icons/DefaultServer.png");

    public static readonly StyledProperty<string> AddressProperty =
        AvaloniaProperty.Register<MinecraftServer, string>(nameof(Address), string.Empty);

    static MinecraftServer()
    {
        AddressProperty.Changed.AddClassHandler<MinecraftServer>((server, e) =>
            Dispatcher.UIThread.Post(() => _ = server.UpdateServerInfoAsync(e.NewValue as string)));
    }

    public MinecraftServer()
    {
        InitializeComponent();
    }

    public string Address
    {
        get => GetValue(AddressProperty);
        set => SetValue(AddressProperty, value);
    }

    public async Task UpdateServerInfoAsync(string? address)
    {
        if (address is null)
            return;
        address = address.Replace("：", ":");
        // 预先重置UI状态
        LabServerDesc.Foreground = Brushes.White;
        LabServerDesc.Text = Lang.Text("Tools.ServerQuery.State.Querying");
        LabServerPlayer.Text = "-/-";
        ToolTip.SetTip(LabServerPlayer, null);
        LabServerLatency.Text = string.Empty;
        SetFallbackLogo();

        try
        {
            // 获取可达地址（DNS解析）
            var addr = await ServerAddressResolver.GetResolvedServerAddressAsync(address);

            // Ping服务器
            using (var query = McPingServiceFactory.CreateService(addr.Host, addr.Ip, addr.Port))
            {
                var ret = await query.PingAsync();

                if (ret is null) throw new Exception(Lang.Text("Tools.ServerQuery.State.NoInfo"));

                // 处理服务器图标
                await SetServerLogoAsync(ret.Favicon);

                // 更新UI
                UpdateServerStatus(ret);
            }
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "[MinecraftServer] 信息查询失败：{Exception}", ex);
            LabServerDesc.Text = Lang.Text("Tools.ServerQuery.Error.UnableToConnect", GetErrorText(ex));
            LabServerDesc.Foreground = Brushes.Red;
            SetFallbackLogo();
        }
    }

    /// <summary>查询失败的原因按 <see cref="ServerQueryError"/> 显示本地化文字，其余异常显示原始信息。</summary>
    private static string GetErrorText(Exception ex)
    {
        return ex is ServerQueryException queryException
            ? Lang.Text("Tools.ServerQuery.Error." + queryException.Error)
            : ex.Message;
    }

    private void SetFallbackLogo()
    {
        using var stream = AssetLoader.Open(FallbackImageUri);
        ImgServerLogo.Source = new Bitmap(stream);
    }

    private async Task SetServerLogoAsync(string? favicon)
    {
        if (string.IsNullOrWhiteSpace(favicon))
        {
            SetFallbackLogo();
            return;
        }

        try
        {
            // 提取 Base64 数据部分
            var base64Data = favicon.Contains(',') ? favicon.Split(',')[1] : favicon;
            var bitmap = await Task.Run(() =>
            {
                using var stream = new MemoryStream(Convert.FromBase64String(base64Data));
                return new Bitmap(stream);
            });
            ImgServerLogo.Source = bitmap;
        }
        catch (Exception)
        {
            SetFallbackLogo();
        }
    }

    private void UpdateServerStatus(McPingResult ret)
    {
        // 延迟颜色判断
        var latencyColor = ret.Latency < 150 ? "a" : ret.Latency < 400 ? "6" : "c";

        // 更新描述
        LabServerDesc.Text = Lang.Text("Tools.ServerQuery.Title.MinecraftServer");
        MotdRenderer.RenderMotd(ret.Description, false, 2, 14);
        MotdRenderer.RenderCanvas();

        // 更新玩家信息
        LabServerPlayer.Text = $"{ret.Players.Online}/{ret.Players.Max}";
        // 延迟文字按延迟颜色代码着色
        var latencyHex = MotdRenderer.TryGetColorFromCode(latencyColor, false, out var colorHex) ? colorHex! : "#888888";
        LabServerLatency.Inlines.Clear();
        LabServerLatency.Inlines.Add(new Run
        {
            Text = $"{ret.Latency}ms",
            Foreground = new SolidColorBrush(Color.Parse(latencyHex))
        });

        // 玩家列表提示
        if (ret.Players.Samples is not null && ret.Players.Samples.Any())
        {
            ToolTip.SetTip(LabServerPlayer, string.Join("\r\n", ret.Players.Samples.Select(x => x.Name)));
            ToolTip.SetPlacement(LabServerPlayer, PlacementMode.Pointer);
        }
    }
}
