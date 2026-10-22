using System.Security.Cryptography;
using System.Text;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 离线账户。UUID 与 Java 的 UUID.nameUUIDFromBytes("OfflinePlayer:" + 名称) 一致。
/// </summary>
/// <param name="Name">玩家名。</param>
/// <param name="Uuid">32 位小写无横线 UUID。</param>
/// <param name="AccessToken">离线账户没有真实令牌，直接使用 UUID。</param>
public sealed record OfflineAccount(string Name, string Uuid, string AccessToken)
{
    public static OfflineAccount Create(string name)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"OfflinePlayer:{name}"));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        var uuid = Convert.ToHexStringLower(hash);
        return new OfflineAccount(name, uuid, uuid);
    }

    /// <summary>
    /// 玩家名只能由 3 到 16 个字母、数字或下划线组成。
    /// </summary>
    public static bool IsValidName(string name)
    {
        return name.Length is >= 3 and <= 16 && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
    }
}
