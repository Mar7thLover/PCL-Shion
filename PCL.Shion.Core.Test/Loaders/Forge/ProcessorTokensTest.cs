using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Loaders.Forge;

namespace PCL.Shion.Core.Test.Loaders.Forge;

[TestClass]
public sealed class ProcessorTokensTest
{
    private static readonly string Libraries = Path.Combine(Path.GetTempPath(), "pcl-shion-libraries");

    private static readonly Dictionary<string, string> Variables = new()
    {
        ["ROOT"] = "/games/.minecraft",
        ["SIDE"] = "client",
        ["PATCHED_SHA"] = "07678ff3"
    };

    [TestMethod]
    public void Replace_SubstitutesVariablesInsideText()
    {
        Assert.AreEqual("/games/.minecraft/libraries/", ProcessorTokens.Replace("{ROOT}/libraries/", Variables));
        Assert.AreEqual("--side=client", ProcessorTokens.Replace("--side={SIDE}", Variables));
    }

    [TestMethod]
    public void Replace_KeepsQuotedTextLiterally()
    {
        Assert.AreEqual("{ROOT}", ProcessorTokens.Replace("'{ROOT}'", Variables));
    }

    [TestMethod]
    public void Replace_HonoursEscapes()
    {
        Assert.AreEqual("{ROOT}", ProcessorTokens.Replace(@"\{ROOT\}", Variables));
        Assert.AreEqual("it's client", ProcessorTokens.Replace(@"it\'s {SIDE}", Variables));
        Assert.AreEqual("a'b", ProcessorTokens.Replace(@"'a\'b'", Variables));
    }

    [TestMethod]
    public void Replace_LeavesPlainTextAlone()
    {
        Assert.AreEqual("--task", ProcessorTokens.Replace("--task", Variables));
    }

    [TestMethod]
    [DataRow("{MISSING}")]
    [DataRow("{ROOT")]
    [DataRow("'open")]
    [DataRow(@"trailing\")]
    public void Replace_RejectsInvalidTemplates(string template)
    {
        Assert.ThrowsExactly<InvalidDataException>(() => ProcessorTokens.Replace(template, Variables));
    }

    [TestMethod]
    public void ResolveArgument_TurnsMavenCoordinatesIntoLibraryPaths()
    {
        var path = ProcessorTokens.ResolveArgument("[de.oceanlabs.mcp:mcp_config:1.20.1-20230612.114412@zip]", Libraries, Variables);

        var expected = Path.Combine(
            Libraries, "de", "oceanlabs", "mcp", "mcp_config", "1.20.1-20230612.114412", "mcp_config-1.20.1-20230612.114412.zip");
        Assert.AreEqual(expected, path);
    }

    [TestMethod]
    public void ResolveArgument_ReplacesVariablesInOtherArguments()
    {
        Assert.AreEqual("client", ProcessorTokens.ResolveArgument("{SIDE}", Libraries, Variables));
    }

    [TestMethod]
    public void CreateVariables_ResolvesLibrariesLiteralsAndInstallerFiles()
    {
        var data = new Dictionary<string, string>
        {
            ["MOJMAPS"] = "[net.minecraft:client:1.20.1-20230612.114412:mappings@txt]",
            ["PATCHED_SHA"] = "'07678ff3edd6990a75bf0707ad2dbb2315b8000f'",
            ["BINPATCH"] = "/data/client.lzma"
        };
        var extracted = new List<string>();

        var variables = ProcessorTokens.CreateVariables(data, Libraries, entry =>
        {
            extracted.Add(entry);
            return "/temp" + entry;
        });

        var mappings = Path.Combine(
            Libraries, "net", "minecraft", "client", "1.20.1-20230612.114412", "client-1.20.1-20230612.114412-mappings.txt");
        Assert.AreEqual(mappings, variables["MOJMAPS"]);
        Assert.AreEqual("07678ff3edd6990a75bf0707ad2dbb2315b8000f", variables["PATCHED_SHA"]);
        Assert.AreEqual("/temp/data/client.lzma", variables["BINPATCH"]);
        CollectionAssert.AreEqual(new[] { "/data/client.lzma" }, extracted);
    }

    [TestMethod]
    public void Prepare_ResolvesTheJarClasspathArgumentsAndOutputs()
    {
        var processor = new InstallProcessor(
            "net.minecraftforge:binarypatcher:1.1.1",
            ["net.sf.jopt-simple:jopt-simple:5.0.4"],
            ["--clean", "[net.minecraft:client:1.20.1:srg]", "--root", "{ROOT}"],
            new Dictionary<string, string> { ["{ROOT}/patched.jar"] = "{PATCHED_SHA}" },
            null);

        var prepared = processor.Prepare(Libraries, Variables);

        Assert.AreEqual(Path.Combine(Libraries, "net", "minecraftforge", "binarypatcher", "1.1.1", "binarypatcher-1.1.1.jar"), prepared.JarPath);
        CollectionAssert.AreEqual(
            new[] { Path.Combine(Libraries, "net", "sf", "jopt-simple", "jopt-simple", "5.0.4", "jopt-simple-5.0.4.jar") },
            prepared.Classpath.ToArray());
        CollectionAssert.AreEqual(
            new[] { "--clean", Path.Combine(Libraries, "net", "minecraft", "client", "1.20.1", "client-1.20.1-srg.jar"), "--root", "/games/.minecraft" },
            prepared.Arguments.ToArray());
        Assert.AreEqual(KeyValuePair.Create("/games/.minecraft/patched.jar", "07678ff3"), prepared.Outputs.Single());
        Assert.AreEqual("/games/.minecraft", prepared.GetOption("--root"));
        Assert.IsNull(prepared.GetOption("--missing"));
    }

    [TestMethod]
    public void RunsOnClient_FollowsTheSides()
    {
        Assert.IsTrue(CreateProcessor(null).RunsOnClient);
        Assert.IsTrue(CreateProcessor(["client"]).RunsOnClient);
        Assert.IsTrue(CreateProcessor(["client", "server"]).RunsOnClient);
        Assert.IsFalse(CreateProcessor(["server"]).RunsOnClient);
    }

    private static InstallProcessor CreateProcessor(IReadOnlyList<string>? sides)
    {
        return new InstallProcessor("group:tool:1.0", [], [], new Dictionary<string, string>(), sides);
    }
}
