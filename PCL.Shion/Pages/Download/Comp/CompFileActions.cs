using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Online;
using PCL.Shion.Localization;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download.Comp;

/// <summary>
/// 资源文件的保存与下载。
/// </summary>
internal static class CompFileActions
{
    /// <summary>
    /// 弹出保存位置选择框，并把文件下载到选定的路径。
    /// </summary>
    public static async Task SaveAsync(Control anchor, CompProject project, CompFile file)
    {
        var topLevel = TopLevel.GetTopLevel(anchor);
        if (topLevel is null)
            return;

        var fileName = CompNaming.GetFileName(project, file);
        var extension = System.IO.Path.GetExtension(file.FileName);
        var picked = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Lang.Text("Download.Comp.Detail.SelectSaveLocation"),
            SuggestedFileName = fileName,
            FileTypeChoices =
            [
                new FilePickerFileType(Lang.Text("Download.Comp.Detail.ResourceFile.Filter", CompText.TypeName(file.Type)))
                {
                    Patterns = [$"*{extension}"]
                }
            ]
        });
        if (picked?.Path.LocalPath is not { Length: > 0 } path)
            return;

        await DownloadAsync(file.ToDownloadItem(path), project, file);
    }

    /// <summary>
    /// 下载一个文件，结果以提示的形式显示。
    /// </summary>
    public static async Task DownloadAsync(PCL.Shion.Core.Download.DownloadItem item, CompProject project, CompFile file)
    {
        try
        {
            var result = await App.Services.Engine.DownloadAsync(
                new List<PCL.Shion.Core.Download.DownloadItem> { item }, null, CancellationToken.None);
            if (result.Failed.Count > 0)
            {
                Hint.Show(Lang.Text("Download.Comp.Detail.Task.Failed", file.FileName,
                    string.Join("\n", result.Errors.Values.SelectMany(e => e).Select(e => e.ToString()))), HintType.Error);
                return;
            }

            Hint.Show(Lang.Text("Download.Comp.QuickDownload.Hint.DownloadStarted", project.RawName), HintType.Success);
            App.Services.Notify(project.RawName, file.FileName);
        }
        catch (Exception ex)
        {
            Hint.Show(Lang.Text("Download.Comp.Error.OperationFailed") + ": " + ex.Message, HintType.Error);
        }
    }
}

/// <summary>
/// 资源卡片的快速下载：下载该资源最新（优先正式版）的兼容文件。
/// </summary>
internal static class CompQuickDownload
{
    /// <summary>
    /// 为工程选出最新文件，并让用户选择保存位置后开始下载。
    /// </summary>
    public static void Run(Control anchor, CompProject project)
    {
        _ = RunAsync(anchor, project);
    }

    private static async Task RunAsync(Control anchor, CompProject project)
    {
        try
        {
            Hint.Show(Lang.Text("Download.Comp.QuickDownload.Hint.Loading"), HintType.Info);
            var api = CompServices.CreateApi();
            var files = (await api.GetFilesAsync(project.Id, project.FromCurseForge).ConfigureAwait(false))
                .Where(f => f.Available)
                .ToList();
            files = FilterFilesByType(files, project.Type);
            var file = PickLatestFile(files);
            if (file is null)
            {
                Hint.Show(Lang.Text("Download.Comp.QuickDownload.Hint.NoFile"), HintType.Info);
                return;
            }

            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => CompFileActions.SaveAsync(anchor, project, file));
        }
        catch (Exception ex)
        {
            Hint.Show(Lang.Text("Minecraft.Comp.Error.OperationFailed") + ": " + ex.Message, HintType.Error);
        }
    }

    private static List<CompFile> FilterFilesByType(List<CompFile> files, CompType type)
    {
        if (type == CompType.Shader || type == CompType.ResourcePack)
            return files;
        return files.Where(f => f.Type == type).ToList();
    }

    private static CompFile? PickLatestFile(List<CompFile> files) => files
        .OrderByDescending(f => f.Status == CompFileStatus.Release)
        .ThenByDescending(f => f.ReleaseDate)
        .FirstOrDefault();
}
