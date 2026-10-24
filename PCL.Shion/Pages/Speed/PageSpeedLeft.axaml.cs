using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Homepage;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;
using Shapes = Avalonia.Controls.Shapes;

namespace PCL.Shion.Pages.Speed;

public partial class PageSpeedLeft : MyPageLeft
{
    private const int watcherInterval = 300;

    // 任务管理右栏，对应 WPF 的 ModMain.frmSpeedRight
    private static PageSpeedRight? frmSpeedRight;

    // 定时器任务
    private readonly Dictionary<string, TaskCardView> rightCards = new();

    // 初始化
    private bool isLoad;

    public PageSpeedLeft()
    {
        InitializeComponent();
        Loaded += (_, _) => Page_Loaded();
    }

    public override void OnNavigated(PageSubType sub)
    {
        frmSpeedRight ??= new PageSpeedRight();
        Navigator.ShowRight(frmSpeedRight);
    }

    private void Page_Loaded()
    {
        // 进入时就刷新一次显示
        Watcher();

        // 如果在页面切换动画的 “上一页消失” 部分已经完成了下载，就直接尝试返回
        TryReturnToHome();

        if (isLoad)
            return;
        isLoad = true;

        // 监控定时器
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(watcherInterval) };
        timer.Tick += (_, _) => Watcher();
        timer.Start();

        // 非调试模式隐藏连接数
        if (!HomepageSupport.ModeDebug)
        {
            RowDefinitions[12].Height = new GridLength(0d);
            RowDefinitions[13].Height = new GridLength(0d);
            RowDefinitions[14].Height = new GridLength(0d);
            RowDefinitions[15].Height = new GridLength(0d);
        }
    }

    private void Watcher()
    {
        if (Navigator.Current != PageType.TaskManager)
            return;
        try
        {
            var loaders = SpeedTaskbar.Loaders;
            if (loaders.Count == 0)
            {
                // 无任务
                LabProgress.Text = Lang.Number(1d, "P0");
                LabSpeed.Text = ByteFormatter.FormatSpeed(0d);
                LabFile.Text = Lang.Number(0, "N0");
                LabThread.Text = Lang.Number(0, "N0") + " / " + Lang.Number(SpeedTaskbar.ConnectionLimit, "N0");
            }
            else
            {
                // 有任务，输出基本信息
                var tasks = loaders.Where(l => l.show).ToList(); // 筛选掉启动 MC 的任务（#6270）
                var rawPercent = tasks.Count > 0
                    ? Math.Clamp(tasks.Average(l => l.Progress), 0d, 1d)
                    : 1d;
                var predictText = Lang.Number(rawPercent, "P2");
                LabProgress.Text = rawPercent > 0.999999d ? Lang.Number(1d, "P0") : predictText;
                LabSpeed.Text = ByteFormatter.FormatSpeed(SpeedTaskbar.Speed);
                LabFile.Text = Lang.Number(SpeedTaskbar.FileRemain, "N0");
                LabThread.Text = Lang.Number(SpeedTaskbar.ConnectionCount, "N0") + " / " +
                                 Lang.Number(SpeedTaskbar.ConnectionLimit, "N0");
            }
        }
        catch (Exception ex)
        {
            ReportError(ex, "任务管理左栏监视出错");
        }

        if (frmSpeedRight is null)
            return;
        try
        {
            foreach (var loader in SpeedTaskbar.Loaders)
                TaskRefresh(loader);
        }
        catch (Exception ex)
        {
            ReportError(ex, "任务管理右栏监视出错");
        }
    }

    public void TaskRefresh(ModLoader.LoaderBase loader)
    {
        if (loader is null || !loader.show)
            return;
        try
        {
            // 获取实际加载器列表
            var loaderList = loader is ModLoader.LoaderCombo combo
                ? combo.GetLoaderList()
                : new List<ModLoader.LoaderBase> { loader };
            if (rightCards.TryGetValue(loader.name, out var view))
            {
                // 已有此卡片
                var newValue = loader.Progress + (double)loader.State;
                if (view.Tag == newValue)
                    return;
                view.Tag = newValue;
                var body = view.Body;
                try
                {
                    switch (loader.State)
                    {
                        case LoadState.Failed:
                        {
                            // 失败，更新卡片
                            body.RowDefinitions.Clear();
                            body.Children.Clear();
                            body.Children.Add(CreateFailedIcon(0));
                            body.Children.Add(CreateErrorText(loader));
                            break;
                        }
                        case LoadState.Finished:
                        {
                            // 完成，销毁卡片并返回
                            ModAnimation.AniDispose(view.Card, true, _ => TryReturnToHome());
                            break;
                        }
                        case LoadState.Loading:
                        case LoadState.Waiting:
                        {
                            // 进度不同，更新卡片
                            do
                            {
                                try
                                {
                                    if (body.Children.Count < loaderList.Count * 2)
                                    {
                                        Info($"[Watcher] 刷新任务管理卡片 {loader.name} 失败：卡片中仅有 {body.Children.Count} 个子项，要求至少有 {loaderList.Count * 2} 个子项");
                                        break;
                                    }

                                    var row = 0;
                                    foreach (var subTask in loaderList)
                                    {
                                        switch (subTask.State)
                                        {
                                            case LoadState.Waiting:
                                            {
                                                if ((body.Children[row * 2].Tag as string) != "Waiting")
                                                {
                                                    body.Children.RemoveAt(row * 2);
                                                    body.Children.Insert(row * 2, CreateWaitingIcon(row));
                                                }

                                                break;
                                            }
                                            case LoadState.Loading:
                                            {
                                                if ((body.Children[row * 2].Tag as string) != "Loading")
                                                {
                                                    body.Children.RemoveAt(row * 2);
                                                    body.Children.Insert(row * 2, CreateLoadingText(subTask.Progress, row));
                                                }
                                                else
                                                {
                                                    ((TextBlock)body.Children[row * 2]).Text =
                                                        Lang.Number(subTask.Progress, "P0");
                                                }

                                                break;
                                            }
                                            case LoadState.Finished:
                                            {
                                                if ((body.Children[row * 2].Tag as string) != "Finished")
                                                {
                                                    body.Children.RemoveAt(row * 2);
                                                    body.Children.Insert(row * 2, CreateFinishedIcon(row));
                                                }

                                                break;
                                            }
                                        }

                                        row += 1;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ReportError(ex, $"刷新任务管理卡片 {loader.name} 失败");
                                }
                            } while (false);

                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ReportError(ex, $"更新任务管理显示失败（{loader.State}）");
                }
            }
            else if (loader.State is not (LoadState.Aborted or LoadState.Finished))
            {
                try
                {
                    // 没有卡片且未中断或完成，添加新的卡片
                    var body = new Grid
                    {
                        Margin = new Thickness(14d, 40d, 15d, 10d),
                        ColumnDefinitions = new ColumnDefinitions("50,*")
                    };
                    for (var i = 0; i < loaderList.Count; i++)
                        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26d) });
                    var row = 0;
                    foreach (var subTask in loaderList)
                    {
                        switch (subTask.State)
                        {
                            case LoadState.Waiting:
                                body.Children.Add(CreateWaitingIcon(row));
                                break;
                            case LoadState.Loading:
                                body.Children.Add(CreateLoadingText(subTask.Progress, row));
                                break;
                            case LoadState.Finished:
                                body.Children.Add(CreateFinishedIcon(row));
                                break;
                            default:
                                body.Children.Add(CreateFailedIcon(row));
                                break;
                        }

                        body.Children.Add(CreateNameText(subTask.name, row));
                        row += 1;
                    }

                    var card = new MyCard
                    {
                        Title = loader.name,
                        Margin = new Thickness(0d, 0d, 0d, 15d)
                    };
                    card.Children.Add(body);
                    // 实例化控件
                    view = new TaskCardView(card, body) { Tag = loader.Progress + (double)loader.State };
                    CardPanel.Children.Insert(0, card);
                    rightCards.Add(loader.name, view);
                    Info($"[Watcher] 新建任务管理卡片：{loader.name}");
                    // 添加取消按钮
                    var cancel = new MyIconButton
                    {
                        Name = "BtnCancel",
                        SvgIcon = "lucide/x",
                        Height = 25d,
                        Width = 25d,
                        Margin = new Thickness(0d, 10d, 10d, 0d),
                        LogoScale = 1.1d,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    card.Children.Add(cancel);
                    cancel.Click += (sender, args) =>
                    {
                        ModAnimation.AniDispose((MyIconButton)sender!, false);
                        ModAnimation.AniDispose(card, true, _ =>
                        {
                            if (CardPanel.Children.Count == 0 && Navigator.Current == PageType.TaskManager)
                                Navigator.Back();
                        });
                        rightCards.Remove(loader.name);
                        SpeedTaskbar.Remove(loader);
                        Info($"[Taskbar] 关闭任务管理卡片：{loader.name}，且移出任务列表");
                        Task.Run(() => loader.Abort());
                    };
                    // 如果已经失败，再刷新一次，修改成失败的控件
                    if (loader.State == LoadState.Failed)
                    {
                        view.Tag = double.NaN; // 避免重复导致刷新无效
                        TaskRefresh(loader);
                    }
                }
                catch (Exception ex)
                {
                    ReportError(ex, "添加任务管理卡片失败");
                }
            }
        }
        catch (Exception ex)
        {
            ReportError(ex, "刷新任务管理显示失败");
        }
    }

    public void TaskRemove(ModLoader.LoaderBase loader)
    {
        if (rightCards.ContainsKey(loader.name))
            RunInUiWait(() =>
            {
                // 移除已有的卡片
                var view = rightCards[loader.name];
                CardPanel.Children.Remove(view.Card);
                rightCards.Remove(loader.name);
                Info($"[Watcher] 移除任务管理卡片：{loader.name}");
            });
    }

    /// <summary>
    ///     若没有任务，尝试返回主页。
    /// </summary>
    private void TryReturnToHome()
    {
        if (frmSpeedRight is not null && frmSpeedRight.CardPanel.Children.Count == 0 &&
            Navigator.Current == PageType.TaskManager)
            Navigator.Back();
    }

    private static Panel CardPanel => frmSpeedRight!.CardPanel;

    private static Shapes.Path CreateWaitingIcon(int row) => CreateIcon(
        "Waiting", "F1 M5,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 Z",
        row, 18d, 6d, new Thickness(0d, 7d, 0d, 0d));

    private static Shapes.Path CreateFinishedIcon(int row) => CreateIcon(
        "Finished",
        "F1 M 23.7501,33.25L 34.8334,44.3333L 52.2499,22.1668L 56.9999,26.9168L 34.8334,53.8333L 19.0001,38L 23.7501,33.25 Z",
        row, 15d, 16d, new Thickness(0d, 3d, 0d, 0d));

    private static Shapes.Path CreateFailedIcon(int row) => CreateIcon(
        "Failed",
        "F1 M2.5,0 L0,2.5 7.5,10 0,17.5 2.5,20 10,12.5 17.5,20 20,17.5 12.5,10 20,2.5 17.5,0 10,7.5 2.5,0Z",
        row, 15d, 15d, new Thickness(0d, 1d, 0d, 0d));

    private static Shapes.Path CreateIcon(string tag, string data, int row, double width, double height,
        Thickness margin)
    {
        var icon = new Shapes.Path
        {
            Tag = tag,
            Data = Geometry.Parse(data),
            Stretch = Stretch.Uniform,
            Width = width,
            Height = height,
            Margin = margin,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(icon, 0);
        Grid.SetRow(icon, row);
        icon.SetResourceReference(Shapes.Shape.FillProperty, "ColorBrush3");
        return icon;
    }

    private static TextBlock CreateLoadingText(double progress, int row)
    {
        var text = new TextBlock
        {
            Text = Lang.Number(progress, "P0"),
            Tag = "Loading",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(text, 0);
        Grid.SetRow(text, row);
        text.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3");
        return text;
    }

    private static TextBlock CreateNameText(string name, int row)
    {
        var text = new TextBlock { Text = name, HorizontalAlignment = HorizontalAlignment.Left };
        Grid.SetColumn(text, 1);
        Grid.SetRow(text, row);
        return text;
    }

    private static TextBlock CreateErrorText(ModLoader.LoaderBase loader)
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0d, 0d, 0d, 5d),
            Text = loader.Error?.ToString() ?? ""
        };
        Grid.SetColumn(text, 1);
        Grid.SetRow(text, 0);
        ToolTip.SetTip(text, Lang.Text("Speed.Error.ClickToCopy"));
        text.PointerPressed += (sender, e) =>
        {
            if (!e.GetCurrentPoint(text).Properties.IsLeftButtonPressed)
                return;
            _ = CopyErrorAsync(text, text.Text ?? "");
        };
        return text;
    }

    private static async Task CopyErrorAsync(Control source, string text)
    {
        var clipboard = TopLevel.GetTopLevel(source)?.Clipboard;
        if (clipboard is null)
            return;
        await clipboard.SetTextAsync(text);
        Hint.Show(Lang.Text("Speed.Error.Copied"), HintType.Success);
    }

    private static void Info(string message) =>
        Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(null, "{Message}", message);

    private static void ReportError(Exception ex, string message)
    {
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, "{Message}：{Error}", message, ex);
        Hint.Show(Lang.Text("Speed.Error.OperationFailed"), HintType.Error);
    }

    private static void RunInUiWait(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Invoke(action);
    }

    /// <summary>右栏中一个任务的卡片及其状态。</summary>
    private sealed class TaskCardView(MyCard card, Grid body)
    {
        public MyCard Card { get; } = card;

        public Grid Body { get; } = body;

        public double Tag { get; set; }
    }
}
