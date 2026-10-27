using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Logging;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Services;

/// <summary>进程级滚动日志。写日志失败绝不再次抛出异常，也不无限占用磁盘。</summary>
internal sealed class DiagnosticLog : ILogSink, IDisposable
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly string _path;
    private StreamWriter? _writer;
    private bool _disposed;
    private static readonly Regex Placeholders = new(@"\{[^{}]+\}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Secrets = new(
        """(?i)(\b(?:access[_-]?token|refresh[_-]?token|id[_-]?token|client[_-]?token|client[_-]?secret|network[_-]?secret|password|authorization|api[_-]?key|device[_-]?code|token|secret)\b["']?\s*(?:[:=]\s*|[ \t]+))(?:"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|[^\r\n]+)""",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static string? CurrentPath { get; private set; }

    internal DiagnosticLog(string directory)
    {
        _path = Path.Combine(directory, $"Launcher-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Environment.ProcessId}.log");
        try
        {
            Directory.CreateDirectory(directory);
            foreach (var old in Directory.GetFiles(directory, "Launcher-*.log*")
                         .OrderByDescending(File.GetLastWriteTimeUtc).Skip(8))
            {
                try { File.Delete(old); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            Open();
            CurrentPath = _path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public static DiagnosticLog Initialize()
    {
        var directory = Path.Combine(SpecialFolders.Get(Environment.SpecialFolder.ApplicationData), "PCL Shion", "Logs");
        var log = new DiagnosticLog(directory);
        Logger.Sink = log;
        return log;
    }

    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Information;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Log(level, area, source, messageTemplate, []);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        if (!IsEnabled(level, area)) return;
        try
        {
            var index = 0;
            var message = Placeholders.Replace(messageTemplate, match => index < propertyValues.Length
                ? Convert.ToString(propertyValues[index++], CultureInfo.InvariantCulture) ?? ""
                : match.Value);
            message = Redact(message);
            if (message.Length > 32768) message = message[..32768] + " [truncated]";
            lock (_gate)
            {
                if (_disposed || _writer is null) return;
                if (_writer.BaseStream.Length >= MaxBytes)
                {
                    _writer.Dispose();
                    _writer = null;
                    File.Move(_path, _path + ".1", overwrite: true);
                    Open();
                }
                _writer!.WriteLine($"{DateTimeOffset.Now:O} [{level}] [{area}] {message}");
                _writer.Flush();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException or RegexMatchTimeoutException) { }
    }

    internal static string Redact(string text) => Secrets.Replace(text, "$1[redacted]");

    private void Open()
    {
        var options = new FileStreamOptions { Mode = FileMode.Append, Access = FileAccess.Write, Share = FileShare.Read | FileShare.Delete };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        _writer = new StreamWriter(new FileStream(_path, options), new UTF8Encoding(false));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            try { _writer?.Dispose(); }
            catch (IOException) { }
            _writer = null;
        }
    }
}
