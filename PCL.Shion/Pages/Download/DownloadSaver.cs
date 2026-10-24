using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Platform.Storage;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Sources;
using PCL.Shion.Core.Versions;
using PCL.Shion.Localization;
using PCL.Shion.Services;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Download;

/// <summary>
/// 版本列表里“保存”操作的执行：保存加载器安装器、下载原版客户端与服务端。
/// 同一个任务正在进行时不会重复开始，结果以提示的形式告知。
/// </summary>
internal static class DownloadSaver
{
    private static readonly HashSet<string> Ongoing = new(StringComparer.Ordinal);

    private static readonly object OngoingLock = new();

    /// <summary>
    /// 让用户选择保存位置，然后下载加载器的安装器。
    /// </summary>
    /// <param name="top">弹出文件选择框所在的顶层窗口。</param>
    /// <param name="loaderName">加载器名称，如 Forge，用于文件类型名称。</param>
    /// <param name="displayName">任务显示名称，如 Forge 1.20.1 - 47.4.0。</param>
    /// <param name="fileName">建议的文件名。</param>
    /// <param name="urls">安装器的候选地址，按优先级排列。</param>
    public static async Task SaveInstallerAsync(TopLevel? top, string loaderName, string displayName, string fileName, IReadOnlyList<Uri> urls)
    {
        if (top is null)
            return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Lang.Text("Download.Version.SelectSaveLocation"),
            SuggestedFileName = fileName,
            FileTypeChoices = [new FilePickerFileType(Lang.Text("Minecraft.Download.Stage.ForgelikeInstallerFilter", loaderName, "jar")) { Patterns = ["*.jar"] }]
        });
        var target = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(target))
            return;

        var name = Lang.Text("Minecraft.Download.Stage.ForgelikeDownload", displayName);
        await RunAsync(name, async () =>
        {
            var result = await App.Services.Engine.DownloadAsync([new DownloadItem(urls, target)], null, CancellationToken.None);
            return result.Succeeded;
        });
    }

    /// <summary>
    /// 让用户选择保存文件夹，然后把原版客户端的版本 JSON 与客户端 jar 保存到 <c>&lt;文件夹&gt;/&lt;版本号&gt;</c>。
    /// </summary>
    public static async Task SaveClientAsync(TopLevel? top, RemoteVersion version)
    {
        var folder = await PickVersionFolderAsync(top, version.Id);
        if (folder is null)
            return;

        var name = Lang.Text("Minecraft.Download.Stage.MinecraftDownload", version.Id);
        await RunAsync(name, () => SaveClientCoreAsync(version, folder));
    }

    /// <summary>
    /// 让用户选择保存文件夹，然后下载原版服务端，并在同一文件夹生成启动脚本。
    /// </summary>
    public static async Task SaveServerAsync(TopLevel? top, RemoteVersion version)
    {
        var folder = await PickVersionFolderAsync(top, version.Id);
        if (folder is null)
            return;

        var name = Lang.Text("Minecraft.Download.Stage.MinecraftServerDownload", version.Id);
        await RunAsync(name, () => SaveServerCoreAsync(version, folder));
    }

    private static async Task<string?> PickVersionFolderAsync(TopLevel? top, string versionId)
    {
        if (top is null)
            return null;

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Lang.Text("Download.Version.SelectSaveLocation"),
            AllowMultiple = false
        });
        var root = folders.FirstOrDefault()?.TryGetLocalPath();
        return string.IsNullOrEmpty(root) ? null : Path.Combine(root, versionId);
    }

    // 客户端：版本 JSON 与客户端 jar
    private static async Task<bool> SaveClientCoreAsync(RemoteVersion version, string folder)
    {
        var engine = App.Services.Engine;
        var source = App.Services.Source;
        var id = version.Id;
        Directory.CreateDirectory(folder);

        var jsonPath = Path.Combine(folder, id + ".json");
        var json = await engine.DownloadAsync(
            [new DownloadItem(MirrorUrls.Resolve(version.JsonUrl.AbsoluteUri, source), jsonPath, Sha1: version.Sha1)],
            null,
            CancellationToken.None);
        if (!json.Succeeded)
            return false;

        var client = ReadDownload(jsonPath, "client", Path.Combine(folder, id + ".jar"), source)
            ?? throw new InvalidDataException(Lang.Text("Minecraft.Download.Error.NotFound"));
        var jar = await engine.DownloadAsync([client], null, CancellationToken.None);
        return jar.Succeeded;
    }

    // 服务端：版本 JSON、服务端 jar 与启动脚本，JSON 用完即删
    private static async Task<bool> SaveServerCoreAsync(RemoteVersion version, string folder)
    {
        var engine = App.Services.Engine;
        var source = App.Services.Source;
        var id = version.Id;
        Directory.CreateDirectory(folder);

        var jsonPath = Path.Combine(folder, id + ".json");
        var json = await engine.DownloadAsync(
            [new DownloadItem(MirrorUrls.Resolve(version.JsonUrl.AbsoluteUri, source), jsonPath, Sha1: version.Sha1)],
            null,
            CancellationToken.None);
        if (!json.Succeeded)
            return false;

        var server = ReadDownload(jsonPath, "server", Path.Combine(folder, id + "-server.jar"), source);
        if (server is null)
        {
            File.Delete(jsonPath);
            if (!Directory.EnumerateFileSystemEntries(folder).Any())
                Directory.Delete(folder);
            throw new InvalidOperationException(Lang.Text("Minecraft.Download.Error.NoOfficialServerDownload", id));
        }

        var jar = await engine.DownloadAsync([server], null, CancellationToken.None);
        if (!jar.Succeeded)
            return false;

        File.WriteAllText(Path.Combine(folder, "Launch Server.bat"), BuildServerScript(id), GetBatchEncoding());
        File.Delete(jsonPath);
        return true;
    }

    // 从版本 JSON 的 downloads 中取出指定的文件，没有则返回 null
    private static DownloadItem? ReadDownload(string jsonPath, string key, string targetPath, DownloadSource source)
    {
        var node = JsonNode.Parse(File.ReadAllText(jsonPath))?["downloads"]?[key];
        var url = node?["url"]?.GetValue<string>();
        if (url is null)
            return null;

        var size = node?["size"]?.GetValue<long>();
        var sha1 = node?["sha1"]?.GetValue<string>();
        return new DownloadItem(MirrorUrls.Resolve(url, source), targetPath, size, sha1);
    }

    private static string BuildServerScript(string id)
    {
        var lines = new[]
        {
            "@echo off",
            "title " + Lang.Text("Minecraft.Download.ServerBatch.Title", id),
            "echo " + Lang.Text("Minecraft.Download.ServerBatch.InstructionJavaPath"),
            "echo " + Lang.Text("Minecraft.Download.ServerBatch.InstructionPclSettings"),
            "echo ------------------------------",
            "echo " + Lang.Text("Minecraft.Download.ServerBatch.InstructionEula"),
            "echo ------------------------------",
            "\"java\" -server -XX:+UseG1GC -Xmx4096M -Xms1024M -XX:+UseCompressedOops -jar " + id + "-server.jar nogui",
            "echo ----------------------",
            "echo " + Lang.Text("Minecraft.Download.ServerBatch.ServerStopped"),
            "pause"
        };
        return string.Join("\r\n", lines) + "\r\n";
    }

    // 启动脚本在中文 Windows 的命令行里以 GB18030 显示
    private static Encoding GetBatchEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding("GB18030");
    }

    // 同名任务正在进行时直接提示，否则运行任务并在结束后给出结果
    private static async Task RunAsync(string name, Func<Task<bool>> work)
    {
        lock (OngoingLock)
        {
            if (!Ongoing.Add(name))
            {
                Hint.Show(Lang.Text("Minecraft.Download.Error.InstanceDownloading"), HintType.Error);
                return;
            }
        }

        try
        {
            var succeeded = await work();
            if (succeeded)
                Hint.Show(name + Lang.Text("Common.Status.Success"), HintType.Success);
            else
                Hint.Show(name + Lang.Text("Common.Status.Failure"), HintType.Error);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "[Download] {Name} 失败：{Exception}", name, ex);
            Hint.Show(name + Lang.Text("Common.Status.Failure") + ex.Message, HintType.Error);
        }
        finally
        {
            lock (OngoingLock)
            {
                Ongoing.Remove(name);
            }
        }
    }
}
