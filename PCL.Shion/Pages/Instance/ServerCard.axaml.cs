using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Launch;
using PCL.Shion.Services;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Instance;

public partial class ServerCard : MyCard
{
    private const string SignalGreen = "#ff00ff21";
    private const string SignalGray = "#ff888888";

    public MinecraftServerInfo server = null!;

    public ServerCard()
    {
        InitializeComponent();
        InitializeEvents();
    }

    public event EventHandler? RemoveServer;
    public event EventHandler? EditServer;

    private void InitializeEvents()
    {
        BtnConnect.Click += BtnConnect_Click;
        BtnSetting.Click += BtnSkin_Click;
        MenuRefresh.Click += BtnRefresh_Click;
        MenuCopy.Click += BtnCopy_Click;
        MenuEdit.Click += BtnEdit_Click;
        MenuRemove.Click += BtnRemove_Click;
    }

    private void BtnSkin_Click(object? sender, RoutedEventArgs eventArgs)
    {
        BtnSetting.ContextMenu!.Open(BtnSetting);
    }

    /// <summary>
    ///     初始化服务器卡片
    /// </summary>
    public void UpdateServerInfo(MinecraftServerInfo serverInfo)
    {
        server = serverInfo;
        Dispatcher.UIThread.Post(UpdateServerUi);
    }

    /// <summary>
    ///     更新服务器UI
    /// </summary>
    private async void UpdateServerUi()
    {
        if (server is null)
            return;

        // 更新服务器名称
        ServerName.Text = server.Name;
        await ImageLoaderHelper.SetServerLogoAsync(server.Icon, ServerIcon);
        if (server.Status == ServerStatus.Online)
        {
            Signal.Content = CreateSignalIcon(GetSignalBars(server.Ping));
            ToolTip.SetTip(Signal, $"{server.Ping}ms");

            if (server.PlayerCount != default && server.MaxPlayers != default)
                ServerPlayer.Text = $"{server.PlayerCount} / {server.MaxPlayers}";
            else
                ServerPlayer.Text = "???";

            ServerMotD.IsVisible = false;
            MotdRenderer.RenderMotd(server.Description, ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark, 2);
            MotdRenderer.RenderCanvas();
        }
        else if (server.Status == ServerStatus.Pinging)
        {
            Signal.Content = CreateLoadingIcon();
            MotdRenderer.ClearCanvas();
            ServerPlayer.Text = Lang.Text("Instance.Server.Card.Connecting");
            ServerMotD.Text = Lang.Text("Instance.Server.Card.ConnectingDots");
            ServerMotD.IsVisible = true;
        }
        else if (server.Status == ServerStatus.Offline)
        {
            Signal.Content = CreateOfflineIcon();
            MotdRenderer.ClearCanvas();
            ServerPlayer.Text = Lang.Text("Instance.Server.Card.Offline");
            ServerMotD.Text = Lang.Text("Instance.Server.Card.ServerOffline");
            ServerMotD.IsVisible = true;
        }
    }

    /// <summary>
    ///     按 ping 值返回信号条数（1 到 5），与 WPF 版的分段一致。
    /// </summary>
    private static int GetSignalBars(int ping)
    {
        return ping switch
        {
            >= 0 and <= 99 => 5,
            >= 100 and <= 299 => 4,
            >= 300 and <= 599 => 3,
            >= 600 and <= 999 => 2,
            _ => 1
        };
    }

    /// <summary>
    ///     信号图标：前 <paramref name="bars"/> 条为绿色，其余为灰色，与 WPF 版 signal_1 到 signal_5 一致。
    /// </summary>
    private static Control CreateSignalIcon(int bars)
    {
        var canvas = new Canvas { Width = 1024, Height = 1024 };
        var geometry = new (double Left, double Top, double Height)[]
        {
            (234.666667, 610.56, 127.04),
            (353.066667, 541.226667, 196.373333),
            (471.445333, 460.373333, 277.226667),
            (589.866667, 379.52, 358.08),
            (708.266667, 298.666667, 438.933333)
        };
        for (var i = 0; i < geometry.Length; i++)
        {
            var (left, top, height) = geometry[i];
            var rectangle = new Rectangle
            {
                Width = 80.853333,
                Height = height,
                Fill = new SolidColorBrush(Color.Parse(i < bars ? SignalGreen : SignalGray))
            };
            Canvas.SetLeft(rectangle, left);
            Canvas.SetTop(rectangle, top);
            canvas.Children.Add(rectangle);
        }

        return new Viewbox { Width = 20, Height = 20, Child = canvas };
    }

    private static Control CreateOfflineIcon() => CreatePathIcon(14, 14, 1280, 1024,
        "M 317.63 349.235 l -67.951 -67.951 l -67.95 67.95 c -18.964 18.964 -48.988 18.964 -67.951 0 c -18.963 -18.962 -18.963 -48.987 0 -67.95 l 67.95 -67.95 l -66.37 -67.951 c -18.963 -18.963 -18.963 -48.988 0 -67.95 c 18.963 -18.964 48.988 -18.964 67.95 0 l 67.951 67.95 l 67.95 -67.95 c 18.964 -18.964 48.989 -18.964 67.951 0 c 18.963 18.962 18.963 48.987 0 67.95 l -67.95 67.95 l 67.95 67.951 c 18.963 18.963 18.963 48.988 0 67.95 c -9.481 9.482 -20.543 14.223 -33.185 14.223 c -14.222 0 -26.864 -6.321 -36.345 -14.222 z M 216.494 752.198 h -48.988 c -26.864 0 -48.987 26.864 -48.987 60.049 v 120.099 c 0 33.185 22.123 60.05 48.987 60.05 h 48.988 c 26.864 0 48.987 -26.865 48.987 -60.05 v -120.1 c 0 -33.184 -22.123 -60.048 -48.987 -60.048 z M 516.74 512 h -48.988 c -26.864 0 -48.988 26.864 -48.988 60.05 v 360.296 c 0 33.185 22.124 60.05 48.988 60.05 h 48.988 c 26.864 0 48.987 -26.865 48.987 -60.05 V 572.049 c 0 -33.185 -22.123 -60.049 -48.987 -60.049 z m 300.247 -240.198 H 768 c -26.864 0 -48.988 26.865 -48.988 60.05 v 600.494 c 0 33.185 22.124 60.05 48.988 60.05 h 48.988 c 26.864 0 48.987 -26.865 48.987 -60.05 V 331.852 c 0 -33.185 -22.123 -60.05 -48.987 -60.05 z m 300.247 -240.197 h -48.988 c -26.864 0 -48.988 26.864 -48.988 60.05 v 840.69 c 0 33.186 22.124 60.05 48.988 60.05 h 48.988 c 26.864 0 48.987 -26.864 48.987 -60.05 V 91.656 c -1.58 -33.186 -22.123 -60.05 -48.987 -60.05 z");

    private static Control CreateLoadingIcon() => CreatePathIcon(20, 20, 1024, 1024,
        "M 256 490.667 a 64 64 0 1 1 -128 0 a 64 64 0 0 1 128 0 z m -42.6667 0 a 21.3333 21.3333 0 1 0 -42.6667 0 a 21.3333 21.3333 0 0 0 42.6667 0 z m 384 0 a 106.667 106.667 0 1 1 -213.376 -0.042667 A 106.667 106.667 0 0 1 597.333 490.667 z m -42.6667 0 a 64 64 0 1 0 -128.043 0.042666 A 64 64 0 0 0 554.667 490.667 z m 298.667 0 a 64 64 0 1 1 -128 0 a 64 64 0 0 1 128 0 z m -42.6667 0 a 21.3333 21.3333 0 1 0 -42.6667 0 a 21.3333 21.3333 0 0 0 42.6667 0 z");

    private static Control CreatePathIcon(double width, double height, double canvasWidth, double canvasHeight, string data)
    {
        var canvas = new Canvas { Width = canvasWidth, Height = canvasHeight };
        canvas.Children.Add(new Path
        {
            Data = Geometry.Parse(data),
            Fill = Brushes.Black,
            Stretch = Stretch.Uniform
        });
        return new Viewbox { Width = width, Height = height, Child = canvas };
    }

    /// <summary>
    ///     刷新服务器状态
    /// </summary>
    public async Task RefreshServerStatusAsync(bool withHint, CancellationToken token = default)
    {
        if (withHint) Hint.Show(Lang.Text("Instance.Server.Card.RefreshingStatus", server.Name));
        server.Status = ServerStatus.Pinging;
        await Dispatcher.UIThread.InvokeAsync(UpdateServerUi);
        var serverInfo = await PageInstanceServer.PingServerAsync(server, token);
        UpdateServerInfo(serverInfo);
    }

    /// <summary>
    ///     连接到服务器
    /// </summary>
    private void BtnConnect_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var launchOptions = new ModLaunch.McLaunchOptions
            {
                ServerIp = server.Address,
                instance = PageInstanceLeft.McInstance
            };
            ModLaunch.McLaunchStart(launchOptions);
            Navigator.Go(PageType.Launch);
            Hint.Show(Lang.Text("Instance.Server.Card.ConnectingTo", server.Name));
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Instance.Server.Card.LaunchFailed"),
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Server.Card.LaunchFailed"));
            Hint.Show(Lang.Text("Instance.Server.Card.LaunchFailedMsg", ex.Message), HintType.Error);
        }
    }

    /// <summary>
    ///     复制服务器地址
    /// </summary>
    private async void BtnCopy_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard
                            ?? throw new InvalidOperationException("Clipboard is unavailable.");
            await clipboard.SetTextAsync(server.Address);
            Hint.Show(Lang.Text("Instance.Server.Card.AddressCopied", server.Address), HintType.Success);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Instance.Server.Card.CopyAddressFailed"));
            Hint.Show(Lang.Text("Instance.Server.Card.CopyAddressFailed"), HintType.Error);
        }
    }

    /// <summary>
    ///     刷新服务器状态
    /// </summary>
    private async void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        await Task.Run(async () => await RefreshServerStatusAsync(true));
    }

    /// <summary>
    ///     编辑服务器信息
    /// </summary>
    private async void BtnEdit_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Get server information
            var result = await PageInstanceServer.GetServerInfoAsync(server);
            if (!result.Success) return;

            EditServer?.Invoke(this, new ResultEventArgs(result.Name, result.Address));
        }
        catch (Exception ex)
        {
            Hint.Show(Lang.Text("Instance.Server.Card.EditFailed", ex.Message), HintType.Error);
        }
    }

    private async void BtnRemove_Click(object? sender, RoutedEventArgs e)
    {
        if (await MsgBox.ShowAsync(
                Lang.Text("Instance.Server.Card.RemoveConfirmMessage", server.Name, server.Address),
                Lang.Text("Instance.Server.Card.RemoveConfirmTitle"), Lang.Text("Common.Action.Confirm"),
                Lang.Text("Common.Action.Cancel")
            ) == 1) RemoveServer?.Invoke(this, EventArgs.Empty);
    }

    public class ResultEventArgs : EventArgs
    {
        public ResultEventArgs(string param1, string param2)
        {
            Param1 = param1;
            Param2 = param2;
        }

        public string Param1 { get; set; }
        public string Param2 { get; set; }
    }
}
