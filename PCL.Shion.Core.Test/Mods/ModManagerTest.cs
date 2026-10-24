using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Mods;

namespace PCL.Shion.Core.Test.Mods;

[TestClass]
public sealed class ModManagerTest
{
    private const string FabricJson = """{ "id": "examplemod", "name": "Example Mod", "version": "1.0" }""";

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

    private string ModPath(string fileName) => _root.GetPath($"game/mods/{fileName}");

    private LocalMod WriteMod(string fileName) =>
        ModScanner.Read(ModJar.Write(ModPath(fileName), ModJar.Text("fabric.mod.json", FabricJson)));

    [TestMethod]
    public void SetEnabled_DisablesByAppendingTheSuffix()
    {
        var mod = WriteMod("example.jar");

        var disabled = ModManager.SetEnabled(mod, false);

        Assert.IsFalse(File.Exists(ModPath("example.jar")));
        Assert.IsTrue(File.Exists(ModPath("example.jar.disabled")));
        Assert.AreEqual(ModPath("example.jar.disabled"), disabled.FilePath);
        Assert.AreEqual("example.jar.disabled", disabled.FileName);
        Assert.IsFalse(disabled.Enabled);
        Assert.AreEqual(mod with { FilePath = disabled.FilePath, FileName = disabled.FileName, Enabled = false }, disabled);
    }

    [TestMethod]
    public void SetEnabled_EnablesByRemovingTheSuffix()
    {
        var mod = WriteMod("example.jar.disabled");

        var enabled = ModManager.SetEnabled(mod, true);

        Assert.IsFalse(File.Exists(ModPath("example.jar.disabled")));
        Assert.IsTrue(File.Exists(ModPath("example.jar")));
        Assert.AreEqual(ModPath("example.jar"), enabled.FilePath);
        Assert.AreEqual("example.jar", enabled.FileName);
        Assert.IsTrue(enabled.Enabled);
    }

    [TestMethod]
    public void SetEnabled_KeepsTheFileWhenAlreadyInTheRequestedState()
    {
        var mod = WriteMod("example.jar");

        var result = ModManager.SetEnabled(mod, true);

        Assert.AreSame(mod, result);
        Assert.IsTrue(File.Exists(ModPath("example.jar")));
    }

    [TestMethod]
    public void SetEnabled_ThrowsWhenTheTargetNameIsTaken()
    {
        var mod = WriteMod("example.jar");
        File.WriteAllText(ModPath("example.jar.disabled"), "occupied");

        Assert.Throws<IOException>(() => ModManager.SetEnabled(mod, false));
        Assert.IsTrue(File.Exists(ModPath("example.jar")));
    }

    [TestMethod]
    public void SetEnabled_IsVisibleToTheNextScan()
    {
        var mod = WriteMod("example.jar");

        ModManager.SetEnabled(mod, false);

        var scanned = ModScanner.Scan(_root.GetPath("game")).Single();
        Assert.IsFalse(scanned.Enabled);
        Assert.AreEqual("examplemod", scanned.Id);
    }
}
