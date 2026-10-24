namespace PCL.Shion.Core.Versions;

/// <summary>
/// Mojang 版本清单。
/// </summary>
/// <param name="LatestRelease">最新正式版的版本号。</param>
/// <param name="LatestSnapshot">最新快照的版本号。</param>
/// <param name="Versions">全部版本，顺序与清单一致（新版本在前）。</param>
public sealed record VersionManifest(string LatestRelease, string LatestSnapshot, IReadOnlyList<RemoteVersion> Versions);
