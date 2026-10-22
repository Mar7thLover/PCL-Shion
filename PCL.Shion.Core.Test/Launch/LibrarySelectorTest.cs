using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Launch;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class LibrarySelectorTest
{
    private static readonly RuleEnvironment Windows = CreateEnvironment("windows", "x86_64");
    private static readonly RuleEnvironment Windows32 = CreateEnvironment("windows", "x86");
    private static readonly RuleEnvironment Linux = CreateEnvironment("linux", "x86_64");
    private static readonly RuleEnvironment Mac = CreateEnvironment("osx", "arm64");

    [TestMethod]
    public void UsesDeclaredArtifactPathOtherwiseDerivesItFromTheName()
    {
        var libraries = Parse("""
            [
              { "name": "com.mojang:authlib:10.0.77", "downloads": { "artifact": { "path": "custom/authlib.jar" } } },
              { "name": "net.fabricmc:fabric-loader:0.16.5", "url": "https://maven.fabricmc.net/" }
            ]
            """);

        var selected = LibrarySelector.Select(libraries, Windows);

        CollectionAssert.AreEqual(
            new[] { "custom/authlib.jar", "net/fabricmc/fabric-loader/0.16.5/fabric-loader-0.16.5.jar" },
            selected.Select(library => library.RelativePath).ToArray());
        Assert.IsTrue(selected.All(library => library.ExtractExclude is null));
    }

    [TestMethod]
    public void FiltersLibrariesByRules()
    {
        var libraries = Parse("""
            [
              { "name": "a:everywhere:1" },
              { "name": "a:windows-only:1", "rules": [ { "action": "allow", "os": { "name": "windows" } } ] },
              { "name": "a:not-mac:1", "rules": [ { "action": "allow" }, { "action": "disallow", "os": { "name": "osx" } } ] },
              { "name": "a:mac-only:1", "rules": [ { "action": "allow", "os": { "name": "osx" } } ] }
            ]
            """);

        CollectionAssert.AreEqual(
            new[] { "a:everywhere", "a:windows-only", "a:not-mac" },
            Names(LibrarySelector.Select(libraries, Windows)));
        CollectionAssert.AreEqual(
            new[] { "a:everywhere", "a:not-mac" },
            Names(LibrarySelector.Select(libraries, Linux)));
        CollectionAssert.AreEqual(
            new[] { "a:everywhere", "a:mac-only" },
            Names(LibrarySelector.Select(libraries, Mac)));
    }

    [TestMethod]
    public void LaterVersionReplacesEarlierOneInPlace()
    {
        var libraries = Parse("""
            [
              { "name": "com.google.guava:guava:31.0-jre" },
              { "name": "org.lwjgl:lwjgl:3.3.1" },
              { "name": "com.google.guava:guava:32.1.2-jre" }
            ]
            """);

        var selected = LibrarySelector.Select(libraries, Windows);

        CollectionAssert.AreEqual(
            new[]
            {
                "com/google/guava/guava/32.1.2-jre/guava-32.1.2-jre.jar",
                "org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1.jar"
            },
            selected.Select(library => library.RelativePath).ToArray());
    }

    [TestMethod]
    public void IdenticalLibraryListedTwiceAppearsOnce()
    {
        var libraries = Parse("""
            [
              { "name": "net.minecraftforge:JarJarFileSystems:0.3.26" },
              { "name": "a:b:1" },
              { "name": "net.minecraftforge:JarJarFileSystems:0.3.26" }
            ]
            """);

        Assert.HasCount(2, LibrarySelector.Select(libraries, Windows));
    }

    [TestMethod]
    public void ClassifierKeepsLibrariesApart()
    {
        var libraries = Parse("""
            [
              { "name": "org.lwjgl:lwjgl:3.3.3" },
              { "name": "org.lwjgl:lwjgl:3.3.3:natives-windows" },
              { "name": "org.lwjgl:lwjgl:3.3.3:natives-windows-arm64" },
              { "name": "net.minecraftforge:forge:1.21.1-52.1.20:universal" },
              { "name": "net.minecraftforge:forge:1.21.1-52.1.20:client" }
            ]
            """);

        Assert.HasCount(5, LibrarySelector.Select(libraries, Windows));
    }

    [TestMethod]
    public void NewStyleNativesStayOnTheClasspath()
    {
        var libraries = Parse("""
            [
              {
                "name": "org.lwjgl:lwjgl:3.3.3:natives-windows",
                "rules": [ { "action": "allow", "os": { "name": "windows" } } ],
                "downloads": { "artifact": { "path": "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-windows.jar" } }
              }
            ]
            """);

        var selected = LibrarySelector.Select(libraries, Windows);

        Assert.HasCount(1, selected);
        Assert.IsNull(selected[0].ExtractExclude);
    }

    [TestMethod]
    public void LegacyNativesAreExtractedAndNotOnTheClasspath()
    {
        var libraries = Parse(LegacyNativesJson);

        var selected = LibrarySelector.Select(libraries, Windows);

        Assert.HasCount(1, selected);
        Assert.AreEqual(
            "org/lwjgl/lwjgl/lwjgl-platform/2.9.4-nightly-20150209/lwjgl-platform-2.9.4-nightly-20150209-natives-windows.jar",
            selected[0].RelativePath);
        CollectionAssert.AreEqual(new[] { "META-INF/", "docs/" }, selected[0].ExtractExclude!.ToArray());
    }

    [TestMethod]
    public void LegacyNativesWithoutDeclaredPathAreDerivedFromTheName()
    {
        var libraries = Parse(LegacyNativesJson);

        var selected = LibrarySelector.Select(libraries, Linux);

        Assert.HasCount(1, selected);
        Assert.AreEqual(
            "org/lwjgl/lwjgl/lwjgl-platform/2.9.4-nightly-20150209/lwjgl-platform-2.9.4-nightly-20150209-natives-linux.jar",
            selected[0].RelativePath);
    }

    [TestMethod]
    public void LegacyNativesWithoutEntryForTheSystemAreSkipped()
    {
        var libraries = Parse(LegacyNativesJson);

        Assert.HasCount(0, LibrarySelector.Select(libraries, Mac));
    }

    [TestMethod]
    public void ArchPlaceholderIsReplacedWithBitness()
    {
        var libraries = Parse("""
            [
              {
                "name": "tv.twitch:twitch-platform:5.16",
                "natives": { "windows": "natives-windows-${arch}" }
              }
            ]
            """);

        Assert.AreEqual(
            "tv/twitch/twitch-platform/5.16/twitch-platform-5.16-natives-windows-64.jar",
            LibrarySelector.Select(libraries, Windows)[0].RelativePath);
        Assert.AreEqual(
            "tv/twitch/twitch-platform/5.16/twitch-platform-5.16-natives-windows-32.jar",
            LibrarySelector.Select(libraries, Windows32)[0].RelativePath);
    }

    [TestMethod]
    public void RuleFilteredLibraryDoesNotReplaceAnAllowedOne()
    {
        var libraries = Parse("""
            [
              { "name": "a:b:1" },
              { "name": "a:b:2", "rules": [ { "action": "allow", "os": { "name": "osx" } } ] }
            ]
            """);

        var selected = LibrarySelector.Select(libraries, Windows);

        Assert.HasCount(1, selected);
        Assert.AreEqual("a/b/1/b-1.jar", selected[0].RelativePath);
    }

    private const string LegacyNativesJson = """
        [
          {
            "name": "org.lwjgl.lwjgl:lwjgl-platform:2.9.4-nightly-20150209",
            "natives": { "linux": "natives-linux", "windows": "natives-windows" },
            "extract": { "exclude": [ "META-INF/", "docs/" ] },
            "downloads": {
              "classifiers": {
                "natives-windows": { "path": "org/lwjgl/lwjgl/lwjgl-platform/2.9.4-nightly-20150209/lwjgl-platform-2.9.4-nightly-20150209-natives-windows.jar" }
              }
            }
          }
        ]
        """;

    private static IReadOnlyList<VersionLibrary> Parse(string librariesJson)
    {
        return VersionFile.Parse($$"""{ "libraries": {{librariesJson}} }""").Libraries;
    }

    private static string[] Names(IReadOnlyList<SelectedLibrary> selected)
    {
        // 测试库的路径都形如 a/<名称>/<版本>/...，取 group:artifact 便于断言
        return selected.Select(library =>
        {
            var parts = library.RelativePath.Split('/');
            return $"{parts[0]}:{parts[1]}";
        }).ToArray();
    }

    private static RuleEnvironment CreateEnvironment(string osName, string arch)
    {
        return new RuleEnvironment(osName, arch, "10.0", new HashSet<string>());
    }
}
