namespace PCL.Shion.Core.Saves;

/// <summary>
/// 存档的游戏模式。硬核模式由 level.dat 中的 hardcore 标记决定，不单独对应 GameType。
/// </summary>
public enum GameMode
{
    Survival,
    Creative,
    Adventure,
    Spectator,
    Hardcore,
}

/// <summary>
/// 世界难度，数值与 level.dat 中的 Difficulty 标签一致。
/// </summary>
public enum Difficulty
{
    Peaceful = 0,
    Easy = 1,
    Normal = 2,
    Hard = 3,
}

/// <summary>
/// 各个数据版本的分界线（DataVersion），用于判断存档格式。
/// </summary>
public static class DataVersionBoundaries
{
    /// <summary>17w47a 起存档支持数据包。</summary>
    public const int _17w47a = 1443;
}

/// <summary>
/// 一个可修改的值。包装后表示用户明确给出了这一项的新值。
/// </summary>
/// <typeparam name="T">值的类型。</typeparam>
public sealed record Editable<T>(T Value);

/// <summary>
/// 存档 level.dat 中的基本信息。
/// </summary>
/// <param name="VersionName">游戏版本名，旧存档中没有时为 null。</param>
/// <param name="VersionId">数据版本号，旧存档中没有时为 null。</param>
/// <param name="LevelName">世界名称。</param>
/// <param name="Seed">世界种子，读取不到时为 null。</param>
/// <param name="LastPlayedUtc">最近一次游玩的时间（UTC）。</param>
/// <param name="Spawn">出生点坐标，没有时为 null。</param>
/// <param name="GameMode">游戏模式。</param>
/// <param name="PlayTime">世界的累计游戏时间。</param>
/// <param name="AllowCommands">是否允许指令。</param>
/// <param name="Difficulty">难度，旧存档中没有时为 null。</param>
/// <param name="IsHardcore">是否为极限模式。</param>
/// <param name="IsDifficultyLocked">难度是否已锁定。</param>
public sealed record SaveInfo(
    string? VersionName,
    int? VersionId,
    string LevelName,
    long? Seed,
    DateTimeOffset LastPlayedUtc,
    (double X, double Y, double Z)? Spawn,
    GameMode GameMode,
    TimeSpan PlayTime,
    bool AllowCommands,
    Difficulty? Difficulty,
    bool IsHardcore,
    bool IsDifficultyLocked);

/// <summary>
/// 对存档的一组修改。未设置的项保持不变。
/// </summary>
public sealed class SaveChanges
{
    public Editable<bool>? AllowCommands { get; init; }

    public Editable<Difficulty>? Difficulty { get; init; }

    public Editable<bool>? LockDifficulty { get; init; }
}
