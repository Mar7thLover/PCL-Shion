using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Mods;

namespace PCL.Shion.Core.Test.Mods;

[TestClass]
public sealed class ModMetadataParserTest
{
    private const string FabricJson = """
        {
          "schemaVersion": 1,
          "id": "examplemod", // 注释会被忽略
          "version": "1.2.3",
          "name": "Example Mod",
          "depends": {
            "fabricloader": ">=0.14.0",
            "minecraft": ["1.20", "1.20.1"],
            "fabric-api": "*"
          },
          "recommends": { "modmenu": "*", },
          "suggests": { "other": "*" },
          "provides": ["example-alias"]
        }
        """;

    private const string QuiltJson = """
        {
          "schema_version": 1,
          "quilt_loader": {
            "group": "com.example",
            "id": "quiltmod",
            "version": "2.0.0",
            "metadata": { "name": "Quilt Mod" },
            "provides": ["quilt-alias", { "id": "other-alias", "version": "1.0" }],
            "depends": [
              "quilt_base",
              { "id": "minecraft", "versions": ">=1.20" },
              { "id": "extra", "versions": { "all": [">=1", "<3"] }, "optional": true },
              { "id": "either", "versions": ["1.x", "2.x"] },
              [ { "id": "alt1" }, { "id": "alt2" } ]
            ]
          }
        }
        """;

    private const string ForgeToml = """
        modLoader="javafml"
        loaderVersion="[47,)"
        license="MIT"

        [[mods]]
        modId="examplemod"
        version="1.0.0"
        displayName="Example Mod"
        description='''
        An example mod.
        '''

        [[mods]]
        modId="examplemod_api"

        [[dependencies.examplemod]]
            modId="forge"
            mandatory=true
            versionRange="[47,)"
            side="BOTH"

        [[dependencies.examplemod]]
            modId="jei"
            mandatory=false
            versionRange=""

        [[dependencies.examplemod]]
            modId="servermod"
            mandatory=true
            side="SERVER"

        [[dependencies.examplemod_api]]
            modId="minecraft"
            mandatory=true
            versionRange="[1.20.1,1.21)"
        """;

    private const string NeoForgeToml = """
        modLoader="javafml"
        loaderVersion="[1,)"

        [[mods]]
        modId="neomod"
        version="3.0"
        displayName="Neo Mod"

        [[dependencies.neomod]]
            modId="neoforge"
            type="required"
            versionRange="[21.0,)"

        [[dependencies.neomod]]
            modId="lib"
            type="optional"

        [[dependencies.neomod]]
            modId="bad"
            type="incompatible"

        [[dependencies.neomod]]
            modId="meh"
            type="discouraged"
        """;

    [TestMethod]
    public void ParseFabric_ReadsFields()
    {
        var metadata = ModMetadataParser.ParseFabric(FabricJson);

        Assert.AreEqual("examplemod", metadata.Id);
        Assert.AreEqual("Example Mod", metadata.Name);
        Assert.AreEqual("1.2.3", metadata.Version);
        CollectionAssert.AreEqual(new[] { "fabric" }, metadata.Loaders.ToArray());
        CollectionAssert.AreEqual(new[] { "example-alias" }, metadata.Provides.ToArray());
    }

    [TestMethod]
    public void ParseFabric_ReadsDependenciesAndTheirRequirement()
    {
        var metadata = ModMetadataParser.ParseFabric(FabricJson);

        ModDependency[] expected =
        [
            new("fabricloader", ">=0.14.0", true),
            new("minecraft", "1.20 || 1.20.1", true),
            new("fabric-api", "*", true),
            new("modmenu", "*", false)
        ];
        CollectionAssert.AreEqual(expected, metadata.Dependencies.ToArray());
    }

    [TestMethod]
    public void ParseFabric_ToleratesMissingFields()
    {
        var metadata = ModMetadataParser.ParseFabric("""{ "id": "bare" }""");

        Assert.AreEqual("bare", metadata.Id);
        Assert.IsNull(metadata.Name);
        Assert.IsNull(metadata.Version);
        Assert.IsEmpty(metadata.Dependencies);
        Assert.IsEmpty(metadata.Provides);
    }

    [TestMethod]
    public void ParseQuilt_ReadsFields()
    {
        var metadata = ModMetadataParser.ParseQuilt(QuiltJson);

        Assert.AreEqual("quiltmod", metadata.Id);
        Assert.AreEqual("Quilt Mod", metadata.Name);
        Assert.AreEqual("2.0.0", metadata.Version);
        CollectionAssert.AreEqual(new[] { "quilt" }, metadata.Loaders.ToArray());
        CollectionAssert.AreEqual(new[] { "quilt-alias", "other-alias" }, metadata.Provides.ToArray());
    }

    [TestMethod]
    public void ParseQuilt_ReadsStringAndObjectDependencies()
    {
        var metadata = ModMetadataParser.ParseQuilt(QuiltJson);

        // 数组形式的“任选其一”依赖无法判断缺失与否，不收录
        ModDependency[] expected =
        [
            new("quilt_base", null, true),
            new("minecraft", ">=1.20", true),
            new("extra", ">=1 <3", false),
            new("either", "1.x || 2.x", true)
        ];
        CollectionAssert.AreEqual(expected, metadata.Dependencies.ToArray());
    }

    [TestMethod]
    public void ParseQuilt_WithoutLoaderSectionHasNoInformation()
    {
        var metadata = ModMetadataParser.ParseQuilt("{}");

        Assert.IsNull(metadata.Id);
        CollectionAssert.AreEqual(new[] { "quilt" }, metadata.Loaders.ToArray());
    }

    [TestMethod]
    public void ParseForge_ReadsMainModAndProvidedMods()
    {
        var metadata = ModMetadataParser.ParseForge(ForgeToml, false);

        Assert.AreEqual("examplemod", metadata.Id);
        Assert.AreEqual("Example Mod", metadata.Name);
        Assert.AreEqual("1.0.0", metadata.Version);
        CollectionAssert.AreEqual(new[] { "forge" }, metadata.Loaders.ToArray());
        CollectionAssert.AreEqual(new[] { "examplemod_api" }, metadata.Provides.ToArray());
    }

    [TestMethod]
    public void ParseForge_ReadsDependenciesOfEveryMod()
    {
        var metadata = ModMetadataParser.ParseForge(ForgeToml, false);

        // 仅服务端的依赖不收录；空的 versionRange 视为未限定
        ModDependency[] expected =
        [
            new("forge", "[47,)", true),
            new("jei", null, false),
            new("minecraft", "[1.20.1,1.21)", true)
        ];
        CollectionAssert.AreEqual(expected, metadata.Dependencies.ToArray());
    }

    [TestMethod]
    public void ParseForge_ReadsTypedDependenciesOfNeoForge()
    {
        var metadata = ModMetadataParser.ParseForge(NeoForgeToml, true);

        Assert.AreEqual("neomod", metadata.Id);
        CollectionAssert.AreEqual(new[] { "neoforge" }, metadata.Loaders.ToArray());
        ModDependency[] expected =
        [
            new("neoforge", "[21.0,)", true),
            new("lib", null, false)
        ];
        CollectionAssert.AreEqual(expected, metadata.Dependencies.ToArray());
    }

    [TestMethod]
    public void ParseForge_TreatsModsTomlThatOnlyDependsOnNeoForgeAsNeoForge()
    {
        var metadata = ModMetadataParser.ParseForge(NeoForgeToml, false);

        CollectionAssert.AreEqual(new[] { "neoforge" }, metadata.Loaders.ToArray());
    }

    [TestMethod]
    public void ParseForge_TreatsModsTomlThatDependsOnBothAsForge()
    {
        var toml = """
            [[mods]]
            modId="both"

            [[dependencies.both]]
                modId="forge"
                mandatory=true

            [[dependencies.both]]
                modId="neoforge"
                mandatory=true
            """;

        CollectionAssert.AreEqual(new[] { "forge" }, ModMetadataParser.ParseForge(toml, false).Loaders.ToArray());
    }

    [TestMethod]
    public void ParseForge_ToleratesMissingModsTable()
    {
        var metadata = ModMetadataParser.ParseForge("modLoader=\"javafml\"", false);

        Assert.IsNull(metadata.Id);
        Assert.IsEmpty(metadata.Dependencies);
        Assert.IsEmpty(metadata.Provides);
    }

    [TestMethod]
    public void ParseLegacy_ReadsArrayForm()
    {
        var metadata = ModMetadataParser.ParseLegacy("""
            [
              {
                "modid": "oldmod",
                "name": "Old Mod",
                "version": "1.0",
                "requiredMods": ["Forge@[14.23,)", "jei"],
                "dependencies": ["ignored"],
              },
              { "modid": "oldmod_extra" }
            ]
            """);

        Assert.AreEqual("oldmod", metadata.Id);
        Assert.AreEqual("Old Mod", metadata.Name);
        Assert.AreEqual("1.0", metadata.Version);
        CollectionAssert.AreEqual(new[] { "forge" }, metadata.Loaders.ToArray());
        CollectionAssert.AreEqual(new[] { "oldmod_extra" }, metadata.Provides.ToArray());
        ModDependency[] expected = [new("Forge", "[14.23,)", true), new("jei", null, true)];
        CollectionAssert.AreEqual(expected, metadata.Dependencies.ToArray());
    }

    [TestMethod]
    public void ParseLegacy_ReadsModListForm()
    {
        var metadata = ModMetadataParser.ParseLegacy("""
            { "modListVersion": 2, "modList": [ { "modid": "listed", "name": "Listed", "version": "2" } ] }
            """);

        Assert.AreEqual("listed", metadata.Id);
        Assert.AreEqual("Listed", metadata.Name);
        Assert.AreEqual("2", metadata.Version);
    }

    [TestMethod]
    public void ParseLegacy_ToleratesEmptyList()
    {
        Assert.IsNull(ModMetadataParser.ParseLegacy("[]").Id);
        Assert.IsNull(ModMetadataParser.ParseLegacy("{}").Id);
    }

    [TestMethod]
    public void ParseJson_ThrowsForInvalidJson()
    {
        Assert.Throws<JsonException>(() => ModMetadataParser.ParseFabric("{ not json"));
        Assert.Throws<JsonException>(() => ModMetadataParser.ParseQuilt(string.Empty));
        Assert.Throws<JsonException>(() => ModMetadataParser.ParseLegacy("[ { \"modid\": \"broken\n\" } ]"));
    }

    [TestMethod]
    public void ParseForge_ThrowsForInvalidToml()
    {
        Assert.ThrowsExactly<FormatException>(() => ModMetadataParser.ParseForge("[[mods]\nmodId=\"x\"", false));
    }
}
