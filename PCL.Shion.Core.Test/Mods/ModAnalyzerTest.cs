using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Mods;

namespace PCL.Shion.Core.Test.Mods;

[TestClass]
public sealed class ModAnalyzerTest
{
    private static LocalMod Mod(string? id, string fileName, params ModDependency[] dependencies) =>
        new($"mods/{fileName}", fileName, true, id, id, "1.0", ["fabric"], dependencies, "0000000000000000000000000000000000000000");

    private static ModDependency Required(string id) => new(id, null, true);

    [TestMethod]
    public void Analyze_ReturnsNothingForAHealthySet()
    {
        var issues = ModAnalyzer.Analyze(
        [
            Mod("a", "a.jar", Required("b"), Required("minecraft")),
            Mod("b", "b.jar")
        ]);

        Assert.IsEmpty(issues);
    }

    [TestMethod]
    public void Analyze_ReportsDuplicateIds()
    {
        var first = Mod("a", "a-1.0.jar");
        var second = Mod("a", "a-2.0.jar");
        var other = Mod("b", "b.jar");

        var issues = ModAnalyzer.Analyze([first, other, second]);

        var issue = issues.Single();
        Assert.AreEqual(ModIssueKind.Duplicate, issue.Kind);
        Assert.AreEqual("a", issue.ModId);
        Assert.IsNull(issue.MissingDependencyId);
        CollectionAssert.AreEqual(new[] { first, second }, issue.Mods.ToArray());
    }

    [TestMethod]
    public void Analyze_ReportsMissingRequiredDependency()
    {
        var needy = Mod("a", "a.jar", Required("lib"));

        var issue = ModAnalyzer.Analyze([needy, Mod("b", "b.jar")]).Single();

        Assert.AreEqual(ModIssueKind.MissingDependency, issue.Kind);
        Assert.AreEqual("a", issue.ModId);
        Assert.AreEqual("lib", issue.MissingDependencyId);
        CollectionAssert.AreEqual(new[] { needy }, issue.Mods.ToArray());
    }

    [TestMethod]
    public void Analyze_ReportsEveryMissingDependency()
    {
        var needy = Mod("a", "a.jar", Required("lib"), Required("api"));

        var issues = ModAnalyzer.Analyze([needy]);

        CollectionAssert.AreEqual(new[] { "lib", "api" }, issues.Select(issue => issue.MissingDependencyId).ToArray());
    }

    [TestMethod]
    public void Analyze_GroupsDuplicateFilesOfTheSameModUnderOneMissingDependency()
    {
        var first = Mod("a", "a-1.jar", Required("lib"));
        var second = Mod("a", "a-2.jar", Required("lib"));

        var issues = ModAnalyzer.Analyze([first, second]);

        Assert.HasCount(2, issues);
        Assert.AreEqual(ModIssueKind.Duplicate, issues[0].Kind);
        Assert.AreEqual(ModIssueKind.MissingDependency, issues[1].Kind);
        CollectionAssert.AreEqual(new[] { first, second }, issues[1].Mods.ToArray());
    }

    [TestMethod]
    public void Analyze_IgnoresOptionalDependencies()
    {
        var mod = Mod("a", "a.jar", new ModDependency("lib", null, false));

        Assert.IsEmpty(ModAnalyzer.Analyze([mod]));
    }

    [TestMethod]
    [DataRow("minecraft")]
    [DataRow("java")]
    [DataRow("fabricloader")]
    [DataRow("quilt_loader")]
    [DataRow("forge")]
    [DataRow("neoforge")]
    [DataRow("Forge")]
    public void Analyze_IgnoresBuiltInDependencies(string builtIn)
    {
        var mod = Mod("a", "a.jar", Required(builtIn));

        Assert.IsEmpty(ModAnalyzer.Analyze([mod]));
    }

    [TestMethod]
    public void Analyze_CountsProvidedIdsAsPresent()
    {
        var needy = Mod("a", "a.jar", Required("fabric"), Required("fabric-api-base"), Required("flywheel"));
        var api = Mod("fabric-api", "fabric-api.jar") with { Provides = ["fabric", "fabric-api-base", "flywheel"] };

        Assert.IsEmpty(ModAnalyzer.Analyze([needy, api]));
    }

    [TestMethod]
    public void Analyze_TreatsDisabledDependencyAsMissing()
    {
        var needy = Mod("a", "a.jar", Required("lib"));
        var lib = Mod("lib", "lib.jar.disabled") with { Enabled = false };

        var issue = ModAnalyzer.Analyze([needy, lib]).Single();

        Assert.AreEqual("lib", issue.MissingDependencyId);
    }

    [TestMethod]
    public void Analyze_SkipsDisabledMods()
    {
        var disabledDuplicate = Mod("a", "a-old.jar.disabled") with { Enabled = false };
        var disabledNeedy = Mod("b", "b.jar.disabled", Required("lib")) with { Enabled = false };

        var issues = ModAnalyzer.Analyze([Mod("a", "a.jar"), disabledDuplicate, disabledNeedy]);

        Assert.IsEmpty(issues);
    }

    [TestMethod]
    public void Analyze_SkipsModsWithoutId()
    {
        var issues = ModAnalyzer.Analyze(
        [
            Mod(null, "unknown-1.jar", Required("lib")),
            Mod(null, "unknown-2.jar")
        ]);

        Assert.IsEmpty(issues);
    }

    [TestMethod]
    public void Analyze_ListsDuplicatesBeforeMissingDependencies()
    {
        var issues = ModAnalyzer.Analyze(
        [
            Mod("a", "a.jar", Required("lib")),
            Mod("b", "b-1.jar"),
            Mod("b", "b-2.jar")
        ]);

        Assert.HasCount(2, issues);
        Assert.AreEqual(ModIssueKind.Duplicate, issues[0].Kind);
        Assert.AreEqual(ModIssueKind.MissingDependency, issues[1].Kind);
    }

    [TestMethod]
    public void Analyze_ReturnsNothingForNoMods()
    {
        Assert.IsEmpty(ModAnalyzer.Analyze([]));
    }
}
