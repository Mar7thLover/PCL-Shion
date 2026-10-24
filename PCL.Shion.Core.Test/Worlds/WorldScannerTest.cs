using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Worlds;
using PCL.Shion.Core.Worlds.Nbt;
using static PCL.Shion.Core.Test.NbtBytes;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class WorldScannerTest
{
    private FakeMinecraftRoot _root = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = new FakeMinecraftRoot();
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        _root.Dispose();
    }

    [TestMethod]
    public void MissingSavesDirectoryYieldsNoWorlds()
    {
        Assert.HasCount(0, WorldScanner.ScanWorlds(_root.RootPath));
    }

    [TestMethod]
    public void ReadsNameLastPlayedAndGameModeFromLevelDat()
    {
        WriteWorld("folder", LevelName("My World"), LastPlayed(1_700_000_000_123), GameType(1));

        var worlds = WorldScanner.ScanWorlds(_root.RootPath);

        var expected = new WorldInfo("folder", "My World", DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123), "creative");
        Assert.HasCount(1, worlds);
        Assert.AreEqual(expected, worlds[0]);
    }

    [TestMethod]
    [DataRow(0, "survival")]
    [DataRow(1, "creative")]
    [DataRow(2, "adventure")]
    [DataRow(3, "spectator")]
    public void NamesTheGameModeOfEachGameType(int gameType, string expected)
    {
        WriteWorld("world", GameType(gameType));

        Assert.AreEqual(expected, WorldScanner.ScanWorlds(_root.RootPath).Single().GameMode);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(4)]
    public void UnknownGameTypeHasNoGameMode(int gameType)
    {
        WriteWorld("world", GameType(gameType));

        Assert.IsNull(WorldScanner.ScanWorlds(_root.RootPath).Single().GameMode);
    }

    [TestMethod]
    public void AcceptsGameTypeStoredAsAnotherIntegerType()
    {
        WriteWorld("world", Named(NbtTagType.Byte, "GameType", ByteValue(2)));

        Assert.AreEqual("adventure", WorldScanner.ScanWorlds(_root.RootPath).Single().GameMode);
    }

    [TestMethod]
    public void DecodesNonAsciiLevelNames()
    {
        WriteWorld("folder", LevelName("新世界 \U0001F30D"));

        Assert.AreEqual("新世界 \U0001F30D", WorldScanner.ScanWorlds(_root.RootPath).Single().DisplayName);
    }

    [TestMethod]
    public void MissingFieldsFallBackToTheFolderName()
    {
        WriteWorld("empty-data");
        WriteWorld("blank-name", LevelName(""));

        var worlds = WorldScanner.ScanWorlds(_root.RootPath);

        Assert.AreEqual(new WorldInfo("blank-name", "blank-name", null, null), worlds[0]);
        Assert.AreEqual(new WorldInfo("empty-data", "empty-data", null, null), worlds[1]);
    }

    [TestMethod]
    public void LevelDatWithoutDataCompoundFallsBackToTheFolderName()
    {
        WriteBytes("saves/odd/level.dat", GZip(Root()));

        Assert.AreEqual(new WorldInfo("odd", "odd", null, null), WorldScanner.ScanWorlds(_root.RootPath).Single());
    }

    [TestMethod]
    public void OutOfRangeLastPlayedIsIgnored()
    {
        WriteWorld("late", LastPlayed(long.MaxValue));
        WriteWorld("early", LastPlayed(long.MinValue));

        var worlds = WorldScanner.ScanWorlds(_root.RootPath);

        Assert.HasCount(2, worlds);
        Assert.IsTrue(worlds.All(world => world.LastPlayed is null));
    }

    [TestMethod]
    public void IgnoresEntriesThatAreNotWorlds()
    {
        WriteWorld("real", LevelName("Real"));
        WriteBytes("saves/backup/level.dat_old", GZip(Root()));
        WriteBytes("saves/readme.txt", [1, 2, 3]);
        Directory.CreateDirectory(_root.GetPath("saves/empty"));

        var worlds = WorldScanner.ScanWorlds(_root.RootPath);

        CollectionAssert.AreEqual(new[] { "real" }, worlds.Select(world => world.FolderName).ToArray());
    }

    [TestMethod]
    public void ListsUnreadableLevelDatWithTheFolderName()
    {
        var valid = GZip(Root(Named(NbtTagType.Compound, "Data", CompoundValue(LevelName("Fine")))));
        WriteBytes("saves/garbage/level.dat", [1, 2, 3, 4]);
        WriteBytes("saves/empty/level.dat", []);
        WriteBytes("saves/truncated/level.dat", valid[..(valid.Length / 2)]);
        WriteBytes("saves/plain/level.dat", Root(Named(NbtTagType.Compound, "Data", CompoundValue(LevelName("Fine")))));

        var worlds = WorldScanner.ScanWorlds(_root.RootPath);

        Assert.HasCount(4, worlds);
        foreach (var world in worlds)
            Assert.AreEqual(new WorldInfo(world.FolderName, world.FolderName, null, null), world);
    }

    [TestMethod]
    public void SortsByLastPlayedDescendingThenByFolderName()
    {
        WriteWorld("a", LastPlayed(1_000));
        WriteWorld("b", LastPlayed(3_000));
        WriteWorld("c");
        WriteWorld("d", LastPlayed(3_000));
        WriteBytes("saves/e/level.dat", [1, 2, 3]);

        var worlds = WorldScanner.ScanWorlds(_root.RootPath);

        CollectionAssert.AreEqual(
            new[] { "b", "d", "a", "c", "e" },
            worlds.Select(world => world.FolderName).ToArray());
    }

    [TestMethod]
    public void MissingServersFileYieldsNoServers()
    {
        Assert.HasCount(0, WorldScanner.ScanServers(_root.RootPath));
    }

    [TestMethod]
    public void ReadsServersInFileOrder()
    {
        WriteServers(
            Server("Zeta", "zeta.example.com"),
            Server("Alpha", "alpha.example.com:25570", Named(NbtTagType.Byte, "hidden", ByteValue(0)), Named(NbtTagType.String, "icon", StringValue("aWNvbg=="))));

        var servers = WorldScanner.ScanServers(_root.RootPath);

        Assert.HasCount(2, servers);
        Assert.AreEqual(new ServerEntry("Zeta", "zeta.example.com"), servers[0]);
        Assert.AreEqual(new ServerEntry("Alpha", "alpha.example.com:25570"), servers[1]);
    }

    [TestMethod]
    public void ServerWithoutNameIsNamedAfterItsAddress()
    {
        WriteServers(
            Server(null, "no-name.example.com"),
            Server("", "blank-name.example.com"));

        var servers = WorldScanner.ScanServers(_root.RootPath);

        Assert.AreEqual(new ServerEntry("no-name.example.com", "no-name.example.com"), servers[0]);
        Assert.AreEqual(new ServerEntry("blank-name.example.com", "blank-name.example.com"), servers[1]);
    }

    [TestMethod]
    public void IgnoresServersWithoutAnAddress()
    {
        WriteServers(
            Server("No address", null),
            Server("Empty address", ""),
            Server("Kept", "kept.example.com"));

        var servers = WorldScanner.ScanServers(_root.RootPath);

        Assert.AreEqual(new ServerEntry("Kept", "kept.example.com"), servers.Single());
    }

    [TestMethod]
    public void EmptyOrMissingServerListYieldsNoServers()
    {
        WriteBytes("servers.dat", Root(Named(NbtTagType.List, "servers", ListValue(NbtTagType.End))));
        Assert.HasCount(0, WorldScanner.ScanServers(_root.RootPath));

        WriteBytes("servers.dat", Root());
        Assert.HasCount(0, WorldScanner.ScanServers(_root.RootPath));
    }

    [TestMethod]
    public void UnreadableServersFileYieldsNoServers()
    {
        WriteBytes("servers.dat", [0x0A, 0x00]);
        Assert.HasCount(0, WorldScanner.ScanServers(_root.RootPath));

        WriteBytes("servers.dat", GZip(Root()));
        Assert.HasCount(0, WorldScanner.ScanServers(_root.RootPath));
    }

    private void WriteWorld(string folderName, params byte[][] dataEntries)
    {
        var level = Root(Named(NbtTagType.Compound, "Data", CompoundValue(dataEntries)));
        WriteBytes($"saves/{folderName}/level.dat", GZip(level));
    }

    private void WriteServers(params byte[][] servers)
    {
        WriteBytes("servers.dat", Root(Named(NbtTagType.List, "servers", ListValue(NbtTagType.Compound, servers))));
    }

    private void WriteBytes(string relativePath, byte[] content)
    {
        var path = _root.GetPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private static byte[] LevelName(string value) => Named(NbtTagType.String, "LevelName", StringValue(value));

    private static byte[] LastPlayed(long milliseconds) => Named(NbtTagType.Long, "LastPlayed", LongValue(milliseconds));

    private static byte[] GameType(int value) => Named(NbtTagType.Int, "GameType", IntValue(value));

    private static byte[] Server(string? name, string? address, params byte[][] extraEntries)
    {
        var entries = new List<byte[]>();
        if (name is not null)
            entries.Add(Named(NbtTagType.String, "name", StringValue(name)));
        if (address is not null)
            entries.Add(Named(NbtTagType.String, "ip", StringValue(address)));
        entries.AddRange(extraEntries);
        return CompoundValue(entries.ToArray());
    }
}
