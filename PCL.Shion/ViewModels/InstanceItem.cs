using System;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

/// <summary>实例列表中的一项。</summary>
public sealed class InstanceItem(GameInstance instance)
{
    public GameInstance Instance => instance;

    public string Name => instance.Name;

    /// <summary>出错的实例不可选。</summary>
    public bool IsSelectable => instance.Error is null;

    public bool HasError => instance.Error is not null;

    /// <summary>列表第二行：Minecraft 版本与加载器；出错时是错误信息。</summary>
    public string Description => instance.Error ?? DescribeVersion();

    /// <summary>列表第三行：累计游玩时长与上次游玩日期；出错的实例没有。</summary>
    public string? PlayInfo => instance.Error is null ? DescribePlay() : null;

    public bool HasPlayInfo => PlayInfo is not null;

    private string DescribeVersion()
    {
        var version = instance.MinecraftVersion ?? Strings.InstanceUnknownVersion;
        return instance.Loader is { } loader ? $"{version} · {FormatLoader(loader)}" : version;
    }

    private string DescribePlay() => instance.LastPlayed is { } lastPlayed
        ? LaunchStrings.Played(TimeSpan.FromSeconds(instance.TotalPlaySeconds), lastPlayed)
        : LaunchStrings.NeverPlayed;

    private static string FormatLoader(string loader) => loader.ToLowerInvariant() switch
    {
        "forge" => "Forge",
        "neoforge" => "NeoForge",
        "fabric" => "Fabric",
        "quilt" => "Quilt",
        _ => loader,
    };
}
