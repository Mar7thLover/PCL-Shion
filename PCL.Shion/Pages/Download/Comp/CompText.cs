using System;
using System.Collections.Generic;
using Avalonia.Controls;
using PCL.Shion.Core.Online;
using PCL.Shion.Core.Sources;
using PCL.Shion.Localization;

namespace PCL.Shion.Pages.Download.Comp;

/// <summary>
/// 指定页面切换时的目标版本，用于把资源列表的筛选项设为与该版本一致。
/// </summary>
/// <param name="VanillaName">原版版本名，例如 "1.20.1"。</param>
/// <param name="Loaders">该版本已安装的加载器。</param>
public sealed record CompTargetVersion(string VanillaName, IReadOnlyList<CompLoaderType> Loaders);

/// <summary>
/// 资源页面共用的网络访问入口。
/// </summary>
internal static class CompServices
{
    /// <summary>当前下载源决定的镜像源策略。</summary>
    public static CompSourceStrategy Strategy =>
        App.Services.Settings.DownloadSource == DownloadSource.Bmclapi
            ? CompSourceStrategy.MirrorFirst
            : CompSourceStrategy.Balanced;

    public static CompApi CreateApi() => new(App.Services.Engine.Http, Strategy);

    public static CompSearchEngine CreateSearchEngine() => new(CreateApi());
}

/// <summary>
/// 资源页面的界面文字与标签读取。
/// </summary>
public static class CompText
{
    /// <summary>英文前后不含空格的可读资源类型名。</summary>
    public static string TypeName(CompType type) => Lang.Text(type switch
    {
        CompType.Mod => "Download.Comp.Type.Mod",
        CompType.ModPack => "Download.Comp.Type.Modpack",
        CompType.ResourcePack => "Download.Comp.Type.ResourcePack",
        CompType.Shader => "Download.Comp.Type.Shader",
        CompType.DataPack => "Download.Comp.Type.DataPack",
        CompType.Plugin => "Download.Comp.Type.Plugin",
        CompType.World => "Download.Comp.Type.World",
        CompType.Schematic => "Download.Comp.Type.Schematic",
        _ => "Download.Comp.Type.Unknown"
    });

    /// <summary>加载中显示的资源类型名。</summary>
    public static string LoadingName(CompType type) => Lang.Text(type switch
    {
        CompType.Mod => "Download.Comp.List.Loading.Mod",
        CompType.ModPack => "Download.Comp.List.Loading.Modpack",
        CompType.ResourcePack => "Download.Comp.List.Loading.ResourcePack",
        CompType.Shader => "Download.Comp.List.Loading.Shader",
        CompType.DataPack => "Download.Comp.List.Loading.DataPack",
        CompType.Plugin => "Download.Comp.List.Loading.Plugin",
        CompType.World => "Download.Comp.List.Loading.World",
        CompType.Schematic => "Download.Comp.List.Loading.Schematic",
        _ => "Download.Comp.List.Loading.Unknown"
    });

    /// <summary>搜索框提示文字使用的资源类型名。</summary>
    public static string SearchName(CompType type) => Lang.Text(type switch
    {
        CompType.Mod => "Download.Comp.List.Search.Mod",
        CompType.ModPack => "Download.Comp.List.Search.Modpack",
        CompType.ResourcePack => "Download.Comp.List.Search.ResourcePack",
        CompType.Shader => "Download.Comp.List.Search.Shader",
        CompType.DataPack => "Download.Comp.List.Search.DataPack",
        CompType.Plugin => "Download.Comp.List.Search.Plugin",
        CompType.World => "Download.Comp.List.Search.World",
        CompType.Schematic => "Download.Comp.List.Search.Schematic",
        _ => "Download.Comp.List.Search.Unknown"
    });

    /// <summary>读取下拉框选项的 Tag 文本，没有则为空字符串。</summary>
    public static string TagOf(object? item) => (item as Control)?.Tag?.ToString() ?? "";
}
