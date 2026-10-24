using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Shion.ViewModels;

namespace PCL.Shion.Views;

public partial class HomeView : UserControl
{
    /// <summary>距离底部多少像素以内仍视为“在底部”。</summary>
    private const double BottomTolerance = 16;

    private ScrollViewer? _logScroll;
    private HomeViewModel? _viewModel;
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // Avalonia 中没有 IsEffectivelyVisibleProperty，这里以自身 IsVisible 的变化作为显示时刷新的时机
        if (change.Property == IsVisibleProperty && IsEffectivelyVisible)
            _viewModel?.RefreshJava();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = DataContext as HomeViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        AttachLog(_viewModel?.Log.Lines);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.Log))
            AttachLog(_viewModel!.Log.Lines);
    }

    /// <summary>切换到另一份日志：从它的末尾开始显示，并重新自动跟随。</summary>
    private void AttachLog(ObservableCollection<string>? lines)
    {
        if (_logLines is not null)
            _logLines.CollectionChanged -= OnLogLinesChanged;
        _logLines = lines;
        if (_logLines is not null)
            _logLines.CollectionChanged += OnLogLinesChanged;

        _followLog = true;
        QueueScrollToEnd();
    }

    /// <summary>用户向上滚动后暂停自动跟随，滚回底部后恢复。</summary>
    private void OnLogScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_logScroll is null || e.OffsetDelta.Y == 0)
            return;
        _followLog = _logScroll.Offset.Y >= _logScroll.Extent.Height - _logScroll.Viewport.Height - BottomTolerance;
    }

    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueScrollToEnd();

    private void QueueScrollToEnd()
    {
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
