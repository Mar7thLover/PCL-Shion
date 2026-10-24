using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Shion.Core.Link.EasyTier;
using PCL.Shion.Core.Link.Lobby;

namespace PCL.Shion.Core.Link;

/// <summary>公告获取失败的原因。</summary>
public enum LinkAnnouncementFailure
{
    /// <summary>未配置联机服务器。</summary>
    NoServer,

    /// <summary>所有服务器都无法连接或数据无效。</summary>
    ConnectFailed,

    /// <summary>服务端要求更高版本的协议，需要更新启动器。</summary>
    UpdateRequired
}

public sealed record LinkAnnouncementResult(
    IReadOnlyList<LinkAnnounceInfo> Announcements,
    LinkAnnouncementFailure? Failure);

/// <summary>
///     获取联机公告与大厅可用性，对应 WPF 页面中的 GetAnnouncement。
///     成功时会同步更新 <see cref="LobbyInfoProvider" /> 与 <see cref="ETRelay.RelayList" />。
/// </summary>
public static class LinkAnnouncementService
{
    private const int RequestTimeoutMs = 7000;

    public static async Task<LinkAnnouncementResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        var servers = LinkHost.LinkServers;
        // 未配置联机服务器（如本地或第三方构建）时直接提示，不去请求空地址
        if (servers.Length == 0)
        {
            LogWrapper.Info("Link", "No lobby server configured, lobby is unavailable");
            LobbyInfoProvider.IsLobbyAvailable = false;
            return new LinkAnnouncementResult([], LinkAnnouncementFailure.NoServer);
        }

        try
        {
            JsonObject? root = null;
            foreach (var server in servers)
            {
                try
                {
                    // 先取缓存版本号，没有变化就直接用本地缓存
                    var cacheText = (await GetStringAsync($"{server}/api/link/v2/cache.ini", cancellationToken)
                        .ConfigureAwait(false)).Trim();
                    var cacheVer = int.Parse(cacheText);
                    if (cacheVer == States.Link.AnnounceCacheVer && !string.IsNullOrEmpty(States.Link.AnnounceCache))
                    {
                        LogWrapper.Info("Link", "Using cached announcement data");
                        root = JsonNode.Parse(States.Link.AnnounceCache) as JsonObject;
                    }
                    else
                    {
                        LogWrapper.Info("Link", "Fetching new announcement data");
                        var received = await GetStringAsync($"{server}/api/link/v2/announce.json", cancellationToken)
                            .ConfigureAwait(false);
                        root = JsonNode.Parse(received) as JsonObject;
                        States.Link.AnnounceCache = received;
                        States.Link.AnnounceCacheVer = cacheVer;
                        States.Link.Save();
                    }

                    if (root is not null) break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogWrapper.Warn(ex, "Link", $"Failed to get announcement from server {server}");
                    States.Link.AnnounceCache = "";
                    States.Link.AnnounceCacheVer = 0;
                }
            }

            if (root is null) throw new InvalidOperationException("Failed to fetch lobby data");

            LobbyInfoProvider.IsLobbyAvailable = root["available"]?.GetValue<bool>() ?? false;
            LobbyInfoProvider.AllowCustomName = root["allowCustomName"]?.GetValue<bool>() ?? false;
            LobbyInfoProvider.RequiresLogin = root["requireLogin"]?.GetValue<bool>() ?? true;
            LobbyInfoProvider.RequiresRealName = root["requireRealname"]?.GetValue<bool>() ?? true;

            if ((root["version"]?.GetValue<double>() ?? 0) > LobbyInfoProvider.ProtocolVersion)
            {
                LobbyInfoProvider.IsLobbyAvailable = false;
                return new LinkAnnouncementResult([], LinkAnnouncementFailure.UpdateRequired);
            }

            var announcements = new List<LinkAnnounceInfo>();
            if (root["notices"] is JsonArray notices)
                foreach (var notice in notices)
                {
                    var content = notice?["content"]?.ToString();
                    if (string.IsNullOrWhiteSpace(content)) continue;

                    // 版本过滤
                    var minVer = notice!["minVer"]?.GetValue<double>() ?? double.MinValue;
                    var maxVer = notice["maxVer"]?.GetValue<double>() ?? double.MaxValue;
                    if (LinkHost.VersionCode < minVer || LinkHost.VersionCode > maxVer) continue;

                    var type = notice["type"]?.ToString().ToLowerInvariant() switch
                    {
                        "important" or "red" => LinkAnnounceType.Important,
                        "warning" or "yellow" => LinkAnnounceType.Warning,
                        _ => LinkAnnounceType.Notice
                    };

                    // 按行拆分公告
                    foreach (var line in content.Split('\n'))
                        if (!string.IsNullOrWhiteSpace(line))
                            announcements.Add(new LinkAnnounceInfo(type, line));
                }

            var relayList = new List<ETRelay>();
            if (root["relays"] is JsonArray relays)
                foreach (var relay in relays)
                    relayList.Add(new ETRelay
                    {
                        Name = relay?["name"]?.ToString() ?? "",
                        Url = relay?["url"]?.ToString() ?? "",
                        Type = relay?["type"]?.ToString() == "official" ? ETRelayType.Selfhosted : ETRelayType.Community
                    });
            ETRelay.RelayList = relayList;

            return new LinkAnnouncementResult(announcements, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LobbyInfoProvider.IsLobbyAvailable = false;
            LogWrapper.Warn(ex, "Link", "Failed to get lobby announcement");
            return new LinkAnnouncementResult([], LinkAnnouncementFailure.ConnectFailed);
        }
    }

    private static async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(RequestTimeoutMs);
        using var response = await HttpRequest.Create(url).SendAsync(cts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
    }
}
