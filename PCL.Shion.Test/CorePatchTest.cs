using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Services;

namespace PCL.Shion.Test;

[TestClass]
public sealed class CorePatchTest
{
    private string _directory = null!;
    private string CorePath => Path.Combine(_directory, "client.jar");

    [TestInitialize]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), "shion-patch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        CreateZip(CorePath, ("net/Test.class", "original"), ("META-INF/MOJANG.RSA", "signature"), ("META-INF/services/test", "service"));
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_directory, true);

    [TestMethod]
    public void InvalidPatchLeavesCoreByteForByteIntact()
    {
        var before = File.ReadAllBytes(CorePath);
        var patch = Path.Combine(_directory, "broken.jar");
        File.WriteAllText(patch, "not a zip");
        Assert.Throws<InvalidDataException>(() => GameServices.AddToCore(CorePath, patch));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(CorePath));
        Assert.IsEmpty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [TestMethod]
    public void SuccessfulPatchReplacesEntriesAndRemovesOnlySignatures()
    {
        var patch = Path.Combine(_directory, "patch.jar");
        CreateZip(patch, ("net/Test.class", "updated"));
        GameServices.AddToCore(CorePath, patch);
        using var archive = ZipFile.OpenRead(CorePath);
        using var reader = new StreamReader(archive.GetEntry("net/Test.class")!.Open());
        Assert.AreEqual("updated", reader.ReadToEnd());
        Assert.IsNull(archive.GetEntry("META-INF/MOJANG.RSA"));
        Assert.IsNotNull(archive.GetEntry("META-INF/services/test"));
    }

    [TestMethod]
    public void ZipPatchUsesContentsOfMinecraftJarFolder()
    {
        var patch = Path.Combine(_directory, "patch.zip");
        CreateZip(patch, ("release/MINECRAFT-JAR/net/Test.class", "updated"), ("readme.txt", "ignore"));
        GameServices.AddToCore(CorePath, patch);
        using var archive = ZipFile.OpenRead(CorePath);
        Assert.IsNull(archive.GetEntry("release/MINECRAFT-JAR/net/Test.class"));
        Assert.IsNull(archive.GetEntry("readme.txt"));
        using var reader = new StreamReader(archive.GetEntry("net/Test.class")!.Open());
        Assert.AreEqual("updated", reader.ReadToEnd());
    }

    [TestMethod]
    public void CancellationDoesNotReplaceCore()
    {
        var before = File.ReadAllBytes(CorePath);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => GameServices.AddToCore(CorePath, CorePath, cancel.Token));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(CorePath));
        Assert.IsEmpty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [TestMethod]
    [DataRow("empty.jar")]
    [DataRow("unrelated.zip")]
    public void PatchWithoutApplicableFilesDoesNotReplaceCore(string fileName)
    {
        var before = File.ReadAllBytes(CorePath);
        var patch = Path.Combine(_directory, fileName);
        if (fileName.EndsWith(".zip")) CreateZip(patch, ("readme.txt", "no game files"));
        else CreateZip(patch);
        Assert.Throws<InvalidDataException>(() => GameServices.AddToCore(CorePath, patch));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(CorePath));
        Assert.IsEmpty(Directory.GetFiles(_directory, "*.tmp"));
    }

    private static void CreateZip(string path, params (string Name, string Content)[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entry.Name).Open());
            writer.Write(entry.Content);
        }
    }
}
