using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PCL.Shion.Core.Mods;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download.Comp;

/// <summary>
/// 下载 Mod 时检查目标文件夹里是否已有同一个 Mod：下载前找完全相同的文件，下载后按 Mod ID 找其他版本。
/// </summary>
internal static class ModDuplicateCheck
{
    /// <summary>
    /// 读取文件夹里已有的 Mod（*.jar），不包括 <paramref name="exceptPath" />。读不了的文件会被跳过。
    /// </summary>
    public static Task<List<LocalMod>> ScanAsync(string folder, string exceptPath) => Task.Run(() =>
    {
        var result = new List<LocalMod>();
        if (!Directory.Exists(folder))
            return result;
        foreach (var file in Directory.EnumerateFiles(folder, "*.jar"))
        {
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(exceptPath), StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                result.Add(ModScanner.Read(file));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // 正被占用或无权读取的文件不参与比较
            }
        }

        return result;
    });

    /// <summary>
    /// 下载前：文件夹里已有内容完全相同的文件时询问是否仍要下载。返回 false 表示取消下载。
    /// </summary>
    public static async Task<bool> ConfirmIdenticalAsync(IReadOnlyList<LocalMod> existing, string? sha1)
    {
        if (sha1 is not { Length: 40 })
            return true;
        var same = existing.FirstOrDefault(mod => string.Equals(mod.Sha1, sha1, StringComparison.OrdinalIgnoreCase));
        if (same is null)
            return true;
        var choice = await MsgBox.ShowAsync(
            Lang.Text("Download.Comp.Duplicate.Identical.Message", same.FileName),
            Lang.Text("Download.Comp.Duplicate.Title"),
            Lang.Text("Download.Comp.Duplicate.Identical.Download"),
            Lang.Text("Common.Action.Cancel"));
        return choice == 1;
    }

    /// <summary>
    /// 下载后：读取新文件的 Mod ID，文件夹里有同一 Mod 的其他已启用文件时，询问是否把旧文件移到回收站。
    /// </summary>
    public static async Task CheckOtherVersionsAsync(IReadOnlyList<LocalMod> existing, string downloadedPath)
    {
        LocalMod downloaded;
        try
        {
            downloaded = await Task.Run(() => ModScanner.Read(downloadedPath));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return;
        }

        if (downloaded.Id is null)
            return;
        var others = existing
            .Where(mod => mod.Enabled && string.Equals(mod.Id, downloaded.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (others.Count == 0)
            return;

        var choice = await MsgBox.ShowAsync(
            Lang.Text("Download.Comp.Duplicate.OtherVersion.Message", downloaded.Name ?? downloaded.Id,
                string.Join("\n", others.Select(mod => mod.FileName))),
            Lang.Text("Download.Comp.Duplicate.Title"),
            Lang.Text("Download.Comp.Duplicate.OtherVersion.Remove"),
            Lang.Text("Download.Comp.Duplicate.OtherVersion.Keep"),
            isWarn: true);
        if (choice != 1)
            return;

        var failed = new List<string>();
        foreach (var mod in others)
        {
            try
            {
                Trash.MoveToTrash(mod.FilePath);
            }
            catch (Exception)
            {
                failed.Add(mod.FileName);
            }
        }

        if (failed.Count == 0)
            Hint.Show(Lang.Text("Download.Comp.Duplicate.OtherVersion.Removed", others.Count), HintType.Success);
        else
            Hint.Show(Lang.Text("Download.Comp.Duplicate.OtherVersion.RemoveFailed", string.Join(", ", failed)), HintType.Error);
    }
}
