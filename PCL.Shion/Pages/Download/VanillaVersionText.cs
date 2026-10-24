using System;
using System.Linq;
using PCL.Shion.Core.Versions;
using PCL.Shion.Localization;

namespace PCL.Shion.Pages.Download;

/// <summary>
/// 原版版本列表的分类枚举，对应 WPF 的 McVersionCategory。
/// </summary>
internal enum VanillaVersionCategory
{
    Release,
    Snapshot,
    BeforeRelease,
    AprilFools
}

/// <summary>
/// 分类后的原版版本。<see cref="Lore"/> 是愚人节版本的附加说明，没有则为 null。
/// </summary>
internal sealed record ClassifiedVanillaVersion(RemoteVersion Version, VanillaVersionCategory Category, string? Lore);

/// <summary>
/// 原版版本的分类与显示名称，对应 WPF 的 McVersionClassifier 与 McFormatter。
/// </summary>
internal static class VanillaVersionText
{
    /// <summary>
    /// 返回分类在界面上的名称。
    /// </summary>
    public static string GetCategoryDisplayName(VanillaVersionCategory category)
    {
        return category switch
        {
            VanillaVersionCategory.Release => Lang.Text("Download.Version.Type.Release"),
            VanillaVersionCategory.Snapshot => Lang.Text("Download.Version.Type.Development"),
            VanillaVersionCategory.BeforeRelease => Lang.Text("Download.Version.Type.BeforeRelease"),
            VanillaVersionCategory.AprilFools => Lang.Text("Download.Version.Type.AprilFools"),
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
    }

    /// <summary>
    /// 把版本归入某个分类。愚人节版本可能因为 ID 或发布日期被识别，ID 写法不规范的会被改为规范写法。
    /// </summary>
    public static ClassifiedVanillaVersion Classify(RemoteVersion version)
    {
        var idLower = version.Id.ToLowerInvariant();
        return version.Type switch
        {
            "release" => new ClassifiedVanillaVersion(version, VanillaVersionCategory.Release, null),
            "special" => TryAprilFools(version, idLower) ?? new ClassifiedVanillaVersion(version, VanillaVersionCategory.AprilFools, null),
            "snapshot" or "pending" => ClassifySnapshotOrPending(version, idLower),
            _ => new ClassifiedVanillaVersion(version, VanillaVersionCategory.BeforeRelease, null)
        };
    }

    private static ClassifiedVanillaVersion ClassifySnapshotOrPending(RemoteVersion version, string idLower)
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
        }

        return TryAprilFools(version, idLower) ?? new ClassifiedVanillaVersion(version, category, null);
    }

    // 识别愚人节版本，识别不出时返回 null
    private static ClassifiedVanillaVersion? TryAprilFools(RemoteVersion version, string idLower)
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
            {
                var id = version.Id.Replace("point", ".", StringComparison.Ordinal);
                var renamed = version with { Id = id, Type = "special" };
                return new ClassifiedVanillaVersion(renamed, VanillaVersionCategory.AprilFools, GetMcFoolName(id));
            }

            case "20w14infinite":
            case "20w14∞":
            {
                var renamed = version with { Id = "20w14∞", Type = "special" };
                return new ClassifiedVanillaVersion(renamed, VanillaVersionCategory.AprilFools, GetMcFoolName("20w14∞"));
            }

            case "3d shareware v1.34":
            case "1.rv-pre1":
            case "15w14a":
            case "22w13oneblockatatime":
            case "23w13a_or_b":
            case "24w14potato":
            case "25w14craftmine":
            case "26w14a":
            {
                var special = version with { Type = "special" };
                return new ClassifiedVanillaVersion(special, VanillaVersionCategory.AprilFools, GetMcFoolName(version.Id));
            }

            default:
            {
                var releaseDate = version.ReleaseTime.ToUniversalTime().AddHours(2d);
                if (releaseDate.Month != 4 || releaseDate.Day != 1)
                    return null;
                var special = version with { Type = "special" };
                return new ClassifiedVanillaVersion(special, VanillaVersionCategory.AprilFools, null);
            }
        }
    }

    /// <summary>
    /// 返回愚人节版本的说明文字，不是愚人节版本时返回空字符串。
    /// </summary>
    public static string GetMcFoolName(string name)
    {
        name = name.ToLowerInvariant();
        return name switch
        {
            _ when name.StartsWith("2.0", StringComparison.Ordinal) || name.StartsWith("2point0", StringComparison.Ordinal)
                => Lang.Text("Minecraft.Fool.Description.2013") + name switch
                {
                    _ when name.EndsWith("red", StringComparison.Ordinal) => Lang.Text("Minecraft.Fool.Tag.Red"),
                    _ when name.EndsWith("blue", StringComparison.Ordinal) => Lang.Text("Minecraft.Fool.Tag.Blue"),
                    _ when name.EndsWith("purple", StringComparison.Ordinal) => Lang.Text("Minecraft.Fool.Tag.Purple"),
                    _ => ""
                },
            "15w14a" => Lang.Text("Minecraft.Fool.Description.2015"),
            "1.rv-pre1" => Lang.Text("Minecraft.Fool.Description.2016"),
            "3d shareware v1.34" => Lang.Text("Minecraft.Fool.Description.2019"),
            _ when name.StartsWith("20w14inf", StringComparison.Ordinal) || name == "20w14∞" => Lang.Text("Minecraft.Fool.Description.2020"),
            "22w13oneblockatatime" => Lang.Text("Minecraft.Fool.Description.2022"),
            "23w13a_or_b" => Lang.Text("Minecraft.Fool.Description.2023"),
            "24w14potato" => Lang.Text("Minecraft.Fool.Description.2024"),
            "25w14craftmine" => Lang.Text("Minecraft.Fool.Description.2025"),
            "26w14a" => Lang.Text("Minecraft.Fool.Description.2026"),
            _ => ""
        };
    }

    /// <summary>
    /// 把版本号转为玩家熟悉的名称，如 Beta 1.6 显示为 Beta_1.6。
    /// </summary>
    public static string FormatVersion(string gameVersion)
    {
        var id = gameVersion.ToLowerInvariant();
        switch (id)
        {
            case "0.30-1":
            case "0.30-2":
            case "c0.30_01c":
                return "Classic_0.30";
            case "in-20100206-2103":
                return "Indev_20100206";
            case "inf-20100630-1":
                return "Infdev_20100630";
            case "inf-20100630-2":
                return "Alpha_v1.0.0";
            case "1.19_deep_dark_experimental_snapshot-1":
                return "1.19-exp1";
            case "in-20100130":
                return "Indev_0.31_20100130";
            case "b1.6-tb3":
                return "Beta_1.6_Test_Build_3";
            case "1_14_combat-212796":
                return "1.14.3_-_Combat_Test";
            case "1_14_combat-0":
                return "Combat_Test_2";
            case "1_14_combat-3":
                return "Combat_Test_3";
            case "1_15_combat-1":
                return "Combat_Test_4";
            case "1_15_combat-6":
                return "Combat_Test_5";
            case "1_16_combat-0":
                return "Combat_Test_6";
            case "1_16_combat-1":
                return "Combat_Test_7";
            case "1_16_combat-2":
                return "Combat_Test_7b";
            case "1_16_combat-3":
                return "Combat_Test_7c";
            case "1_16_combat-4":
                return "Combat_Test_8";
            case "1_16_combat-5":
                return "Combat_Test_8b";
            case "1_16_combat-6":
                return "Combat_Test_8c";
        }

        if (id.StartsWith("1.0.0-rc2", StringComparison.Ordinal)) return "RC2";
        if (id.StartsWith("2.0", StringComparison.Ordinal) || id.StartsWith("2point0", StringComparison.Ordinal)) return "2.0";
        if (id.StartsWith("b1.8-pre1", StringComparison.Ordinal)) return "Beta_1.8-pre1";
        if (id.StartsWith("b1.1-", StringComparison.Ordinal)) return "Beta_1.1";
        if (id.StartsWith("a1.1.0", StringComparison.Ordinal)) return "Alpha_v1.1.0";
        if (id.StartsWith("a1.0.14", StringComparison.Ordinal)) return "Alpha_v1.0.14";
        if (id.StartsWith("a1.0.13_01", StringComparison.Ordinal)) return "Alpha_v1.0.13_01";
        if (id.StartsWith("in-20100214", StringComparison.Ordinal)) return "Indev_20100214";

        if (id.Contains("experimental-snapshot", StringComparison.Ordinal)) return id.Replace("_experimental-snapshot-", "-exp", StringComparison.Ordinal);

        if (id.StartsWith("inf-", StringComparison.Ordinal)) return "Infdev_" + id[4..];
        if (id.StartsWith("in-", StringComparison.Ordinal)) return "Indev_" + id[3..];
        if (id.StartsWith("rd-", StringComparison.Ordinal)) return "pre-Classic_" + id;
        if (id.StartsWith('b')) return "Beta_" + id[1..];
        if (id.StartsWith('a')) return "Alpha_v" + id[1..];

        return id.StartsWith('c') ? ("Classic_" + id[1..]).Replace("st", "SURVIVAL_TEST", StringComparison.Ordinal) : id;
    }

    /// <summary>
    /// 返回版本在 Minecraft Wiki 上的更新日志地址，根据界面语言选择站点。
    /// </summary>
    public static string GetWikiUrl(string gameVersion)
    {
        var langCode = LocalizationService.CurrentLanguage.Code;
        var prefix = new[] { "de", "es", "fr", "it", "ja", "ko", "lzh", "nl", "pt", "ru", "th", "uk", "zh" }
            .FirstOrDefault(code => langCode.StartsWith(code, StringComparison.Ordinal));
        var baseUrl = prefix is null ? "https://minecraft.wiki" : $"https://{prefix}.minecraft.wiki";

        var formattedVersion = FormatVersion(gameVersion);
        string suffix;
        if (!langCode.StartsWith("zh", StringComparison.Ordinal))
            suffix = "/w/Special:Search?search=" + Uri.EscapeDataString($"Java Edition {formattedVersion}");
        else if (gameVersion.Contains('w'))
            suffix = formattedVersion;
        else
            suffix = "Java版" + formattedVersion;
        return $"{baseUrl}/{suffix.TrimStart('/')}";
    }
}
