using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Mods;

namespace PCL.Shion.Core.Test.Mods;

[TestClass]
public sealed class ModScannerTest
{
    private const string FabricJson = """
        {
          "schemaVersion": 1,
          "id": "examplemod",
          "version": "1.2.3",
          "name": "Example Mod",
          "depends": { "fabricloader": ">=0.14.0", "fabric-api": "*" }
        }
        """;

    private const string QuiltJson = """
        {
          "schema_version": 1,
          "quilt_loader": {
            "id": "examplemod",
            "version": "1.2.3",
            "metadata": { "name": "Example Mod" },
            "depends": ["quilt_loader", "fabric-api"]
          }
        }
        """;

    private const string ForgeToml = """
        modLoader="javafml"
        loaderVersion="[47,)"

        [[mods]]
        modId="examplemod"
        version="${file.jarVersion}"
        displayName="Example Mod"

        [[dependencies.examplemod]]
            modId="forge"
            mandatory=true
            versionRange="[47,)"
        """;

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

    private string GameDirectory => _root.GetPath("game");

    private string ModPath(string fileName) => _root.GetPath($"game/mods/{fileName}");

    [TestMethod]
    public void GetModsDirectory_IsModsUnderTheGameDirectory()
    {
        Assert.AreEqual(Path.Combine("game", "mods"), ModScanner.GetModsDirectory("game"));
    }

    [TestMethod]
    public void Scan_ReturnsEmptyWithoutModsDirectory()
    {
        Assert.IsEmpty(ModScanner.Scan(GameDirectory));
    }

    [TestMethod]
    public void Scan_ListsJarAndDisabledJarSortedByName()
    {
        ModJar.Write(ModPath("zeta.jar"), ModJar.Text("fabric.mod.json", """{ "id": "zeta", "name": "Alpha Mod" }"""));
        ModJar.Write(ModPath("alpha.jar.disabled"), ModJar.Text("fabric.mod.json", """{ "id": "alpha", "name": "Bravo Mod" }"""));
        ModJar.Write(ModPath("Charlie.JAR"));
        ModJar.Write(ModPath("beta.jar"), ModJar.Text("fabric.mod.json", """{ "id": "beta" }"""));
        File.WriteAllText(ModPath("readme.txt"), "not a mod");
        File.WriteAllText(ModPath("backup.jar.bak"), "not a mod");
        Directory.CreateDirectory(ModPath("folder.jar"));

        var mods = ModScanner.Scan(GameDirectory);

        // 有名称按名称排序，没有名称的按文件名排序
        CollectionAssert.AreEqual(
            new[] { "zeta.jar", "beta.jar", "alpha.jar.disabled", "Charlie.JAR" },
            mods.Select(mod => mod.FileName).ToArray());
    }

    [TestMethod]
    public void Read_ReadsFabricMod()
    {
        var path = ModJar.Write(ModPath("example.jar"), ModJar.Text("fabric.mod.json", FabricJson));

        var mod = ModScanner.Read(path);

        Assert.AreEqual(path, mod.FilePath);
        Assert.AreEqual("example.jar", mod.FileName);
        Assert.IsTrue(mod.Enabled);
        Assert.AreEqual("examplemod", mod.Id);
        Assert.AreEqual("Example Mod", mod.Name);
        Assert.AreEqual("1.2.3", mod.Version);
        CollectionAssert.AreEqual(new[] { "fabric" }, mod.Loaders.ToArray());
        ModDependency[] expected = [new("fabricloader", ">=0.14.0", true), new("fabric-api", "*", true)];
        CollectionAssert.AreEqual(expected, mod.Dependencies.ToArray());
        Assert.AreEqual(Convert.ToHexString(SHA1.HashData(File.ReadAllBytes(path))).ToLowerInvariant(), mod.Sha1);
    }

    [TestMethod]
    public void Read_MarksDisabledFile()
    {
        var path = ModJar.Write(ModPath("example.jar.disabled"), ModJar.Text("fabric.mod.json", FabricJson));

        var mod = ModScanner.Read(path);

        Assert.IsFalse(mod.Enabled);
        Assert.AreEqual("example.jar.disabled", mod.FileName);
        Assert.AreEqual("examplemod", mod.Id);
    }

    [TestMethod]
    public void Read_MergesJarThatSupportsSeveralLoaders()
    {
        var path = ModJar.Write(
            ModPath("multi.jar"),
            ModJar.Text("fabric.mod.json", FabricJson),
            ModJar.Text("quilt.mod.json", QuiltJson),
            ModJar.Text("META-INF/mods.toml", ForgeToml));

        var mod = ModScanner.Read(path);

        CollectionAssert.AreEqual(new[] { "fabric", "quilt", "forge" }, mod.Loaders.ToArray());
        Assert.AreEqual("examplemod", mod.Id);
        Assert.AreEqual("1.2.3", mod.Version);
        // 同一个依赖在多个格式里出现时只保留一份
        CollectionAssert.AreEqual(
            new[] { "fabricloader", "fabric-api", "quilt_loader", "forge" },
            mod.Dependencies.Select(dependency => dependency.Id).ToArray());
    }

    [TestMethod]
    public void Read_ReadsNeoForgeMod()
    {
        var toml = """
            [[mods]]
            modId="neomod"
            version="3.0"
            """;
        var path = ModJar.Write(ModPath("neo.jar"), ModJar.Text("META-INF/neoforge.mods.toml", toml));

        var mod = ModScanner.Read(path);

        Assert.AreEqual("neomod", mod.Id);
        CollectionAssert.AreEqual(new[] { "neoforge" }, mod.Loaders.ToArray());
    }

    [TestMethod]
    public void Read_ReadsLegacyMod()
    {
        var path = ModJar.Write(
            ModPath("legacy.jar"),
            ModJar.Text("mcmod.info", """[{ "modid": "oldmod", "name": "Old Mod", "version": "1.0" }]"""));

        var mod = ModScanner.Read(path);

        Assert.AreEqual("oldmod", mod.Id);
        Assert.AreEqual("Old Mod", mod.Name);
        CollectionAssert.AreEqual(new[] { "forge" }, mod.Loaders.ToArray());
    }

    [TestMethod]
    public void Read_TakesVersionPlaceholderFromManifest()
    {
        var manifest = "Manifest-Version: 1.0\r\nImplementation-Title: example\r\nImplementation-Version: 4.5.6\r\n\r\n";
        var path = ModJar.Write(
            ModPath("forge.jar"),
            ModJar.Text("META-INF/mods.toml", ForgeToml),
            ModJar.Text("META-INF/MANIFEST.MF", manifest));

        Assert.AreEqual("4.5.6", ModScanner.Read(path).Version);
    }

    [TestMethod]
    public void Read_LeavesVersionEmptyWhenPlaceholderCannotBeResolved()
    {
        var withoutManifest = ModJar.Write(ModPath("forge.jar"), ModJar.Text("META-INF/mods.toml", ForgeToml));
        var unreplaced = ModJar.Write(ModPath("fabric.jar"), ModJar.Text("fabric.mod.json", """{ "id": "dev", "version": "${version}" }"""));

        Assert.IsNull(ModScanner.Read(withoutManifest).Version);
        Assert.IsNull(ModScanner.Read(unreplaced).Version);
    }

    [TestMethod]
    public void Read_FallsBackToAnotherFormatWhenVersionIsUnresolved()
    {
        var toml = ForgeToml.Replace("${file.jarVersion}", "2.0.0");
        var path = ModJar.Write(
            ModPath("multi.jar"),
            ModJar.Text("fabric.mod.json", """{ "id": "examplemod", "version": "${version}" }"""),
            ModJar.Text("META-INF/mods.toml", toml));

        Assert.AreEqual("2.0.0", ModScanner.Read(path).Version);
    }

    [TestMethod]
    public void Read_ListsJarWithoutMetadata()
    {
        var path = ModJar.Write(ModPath("plain.jar"), ModJar.Text("com/example/Main.class", "bytes"));

        var mod = ModScanner.Read(path);

        Assert.IsNull(mod.Id);
        Assert.IsNull(mod.Name);
        Assert.IsNull(mod.Version);
        Assert.IsEmpty(mod.Loaders);
        Assert.IsEmpty(mod.Dependencies);
        Assert.AreEqual(Convert.ToHexString(SHA1.HashData(File.ReadAllBytes(path))).ToLowerInvariant(), mod.Sha1);
    }

    [TestMethod]
    public void Read_ListsFileThatIsNotAnArchive()
    {
        var path = ModPath("broken.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "this is not a zip file");

        var mod = ModScanner.Read(path);

        Assert.IsNull(mod.Id);
        Assert.IsEmpty(mod.Loaders);
        Assert.AreEqual(Convert.ToHexString(SHA1.HashData(File.ReadAllBytes(path))).ToLowerInvariant(), mod.Sha1);
    }

    [TestMethod]
    public void Read_IgnoresDamagedMetadataFile()
    {
        var path = ModJar.Write(
            ModPath("damaged.jar"),
            ModJar.Text("fabric.mod.json", "{ \"id\": \"broken"),
            ModJar.Text("META-INF/mods.toml", ForgeToml.Replace("${file.jarVersion}", "1.0")));

        var mod = ModScanner.Read(path);

        Assert.AreEqual("examplemod", mod.Id);
        Assert.AreEqual("1.0", mod.Version);
        CollectionAssert.AreEqual(new[] { "forge" }, mod.Loaders.ToArray());
    }

    [TestMethod]
    public void Read_AcceptsMetadataWithByteOrderMark()
    {
        var withBom = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(FabricJson)).ToArray();
        var path = ModJar.Write(ModPath("bom.jar"), ("fabric.mod.json", withBom));

        Assert.AreEqual("examplemod", ModScanner.Read(path).Id);
    }

    [TestMethod]
    public void Read_CollectsIdsOfNestedMods()
    {
        var module = ModJar.Build(ModJar.Text("fabric.mod.json", """{ "id": "fabric-api-base", "provides": ["base-alias"] }"""));
        var deep = ModJar.Build(
            ModJar.Text("fabric.mod.json", "{ \"id\": \"outer-lib\" }"),
            ("META-INF/jars/inner.jar", ModJar.Build(ModJar.Text("fabric.mod.json", "{ \"id\": \"inner-lib\" }"))));
        var path = ModJar.Write(
            ModPath("fabric-api.jar"),
            ModJar.Text("fabric.mod.json", """{ "id": "fabric-api", "provides": ["fabric"] }"""),
            ("META-INF/jars/fabric-api-base.jar", module),
            ("META-INF/jars/deep.jar", deep),
            ("META-INF/jars/no-metadata.jar", ModJar.Build(ModJar.Text("a.txt", "x"))),
            ("META-INF/jars/damaged.jar", Encoding.UTF8.GetBytes("not a zip")),
            ModJar.Text("META-INF/jars/readme.txt", "ignored"));

        var mod = ModScanner.Read(path);

        Assert.AreEqual("fabric-api", mod.Id);
        CollectionAssert.AreEquivalent(
            new[] { "fabric", "fabric-api-base", "base-alias", "outer-lib", "inner-lib" },
            mod.Provides.ToArray());
    }

    [TestMethod]
    public void Read_CollectsIdsOfJarJarMods()
    {
        var library = ModJar.Build(ModJar.Text("META-INF/mods.toml", "[[mods]]\nmodId=\"flywheel\"\nversion=\"1\""));
        var path = ModJar.Write(
            ModPath("create.jar"),
            ModJar.Text("META-INF/mods.toml", ForgeToml),
            ("META-INF/jarjar/flywheel-1.jar", library));

        CollectionAssert.AreEqual(new[] { "flywheel" }, ModScanner.Read(path).Provides.ToArray());
    }

    [TestMethod]
    public void Read_DoesNotLockTheFile()
    {
        var path = ModJar.Write(ModPath("example.jar"), ModJar.Text("fabric.mod.json", FabricJson));

        ModScanner.Read(path);

        File.Delete(path);
        Assert.IsFalse(File.Exists(path));
    }
}
