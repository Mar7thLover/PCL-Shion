using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Text.RegularExpressions;

namespace PCL.Shion.Core.Link;

/// <summary>联机代码用到的字符串辅助方法，与 WPF 版 PCL.Core.Utils.Exts 对应。</summary>
public static class LinkStringHelper
{
    extension(string value)
    {
        public bool IsNullOrWhiteSpace() => string.IsNullOrWhiteSpace(value);

        public bool IsASCII() => value.All(c => c < 128);

        public List<string> RegexSearch(Regex regex) =>
            regex.Matches(value).Select(m => m.Value).ToList();

        /// <summary>将 Base32 文本重新编码为 Base10 文本。</summary>
        public string FromB32ToB10()
        {
            var ns = value.Select(Parse).ToArray();
            var nb = ns.Aggregate(new BigInteger(0), (n, i) => n * 32 + i);
            return nb.ToString();

            int Parse(char c) => c switch
            {
                >= '2' and <= '9' => c - '2',
                >= 'A' and <= 'H' => c - 'A' + 8,
                >= 'J' and <= 'N' => c - 'J' + 16,
                >= 'P' and <= 'Z' => c - 'P' + 21,
                _ => throw new ArgumentOutOfRangeException(nameof(value), $"Character '{c}' out of Base32 range")
            };
        }
    }
}

public static partial class RegexPatterns
{
    public static readonly Regex TerracottaId = new("([0-9A-Z]{5}-){4}[0-9A-Z]{5}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public static readonly Regex BroadcastMotd = new(@"\[MOTD\](.*?)\[/MOTD\]", RegexOptions.Compiled);
    public static readonly Regex Iso8601EndOfDay = new(@"[Tt]24:00(?::00(?:\.0+)?)?(?=[Zz+\-]|$)", RegexOptions.Compiled);
    public static readonly Regex BroadcastAd = new(@"\[AD\](.*?)\[/AD\]", RegexOptions.Compiled);
}

public static class NetworkUtils
{
    private static readonly Lazy<IPAddress[]> LocalIpAddresses = new(() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(x => x is { OperationalStatus: OperationalStatus.Up })
        .SelectMany(x => x.GetIPProperties().UnicastAddresses)
        .Where(ua => ua.Address is { AddressFamily: AddressFamily.InterNetwork or AddressFamily.InterNetworkV6 } &&
                     !ua.Address.Equals(IPAddress.Any) &&
                     !ua.Address.Equals(IPAddress.IPv6Any) &&
                     !ua.Address.Equals(IPAddress.Loopback) &&
                     !ua.Address.Equals(IPAddress.IPv6Loopback))
        .Select(ua => ua.Address)
        .ToArray());

    public static IPAddress[] GetAllLocalAddress() => LocalIpAddresses.Value;
}

public static class SocketExtensions
{
    public static void CloseGracefully(this Socket? socket)
    {
        if (socket is null) return;

        if (socket is { IsBound: false, Connected: false })
        {
            socket.Dispose();
            return;
        }

        if (socket.Connected)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // 忽略关闭时的任何错误
            }
        }

        try
        {
            socket.Close();
        }
        catch
        {
            // 忽略关闭时的任何错误
        }
    }
}
