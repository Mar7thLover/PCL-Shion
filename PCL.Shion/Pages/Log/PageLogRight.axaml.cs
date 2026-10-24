using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Log;

public partial class PageLogRight : MyPageRight
{
    private readonly Run labDebug;
    private readonly Run labError;
    private readonly Run labFatal;
    private readonly Run labInfo;
    private readonly Run labWarn;

    private GameLogEntry? shownEntry;
    private bool sliderSyncing;

    public PageLogRight()
    {
        InitializeComponent();
        Loaded += (_, _) => OnLoaded();

        CheckAutoScroll.Checked = true;
        SliderMaxLog.getHintText = new Func<object, object>(v => MaxLinesHint((int)v));
        SliderMaxLog.Change += OnSliderChanged;
        BtnOperationExport.Click += (_, _) => RunAsync(ExportLogAsync);
        BtnOperationClear.Click += (_, _) => ClearLog();
        BtnOperationKill.Click += (_, _) => KillGame();
        BtnOperationExportStackDump.Click += (_, _) => RunAsync(ExportStackDumpAsync);

        PanLogCard.Inlines.Clear();
        PanLogCard.Inlines.Add(new Run(Lang.Text("LogPage.Title")));
        PanLogCard.Inlines.Add(new Run(" | "));
        labDebug = new Run($"0 {Lang.Text("LogPage.Level.Debug")}") { Foreground = FindBrush("ColorBrushDebug") };
        PanLogCard.Inlines.Add(labDebug);
        PanLogCard.Inlines.Add(new Run(" | "));
        labInfo = new Run($"0 {Lang.Text("LogPage.Level.Info")}")
        {
            Foreground = FindBrush(ActualThemeVariant == ThemeVariant.Dark ? "ColorBrushInfoDark" : "ColorBrushInfo")
        };
        PanLogCard.Inlines.Add(labInfo);
        PanLogCard.Inlines.Add(new Run(" | "));
        labWarn = new Run($"0 {Lang.Text("LogPage.Level.Warn")}") { Foreground = FindBrush("ColorBrushWarn") };
        PanLogCard.Inlines.Add(labWarn);
        PanLogCard.Inlines.Add(new Run(" | "));
        labError = new Run($"0 {Lang.Text("LogPage.Level.Error")}") { Foreground = FindBrush("ColorBrushError") };
        PanLogCard.Inlines.Add(labError);
        PanLogCard.Inlines.Add(new Run(" | "));
        labFatal = new Run($"0 {Lang.Text("LogPage.Level.Fatal")}") { Foreground = FindBrush("ColorBrushFatal") };
        PanLogCard.Inlines.Add(labFatal);
    }

    public void Reload()
    {
        var entry = PageLogLeft.CurrentEntry;
        if (entry is null)
        {
            PanAllBack.IsVisible = false;
            Attach(null);
            return;
        }

        PanAllBack.IsVisible = true;
        CardOperation.IsVisible = true;
        BtnOperationKill.IsEnabled = !entry.Exited;
        BtnOperationExportStackDump.IsEnabled = !entry.Exited && entry.Game is not null;
        SetSliderSilently(App.Services.Settings.SystemMaxLog);
        Attach(entry);
        RefreshLabText(entry);
    }

    private void OnLoaded()
    {
        ModAnimation.AniControlEnabled += 1;
        Reload();
        ModAnimation.AniControlEnabled -= 1;
    }

    /// <summary>切换到另一份日志：界面改为显示它的内容，并在它输出新行时更新计数与滚动。</summary>
    private void Attach(GameLogEntry? entry)
    {
        if (ReferenceEquals(shownEntry, entry))
            return;
        if (shownEntry is not null)
            shownEntry.View.CollectionChanged -= OnViewChanged;
        shownEntry = entry;
        PanLog.ItemsSource = entry?.View;
        if (entry is not null)
            entry.View.CollectionChanged += OnViewChanged;
    }

    private void OnViewChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (shownEntry is null)
            return;
        RefreshLabText(shownEntry);
        if (e.Action == NotifyCollectionChangedAction.Add && CheckAutoScroll.Checked == true)
            Dispatcher.UIThread.Post(() => PanBack.ScrollToVerticalOffset(PanBack.ScrollableHeight),
                DispatcherPriority.Background);
    }

    private void RefreshLabText(GameLogEntry entry)
    {
        labFatal.Text = $"{entry.CountOf(GameLogLevel.Fatal)} {Lang.Text("LogPage.Level.Fatal")}";
        labError.Text = $"{entry.CountOf(GameLogLevel.Error)} {Lang.Text("LogPage.Level.Error")}";
        labWarn.Text = $"{entry.CountOf(GameLogLevel.Warn)} {Lang.Text("LogPage.Level.Warn")}";
        labInfo.Text = $"{entry.CountOf(GameLogLevel.Info)} {Lang.Text("LogPage.Level.Info")}";
        labDebug.Text = $"{entry.CountOf(GameLogLevel.Debug)} {Lang.Text("LogPage.Level.Debug")}";
    }

    #region 滑动条

    private static string MaxLinesHint(int value)
    {
        var lines = GameLogEntry.MaxLinesOf(value);
        return lines == int.MaxValue ? Lang.Text("LogPage.MaxLines.Unlimited") : lines.ToString(CultureInfo.InvariantCulture);
    }

    private void SetSliderSilently(int value)
    {
        sliderSyncing = true;
        try
        {
            SliderMaxLog.Value = value;
        }
        finally
        {
            sliderSyncing = false;
        }
    }

    private void OnSliderChanged(object sender, bool user)
    {
        if (sliderSyncing)
            return;
        var settings = App.Services.Settings;
        settings.SystemMaxLog = SliderMaxLog.Value;
        settings.Save();
    }

    #endregion

    #region 卡片按钮

    private void ClearLog()
    {
        if (PageLogLeft.CurrentEntry is not { } entry)
            return;
        entry.Log.Lines.Clear();
        entry.Log.HasLines = false;
    }

    private async Task ExportLogAsync()
    {
        if (PageLogLeft.CurrentEntry is not { } entry)
            return;
        var savePath = await SelectSaveFileAsync(
            Lang.Text("LogPage.Export.SelectLocation"),
            Lang.Text("LogPage.Export.GameLog.FileName", entry.Name),
            Lang.Text("LogPage.Export.GameLog.Filter"));
        if (string.IsNullOrEmpty(savePath))
            return;
        await File.WriteAllLinesAsync(savePath, entry.Log.Lines.ToList());
        Hint.Show(Lang.Text("LogPage.Export.Success"), HintType.Success);
        SystemShell.OpenFolder(Path.GetDirectoryName(savePath)!);
    }

    private void KillGame()
    {
        if (PageLogLeft.CurrentEntry is not { Exited: false } entry)
            return;
        entry.Kill();
        Hint.Show(Lang.Text("LogPage.Action.GameClosed", entry.Name), HintType.Success);
    }

    private async Task ExportStackDumpAsync()
    {
        if (PageLogLeft.CurrentEntry is not { Exited: false, Game: not null } entry)
            return;
        var formattedDate = DateTime.Now.ToString("G", CultureInfo.InvariantCulture)
            .Replace("/", "-")
            .Replace(":", ".")
            .Replace(" ", "_");
        var savePath = await SelectSaveFileAsync(
            Lang.Text("LogPage.Export.SelectLocation"),
            Lang.Text("LogPage.ExportStack.FileName", formattedDate),
            Lang.Text("LogPage.ExportStack.Filter"));
        if (string.IsNullOrEmpty(savePath))
            return;
        Hint.Show(Lang.Text("LogPage.ExportStack.Progress"));
        BtnOperationExportStackDump.IsEnabled = false;
        try
        {
            var dump = await Task.Run(() => entry.ExportStackDump());
            await File.WriteAllLinesAsync(savePath, dump);
            Hint.Show(Lang.Text("LogPage.ExportStack.Success"), HintType.Success);
            SystemShell.OpenFolder(Path.GetDirectoryName(savePath)!);
        }
        finally
        {
            BtnOperationExportStackDump.IsEnabled = !entry.Exited && entry.Game is not null;
        }
    }

    private async Task<string?> SelectSaveFileAsync(string title, string suggestedName, string filter)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return null;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType(filter.Split('|')[0]) { Patterns = ["*.log"] }]
        });
        return file?.TryGetLocalPath();
    }

    private static void RunAsync(Func<Task> action) => _ = RunSafelyAsync(action);

    private static async Task RunSafelyAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "{Message}：{Error}", "日志页面操作失败", ex);
            Hint.Show(Lang.Text("LogPage.Error.OperationFailed"), HintType.Error);
        }
    }

    #endregion

    private IBrush? FindBrush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var resource) ? resource as IBrush : null;
}
