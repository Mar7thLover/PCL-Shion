using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Shion.ViewModels;

namespace PCL.Shion.Views;

public partial class HomeView : UserControl
{
    /// <summary>距离底部多少像素以内仍视为“在底部”。</summary>
    private const double BottomTolerance = 16;

    private ScrollViewer? _logScroll;
    private ObservableCollection<string>? _logLines;
    private bool _followLog = true;
    private bool _scrollQueued;

    public HomeView()
    {
        InitializeComponent();
        LogList.TemplateApplied += (_, e) =>
        {
            _logScroll = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
            if (_logScroll is not null)
                _logScroll.ScrollChanged += OnLogScrollChanged;
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_logLines is not null)
            _logLines.CollectionChanged -= OnLogLinesChanged;
        _logLines = (DataContext as HomeViewModel)?.Log.Lines;
        if (_logLines is not null)
            _logLines.CollectionChanged += OnLogLinesChanged;
    }

    /// <summary>用户向上滚动后暂停自动跟随，滚回底部后恢复。</summary>
    private void OnLogScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_logScroll is null || e.OffsetDelta.Y == 0)
            return;
        _followLog = _logScroll.Offset.Y >= _logScroll.Extent.Height - _logScroll.Viewport.Height - BottomTolerance;
    }

    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
            _followLog = true;
        if (!_followLog || _scrollQueued)
            return;

        _scrollQueued = true;
        Dispatcher.UIThread.Post(ScrollLogToEnd, DispatcherPriority.Background);
    }

    private void ScrollLogToEnd()
    {
        _scrollQueued = false;
        if (_logLines is { Count: > 0 })
            LogList.ScrollIntoView(_logLines.Count - 1);
    }
}
