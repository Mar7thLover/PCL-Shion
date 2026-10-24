using System.IO.Compression;
using System.Text;
using PCL.Shion.Core.Worlds.Nbt;

namespace PCL.Shion.Core.Saves;

/// <summary>
/// 读取与修改存档目录下的 level.dat。无状态，可在任意线程并发使用；同一存档的写入需由调用方串行化。
/// </summary>
public sealed class SaveManager
{
    private const string LevelFileName = "level.dat";

    /// <summary>读取存档的基本信息。</summary>
    /// <exception cref="FileNotFoundException">存档缺少 level.dat。</exception>
    /// <exception cref="InvalidDataException">level.dat 格式有误。</exception>
    public Task<SaveInfo> LoadSaveAsync(string saveFolder, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => LoadSave(saveFolder), cancellationToken);
    }

    /// <summary>把修改写回存档的 level.dat，未设置的项保持原样。</summary>
    /// <exception cref="FileNotFoundException">存档缺少 level.dat。</exception>
    /// <exception cref="InvalidDataException">level.dat 格式有误。</exception>
    public Task ApplyChangesAsync(string saveFolder, SaveChanges changes, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ApplyChanges(saveFolder, changes), cancellationToken);
    }

    private static SaveInfo LoadSave(string saveFolder)
    {
        var root = ReadLevel(saveFolder);
        var data = root.GetCompound("Data") ?? throw new InvalidDataException("level.dat has no Data compound.");

        var version = data.GetCompound("Version");
        var versionName = version?.GetString("Name");
        var versionId = GetInt(version, "Id") ?? GetInt(data, "DataVersion");

        var seed = data.GetCompound("WorldGenSettings") is { } worldGen
            ? worldGen.GetLong("seed")
            : data.GetLong("RandomSeed");

        (double X, double Y, double Z)? spawn = null;
        var spawnX = GetInt(data, "SpawnX");
        var spawnY = GetInt(data, "SpawnY");
        var spawnZ = GetInt(data, "SpawnZ");
        if (spawnX is { } x && spawnY is { } y && spawnZ is { } z)
            spawn = (x, y, z);

        var isHardcore = GetFlag(data, "hardcore") ?? false;
        var gameMode = isHardcore ? GameMode.Hardcore : ParseGameType(GetInt(data, "GameType") ?? 0);

        var difficulty = GetByte(data, "Difficulty") is { } difficultyValue
            ? (Difficulty)Math.Clamp((int)difficultyValue, 0, 3)
            : (Difficulty?)null;

        var lastPlayedMs = data.GetLong("LastPlayed");
        var lastPlayed = lastPlayedMs is { } ms
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
            : DateTimeOffset.UnixEpoch;

        var playTicks = data.GetLong("Time") ?? 0L;

        return new SaveInfo(
            VersionName: versionName,
            VersionId: versionId,
            LevelName: data.GetString("LevelName") ?? Path.GetFileName(saveFolder.TrimEnd(Path.DirectorySeparatorChar)),
            Seed: seed,
            LastPlayedUtc: lastPlayed,
            Spawn: spawn,
            GameMode: gameMode,
            PlayTime: TimeSpan.FromSeconds(playTicks / 20d),
            AllowCommands: GetFlag(data, "allowCommands") ?? false,
            Difficulty: difficulty,
            IsHardcore: isHardcore,
            IsDifficultyLocked: GetFlag(data, "DifficultyLocked") ?? false);
    }

    private static void ApplyChanges(string saveFolder, SaveChanges changes)
    {
        var root = ReadLevel(saveFolder);
        var data = root.GetCompound("Data") ?? throw new InvalidDataException("level.dat has no Data compound.");

        var entries = new Dictionary<string, NbtTag>(data.Entries);
        if (changes.AllowCommands is { } allowCommands)
            entries["allowCommands"] = new NbtValue<sbyte>(NbtTagType.Byte, allowCommands.Value ? (sbyte)1 : (sbyte)0);
        if (changes.Difficulty is { } difficulty)
            entries["Difficulty"] = new NbtValue<sbyte>(NbtTagType.Byte, (sbyte)(int)difficulty.Value);
        if (changes.LockDifficulty is { } lockDifficulty)
            entries["DifficultyLocked"] = new NbtValue<sbyte>(NbtTagType.Byte, lockDifficulty.Value ? (sbyte)1 : (sbyte)0);

        var rootEntries = new Dictionary<string, NbtTag>(root.Entries) { ["Data"] = new NbtCompound(entries) };
        WriteLevel(saveFolder, new NbtCompound(rootEntries));
    }

    private static NbtCompound ReadLevel(string saveFolder)
    {
        var path = Path.Combine(saveFolder, LevelFileName);
        using var stream = File.OpenRead(path);
        return NbtReader.ReadGZip(stream);
    }

    private static void WriteLevel(string saveFolder, NbtCompound root)
    {
        var path = Path.Combine(saveFolder, LevelFileName);
        var temp = path + ".tmp";

        using (var buffer = new MemoryStream())
        {
            var writer = new NbtWriter(buffer);
            writer.WriteRoot(root);

            using var file = File.Create(temp);
            using var gzip = new GZipStream(file, CompressionLevel.Optimal);
            gzip.Write(buffer.GetBuffer(), 0, (int)buffer.Length);
        }

        File.Move(temp, path, overwrite: true);
    }

    private static GameMode ParseGameType(int gameType) => gameType switch
    {
        1 => GameMode.Creative,
        2 => GameMode.Adventure,
        3 => GameMode.Spectator,
        _ => GameMode.Survival,
    };

    private static int? GetInt(NbtCompound? compound, string name) =>
        compound?.Get(name) is NbtValue<int> value ? value.Value : null;

    private static sbyte? GetByte(NbtCompound? compound, string name) =>
        compound?.Get(name) is NbtValue<sbyte> value ? value.Value : null;

    private static bool? GetFlag(NbtCompound? compound, string name) =>
        GetByte(compound, name) is { } value ? value != 0 : null;

    /// <summary>
    /// 把 NBT 树按照 level.dat 的格式（大端，根标签为无名 Compound）写入流。
    /// </summary>
    private sealed class NbtWriter(Stream stream)
    {
        private readonly BinaryWriter _writer = new(stream, Encoding.UTF8, leaveOpen: true);

        public void WriteRoot(NbtCompound root)
        {
            _writer.Write((byte)NbtTagType.Compound);
            WriteString(string.Empty);
            WriteCompound(root);
            _writer.Flush();
        }

        private void WriteCompound(NbtCompound compound)
        {
            foreach (var (name, tag) in compound.Entries)
            {
                _writer.Write((byte)tag.Type);
                WriteString(name);
                WritePayload(tag);
            }
            _writer.Write((byte)NbtTagType.End);
        }

        private void WritePayload(NbtTag tag)
        {
            switch (tag)
            {
                case NbtCompound compound:
                    WriteCompound(compound);
                    break;
                case NbtList list:
                    _writer.Write((byte)list.ElementType);
                    WriteInt(list.Count);
                    foreach (var item in list)
                        WritePayload(item);
                    break;
                case NbtValue<sbyte> value:
                    _writer.Write((byte)value.Value);
                    break;
                case NbtValue<short> value:
                    WriteBigEndian(BitConverter.GetBytes(value.Value));
                    break;
                case NbtValue<int> value:
                    WriteInt(value.Value);
                    break;
                case NbtValue<long> value:
                    WriteBigEndian(BitConverter.GetBytes(value.Value));
                    break;
                case NbtValue<float> value:
                    WriteBigEndian(BitConverter.GetBytes(value.Value));
                    break;
                case NbtValue<double> value:
                    WriteBigEndian(BitConverter.GetBytes(value.Value));
                    break;
                case NbtValue<byte[]> value:
                    WriteInt(value.Value.Length);
                    _writer.Write(value.Value);
                    break;
                case NbtValue<string> value:
                    WriteString(value.Value);
                    break;
                case NbtValue<int[]> value:
                    WriteInt(value.Value.Length);
                    foreach (var item in value.Value)
                        WriteInt(item);
                    break;
                case NbtValue<long[]> value:
                    WriteInt(value.Value.Length);
                    foreach (var item in value.Value)
                        WriteBigEndian(BitConverter.GetBytes(item));
                    break;
                default:
                    throw new InvalidDataException($"Unsupported NBT tag {tag.GetType().Name}.");
            }
        }

        private void WriteInt(int value) => WriteBigEndian(BitConverter.GetBytes(value));

        private void WriteString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > ushort.MaxValue)
                throw new InvalidDataException("NBT string is too long.");
            WriteBigEndian(BitConverter.GetBytes((ushort)bytes.Length));
            _writer.Write(bytes);
        }

        private void WriteBigEndian(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            _writer.Write(bytes);
        }
    }
}
