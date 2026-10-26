using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.UI;
using PCL.Shion.Services;

namespace PCL.Shion.Pages.Setup;

public partial class PageSetupLog : MyPageRight
{
    public PageSetupLog()
    {
        InitializeComponent();
        PanScroll = PanBack; // 不知道为啥不能在 XAML 设置
        Loaded += PageOtherLog_Loaded;
    }

    private static string LogDirectory
    {
        get
        {
            Directory.CreateDirectory(SetupPaths.Logs);
            return SetupPaths.Logs;
        }
    }

    /// <summary>
    ///     本次运行正在写入的日志文件。
    /// </summary>
    private static List<string> CurrentLogs => DiagnosticLog.CurrentPath is { } path && File.Exists(path) ? [path] : [];

    private void PageOtherLog_Loaded(object? sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        LoadList();
    }

    public void LoadList()
    {
        PanList.Children.Clear();
        var current = CurrentLogs;
        var logFiles = Directory.GetFiles(LogDirectory).OrderByDescending(f => File.GetLastWriteTime(f)).ToArray();
        foreach (var item in logFiles)
        {
            var fullPath = Path.GetFullPath(item);
            var title = Path.GetFileName(item);
            if (title.StartsWith("Launch-") && title.EndsWith(".log") && title.Length > 11)
            {
                title = title.Substring(7, title.Length - 11);
                var r = DateTime.TryParseExact(title, "yyyy-M-d-HHmmssfff", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dt);
                if (r)
                    title = Lang.Date(dt, "G");
                if (current.Any(log => log.Equals(fullPath)))
                    title = Lang.Text("Setup.Log.CurrentSuffix", title);
            }
            else if (title.StartsWith("LastPending") && title.Length >= 15)
            {
                title = title.Substring(11, title.Length - 15);
                if (title.Length > 1)
                    title = Lang.Text("Setup.Log.TempStored", title.Substring(1));
                else
                    title = Lang.Text("Setup.Log.TempUnoutput");
            }

            var ele = new MyListItem
            {
                Type = MyListItem.CheckType.Clickable,
                Title = title,
                Info = fullPath,
                Tag = fullPath
            };
            ele.Click += (sender, e) =>
            {
                var s = (MyListItem)sender!;
                var file = (string)s.Tag!;
                SystemShell.OpenFolder(file);
            };
            PanList.Children.Add(ele);
        }
    }

    private static async Task ExportLog(Visual anchor, IEnumerable<string> sourceFiles)
    {
        var filter = Lang.Text("Setup.Log.ExportFilter");
        var baseName = "PCL_CE_Logs_" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var tempDirName = Path.Combine(Path.GetTempPath(), baseName + ".tmp");
        var fileName = baseName + ".zip";
        var selectedPath = await SetupDialogs.SelectSaveFileAsync(anchor, Lang.Text("Setup.Log.ExportSaveTitle"), fileName,
            filter, ["*.zip"]);
        if (string.IsNullOrEmpty(selectedPath))
            return;
        try
        {
            Directory.CreateDirectory(tempDirName);
            if (File.Exists(selectedPath))
                File.Delete(selectedPath);
            using (var zip = ZipFile.Open(selectedPath, ZipArchiveMode.Create))
            {
                foreach (var item in sourceFiles)
                {
                    var itemFileName = Path.GetFileName(item);
                    var tempPath = Path.Combine(tempDirName, itemFileName);
                    File.Copy(item, tempPath, true);
                    zip.CreateEntryFromFile(tempPath, itemFileName, CompressionLevel.Fastest);
                    File.Delete(tempPath);
                }
            }

            HintService.Hint(Lang.Text("Setup.Log.ExportSuccess"), HintType.Success);
        }
        catch (Exception ex)
        {
            SetupConfig.Log(
                ex,
                Lang.Text("Setup.Log.ExportFailed"),
                SetupLogLevel.Hint,
                Lang.Text("Setup.Log.ExportFailed"));
        }
        finally
        {
            if (Directory.Exists(tempDirName))
                Directory.Delete(tempDirName, true);
        }
    }

    private void ButtonOpenDir_OnClick(object? sender, PointerReleasedEventArgs e)
    {
        SystemShell.OpenFolder(LogDirectory);
    }

    private async void ButtonClean_OnClick(object? sender, PointerReleasedEventArgs e)
    {
        var r = await MsgBox.ShowAsync(Lang.Text("Setup.Log.Clear.Confirm.Message"), Lang.Text("Setup.Log.Clear.Confirm.Title"), Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true);
        if (r != 1)
            return;
        var currentSet = new HashSet<string>(CurrentLogs);
        foreach (var item in Directory.GetFiles(LogDirectory))
            if (!currentSet.Contains(item))
                File.Delete(item);
        HintService.Hint(Lang.Text("Setup.Log.Clear.Success"), HintType.Success);
        LoadList();
    }

    private async void ButtonExportAll_OnClick(object? sender, PointerReleasedEventArgs e)
    {
        await ExportLog(this, Directory.GetFiles(LogDirectory));
    }

    private async void ButtonExport_OnClick(object? sender, PointerReleasedEventArgs e)
    {
        var pendingLogs = Array.FindAll(Directory.GetFiles(LogDirectory),
            s => Path.GetFileName(s).StartsWith("LastPending", StringComparison.Ordinal));
        await ExportLog(this, CurrentLogs.Concat(pendingLogs));
    }
}
