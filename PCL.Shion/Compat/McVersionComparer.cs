using PCL.Shion.Core.Online;
using PCL.Shion.Localization;

namespace PCL.Shion;

/// <summary>
/// Minecraft 版本名的比较，对应 WPF 的 McVersionComparer，实际比较交给 <see cref="CompVersions"/>。
/// </summary>
public static class McVersionComparer
{
    /// <summary>
    /// 比较两个版本名；等同 left &gt;= right。无法比较两个预发布版的大小。
    /// </summary>
    public static bool CompareVersionGe(string left, string right) => CompareVersion(left, right) >= 0;

    /// <summary>
    /// 比较两个版本名，left 较新返回 1，相同返回 0，right 较新返回 -1；等同 left - right。
    /// 「未知版本」排在所有具体版本之后。
    /// </summary>
    public static int CompareVersion(string left, string right) =>
        CompVersions.CompareVersion(left, right, Lang.Text("Minecraft.Version.Unknown"));
}
