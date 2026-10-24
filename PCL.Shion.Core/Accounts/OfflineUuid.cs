using PCL.Shion.Core.Launch;

namespace PCL.Shion.Core.Accounts;

/// <summary>
/// 离线档案的 UUID 计算。
/// </summary>
public static class OfflineUuid
{
    /// <summary>
    /// 标准离线 UUID，与 Java 的 <c>UUID.nameUUIDFromBytes("OfflinePlayer:" + 名称)</c> 相同。
    /// </summary>
    public static string Standard(string name) => OfflineAccount.Create(name).Uuid;

    /// <summary>
    /// 旧版离线 UUID，与早期版本的启动器保持一致，便于沿用已有存档的玩家数据。
    /// </summary>
    public static string Legacy(string name)
    {
        var fullUuid = name.Length.ToString("X").PadLeft(16, '0') + Hash(name).ToString("X").PadLeft(16, '0');
        return fullUuid[..12] + "3" + fullUuid[13..16] + "9" + fullUuid[17..];
    }

    private static ulong Hash(string text)
    {
        var hash = 5381UL;
        foreach (var character in text)
            hash = (hash << 5) ^ hash ^ character;
        return hash ^ 0xA98F501BC684032FUL;
    }
}
