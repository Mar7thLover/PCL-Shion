using System.Security.Cryptography;

namespace PCL.Shion.Core.Download;

/// <summary>
/// 按大小与 SHA-1 校验本地文件。
/// </summary>
public static class FileVerifier
{
    /// <summary>
    /// 判断文件是否存在且与给定的大小和 SHA-1 一致；为 null 的条件不检查。
    /// </summary>
    public static bool Matches(string path, long? size, string? sha1)
    {
        var file = new FileInfo(path);
        if (!file.Exists || (size is { } expected && file.Length != expected)) return false;
        if (sha1 is null) return true;

        using var stream = file.OpenRead();
        return HashEquals(SHA1.HashData(stream), sha1);
    }

    internal static async Task<bool> MatchesAsync(string path, long? size, string? sha1, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = new FileInfo(path);
        if (!file.Exists || (size is { } expected && file.Length != expected)) return false;
        if (sha1 is null) return true;

        await using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        var hash = await SHA1.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return HashEquals(hash, sha1);
    }

    internal static bool HashEquals(byte[] hash, string sha1) =>
        string.Equals(Convert.ToHexString(hash), sha1, StringComparison.OrdinalIgnoreCase);
}
