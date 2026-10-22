using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class VersionResolverTest
{
    private const string VanillaJson = """
        {
          "id": "1.20.1",
          "type": "release",
          "mainClass": "net.minecraft.client.main.Main",
          "assets": "5",
          "assetIndex": { "id": "5" },
          "javaVersion": { "component": "java-runtime-gamma", "majorVersion": 17 },
          "downloads": { "client": { "url": "https://example.invalid/client.jar" } },
          "arguments": {
            "game": [ "--username", "${auth_player_name}" ],
            "jvm": [ "-cp", "${classpath}" ]
          },
          "libraries": [
            { "name": "com.google.guava:guava:31.0-jre" },
            { "name": "org.lwjgl:lwjgl:3.3.1" }
          ]
        }
        """;

    private const string FabricJson = """
        {
          "id": "fabric-loader-0.15.0-1.20.1",
          "inheritsFrom": "1.20.1",
          "type": "release",
          "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
          "arguments": {
            "game": [ "--fabric" ],
            "jvm": [ "-DFabricMcEmu=net.minecraft.client.main.Main" ]
          },
          "libraries": [
            { "name": "com.google.guava:guava:32.0-jre" },
            { "name": "net.fabricmc:fabric-loader:0.15.0" }
          ]
        }
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

    [TestMethod]
    public void ResolvesVersionWithoutInheritance()
    {
        _root.WriteVersion("1.20.1", VanillaJson);

        var version = VersionResolver.Resolve(_root.RootPath, "1.20.1");

        Assert.AreEqual("1.20.1", version.Id);
        Assert.AreEqual("net.minecraft.client.main.Main", version.MainClass);
        Assert.AreEqual("release", version.Type);
        Assert.AreEqual("5", version.AssetIndexId);
        Assert.AreEqual(17, version.JavaMajorVersion);
        Assert.HasCount(2, version.Libraries);
        Assert.HasCount(2, version.JvmArguments);
        Assert.HasCount(2, version.GameArguments);
        Assert.IsNull(version.LegacyMinecraftArguments);
        Assert.AreEqual("1.20.1", version.MinecraftVersion);
        Assert.IsNull(version.Loader);
    }

    [TestMethod]
    public void MergesInheritanceChainChildOverParent()
    {
        _root.WriteVersion("1.20.1", VanillaJson);
        _root.WriteVersion("fabric-1.20.1", FabricJson);

        var version = VersionResolver.Resolve(_root.RootPath, "fabric-1.20.1");

        Assert.AreEqual("fabric-1.20.1", version.Id);
        Assert.AreEqual("net.fabricmc.loader.impl.launch.knot.KnotClient", version.MainClass);
        Assert.AreEqual(17, version.JavaMajorVersion);
        Assert.AreEqual("5", version.AssetIndexId);
        CollectionAssert.AreEqual(
            new[]
            {
                "com.google.guava:guava:31.0-jre",
                "org.lwjgl:lwjgl:3.3.1",
                "com.google.guava:guava:32.0-jre",
                "net.fabricmc:fabric-loader:0.15.0"
            },
            version.Libraries.Select(library => library.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "-cp", "${classpath}", "-DFabricMcEmu=net.minecraft.client.main.Main" },
            version.JvmArguments.SelectMany(argument => argument.Values).ToArray());
        CollectionAssert.AreEqual(
            new[] { "--username", "${auth_player_name}", "--fabric" },
            version.GameArguments.SelectMany(argument => argument.Values).ToArray());
    }

    [TestMethod]
    public void ResolvesThreeLevelChain()
    {
        _root.WriteVersion("1.20.1", VanillaJson);
        _root.WriteVersion("fabric-1.20.1", FabricJson);
        _root.WriteVersion("custom", """
            {
              "inheritsFrom": "fabric-1.20.1",
              "libraries": [ { "name": "example:mod-loader-extra:1.0" } ]
            }
            """);

        var version = VersionResolver.Resolve(_root.RootPath, "custom");

        Assert.AreEqual("net.fabricmc.loader.impl.launch.knot.KnotClient", version.MainClass);
        Assert.AreEqual("example:mod-loader-extra:1.0", version.Libraries[^1].Name);
        Assert.AreEqual("1.20.1", version.MinecraftVersion);
        Assert.AreEqual("fabric", version.Loader);
    }

    [TestMethod]
    public void LegacyArgumentsAreInheritedAndOverridden()
    {
        _root.WriteVersion("1.12.2", """
            {
              "mainClass": "net.minecraft.client.main.Main",
              "minecraftArguments": "--username ${auth_player_name} --version ${version_name}",
              "assets": "1.12"
            }
            """);
        _root.WriteVersion("fabric-1.12.2", """
            {
              "inheritsFrom": "1.12.2",
              "mainClass": "net.fabricmc.loader.launch.knot.KnotClient",
              "arguments": { "game": [], "jvm": [ "-DFabricMcEmu= net.minecraft.client.main.Main " ] }
            }
            """);
        _root.WriteVersion("forge-1.12.2", """
            {
              "inheritsFrom": "1.12.2",
              "mainClass": "net.minecraft.launchwrapper.Launch",
              "minecraftArguments": "--username ${auth_player_name} --tweakClass net.minecraftforge.fml.common.launcher.FMLTweaker"
            }
            """);

        var inherited = VersionResolver.Resolve(_root.RootPath, "fabric-1.12.2");
        var overridden = VersionResolver.Resolve(_root.RootPath, "forge-1.12.2");

        Assert.AreEqual("--username ${auth_player_name} --version ${version_name}", inherited.LegacyMinecraftArguments);
        Assert.AreEqual("1.12", inherited.AssetIndexId);
        Assert.AreEqual(
            "--username ${auth_player_name} --tweakClass net.minecraftforge.fml.common.launcher.FMLTweaker",
            overridden.LegacyMinecraftArguments);
    }

    [TestMethod]
    public void AssetIndexDefaultsToLegacy()
    {
        _root.WriteVersion("b1.7.3", """{ "mainClass": "net.minecraft.client.Minecraft" }""");

        var version = VersionResolver.Resolve(_root.RootPath, "b1.7.3");

        Assert.AreEqual("legacy", version.AssetIndexId);
    }

    [TestMethod]
    public void ClientJarComesFromTheNearestVersionThatHasOne()
    {
        _root.WriteVersion("1.20.1", VanillaJson);
        _root.WriteVersion("fabric-1.20.1", FabricJson);
        var parentJar = _root.WriteFile("versions/1.20.1/1.20.1.jar");

        var fromParent = VersionResolver.Resolve(_root.RootPath, "fabric-1.20.1");
        var childJar = _root.WriteFile("versions/fabric-1.20.1/fabric-1.20.1.jar");
        var fromChild = VersionResolver.Resolve(_root.RootPath, "fabric-1.20.1");

        Assert.AreEqual(parentJar, fromParent.ClientJarPath);
        Assert.AreEqual(childJar, fromChild.ClientJarPath);
    }

    [TestMethod]
    public void MissingClientJarPointsAtTheVersionThatDeclaresIt()
    {
        _root.WriteVersion("1.20.1", VanillaJson);
        _root.WriteVersion("fabric-1.20.1", FabricJson);

        var inherited = VersionResolver.Resolve(_root.RootPath, "fabric-1.20.1");
        var own = VersionResolver.Resolve(_root.RootPath, "1.20.1");

        Assert.AreEqual(_root.GetPath("versions/1.20.1/1.20.1.jar"), inherited.ClientJarPath);
        Assert.AreEqual(_root.GetPath("versions/1.20.1/1.20.1.jar"), own.ClientJarPath);
    }

    [TestMethod]
    public void MissingClientJarWithoutAnyDeclarationPointsAtTheRoot()
    {
        _root.WriteVersion("base", """{ "mainClass": "Main" }""");
        _root.WriteVersion("child", """{ "inheritsFrom": "base" }""");

        var version = VersionResolver.Resolve(_root.RootPath, "child");

        Assert.AreEqual(_root.GetPath("versions/base/base.jar"), version.ClientJarPath);
    }

    [TestMethod]
    public void DetectsMinecraftVersionFromInheritsFromThenClientVersionThenId()
    {
        _root.WriteVersion("1.20.1", VanillaJson);
        _root.WriteVersion("fabric-1.20.1", FabricJson);
        _root.WriteVersion("forge-merged", """{ "mainClass": "Main", "clientVersion": "1.21.1" }""");
        _root.WriteVersion("1.19.4-OptiFine_HD_U_I3", """{ "mainClass": "Main" }""");
        _root.WriteVersion("Survival", """{ "mainClass": "Main" }""");

        Assert.AreEqual("1.20.1", VersionResolver.Resolve(_root.RootPath, "fabric-1.20.1").MinecraftVersion);
        Assert.AreEqual("1.21.1", VersionResolver.Resolve(_root.RootPath, "forge-merged").MinecraftVersion);
        Assert.AreEqual("1.19.4", VersionResolver.Resolve(_root.RootPath, "1.19.4-OptiFine_HD_U_I3").MinecraftVersion);
        Assert.IsNull(VersionResolver.Resolve(_root.RootPath, "Survival").MinecraftVersion);
    }

    [TestMethod]
    [DataRow("net.minecraftforge:forge:1.21.1-52.1.20:universal", "forge")]
    [DataRow("net.minecraftforge:fmlloader:1.20.1-47.2.0", "forge")]
    [DataRow("net.neoforged:neoforge:21.1.77:universal", "neoforge")]
    [DataRow("net.neoforged.fancymodloader:loader:4.0.30", "neoforge")]
    [DataRow("net.fabricmc:fabric-loader:0.16.5", "fabric")]
    [DataRow("org.quiltmc:quilt-loader:0.26.0", "quilt")]
    public void DetectsLoaderFromLibraryNames(string library, string expected)
    {
        _root.WriteVersion("modded", $$"""
            {
              "mainClass": "Main",
              "libraries": [
                { "name": "net.fabricmc:intermediary:1.20.1" },
                { "name": "com.google.guava:guava:31.0-jre" },
                { "name": "{{library}}" }
              ]
            }
            """);

        Assert.AreEqual(expected, VersionResolver.Resolve(_root.RootPath, "modded").Loader);
    }

    [TestMethod]
    public void VanillaVersionHasNoLoader()
    {
        _root.WriteVersion("1.20.1", VanillaJson);

        Assert.IsNull(VersionResolver.Resolve(_root.RootPath, "1.20.1").Loader);
    }

    [TestMethod]
    public void ReadsLoggingConfigurationFromTheNearestDeclaringVersion()
    {
        _root.WriteVersion("base", """
            {
              "mainClass": "Main",
              "logging": { "client": { "argument": "-Dlog4j.configurationFile=${path}", "file": { "id": "client-1.12.xml" } } }
            }
            """);
        _root.WriteVersion("child", """{ "inheritsFrom": "base" }""");

        var version = VersionResolver.Resolve(_root.RootPath, "child");

        Assert.AreEqual("-Dlog4j.configurationFile=${path}", version.LoggingArgument);
        Assert.AreEqual("client-1.12.xml", version.LoggingFileId);
    }

    [TestMethod]
    public void CircularInheritanceIsRejected()
    {
        _root.WriteVersion("a", """{ "inheritsFrom": "b", "mainClass": "Main" }""");
        _root.WriteVersion("b", """{ "inheritsFrom": "a", "mainClass": "Main" }""");

        Assert.ThrowsExactly<InvalidDataException>(() => VersionResolver.Resolve(_root.RootPath, "a"));
    }

    [TestMethod]
    public void MissingParentVersionIsReportedAsIoError()
    {
        _root.WriteVersion("orphan", """{ "inheritsFrom": "1.20.1", "mainClass": "Main" }""");

        Assert.Throws<IOException>(() => VersionResolver.Resolve(_root.RootPath, "orphan"));
    }

    [TestMethod]
    public void MissingVersionIsReportedAsIoError()
    {
        Assert.Throws<IOException>(() => VersionResolver.Resolve(_root.RootPath, "absent"));
    }

    [TestMethod]
    public void VersionWithoutMainClassIsRejected()
    {
        _root.WriteVersion("broken", """{ "id": "broken" }""");

        Assert.ThrowsExactly<InvalidDataException>(() => VersionResolver.Resolve(_root.RootPath, "broken"));
    }

    [TestMethod]
    public void MalformedJsonIsRejected()
    {
        _root.WriteVersion("broken", "{ not json");

        Assert.ThrowsExactly<InvalidDataException>(() => VersionResolver.Resolve(_root.RootPath, "broken"));
    }
}
