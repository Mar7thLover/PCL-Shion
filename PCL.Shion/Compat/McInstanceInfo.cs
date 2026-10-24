using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Online;
using PCL.Shion.Localization;

namespace PCL.Shion;

/// <summary>
/// 某个 Minecraft 实例的原版版本名与附加组件信息，对应 WPF 的 McInstanceInfo。
/// 通过 <see cref="GameInstanceExtensions.Info"/> 获取，数据来自实例的版本 JSON 解析结果。
/// </summary>
public sealed class McInstanceInfo
{
    /// <summary>原版版本名，如 1.20.1、26.1-snapshot-1。无法确定时为空字符串。</summary>
    public string VanillaName { get; set; } = "";

    /// <summary>Forge 版本号，如 47.2.0。</summary>
    public string Forge { get; set; } = "";

    /// <summary>NeoForge 版本号，如 21.0.2-beta。</summary>
    public string NeoForge { get; set; } = "";

    /// <summary>Fabric 版本号，如 0.15.11。</summary>
    public string Fabric { get; set; } = "";

    /// <summary>Quilt 版本号，如 0.26.1。</summary>
    public string Quilt { get; set; } = "";

    /// <summary>LegacyFabric 版本号。</summary>
    public string LegacyFabric { get; set; } = "";

    /// <summary>Cleanroom 版本号，如 0.2.4-alpha。</summary>
    public string Cleanroom { get; set; } = "";

    /// <summary>LabyMod 版本号，如 4.2.59。</summary>
    public string LabyMod { get; set; } = "";

    /// <summary>OptiFine 版本号，如 C8、C9_pre10。</summary>
    public string OptiFine { get; set; } = "";

    /// <summary>是否安装了 Forge。</summary>
    public bool HasForge { get; set; }

    /// <summary>是否安装了 NeoForge。</summary>
    public bool HasNeoForge { get; set; }

    /// <summary>是否安装了 Fabric。</summary>
    public bool HasFabric { get; set; }

    /// <summary>是否安装了 Quilt。</summary>
    public bool HasQuilt { get; set; }

    /// <summary>是否安装了 LegacyFabric。</summary>
    public bool HasLegacyFabric { get; set; }

    /// <summary>是否安装了 Cleanroom。</summary>
    public bool HasCleanroom { get; set; }

    /// <summary>是否安装了 LabyMod。</summary>
    public bool HasLabyMod { get; set; }

    /// <summary>是否安装了 LiteLoader。</summary>
    public bool HasLiteLoader { get; set; }

    /// <summary>是否通过版本 JSON 安装了 OptiFine。</summary>
    public bool HasOptiFine { get; set; }

    /// <summary>版本字符串是否符合 Minecraft 原版格式，例如 1.x、26.x。</summary>
    public static bool IsFormatFit(string? version) => CompVersions.IsFormatFit(version);

    /// <summary>尝试将版本字符串转换为 Drop 序数，无法转换时返回 -1。</summary>
    public static int VersionToDrop(string? version, bool allowSnapshot = false) =>
        CompVersions.VersionToDrop(version, allowSnapshot);

    /// <summary>
    /// 从实例解析出附加组件信息。实例出错（Error 不为空）时只返回空的信息。
    /// 版本 JSON 读取失败也按空信息处理，不抛出异常。
    /// </summary>
    internal static McInstanceInfo From(GameInstance instance)
    {
        var info = new McInstanceInfo { VanillaName = instance.MinecraftVersion ?? "" };
        if (instance.Error is not null)
            return info;

        // 实例位于 <root>/versions/<name>，由此推出 .minecraft 根目录
        var root = Path.GetFullPath(Path.Combine(instance.Directory, "..", ".."));
        ResolvedVersion resolved;
        try
        {
            resolved = VersionResolver.Resolve(root, instance.Name);
        }
        catch (Exception)
        {
            return info;
        }

        var names = resolved.Libraries.Select(library => library.Name).ToList();
        var unknown = Lang.Text("Minecraft.Version.Unknown");
        string OrUnknown(string? version) => string.IsNullOrEmpty(version) ? unknown : version;

        switch (resolved.Loader)
        {
            case "forge":
                info.HasForge = true;
                info.Forge = OrUnknown(ForgeVersion(FindVersion(names, "net.minecraftforge:forge:")));
                break;
            case "neoforge":
                info.HasNeoForge = true;
                info.NeoForge = OrUnknown(FindVersion(names, "net.neoforged:neoforge:")
                                          ?? FindVersion(names, "net.neoforged:forge:"));
                break;
            case "fabric":
                info.HasFabric = true;
                info.Fabric = OrUnknown(FindVersion(names, "net.fabricmc:fabric-loader:"));
                break;
            case "quilt":
                info.HasQuilt = true;
                info.Quilt = OrUnknown(FindVersion(names, "org.quiltmc:quilt-loader:"));
                break;
        }

        if (FindVersion(names, "com.mumfrey:liteloader:") is not null)
            info.HasLiteLoader = true;

        if (FindVersion(names, "optifine:OptiFine:") is { } optiFine)
        {
            info.HasOptiFine = true;
            // 库版本形如 1.20.1_HD_U_I6，取 HD_U_ 之后的部分，与 WPF 中的 OptiFine 字段一致
            const string marker = "HD_U_";
            var index = optiFine.IndexOf(marker, StringComparison.Ordinal);
            info.OptiFine = index >= 0 ? optiFine[(index + marker.Length)..] : optiFine;
        }

        return info;
    }

    /// <summary>
    /// 在库列表中查找以 prefix 开头的第一个 Maven 名称，返回其版本段；没有则返回 null。
    /// </summary>
    private static string? FindVersion(IEnumerable<string> names, string prefix)
    {
        foreach (var name in names)
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var coordinates = name.Split('@')[0].Split(':');
            if (coordinates.Length >= 3)
                return coordinates[2];
        }

        return null;
    }

    /// <summary>
    /// Forge 库版本形如 1.20.1-47.2.0 或 1.7.10-10.13.4.1614-1.7.10，取中间的 Forge 版本号。
    /// </summary>
    private static string? ForgeVersion(string? version)
    {
        if (version is null)
            return null;
        var parts = version.Split('-');
        return parts.Length >= 2 ? parts[1] : version;
    }
}
