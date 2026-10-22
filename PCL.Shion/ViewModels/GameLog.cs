using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PCL.Shion.ViewModels;

/// <summary>
/// 游戏日志。<see cref="Append"/> 可在任意线程调用，行会分批合并到 UI 线程上的 <see cref="Lines"/>，最多保留 <see cref="MaxLines"/> 行。
/// </summary>
public sealed partial class GameLog : ObservableObject
{
    public const int MaxLines = 5000;

    private const int BatchSize = 500;

    private readonly ConcurrentQueue<string> _pending = new();
    private int _flushScheduled;

    public ObservableCollection<string> Lines { get; } = [];

    [ObservableProperty]
    public partial bool HasLines { get; set; }

    public void Append(string line)
    {
        // 文本控件不会正确渲染制表符，Java 堆栈里全是它
        _pending.Enqueue(line.Replace("\t", "    "));
        ScheduleFlush();
    }

    /// <summary>清空日志，只能在 UI 线程调用。</summary>
    public void Clear()
    {
        while (_pending.TryDequeue(out _))
        {
        }

        Lines.Clear();
        HasLines = false;
    }

    private void ScheduleFlush()
    {
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 0)
            Dispatcher.UIThread.Post(Flush, DispatcherPriority.Background);
    }

    private void Flush()
    {
        for (var i = 0; i < BatchSize && _pending.TryDequeue(out var line); i++)
            Lines.Add(line);

        while (Lines.Count > MaxLines)
            Lines.RemoveAt(0);
        HasLines = Lines.Count > 0;

        // 先复位再检查队列，保证复位期间到达的行不会被漏掉
        Interlocked.Exchange(ref _flushScheduled, 0);
        if (!_pending.IsEmpty)
            ScheduleFlush();
    }
}
