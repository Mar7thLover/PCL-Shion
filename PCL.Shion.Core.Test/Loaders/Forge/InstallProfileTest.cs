using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Loaders.Forge;

namespace PCL.Shion.Core.Test.Loaders.Forge;

[TestClass]
public sealed class InstallProfileTest
{
    private const string Profile = """
        {
          "spec": 1,
          "profile": "forge",
          "version": "1.20.1-forge-47.4.0",
          "minecraft": "1.20.1",
          "json": "/version.json",
          "data": {
            "BINPATCH": { "client": "/data/client.lzma", "server": "/data/server.lzma" },
            "MC_UNPACKED": { "server": "[net.minecraft:server:1.20.1:unpacked]" },
            "PATCHED_SHA": { "client": "'b268fd6f'", "server": "'00000000'" }
          },
          "processors": [
            {
              "sides": [ "server" ],
              "jar": "net.minecraftforge:installertools:1.4.1",
              "args": [ "--task", "BUNDLER_EXTRACT", "--input", "{MINECRAFT_JAR}" ]
            },
            {
              "jar": "net.minecraftforge:binarypatcher:1.1.1",
              "classpath": [ "net.sf.jopt-simple:jopt-simple:5.0.4" ],
              "args": [ "--clean", "{MC_SRG}", "--output", "{PATCHED}" ],
              "outputs": { "{PATCHED}": "{PATCHED_SHA}" }
            }
          ],
          "libraries": [
            {
              "name": "net.minecraftforge:binarypatcher:1.1.1",
              "downloads": {
                "artifact": {
                  "path": "net/minecraftforge/binarypatcher/1.1.1/binarypatcher-1.1.1.jar",
                  "url": "https://maven.minecraftforge.net/net/minecraftforge/binarypatcher/1.1.1/binarypatcher-1.1.1.jar",
                  "sha1": "23176c4f9fd7a8db961dc0b2ec32ad97ab08513c",
                  "size": 21892
                }
              }
            }
          ]
        }
        """;

    [TestMethod]
    public void Parse_ReadsTheVersionJsonEntryAndLibraries()
    {
        var profile = InstallProfile.Parse(Profile);

        Assert.AreEqual("1.20.1", profile.Minecraft);
        Assert.AreEqual("/version.json", profile.VersionJsonEntry);
        var library = profile.Libraries.Single();
        Assert.AreEqual("net.minecraftforge:binarypatcher:1.1.1", library.Name);
        Assert.AreEqual(21892L, library.Artifact!.Size);
    }

    [TestMethod]
    public void Parse_KeepsOnlyTheClientSideOfData()
    {
        var profile = InstallProfile.Parse(Profile);

        Assert.HasCount(2, profile.Data);
        Assert.AreEqual("/data/client.lzma", profile.Data["BINPATCH"]);
        Assert.AreEqual("'b268fd6f'", profile.Data["PATCHED_SHA"]);
    }

    [TestMethod]
    public void Parse_ReadsProcessorsInOrder()
    {
        var processors = InstallProfile.Parse(Profile).Processors;

        Assert.HasCount(2, processors);
        CollectionAssert.AreEqual(new[] { "server" }, processors[0].Sides!.ToArray());
        Assert.IsFalse(processors[0].RunsOnClient);

        var patcher = processors[1];
        Assert.IsNull(patcher.Sides);
        Assert.AreEqual("net.minecraftforge:binarypatcher:1.1.1", patcher.Jar);
        CollectionAssert.AreEqual(new[] { "net.sf.jopt-simple:jopt-simple:5.0.4" }, patcher.Classpath.ToArray());
        CollectionAssert.AreEqual(new[] { "--clean", "{MC_SRG}", "--output", "{PATCHED}" }, patcher.Arguments.ToArray());
        Assert.AreEqual("{PATCHED_SHA}", patcher.Outputs["{PATCHED}"]);
    }

    [TestMethod]
    public void Parse_RejectsAProfileWithoutAVersionJson()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => InstallProfile.Parse("""{ "minecraft": "1.20.1" }"""));
    }

    [TestMethod]
    public void TryParseLegacy_ReturnsNullForTheCurrentFormat()
    {
        Assert.IsNull(LegacyInstallProfile.TryParse(Profile));
    }

    [TestMethod]
    public void TryParseLegacy_ReadsTheUniversalJarAndVersionInfo()
    {
        const string json = """
            {
              "install": {
                "path": "net.minecraftforge:forge:1.7.10-10.13.4.1614-1.7.10",
                "filePath": "forge-1.7.10-10.13.4.1614-1.7.10-universal.jar",
                "minecraft": "1.7.10"
              },
              "versionInfo": { "id": "1.7.10-Forge10.13.4.1614-1.7.10", "inheritsFrom": "1.7.10", "libraries": [] }
            }
            """;

        var profile = LegacyInstallProfile.TryParse(json)!;

        Assert.AreEqual("forge-1.7.10-10.13.4.1614-1.7.10-universal.jar", profile.UniversalEntry);
        Assert.AreEqual("net.minecraftforge:forge:1.7.10-10.13.4.1614-1.7.10", profile.UniversalName);
        Assert.IsFalse(profile.StripMeta);
        Assert.AreEqual("1.7.10-Forge10.13.4.1614-1.7.10", profile.VersionInfo["id"]!.GetValue<string>());
    }

    [TestMethod]
    public void TryParseLegacy_ReadsStripMeta()
    {
        const string json = """
            {
              "install": { "path": "net.minecraftforge:minecraftforge:7.8.1.738", "filePath": "universal.jar", "stripMeta": true },
              "versionInfo": { "id": "1.5.2-Forge7.8.1.738", "libraries": [] }
            }
            """;

        Assert.IsTrue(LegacyInstallProfile.TryParse(json)!.StripMeta);
    }
}
