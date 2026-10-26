using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Mods;

namespace PCL.Shion.Core.Test.Mods;

[TestClass]
public sealed class ModScanReliabilityTest
{
    private FakeMinecraftRoot _root = null!;

    [TestInitialize]
    public void Initialize() => _root = new FakeMinecraftRoot();

    [TestCleanup]
    public void Cleanup() => _root.Dispose();

    private string GameDirectory => _root.GetPath("game");

    private string ModPath(string name) => _root.GetPath("game/mods/" + name);

    private static (string Name, byte[] Data) Metadata(string id) =>
        ModJar.Text("fabric.mod.json", "{\"id\":\"" + id + "\"}");

    [TestMethod]
    public void Read_NestedDepthLimitPreservesOuterModAndStopsRecursion()
    {
        var third = ModJar.Build(Metadata("third"));
        var second = ModJar.Build(Metadata("second"), ("META-INF/jars/third.jar", third));
        var first = ModJar.Build(Metadata("first"), ("META-INF/jars/second.jar", second));
        var path = ModJar.Write(ModPath("root.jar"), Metadata("root"), ("META-INF/jars/first.jar", first));

        var mod = ModScanner.Read(path, new ModScanOptions { MaxNestedDepth = 2 });

        Assert.AreEqual("root", mod.Id);
        CollectionAssert.AreEquivalent(new[] { "first", "second" }, mod.Provides.ToArray());
        Assert.IsFalse(mod.MetadataComplete);
        Assert.IsNotNull(mod.ReadWarning);
    }

    [TestMethod]
    public void Read_NodeBudgetIsSharedBySiblingTrees()
    {
        var first = ModJar.Build(Metadata("first"), ("META-INF/jars/leaf.jar", ModJar.Build(Metadata("leaf"))));
        var second = ModJar.Build(Metadata("second"), ("META-INF/jars/last.jar", ModJar.Build(Metadata("last"))));
        var path = ModJar.Write(ModPath("root.jar"), Metadata("root"),
            ("META-INF/jars/first.jar", first), ("META-INF/jars/second.jar", second));

        var mod = ModScanner.Read(path, new ModScanOptions { MaxNestedJars = 3 });

        CollectionAssert.AreEquivalent(new[] { "first", "leaf", "second" }, mod.Provides.ToArray());
        Assert.IsFalse(mod.MetadataComplete);
    }

    [TestMethod]
    public void Read_OversizedMetadataDoesNotHideOtherFormats()
    {
        var path = ModJar.Write(ModPath("mod.jar"),
            ModJar.Text("fabric.mod.json", "{\"id\":\"large\",\"description\":\"" + new string('x', 2000) + "\"}"),
            ModJar.Text("META-INF/mods.toml", "[[mods]]\nmodId=\"small\"\nversion=\"1\""));

        var mod = ModScanner.Read(path, new ModScanOptions { MaxMetadataBytes = 128 });

        Assert.AreEqual("small", mod.Id);
        Assert.AreEqual("1", mod.Version);
        Assert.IsFalse(mod.MetadataComplete);
    }

    [TestMethod]
    public void Read_OversizedNestedJarDoesNotHideHealthySibling()
    {
        var payload = new byte[4096];
        new Random(1).NextBytes(payload);
        var large = ModJar.Build(Metadata("large"), ("data.bin", payload));
        var small = ModJar.Build(Metadata("small"));
        var path = ModJar.Write(ModPath("root.jar"), Metadata("root"),
            ("META-INF/jars/large.jar", large), ("META-INF/jars/small.jar", small));

        var mod = ModScanner.Read(path, new ModScanOptions { MaxNestedJarBytes = small.Length + 1 });

        CollectionAssert.AreEqual(new[] { "small" }, mod.Provides.ToArray());
        Assert.IsFalse(mod.MetadataComplete);
    }

    [TestMethod]
    public void Read_UncompressedBudgetIncludesNestedJarsAndTheirMetadata()
    {
        var firstMetadata = Metadata("first");
        var first = ModJar.Build(firstMetadata);
        var second = ModJar.Build(Metadata("second"));
        var rootMetadata = Metadata("root");
        var path = ModJar.Write(ModPath("root.jar"), rootMetadata,
            ("META-INF/jars/first.jar", first), ("META-INF/jars/second.jar", second));
        var budget = rootMetadata.Data.Length + first.Length + firstMetadata.Data.Length + second.Length - 1;

        var mod = ModScanner.Read(path, new ModScanOptions { MaxTotalUncompressedBytes = budget });

        CollectionAssert.AreEqual(new[] { "first" }, mod.Provides.ToArray());
        Assert.IsFalse(mod.MetadataComplete);
    }

    [TestMethod]
    public void Scan_OneLockedFileDoesNotDiscardReadableMods()
    {
        var locked = ModJar.Write(ModPath("locked.jar"), Metadata("locked"));
        ModJar.Write(ModPath("healthy.jar"), Metadata("healthy"));
        using var held = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var mods = ModScanner.Scan(GameDirectory);

        Assert.HasCount(2, mods);
        Assert.AreEqual("healthy", mods.Single(mod => mod.FileName == "healthy.jar").Id);
        var unreadable = mods.Single(mod => mod.FileName == "locked.jar");
        Assert.IsNotNull(unreadable.ReadWarning);
        Assert.IsFalse(unreadable.MetadataComplete);
    }

    [TestMethod]
    public void Scan_WithParsingOffDoesNotReadOrHashFiles()
    {
        var path = ModJar.Write(ModPath("locked.jar.disabled"), Metadata("hidden"));
        using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var mod = ModScanner.Scan(GameDirectory, new ModScanOptions { ParseMetadata = false }).Single();

        Assert.AreEqual("locked.jar.disabled", mod.FileName);
        Assert.IsFalse(mod.Enabled);
        Assert.IsNull(mod.Id);
        Assert.AreEqual("", mod.Sha1);
        Assert.IsNull(mod.ReadWarning);
        Assert.IsFalse(mod.MetadataComplete);
    }

    [TestMethod]
    public void Read_CancellationIsNotConvertedIntoDamagedMod()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => ModScanner.Read(ModPath("missing.jar"),
            cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => ModScanner.Scan(GameDirectory,
            cancellationToken: cancellation.Token));
    }

    [TestMethod]
    public void Read_DamagedNestedJarPreservesRootAndReportsIncompleteMetadata()
    {
        var path = ModJar.Write(ModPath("root.jar"), Metadata("root"),
            ("META-INF/jars/broken.jar", Encoding.UTF8.GetBytes("broken zip")));

        var mod = ModScanner.Read(path);

        Assert.AreEqual("root", mod.Id);
        Assert.IsFalse(mod.MetadataComplete);
        Assert.IsNotNull(mod.ReadWarning);
        Assert.AreEqual(40, mod.Sha1.Length);
    }

    [TestMethod]
    public void Read_DeepTomlValuesBecomeARecoverableMetadataWarning()
    {
        var toml = "[[mods]]\nmodId=\"test\"\nextra=" + new string('[', 1000) + "0" + new string(']', 1000);
        var path = ModJar.Write(ModPath("deep.jar"), ModJar.Text("META-INF/mods.toml", toml));

        var mod = ModScanner.Read(path);

        Assert.IsNull(mod.Id);
        Assert.IsNotNull(mod.ReadWarning);
        Assert.IsFalse(mod.MetadataComplete);
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public void Scan_ReloadsExternalRenameAndRecognizesOldSuffix()
    {
        var path = ModJar.Write(ModPath("my.old.mod.jar"), Metadata("example"));
        Assert.IsTrue(ModScanner.Scan(GameDirectory).Single().Enabled);
        File.Move(path, path + ".OLD");

        var mod = ModScanner.Scan(GameDirectory).Single();

        Assert.IsFalse(mod.Enabled);
        var enabled = ModManager.SetEnabled(mod, true);
        Assert.AreEqual("my.old.mod.jar", enabled.FileName);
        Assert.IsTrue(File.Exists(path));
        Assert.IsFalse(File.Exists(path + ".OLD"));
    }

    [TestMethod]
    public void Analyze_DoesNotInferMissingDependencyFromIncompleteScan()
    {
        var consumer = new LocalMod("consumer.jar", "consumer.jar", true, "consumer", null, null,
            [], [new ModDependency("provider", "*", true)], "");
        var unread = new LocalMod("provider.jar", "provider.jar", true, null, null, null, [], [], "")
        {
            MetadataComplete = false
        };

        Assert.IsEmpty(ModAnalyzer.Analyze([consumer, unread]));
        Assert.HasCount(1, ModAnalyzer.Analyze([consumer]));
    }
}
