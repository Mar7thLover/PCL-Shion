using System.Text.Json;

namespace PCL.Shion.Core.IO;

public enum JsonFileStatus { Missing, Loaded, Recovered, Unreadable }

public sealed record JsonFileResult<T>(T? Value, JsonFileStatus Status, Exception? Error) where T : class
{
    // 权限或临时 IO 错误不代表原文件已经损坏，不能用较旧的备份主动覆盖它。
    public bool CanPersist => Status is JsonFileStatus.Loaded or JsonFileStatus.Missing
        || Status == JsonFileStatus.Recovered && Error is JsonException or FileNotFoundException or DirectoryNotFoundException;
}

/// <summary>JSON 原子持久化和上次有效版本恢复。损坏的主文件不会覆盖有效备份。</summary>
public static class JsonFileStore
{
    public static JsonFileResult<T> Read<T>(string path, JsonSerializerOptions? options = null) where T : class
    {
        // Windows 替换文件时，新打开的句柄可能短暂被拒绝。与本进程的写入者协调，
        // 避免把正常保存误判为主文件损坏，或退回旧备份后永久关闭配置写入。
        lock (AtomicFile.GateFor(path))
        {
            if (TryRead(path, options, out T? value, out var error))
                return new(value, JsonFileStatus.Loaded, null);
            if (TryRead(path + ".bak", options, out value, out var backupError))
                return new(value, JsonFileStatus.Recovered, error);
            var missing = IsMissing(error) && IsMissing(backupError);
            return new(null, missing ? JsonFileStatus.Missing : JsonFileStatus.Unreadable, IsMissing(error) ? backupError : error);
        }
    }

    public static void Save<T>(string path, T value, JsonSerializerOptions? options = null, bool ownerOnly = true) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        // 序列化失败时连临时文件都不创建。
        var json = JsonSerializer.Serialize(value, options);
        lock (AtomicFile.GateFor(path))
        {
            var validPrevious = TryRead<T>(path, options, out _, out var error);
            if (!validPrevious && error is not (JsonException or FileNotFoundException or DirectoryNotFoundException))
                throw new IOException("The existing JSON file could not be read; it has been preserved.", error);
            AtomicFile.WriteAllText(path, json, keepBackup: validPrevious, ownerOnly: ownerOnly);
        }
    }

    private static bool TryRead<T>(string path, JsonSerializerOptions? options, out T? value, out Exception? error) where T : class
    {
        try
        {
            // 允许原子替换发生在读取期间；已经打开的句柄仍指向完整的旧文件。
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            value = JsonSerializer.Deserialize<T>(reader.ReadToEnd(), options)
                    ?? throw new JsonException("The settings document must not be null.");
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            value = null;
            error = ex;
            return false;
        }
    }

    private static bool IsMissing(Exception? error) => error is FileNotFoundException or DirectoryNotFoundException;
}
