using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using PCL.Shion.Core.Launch;
using PCL.Shion.Core.Platform;
using PCL.Shion.Services;
using PCL.Shion.ViewModels;

namespace PCL.Shion.Pages.Log;

/// <summary>日志行的级别，由 Minecraft 日志行中的 "/INFO]" 这类标记决定。</summary>
internal enum GameLogLevel
{
    None,
    Debug,
    Info,
    Warn,
    Error,
    Fatal
}

/// <summary>实时日志界面中的一行，颜色由级别决定。</summary>
internal sealed class GameLogLine
{
    public GameLogLine(string text, GameLogLevel level)
    {
        Text = text;
        Brush = GameLogEntry.BrushOf(level);
    }

    public string Text { get; }

    public IBrush? Brush { get; }
}

/// <summary>
///     一局游戏的日志。<see cref="View" /> 与 <see cref="GameLog.Lines" /> 保持一致，
///     并累计各级别的行数，对应 WPF 中 ModWatcher 的计数。
/// </summary>
internal sealed class GameLogEntry : IDisposable
{
    private static readonly Regex LevelPattern = new(@"/(FATAL|ERROR|WARN|INFO|DEBUG)\]", RegexOptions.Compiled);

    private readonly int[] counts = new int[6];
    private bool trimScheduled;

    public GameLogEntry(int uuid, RunningGameItem item, RunningGame? game)
    {
        Uuid = uuid;
        Item = item;
        Game = game;
        View = new ObservableCollection<GameLogLine>();
        foreach (var line in Log.Lines)
            AddLine(line);
        Log.Lines.CollectionChanged += OnLinesChanged;
    }

    public int Uuid { get; }

    public RunningGameItem Item { get; }

    public RunningGame? Game { get; }

    public GameLog Log => Item.Log;

    public string Name => Item.Name;

    public string Info => Item.Description;

    public bool Exited { get; private set; }

    public ObservableCollection<GameLogLine> View { get; }

    public int CountOf(GameLogLevel level) => counts[(int)level];

    public void MarkExited() => Exited = true;

    public void Kill()
    {
        if (!Exited)
            Item.EndGameCommand.Execute(null);
    }

    /// <summary>
    ///     导出运行栈：对游戏进程连续执行三次 jstack，每次之间间隔 3 秒，与 WPF 的 ExportStackDump 相同。
    /// </summary>
    public List<string> ExportStackDump()
    {
        if (Game is not { } game)
            throw new InvalidOperationException("游戏进程已经结束。");
        var jstack = FindJStack(game.ProcessId) ?? throw new InvalidOperationException("未找到 jstack。");
        var dump = new List<string>();
        for (var i = 1; i <= 3; i++)
        {
            dump.Add(RunJStack(jstack, game.ProcessId));
            Thread.Sleep(3000);
        }

        return dump;
    }

    public void Dispose() => Log.Lines.CollectionChanged -= OnLinesChanged;

    /// <summary>级别对应的行颜色，与 WPF 中 ColorBrushDebug、ColorBrushInfo 等资源相同。</summary>
    public static IBrush? BrushOf(GameLogLevel level)
    {
        var app = Application.Current;
        if (app is null)
            return null;
        var dark = app.RequestedThemeVariant == ThemeVariant.Dark;
        var key = level switch
        {
            GameLogLevel.Fatal => "ColorBrushFatal",
            GameLogLevel.Error => "ColorBrushError",
            GameLogLevel.Warn => "ColorBrushWarn",
            GameLogLevel.Info => dark ? "ColorBrushInfoDark" : "ColorBrushInfo",
            GameLogLevel.Debug => "ColorBrushDebug",
            _ => null
        };
        return key is not null && app.TryGetResource(key, null, out var brush) ? brush as IBrush : null;
    }

    public static GameLogLevel LevelOf(string line)
    {
        var match = LevelPattern.Match(line);
        if (!match.Success)
            return GameLogLevel.None;
        return match.Groups[1].Value switch
        {
            "FATAL" => GameLogLevel.Fatal,
            "ERROR" => GameLogLevel.Error,
            "WARN" => GameLogLevel.Warn,
            "INFO" => GameLogLevel.Info,
            _ => GameLogLevel.Debug
        };
    }

    /// <summary>
    ///     滑动条对应的最大行数，与 WPF 的换算相同：y = 10x + 50（0 ≤ x ≤ 5）、y = 50x − 150（5 &lt; x ≤ 13）、
    ///     y = 100x − 800（13 &lt; x ≤ 28），超过时不限制。
    /// </summary>
    public static int MaxLinesOf(int value) => value switch
    {
        <= 5 => value * 10 + 50,
        <= 13 => value * 50 - 150,
        <= 28 => value * 100 - 800,
        _ => int.MaxValue
    };

    private void AddLine(string text)
    {
        var level = LevelOf(text);
        if (level != GameLogLevel.None)
            counts[(int)level] += 1;
        View.Add(new GameLogLine(text, level));
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems is not null)
                    foreach (var item in e.NewItems)
                        AddLine((string)item!);
                ScheduleTrim();
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems is not null)
                    for (var i = 0; i < e.OldItems.Count; i++)
                        View.RemoveAt(e.OldStartingIndex);
                break;
            default:
                View.Clear();
                foreach (var line in Log.Lines)
                    View.Add(new GameLogLine(line, LevelOf(line)));
                break;
        }
    }

    /// <summary>按滑动条设置裁剪日志行，放到 UI 线程上执行，避免在集合变化事件中修改集合。</summary>
    private void ScheduleTrim()
    {
        if (trimScheduled)
            return;
        trimScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            trimScheduled = false;
            var limit = MaxLinesOf(App.Services.Settings.SystemMaxLog);
            while (Log.Lines.Count > limit)
                Log.Lines.RemoveAt(0);
        });
    }

    private static string? FindJStack(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var javaPath = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(javaPath))
                return null;
            var jstack = Path.Combine(Path.GetDirectoryName(javaPath)!,
                PlatformInfo.Os == OsKind.Windows ? "jstack.exe" : "jstack");
            return File.Exists(jstack) ? jstack : null;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException
                                      or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string RunJStack(string jstack, int pid)
    {
        var startInfo = new ProcessStartInfo(jstack)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add(pid.ToString(CultureInfo.InvariantCulture));
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 jstack。");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
