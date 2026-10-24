using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.Launch;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.ViewModels;

namespace PCL.Shion.Pages.Log;

public partial class PageLogLeft : MyPageLeft
{
    // 所有已显示的日志，对应 WPF 的 shownLogs
    private static readonly List<GameLogEntry> entries = [];

    private static GameLogEntry? currentEntry;

    private static int uuidSeed;

    private static bool subscribed;

    // 日志右栏，对应 WPF 的 ModMain.frmLogRight
    private static PageLogRight? frmLogRight;

    private int isLoading;

    public PageLogLeft()
    {
        InitializeComponent();
        AnimatedControl = PanList;
        Loaded += (_, _) => Reload();
    }

    /// <summary>当前显示的日志，供右栏使用。</summary>
    internal static GameLogEntry? CurrentEntry => currentEntry;

    public override void OnNavigated(PageSubType sub)
    {
        if (Home() is { } home)
        {
            Subscribe(home);
            SyncEntries(home);
        }

        frmLogRight ??= new PageLogRight();
        Navigator.ShowRight(frmLogRight);
        RefreshAll();
    }

    /// <summary>点击选项，对应 WPF 的 SelectionChange。</summary>
    public void SelectionChange(int uuid)
    {
        if (isLoading > 0)
            return;
        currentEntry = uuid <= 0
            ? null
            : entries.FirstOrDefault(e => e.Uuid == uuid) ?? currentEntry;
        RunInUi(RefreshAll);
    }

    public void RemoveItem(int uuid)
    {
        var index = entries.FindIndex(e => e.Uuid == uuid);
        if (index < 0)
            return;
        var entry = entries[index];
        entries.RemoveAt(index);
        entry.Dispose();
        if (ReferenceEquals(currentEntry, entry))
            currentEntry = entries.Count == 0 ? null : entries[Math.Clamp(index, 0, entries.Count - 1)];
        RunInUi(RefreshAll);
    }

    /// <summary>构建左栏的日志列表，对应 WPF 的 Reload。</summary>
    public void Reload()
    {
        if (entries.Count == 0)
        {
            PanList.Children.Clear();
            if (Navigator.Current == PageType.GameLog)
                Dispatcher.UIThread.Post(Navigator.Back);
            return;
        }

        isLoading += 1;
        try
        {
            PanList.Children.Clear();

            // 测试实例列表
            PanList.Children.Add(new TextBlock
            {
                Text = Lang.Text("LogPage.Left.InstancesTitle"),
                Margin = new Thickness(13d, 18d, 5d, 4d),
                Opacity = 0.6d,
                FontSize = 12d
            });
            foreach (var entry in entries)
                PanList.Children.Add(CreateItem(entry));

            // 通知日志保留设置
            var settings = App.Services.Settings;
            if (!settings.HintMaxLog)
            {
                settings.HintMaxLog = true;
                settings.Save();
                Hint.Show(Lang.Text("LogPage.MaxLines.Hint", 500));
            }
        }
        catch (Exception ex)
        {
            ReportError(ex, "构建游戏实时日志 UI 出错");
        }
        finally
        {
            isLoading -= 1;
        }
    }

    private MyListItem CreateItem(GameLogEntry entry)
    {
        var uuid = entry.Uuid;
        var newItem = new MyListItem
        {
            IsScaleAnimationEnabled = false,
            Type = MyListItem.CheckType.RadioBox,
            MinPaddingRight = 30,
            Title = entry.Name,
            Info = entry.Info,
            Height = 40d,
            Tag = uuid
        };
        newItem.Changed += (_, _) => SelectionChange(uuid);
        var removeButton = new MyIconButton { SvgIcon = "lucide/trash-2", LogoScale = 1.1d };
        removeButton.Click += (_, _) => RemoveItem(uuid);
        newItem.Buttons = [removeButton];
        if (ReferenceEquals(entry, currentEntry))
            newItem.SetChecked(true, false, false);
        return newItem;
    }

    private void RefreshAll()
    {
        frmLogRight?.Reload();
        Reload();
    }

    private static HomeViewModel? Home() => MainWindow.Current?.Home;

    private static void Subscribe(HomeViewModel home)
    {
        if (subscribed)
            return;
        subscribed = true;
        home.RunningGames.CollectionChanged += (_, _) => Dispatcher.UIThread.Post(OnRunningGamesChanged);
    }

    private static void OnRunningGamesChanged()
    {
        if (Home() is not { } home)
            return;
        SyncEntries(home);
        if (Navigator.Current == PageType.GameLog && Navigator.PageLeft is PageLogLeft left)
            left.RefreshAll();
    }

    /// <summary>
    ///     把正在运行的游戏加入日志列表，并标记已经退出的游戏。新启动的游戏会被选中，与 WPF 的 Add 相同。
    /// </summary>
    private static void SyncEntries(HomeViewModel home)
    {
        var running = home.RunningGames.ToList();
        GameLogEntry? created = null;
        foreach (var item in running)
        {
            if (entries.Any(e => ReferenceEquals(e.Item, item)))
                continue;
            created = new GameLogEntry(++uuidSeed, item, FindGame(item.Name));
            entries.Add(created);
        }

        foreach (var entry in entries)
            if (!running.Any(item => ReferenceEquals(item, entry.Item)))
                entry.MarkExited();

        if (created is not null)
            currentEntry = created;
        else if (currentEntry is null || !entries.Contains(currentEntry))
            currentEntry = entries.LastOrDefault();
    }

    /// <summary>找出与日志对应的游戏进程，用于结束进程与导出运行栈。</summary>
    private static RunningGame? FindGame(string instanceName) =>
        App.Services.Runner.Running.FirstOrDefault(game =>
            game.InstanceName == instanceName && !entries.Any(e => ReferenceEquals(e.Game, game)));

    private static void RunInUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    private static void ReportError(Exception ex, string message)
    {
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);
        Hint.Show(Lang.Text("LogPage.Error.OperationFailed"), HintType.Error);
    }
}
