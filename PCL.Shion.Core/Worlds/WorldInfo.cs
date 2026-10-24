namespace PCL.Shion.Core.Worlds;

/// <summary>
/// 一个单人存档。
/// </summary>
/// <param name="FolderName">存档文件夹名，快速进入存档时使用。</param>
/// <param name="DisplayName">存档名称；level.dat 中没有时用文件夹名。</param>
/// <param name="LastPlayed">最后游玩时间，未知时为 null。</param>
/// <param name="GameMode">游戏模式：survival、creative、adventure 或 spectator，未知时为 null。</param>
public sealed record WorldInfo(string FolderName, string DisplayName, DateTimeOffset? LastPlayed, string? GameMode);
