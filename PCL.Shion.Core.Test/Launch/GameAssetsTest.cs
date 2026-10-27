using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Launch;
using System.Text.Json;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class GameAssetsTest
{
    private const string HashA = "aa11bb22cc33dd44ee55ff66aa11bb22cc33dd44";
    private const string HashB = "bb22cc33dd44ee55ff66aa11bb22cc33dd44ee55";
    private const string HashC = "cc33dd44ee55ff66aa11bb22cc33dd44ee55ff66";

    private FakeMinecraftRoot _root = null!;
    private string _assets = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = new FakeMinecraftRoot();
        _assets = _root.GetPath("assets");
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        _root.Dispose();
    }

    [TestMethod]
    public void MissingIndexIsReported()
    {
        var missing = GameAssets.Prepare(_assets, "17", isVirtual: false);

        CollectionAssert.AreEqual(new[] { Path.Combine(_assets, "indexes", "17.json") }, missing.ToArray());
    }

    [TestMethod]
    public void CompleteAssetsReportNothing()
    {
        WriteIndex("17", ("minecraft/sounds/a.ogg", HashA), ("minecraft/lang/en_us.json", HashB));
        WriteObject(HashA);
        WriteObject(HashB);

        Assert.HasCount(0, GameAssets.Prepare(_assets, "17", isVirtual: false));
    }

    [TestMethod]
    public void MissingObjectsAreReportedOnce()
    {
        WriteIndex("17", ("a.ogg", HashA), ("copy-of-a.ogg", HashA), ("b.ogg", HashB), ("c.ogg", HashC));
        WriteObject(HashB);

        var missing = GameAssets.Prepare(_assets, "17", isVirtual: false);

        CollectionAssert.AreEquivalent(
            new[] { ObjectPath(HashA), ObjectPath(HashC) },
            missing.ToArray());
    }

    [TestMethod]
    public void VirtualAssetsAreCopiedUnderTheirOriginalNames()
    {
        WriteIndex("legacy", ("sound/ambient/cave1.ogg", HashA), ("lang/en_US.lang", HashB));
        WriteObject(HashA, "cave");
        WriteObject(HashB, "lang");

        var missing = GameAssets.Prepare(_assets, "legacy", isVirtual: true);

        var virtualDirectory = GameAssets.GetVirtualDirectory(_assets);
        Assert.HasCount(0, missing);
        Assert.AreEqual("cave", File.ReadAllText(Path.Combine(virtualDirectory, "sound", "ambient", "cave1.ogg")));
        Assert.AreEqual("lang", File.ReadAllText(Path.Combine(virtualDirectory, "lang", "en_US.lang")));
    }

    [TestMethod]
    public void ExistingVirtualFilesAreNotOverwritten()
    {
        WriteIndex("legacy", ("lang/en_US.lang", HashA));
        WriteObject(HashA, "from-objects");
        var existing = _root.WriteFile("assets/virtual/legacy/lang/en_US.lang", "already-there");

        GameAssets.Prepare(_assets, "legacy", isVirtual: true);

        Assert.AreEqual("already-there", File.ReadAllText(existing));
    }

    [TestMethod]
    public void NonVirtualAssetsAreNotCopied()
    {
        WriteIndex("17", ("a.ogg", HashA));
        WriteObject(HashA);

        GameAssets.Prepare(_assets, "17", isVirtual: false);

        Assert.IsFalse(Directory.Exists(GameAssets.GetVirtualDirectory(_assets)));
    }

    [TestMethod]
    [DataRow("../outside.txt")]
    [DataRow("..\\outside.txt")]
    [DataRow("/outside.txt")]
    [DataRow("C:\\outside.txt")]
    public void VirtualAssetNamesCannotEscapeTheirDirectory(string name)
    {
        WriteIndex("legacy", (name, HashA));
        WriteObject(HashA, "untrusted content");

        Assert.ThrowsExactly<InvalidDataException>(() => GameAssets.Prepare(_assets, "legacy", isVirtual: true));

        Assert.IsFalse(File.Exists(_root.GetPath("assets/virtual/outside.txt")));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("a")]
    [DataRow("../../outside")]
    [DataRow("zz11bb22cc33dd44ee55ff66aa11bb22cc33dd44")]
    public void RejectsInvalidAssetHashesBeforeConstructingPaths(string hash)
    {
        WriteIndex("17", ("asset", hash));

        Assert.ThrowsExactly<InvalidDataException>(() => GameAssets.Prepare(_assets, "17", isVirtual: false));
    }

    [TestMethod]
    public void IndexNameCannotReadOutsideTheIndexDirectory()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => GameAssets.Prepare(_assets, "../outside", isVirtual: false));
    }

    private string ObjectPath(string hash)
    {
        return Path.Combine(_assets, "objects", hash[..2], hash);
    }

    private void WriteObject(string hash, string content = "data")
    {
        _root.WriteFile($"assets/objects/{hash[..2]}/{hash}", content);
    }

    private void WriteIndex(string id, params (string Name, string Hash)[] objects)
    {
        var entries = objects.Select(item => $$"""  {{JsonSerializer.Serialize(item.Name)}}: { "hash": {{JsonSerializer.Serialize(item.Hash)}}, "size": 1 }""");
        _root.WriteFile($"assets/indexes/{id}.json", $$"""{ "objects": { {{string.Join(",", entries)}} } }""");
    }
}
