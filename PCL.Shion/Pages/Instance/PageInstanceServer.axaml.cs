using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.ServerQuery;
using PCL.Shion.Core.Worlds;
using PCL.Shion.Localization;
using PCL.Shion.Pages;
using PCL.Shion.Services;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Instance;

public partial class PageInstanceServer : MyPageRight
{
    private const int debounceInterval = 2000;

    public static readonly List<MinecraftServerInfo> serverList = new();
    private static readonly List<ServerCard> serverCardList = new();

    private CancellationTokenSource? _cts;

    private DateTime _lastRefresh = DateTime.MinValue;

    public PageInstanceServer()
    {
        InitializeComponent();
        PanScroll = PanBack;
        InitializeEvents();
        Loaded += PageLoaded;
        PropertyChanged += PageInstanceServer_PropertyChanged;
    }

    private void InitializeEvents()
    {
        BtnRefreshEmpty.Click += BtnRefresh_Click;
        BtnRefreshAll.Click += BtnRefresh_Click;
        BtnAddServerTop.Click += BtnAddServer_Click;
        BtnAddServer.Click += BtnAddServer_Click;
    }

    private async void PageLoaded(object? sender, RoutedEventArgs e)
    {
        serverList.Clear();
        serverCardList.Clear();
        PanServers.Children.Clear();

        await LoadServersFromFileAsync();
        RefreshTip();

        foreach (var server in serverList)
        {
            var serverCard = new ServerCard();
            serverCard.RemoveServer += RemoveServerEvent;
            serverCard.EditServer += EditServerEvent;
            serverCard.UpdateServerInfo(server);
            serverCardList.Add(serverCard);
            PanServers.Children.Add(serverCard);
        }

        PingAllServers();
    }

    private void PageInstanceServer_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != IsVisibleProperty || IsVisible)
            return;
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose(); // 清理旧的 CancellationTokenSource
            _cts = null;
        }
    }

    private void EditServerEvent(object? sender, EventArgs e)
    {
        if (sender is ServerCard card && e is ServerCard.ResultEventArgs args)
            _ = EditServer(card, args);
    }

    private async void RemoveServerEvent(object? sender, EventArgs e)
    {
        if (sender is not ServerCard card)
            return;
        // Get server index
        var index = PanServers.Children.IndexOf(card);
        if (index < 0)
        {
            Hint.Show(Lang.Text("Instance.Server.IndexNotFound"), HintType.Error);
            return;
        }

        // Read servers.dat
        var serversDatPath = Path.Combine(PageInstanceLeft.McInstance.PathIndie, "servers.dat");
        List<ServersDatEntry> entries;
        try
        {
            entries = ServersDat.Read(serversDatPath);
        }
        catch (Exception)
        {
            Hint.Show(Lang.Text("Instance.Server.ReadDataFailed"), HintType.Error);
            return;
        }

        if (index >= entries.Count)
        {
            Hint.Show(Lang.Text("Instance.Server.IndexNotFound"), HintType.Error);
            return;
        }

        // Remove server from NBT data
        entries.RemoveAt(index);

        // Write back to NBT file
        if (!TryWriteServers(serversDatPath, entries))
        {
            Hint.Show(Lang.Text("Instance.Server.WriteDataFailed"), HintType.Error);
            return;
        }

        // Remove server from list and UI
        serverList.RemoveAt(index);
        serverCardList.Remove(card);
        if (serverList.Count == 0) RefreshTip();

        // Remove UI element
        PanServers.Children.Remove(card);

        // Success message
        Hint.Show(Lang.Text("Instance.Server.Removed"), HintType.Success);
    }

    private async Task EditServer(ServerCard sender, ServerCard.ResultEventArgs e)
    {
        // Read servers.dat
        var serversDatPath = Path.Combine(PageInstanceLeft.McInstance.PathIndie, "servers.dat");
        List<ServersDatEntry> entries;
        try
        {
            entries = ServersDat.Read(serversDatPath);
        }
        catch (Exception)
        {
            Hint.Show(Lang.Text("Instance.Server.ReadDataFailed"), HintType.Error);
            return;
        }

        // Get server index
        var index = PanServers.Children.IndexOf(sender);
        if (index < 0 || index >= entries.Count)
        {
            Hint.Show(Lang.Text("Instance.Server.IndexNotFound"), HintType.Error);
            return;
        }

        // Update server data
        entries[index] = entries[index] with { Name = e.Param1, Address = e.Param2 };

        // Write updated NBT data
        if (!TryWriteServers(serversDatPath, entries))
        {
            Hint.Show(Lang.Text("Instance.Server.WriteDataFailed"), HintType.Error);
            return;
        }

        sender.server.Name = e.Param1;
        sender.server.Address = e.Param2;

        await sender.RefreshServerStatusAsync(true);

        // Success message
        Hint.Show(Lang.Text("Instance.Server.Updated"), HintType.Success);
    }

    /// <summary>
    ///     刷新服务器列表
    /// </summary>
    public async void RefreshServers()
    {
        ModBase.Log("刷新服务器列表");
        try
        {
            // 读取服务器信息
            await LoadServersFromFileAsync();

            // 在UI线程中更新界面
            Dispatcher.UIThread.Post(UpdateServerUi);

            // 异步ping所有服务器
            PingAllServers();
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Instance.Server.RefreshFailed"),
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Server.RefreshFailed"));
            Dispatcher.UIThread.Post(() => Hint.Show(
                Lang.Text("Instance.Server.RefreshFailed.WithDetail", ex.ToString()),
                HintType.Error));
        }
    }

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        if ((DateTime.Now - _lastRefresh).TotalMilliseconds < debounceInterval)
        {
            Hint.Show(Lang.Text("Instance.Server.NoFrequentRefresh"));
            return;
        }

        _lastRefresh = DateTime.Now;
        Hint.Show(Lang.Text("Instance.Server.RefreshingList"));
        try
        {
            RefreshServers();
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Instance.Server.RefreshFailed"),
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Server.RefreshFailed"));
            Hint.Show(
                Lang.Text("Instance.Server.RefreshFailed.WithDetail", ex.ToString()),
                HintType.Error);
        }
    }

    private async void BtnAddServer_Click(object? sender, RoutedEventArgs e)
    {
        var result = await GetServerInfoAsync(new MinecraftServerInfo
        {
            Name = Lang.Text("Instance.Server.DefaultName"),
            Address = ""
        });
        if (!result.Success)
            return;

        var newServer = new MinecraftServerInfo
        {
            Name = result.Name,
            Address = result.Address,
            Status = ServerStatus.Unknown
        };
        serverList.Add(newServer);

        RefreshTip();

        var serverCard = new ServerCard();
        serverCard.RemoveServer += RemoveServerEvent;
        serverCard.EditServer += EditServerEvent;
        serverCard.UpdateServerInfo(newServer);
        serverCardList.Add(serverCard);
        PanServers.Children.Add(serverCard);

        await serverCard.RefreshServerStatusAsync(false);

        var serversDatPath = Path.Combine(PageInstanceLeft.McInstance.PathIndie, "servers.dat");

        List<ServersDatEntry> entries;
        if (!File.Exists(serversDatPath))
        {
            entries = new List<ServersDatEntry>();
            RefreshTip();
        }
        else
        {
            entries = ServersDat.Read(serversDatPath);
        }

        entries.Add(new ServersDatEntry(result.Name, result.Address, null));
        TryWriteServers(serversDatPath, entries);
    }

    /// <summary>
    ///     弹出两次输入框，让用户输入服务器名称与地址。
    /// </summary>
    public static async Task<(string Name, string Address, bool Success)> GetServerInfoAsync(MinecraftServerInfo server)
    {
        var newName = await MsgBox.InputAsync(Lang.Text("Instance.Server.EditTitle"), Lang.Text("Instance.Server.NamePrompt"), server.Name,
            ValidateNotBlank);

        if (string.IsNullOrEmpty(newName)) return (string.Empty, string.Empty, false);

        var newAddress = await MsgBox.InputAsync(Lang.Text("Instance.Server.EditTitle"), Lang.Text("Instance.Server.AddressPrompt"), server.Address,
            ValidateNotBlank);
        if (string.IsNullOrEmpty(newAddress)) return (string.Empty, string.Empty, false);
        return (newName, newAddress, true);
    }

    private static string? ValidateNotBlank(string input) =>
        string.IsNullOrWhiteSpace(input) ? Lang.Text("Common.Validate.NotBlank") : null;

    private static bool TryWriteServers(string path, IReadOnlyList<ServersDatEntry> entries)
    {
        try
        {
            ServersDat.Write(path, entries);
            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "写入 servers.dat 失败");
            return false;
        }
    }

    /// <summary>
    ///     从servers.dat文件读取服务器信息
    /// </summary>
    private async Task LoadServersFromFileAsync()
    {
        serverList.Clear();

        var serversFile = Path.Combine(PageInstanceLeft.McInstance.PathIndie, "servers.dat");
        if (!File.Exists(serversFile))
            return;

        try
        {
            // 读取NBT格式的servers.dat文件
            var entries = await Task.Run(() => ServersDat.Read(serversFile));
            ParseServersFromNBT(entries);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Instance.Server.ReadFileFailed"));
        }
    }

    /// <summary>
    ///     解析 servers.dat 中的服务器数据
    /// </summary>
    private void ParseServersFromNBT(IReadOnlyList<ServersDatEntry> entries)
    {
        ModBase.Log($"Found {entries.Count} servers:");
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            ModBase.Log($"服务器 {i + 1}:");
            ModBase.Log($"  名字: {entry.Name}");
            ModBase.Log($"  IP: {entry.Address}");
            serverList.Add(new MinecraftServerInfo
            {
                Name = entry.Name,
                Address = entry.Address,
                Status = ServerStatus.Unknown,
                Icon = entry.Icon ?? ""
            });
        }
    }

    /// <summary>
    ///     更新服务器UI显示
    /// </summary>
    private void UpdateServerUi()
    {
        PanServers.Children.Clear();

        RefreshTip();

        foreach (var server in serverList)
        {
            var serverCard = new ServerCard();
            serverCard.RemoveServer += RemoveServerEvent;
            serverCard.EditServer += EditServerEvent;
            serverCard.UpdateServerInfo(server);
            serverCardList.Add(serverCard);
            PanServers.Children.Add(serverCard);
        }
    }

    private void RefreshTip()
    {
        if (serverList.Count == 0)
        {
            ModBase.Log(Lang.Text("Instance.Server.NoServersFound"));
            PanNoServer.IsVisible = true;
            PanContent.IsVisible = false;
            PanServers.IsVisible = false;
            return;
        }

        ModBase.Log(Lang.Text("Instance.Server.FoundServers"));
        PanNoServer.IsVisible = false;
        PanContent.IsVisible = true;
        PanServers.IsVisible = true;
    }

    private async void PingAllServers()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        using var semaphore = new SemaphoreSlim(5); // 限制最多 5 个并发任务

        var tasks = new List<Task>();
        try
        {
            var snapshot = serverCardList.ToList();
            foreach (var server in snapshot)
            {
                var currentServer = server;
                await semaphore.WaitAsync(token);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await currentServer.RefreshServerStatusAsync(false, token);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, $"Ping 服务器失败: {currentServer}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, token));
            }

            await Task.WhenAll(tasks); // 等待所有任务完成
        }
        catch (OperationCanceledException)
        {
            ModBase.Log("PingAllServers 被取消", ModBase.LogLevel.Debug);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "PingAllServers 失败");
        }
    }

    /// <summary>
    ///     ping单个服务器
    /// </summary>
    public static async Task<MinecraftServerInfo> PingServerAsync(MinecraftServerInfo server, CancellationToken token)
    {
        try
        {
            var addr = await ServerAddressResolver.GetResolvedServerAddressAsync(server.Address, token);
            using var query = McPingServiceFactory.CreateService(addr.Host, addr.Ip, addr.Port);
            ModBase.Log("Pinging server: " + server.Address + ":" + addr.Port);
            var result = await query.PingAsync(token);
            ModBase.Log("Ping result: " + (result is not null ? "Success" : "Failed"));
            if (result is not null)
            {
                server.Status = ServerStatus.Online;
                server.PlayerCount = result.Players.Online;
                server.MaxPlayers = result.Players.Max;
                server.Description = result.Description ?? string.Empty;
                server.Version = result.Version.Name;
                server.Ping = (int)result.Latency;
                server.Icon = result.Favicon;
            }
            else
            {
                server.Status = ServerStatus.Offline;
            }
        }
        catch (OperationCanceledException)
        {
            server.Status = ServerStatus.Offline;
            ModBase.Log("Ping 服务器被取消: " + server.Address, ModBase.LogLevel.Debug);
        }
        catch (Exception ex)
        {
            server.Status = ServerStatus.Offline;
            ModBase.Log(ex, $"Ping 服务器失败: {server.Address}:{server.Port}");
        }

        return server;
    }
}

/// <summary>
///     Minecraft服务器信息类
/// </summary>
public class MinecraftServerInfo
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public int Port { get; set; } = 25565;
    public ServerStatus Status { get; set; } = ServerStatus.Unknown;
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public int Ping { get; set; }
    public string Icon { get; set; } = "";
}

/// <summary>
///     服务器状态枚举
/// </summary>
public enum ServerStatus
{
    Unknown,
    Online,
    Offline,
    Pinging
}
