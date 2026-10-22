using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class InstanceScannerTest
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
    public void MissingVersionsDirectoryYieldsNoInstances()
    {
        Assert.HasCount(0, InstanceScanner.Scan(_root.RootPath));
    }

    [TestMethod]
    public void ListsInstancesSortedByName()
    {
        _root.WriteVersion("b-second", """{ "mainClass": "Main" }""");
        _root.WriteVersion("A-first", """{ "mainClass": "Main" }""");
        _root.WriteVersion("c-third", """{ "mainClass": "Main" }""");

        var instances = InstanceScanner.Scan(_root.RootPath);

        CollectionAssert.AreEqual(
            new[] { "A-first", "b-second", "c-third" },
            instances.Select(instance => instance.Name).ToArray());
        Assert.AreEqual(_root.GetPath("versions/A-first"), instances[0].Directory);
    }

    [TestMethod]
    public void IgnoresDirectoriesWithoutMatchingJson()
    {
        _root.WriteVersion("real", """{ "mainClass": "Main" }""");
        _root.WriteFile("versions/stray/other.json", "{}");
        Directory.CreateDirectory(_root.GetPath("versions/empty"));

        var instances = InstanceScanner.Scan(_root.RootPath);

        CollectionAssert.AreEqual(new[] { "real" }, instances.Select(instance => instance.Name).ToArray());
    }

    [TestMethod]
    public void ReportsVersionAndLoader()
    {
        _root.WriteVersion("1.20.1", """{ "mainClass": "Main" }""");
        _root.WriteVersion("fabric-1.20.1", """
            {
              "inheritsFrom": "1.20.1",
              "libraries": [ { "name": "net.fabricmc:fabric-loader:0.15.0" } ]
            }
            """);
        _root.WriteVersion("1.21.1-Forge_52.1.20", """
            {
              "mainClass": "net.minecraftforge.bootstrap.ForgeBootstrap",
              "clientVersion": "1.21.1",
              "libraries": [ { "name": "net.minecraftforge:forge:1.21.1-52.1.20:universal" } ]
            }
            """);

        var instances = InstanceScanner.Scan(_root.RootPath).ToDictionary(instance => instance.Name);

        Assert.AreEqual("1.20.1", instances["1.20.1"].MinecraftVersion);
        Assert.IsNull(instances["1.20.1"].Loader);
        Assert.AreEqual("1.20.1", instances["fabric-1.20.1"].MinecraftVersion);
        Assert.AreEqual("fabric", instances["fabric-1.20.1"].Loader);
        Assert.AreEqual("1.21.1", instances["1.21.1-Forge_52.1.20"].MinecraftVersion);
        Assert.AreEqual("forge", instances["1.21.1-Forge_52.1.20"].Loader);
        Assert.IsTrue(instances.Values.All(instance => instance.Error is null));
    }

    [TestMethod]
    public void BrokenInstancesAreListedWithAnError()
    {
        _root.WriteVersion("good", """{ "mainClass": "Main" }""");
        _root.WriteVersion("bad-json", "{ not json");
        _root.WriteVersion("no-main-class", "{}");
        _root.WriteVersion("orphan", """{ "inheritsFrom": "missing-parent", "mainClass": "Main" }""");

        var instances = InstanceScanner.Scan(_root.RootPath).ToDictionary(instance => instance.Name);

        Assert.HasCount(4, instances);
        Assert.IsNull(instances["good"].Error);
        foreach (var name in new[] { "bad-json", "no-main-class", "orphan" })
        {
            Assert.IsFalse(string.IsNullOrEmpty(instances[name].Error), name);
            Assert.IsNull(instances[name].MinecraftVersion);
            Assert.IsNull(instances[name].Loader);
        }
    }

    [TestMethod]
    public void IsolationIsReadFromInstanceConfig()
    {
        _root.WriteVersion("isolated", """{ "mainClass": "Main" }""");
        _root.WriteFile("versions/isolated/PCL/config.v1.yml", "State: 1\nVersionArgumentIndieV2: true\nVersionLaunchCount: 3\n");
        _root.WriteVersion("shared", """{ "mainClass": "Main" }""");
        _root.WriteFile("versions/shared/PCL/config.v1.yml", "State: 1\r\nVersionArgumentIndieV2: false\r\n");
        _root.WriteVersion("unconfigured", """{ "mainClass": "Main" }""");
        _root.WriteVersion("no-field", """{ "mainClass": "Main" }""");
        _root.WriteFile("versions/no-field/PCL/config.v1.yml", "State: 1\n");

        var instances = InstanceScanner.Scan(_root.RootPath).ToDictionary(instance => instance.Name);

        Assert.IsTrue(instances["isolated"].IsIsolated);
        Assert.IsFalse(instances["shared"].IsIsolated);
        Assert.IsFalse(instances["unconfigured"].IsIsolated);
        Assert.IsFalse(instances["no-field"].IsIsolated);
    }

    [TestMethod]
    public void IsolationConfigWithByteOrderMarkIsRead()
    {
        _root.WriteVersion("bom", """{ "mainClass": "Main" }""");
        _root.WriteFile(
            "versions/bom/PCL/config.v1.yml",
            "VersionArgumentIndieV2: true\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var instance = InstanceScanner.Scan(_root.RootPath).Single();

        Assert.IsTrue(instance.IsIsolated);
    }

    [TestMethod]
    public void BrokenInstanceStillReportsIsolation()
    {
        _root.WriteVersion("bad", "{ not json");
        _root.WriteFile("versions/bad/PCL/config.v1.yml", "VersionArgumentIndieV2: true\n");

        var instance = InstanceScanner.Scan(_root.RootPath).Single();

        Assert.IsNotNull(instance.Error);
        Assert.IsTrue(instance.IsIsolated);
    }
}
