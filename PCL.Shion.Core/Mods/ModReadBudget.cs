using System.Buffers;
using System.IO.Compression;

namespace PCL.Shion.Core.Mods;

/// <summary>整个 Jar-in-Jar 递归共用预算；不能在进入子 Jar 时重置节点数或解压总量。</summary>
internal sealed class ModReadBudget(ModScanOptions options, CancellationToken cancellationToken)
{
    private int _nestedJars;
    private long _remainingBytes = options.MaxTotalUncompressedBytes;

    public ModScanOptions Options { get; } = options;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public string? Warning { get; private set; }

    public void Warn(string message) => Warning ??= message;

    public bool TryEnterNested(int parentDepth)
    {
        if (parentDepth >= Options.MaxNestedDepth || _nestedJars >= Options.MaxNestedJars || _remainingBytes == 0)
        {
            Warn("Nested mod metadata exceeded the scan budget.");
            return false;
        }
        _nestedJars++;
        return true;
    }

    public MemoryStream? ReadEntry(ZipArchiveEntry entry, int maximumBytes)
    {
        CancellationToken.ThrowIfCancellationRequested();
        var limit = Math.Min(maximumBytes, _remainingBytes);
        if (entry.Length > limit)
        {
            Warn($"Mod metadata entry '{entry.FullName}' exceeded the scan budget.");
            return null;
        }

        var result = new MemoryStream((int)entry.Length);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            using var source = entry.Open();
            while (true)
            {
                CancellationToken.ThrowIfCancellationRequested();
                var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, limit - result.Length + 1));
                if (read == 0)
                    break;
                if (read > limit - result.Length)
                    throw new InvalidDataException($"Mod metadata entry '{entry.FullName}' exceeded the scan budget.");
                _remainingBytes -= read;
                result.Write(buffer, 0, read);
            }
            result.Position = 0;
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
