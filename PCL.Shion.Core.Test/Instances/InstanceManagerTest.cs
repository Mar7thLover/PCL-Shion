using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Minecraft;
using InstanceSettings = PCL.Shion.Core.Instances.InstanceSettings;

namespace PCL.Shion.Core.Test.Instances;

[TestClass]
public sealed class InstanceManagerTest
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
    [DataRow("1.20.1")]
    [DataRow("1.20.1-Fabric 0.15.11")]
    [DataRow("我的世界")]
    [DataRow(".hidden")]
    public void AcceptsOrdinaryNames(string name)
    {
        Assert.IsTrue(InstanceManager.IsValidName(name));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow(" padded")]
    [DataRow("padded ")]
    [DataRow(".")]
    [DataRow("..")]
    [DataRow("trailing.")]
    [DataRow("a/b")]
    [DataRow("a\\b")]
    [DataRow("a:b")]
    [DataRow("a*b")]
    [DataRow("a?b")]
    [DataRow("a\"b")]
    [DataRow("a<b")]
    [DataRow("a>b")]
    [DataRow("a|b")]
    [DataRow("a\tb")]
    public void RejectsNamesUnusableAsDirectories(string name)
    {
        Assert.IsFalse(InstanceManager.IsValidName(name));
    }

    [TestMethod]
    [DataRow(null, false, false)]
    [DataRow(null, true, true)]
    [DataRow(true, false, true)]
    [DataRow(false, true, false)]
    public void GameDirectoryFollowsSettingsThenInstance(bool? isolatedSetting, bool instanceIsolated, bool expectInstanceDirectory)
    {
        var instance = new GameInstance("demo", _root.GetPath("versions/demo"), "1.20.1", null, instanceIsolated, null);

        var directory = InstanceManager.GetGameDirectory(_root.RootPath, instance, new InstanceSettings { Isolated = isolatedSetting });

        Assert.AreEqual(expectInstanceDirectory ? instance.Directory : _root.RootPath, directory);
    }

    [TestMethod]
    public void RenameMovesEverythingNamedAfterTheInstance()
    {
        _root.WriteVersion("old", """
            { "id": "old", "mainClass": "Main", "note": "中文", "libraries": [ { "name": "com.example:lib:1.0" } ] }
            """);
        _root.WriteFile("versions/old/old.jar", "jar");
        _root.WriteFile("versions/old/old-natives/lwjgl.dll", "native");
        _root.WriteFile("versions/old/saves/world/level.dat", "world");
        InstanceStore.Save(_root.RootPath, "old", new InstanceSettings { TotalPlaySeconds = 42 });

        InstanceManager.Rename(_root.RootPath, "old", "new");

        Assert.IsFalse(Directory.Exists(_root.GetPath("versions/old")));
        Assert.AreEqual("jar", File.ReadAllText(_root.GetPath("versions/new/new.jar")));
        Assert.AreEqual("native", File.ReadAllText(_root.GetPath("versions/new/new-natives/lwjgl.dll")));
        Assert.AreEqual("world", File.ReadAllText(_root.GetPath("versions/new/saves/world/level.dat")));
        Assert.IsFalse(File.Exists(_root.GetPath("versions/new/old.json")));
        Assert.IsFalse(File.Exists(_root.GetPath("versions/new/old.jar")));
        Assert.AreEqual(42L, InstanceStore.Load(_root.RootPath, "new").TotalPlaySeconds);

        var jsonText = File.ReadAllText(_root.GetPath("versions/new/new.json"));
        var json = JsonNode.Parse(jsonText)!;
        Assert.AreEqual("new", (string?)json["id"]);
        Assert.AreEqual("Main", (string?)json["mainClass"]);
        Assert.AreEqual("com.example:lib:1.0", (string?)json["libraries"]![0]!["name"]);
        Assert.Contains("中文", jsonText);
        Assert.AreEqual("new", VersionResolver.Resolve(_root.RootPath, "new").Id);
    }

    [TestMethod]
    public void RenameToleratesInstancesWithoutJarOrNatives()
    {
        WriteInstance("old");

        InstanceManager.Rename(_root.RootPath, "old", "new");

        Assert.IsTrue(File.Exists(_root.GetPath("versions/new/new.json")));
        Assert.IsFalse(Directory.Exists(_root.GetPath("versions/old")));
    }

    [TestMethod]
    public void RenamePointsChildInstancesAtTheNewName()
    {
        WriteInstance("vanilla");
        WriteInstance("child", inheritsFrom: "vanilla");
        const string unrelated = """{ "id": "unrelated", "inheritsFrom": "other", "mainClass": "Main" }""";
        _root.WriteVersion("unrelated", unrelated);
        _root.WriteVersion("broken", "{ not json");

        InstanceManager.Rename(_root.RootPath, "vanilla", "base");

        var child = JsonNode.Parse(File.ReadAllText(_root.GetPath("versions/child/child.json")))!;
        Assert.AreEqual("base", (string?)child["inheritsFrom"]);
        Assert.AreEqual("child", (string?)child["id"]);
        Assert.AreEqual("Main", VersionResolver.Resolve(_root.RootPath, "child").MainClass);
        Assert.AreEqual(unrelated, File.ReadAllText(_root.GetPath("versions/unrelated/unrelated.json")));
        Assert.AreEqual("{ not json", File.ReadAllText(_root.GetPath("versions/broken/broken.json")));
    }

    [TestMethod]
    public void RenameToExistingNameFailsAndChangesNothing()
    {
        WriteInstance("a");
        WriteInstance("b");

        Assert.Throws<IOException>(() => InstanceManager.Rename(_root.RootPath, "a", "b"));

        Assert.IsTrue(File.Exists(_root.GetPath("versions/a/a.json")));
        Assert.IsTrue(File.Exists(_root.GetPath("versions/b/b.json")));
    }

    [TestMethod]
    public void RenameOfInstanceWithInvalidJsonFailsBeforeMovingAnything()
    {
        _root.WriteVersion("bad", "{ not json");

        Assert.Throws<InvalidDataException>(() => InstanceManager.Rename(_root.RootPath, "bad", "good"));

        Assert.IsTrue(Directory.Exists(_root.GetPath("versions/bad")));
        Assert.IsFalse(Directory.Exists(_root.GetPath("versions/good")));
    }

    [TestMethod]
    public void RenameOfMissingInstanceFails()
    {
        Assert.Throws<IOException>(() => InstanceManager.Rename(_root.RootPath, "missing", "new"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("..")]
    [DataRow("a/b")]
    public void RenameRejectsInvalidNewName(string newName)
    {
        WriteInstance("old");

        Assert.Throws<ArgumentException>(() => InstanceManager.Rename(_root.RootPath, "old", newName));

        Assert.IsTrue(File.Exists(_root.GetPath("versions/old/old.json")));
    }

    [TestMethod]
    public void DeleteMovesTheInstanceDirectoryToTrash()
    {
        WriteInstance("gone");
        WriteInstance("kept");
        var trashed = new List<string>();

        InstanceManager.Delete(_root.RootPath, "gone", trashed.Add);

        Assert.HasCount(1, trashed);
        Assert.AreEqual(_root.GetPath("versions/gone"), trashed[0]);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("..")]
    [DataRow("a/b")]
    public void InvalidInstanceNamesAreNeverRenamedOrTrashed(string name)
    {
        WriteInstance("keep");
        var trashed = new List<string>();

        Assert.Throws<ArgumentException>(() => InstanceManager.Rename(_root.RootPath, name, "new"));
        Assert.Throws<ArgumentException>(() => InstanceManager.Delete(_root.RootPath, name, trashed.Add));

        Assert.IsEmpty(trashed);
    }

    private void WriteInstance(string name, string? inheritsFrom = null)
    {
        var json = new JsonObject { ["id"] = name, ["mainClass"] = "Main" };
        if (inheritsFrom is not null)
            json["inheritsFrom"] = inheritsFrom;
        _root.WriteVersion(name, json.ToJsonString());
    }
}
