using System;
using System.Collections.Generic;
using System.IO;
using PCL.Shion.Core.Online;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;

namespace PCL.Shion.Pages.Download.Comp;

/// <summary>
/// 资源收藏夹的持久化，保存在 <c>PCL Shion/comp-favorites.json</c>。
/// </summary>
public static class CompFavoriteStore
{
    private static List<CompFavoriteFolder>? _folders;

    private static string FilePath => Path.Combine(
        SpecialFolders.Get(Environment.SpecialFolder.ApplicationData), "PCL Shion", "comp-favorites.json");

    /// <summary>
    /// 全部收藏夹，首次访问时从磁盘读取；至少包含一个收藏夹。
    /// </summary>
    public static List<CompFavoriteFolder> Folders
    {
        get
        {
            if (_folders is not null)
                return _folders;
            string? raw = null;
            try
            {
                if (File.Exists(FilePath))
                    raw = File.ReadAllText(FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            _folders = CompFavorites.Parse(raw, Lang.Text("Download.Comp.Detail.Favorites.DefaultName"));
            return _folders;
        }
    }

    /// <summary>
    /// 工程是否已在任一收藏夹中。
    /// </summary>
    public static bool IsFavourite(string id) => CompFavorites.IsFavourite(Folders, id);

    /// <summary>
    /// 保存收藏夹到磁盘。写入失败时保留内存中的数据。
    /// </summary>
    public static void Save()
    {
        try
        {
            var json = CompFavorites.Serialize(Folders);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
