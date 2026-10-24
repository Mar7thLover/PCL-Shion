using System;
using System.Text.Json.Nodes;
using PCL.Shion.Pages.Download;

namespace PCL.Shion;

/// <summary>
/// 原版版本列表的分类，对应 WPF 的 McVersionClassifier。
/// 输入为版本清单中的原始 JSON 节点，分类时会按 WPF 的做法就地修改其 id / type / lore 字段。
/// 名称与愚人节说明文字交给 <see cref="VanillaVersionText"/>。
/// </summary>
internal static class McVersionClassifier
{
    /// <summary>
    /// 返回分类在界面上的名称。
    /// </summary>
    public static string GetCategoryDisplayName(VanillaVersionCategory category) =>
        VanillaVersionText.GetCategoryDisplayName(category);

    /// <summary>
    /// 返回愚人节版本的说明文字，不是愚人节版本时返回空字符串。
    /// </summary>
    public static string GetMcFoolName(string name) => VanillaVersionText.GetMcFoolName(name);

    /// <summary>
    /// 读取版本的发布时间，读不到时返回 <see cref="DateTime.MinValue"/>。
    /// </summary>
    public static DateTime GetReleaseTime(JsonObject version)
    {
        // 版本列表里的时间以字符串保存，GetValue<DateTime> 只能读取来自 JSON 文本的节点，因此两种情况都要处理
        if (version["releaseTime"] is not JsonValue value)
            return DateTime.MinValue;
        if (value.TryGetValue<DateTime>(out var time))
            return time;
        return value.TryGetValue<string>(out var text) &&
               DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.RoundtripKind, out time)
            ? time
            : DateTime.MinValue;
    }

    /// <summary>
    /// 把版本归入某个分类，愚人节版本会被就地标记为 special 并补上 lore。
    /// </summary>
    public static VanillaVersionCategory ClassifyVersion(JsonObject version)
    {
        var type = GetString(version, "type");
        var idLower = GetString(version, "id").ToLowerInvariant();

        return type switch
        {
            "release" => VanillaVersionCategory.Release,
            "special" => RefreshAprilFools(version, idLower),
            "snapshot" or "pending" => ClassifySnapshotOrPending(version, idLower),
            _ => VanillaVersionCategory.BeforeRelease
        };
    }

    private static VanillaVersionCategory RefreshAprilFools(JsonObject version, string idLower)
    {
        TryMarkAprilFoolsVersion(version, idLower);
        return VanillaVersionCategory.AprilFools;
    }

    private static VanillaVersionCategory ClassifySnapshotOrPending(JsonObject version, string idLower)
    {
        var category = VanillaVersionCategory.Snapshot;

        if (idLower.StartsWith("1.", StringComparison.Ordinal) &&
            !idLower.Contains("combat", StringComparison.Ordinal) &&
            !idLower.Contains("rc", StringComparison.Ordinal) &&
            !idLower.Contains("experimental", StringComparison.Ordinal) &&
            idLower != "1.2" &&
            !idLower.Contains("pre", StringComparison.Ordinal))
        {
            category = VanillaVersionCategory.Release;
            version["type"] = "release";
        }

        return TryMarkAprilFoolsVersion(version, idLower)
            ? VanillaVersionCategory.AprilFools
            : category;
    }

    private static bool TryMarkAprilFoolsVersion(JsonObject version, string idLower)
    {
        switch (idLower)
        {
            case "2point0_blue":
            case "2point0_red":
            case "2point0_purple":
            case "2.0_blue":
            case "2.0_red":
            case "2.0_purple":
            case "2.0":
                version["id"] = GetString(version, "id").Replace("point", ".", StringComparison.Ordinal);
                MarkAsAprilFools(version, true);
                return true;

            case "20w14infinite":
            case "20w14∞":
                version["id"] = "20w14∞";
                MarkAsAprilFools(version, true);
                return true;

            case "3d shareware v1.34":
            case "1.rv-pre1":
            case "15w14a":
            case "22w13oneblockatatime":
            case "23w13a_or_b":
            case "24w14potato":
            case "25w14craftmine":
            case "26w14a":
                MarkAsAprilFools(version, true);
                return true;

            default:
            {
                // 4/1 自动视作愚人节版，北京时间（UTC+8）按 +2 小时的旧逻辑保留
                var releaseTime = GetReleaseTime(version);
                if (releaseTime == DateTime.MinValue)
                    return false;
                var releaseDate = releaseTime.ToUniversalTime().AddHours(2d);
                if (releaseDate is not { Month: 4, Day: 1 })
                    return false;
                MarkAsAprilFools(version, false);
                return true;
            }
        }
    }

    private static void MarkAsAprilFools(JsonObject version, bool addLore)
    {
        version["type"] = "special";

        if (addLore)
            version["lore"] = GetMcFoolName(GetString(version, "id"));
    }

    private static string GetString(JsonObject obj, string key)
    {
        var node = obj[key];
        if (node is null) return "";

        return node is JsonValue value && value.TryGetValue<string>(out var result)
            ? result
            : node.ToString();
    }
}
