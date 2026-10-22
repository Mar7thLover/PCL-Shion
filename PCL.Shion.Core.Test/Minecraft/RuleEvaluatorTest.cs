using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class RuleEvaluatorTest
{
    private static readonly RuleEnvironment Windows = CreateEnvironment("windows", "x86_64", "10.0");
    private static readonly RuleEnvironment Mac = CreateEnvironment("osx", "arm64", "14.5");

    [TestMethod]
    public void NoRulesAllow()
    {
        Assert.IsTrue(RuleEvaluator.IsAllowed([], Windows));
    }

    [TestMethod]
    public void AllowRuleMatchesByOsName()
    {
        VersionRule[] rules = [Rule(true, osName: "windows")];

        Assert.IsTrue(RuleEvaluator.IsAllowed(rules, Windows));
        Assert.IsFalse(RuleEvaluator.IsAllowed(rules, Mac));
    }

    [TestMethod]
    public void RuleWithoutConditionsAppliesEverywhere()
    {
        VersionRule[] rules = [Rule(true)];

        Assert.IsTrue(RuleEvaluator.IsAllowed(rules, Windows));
        Assert.IsTrue(RuleEvaluator.IsAllowed(rules, Mac));
    }

    [TestMethod]
    public void LaterDisallowOverridesEarlierAllow()
    {
        VersionRule[] rules = [Rule(true), Rule(false, osName: "osx")];

        Assert.IsTrue(RuleEvaluator.IsAllowed(rules, Windows));
        Assert.IsFalse(RuleEvaluator.IsAllowed(rules, Mac));
    }

    [TestMethod]
    public void LaterAllowOverridesEarlierDisallow()
    {
        VersionRule[] rules = [Rule(false), Rule(true, osName: "windows")];

        Assert.IsTrue(RuleEvaluator.IsAllowed(rules, Windows));
        Assert.IsFalse(RuleEvaluator.IsAllowed(rules, Mac));
    }

    [TestMethod]
    public void NoMatchingRuleDenies()
    {
        VersionRule[] rules = [Rule(true, osName: "linux")];

        Assert.IsFalse(RuleEvaluator.IsAllowed(rules, Windows));
    }

    [TestMethod]
    public void ArchMustMatchTheWholeName()
    {
        VersionRule[] x86 = [Rule(true, osArch: "x86")];
        VersionRule[] x64 = [Rule(true, osArch: "x86_64")];

        Assert.IsFalse(RuleEvaluator.IsAllowed(x86, Windows));
        Assert.IsTrue(RuleEvaluator.IsAllowed(x86, CreateEnvironment("windows", "x86", "10.0")));
        Assert.IsTrue(RuleEvaluator.IsAllowed(x64, Windows));
    }

    [TestMethod]
    public void ArchAcceptsAlternation()
    {
        VersionRule[] rules = [Rule(true, osArch: "x86_64|arm64")];

        Assert.IsTrue(RuleEvaluator.IsAllowed(rules, Windows));
        Assert.IsTrue(RuleEvaluator.IsAllowed(rules, Mac));
        Assert.IsFalse(RuleEvaluator.IsAllowed(rules, CreateEnvironment("linux", "x86", "6.8")));
    }

    [TestMethod]
    public void OsVersionIsASearchedRegex()
    {
        VersionRule[] anchored = [Rule(true, osName: "windows", osVersion: @"^10\.")];
        VersionRule[] exact = [Rule(true, osName: "osx", osVersion: @"^10\.5\.\d$")];

        Assert.IsTrue(RuleEvaluator.IsAllowed(anchored, Windows));
        Assert.IsFalse(RuleEvaluator.IsAllowed(anchored, CreateEnvironment("windows", "x86_64", "6.1")));
        Assert.IsFalse(RuleEvaluator.IsAllowed(exact, Mac));
        Assert.IsTrue(RuleEvaluator.IsAllowed(exact, CreateEnvironment("osx", "x86_64", "10.5.8")));
    }

    [TestMethod]
    public void FeatureMustBeEnabledWhenExpectedTrue()
    {
        VersionRule[] rules = [Rule(true, features: new Dictionary<string, bool> { ["has_custom_resolution"] = true })];

        Assert.IsFalse(RuleEvaluator.IsAllowed(rules, Windows));
        Assert.IsTrue(RuleEvaluator.IsAllowed(rules, CreateEnvironment("windows", "x86_64", "10.0", "has_custom_resolution")));
    }

    [TestMethod]
    public void FeatureMustBeDisabledWhenExpectedFalse()
    {
        VersionRule[] rules = [Rule(true, features: new Dictionary<string, bool> { ["is_demo_user"] = false })];

        Assert.IsTrue(RuleEvaluator.IsAllowed(rules, Windows));
        Assert.IsFalse(RuleEvaluator.IsAllowed(rules, CreateEnvironment("windows", "x86_64", "10.0", "is_demo_user")));
    }

    [TestMethod]
    public void AllConditionsOfOneRuleMustHold()
    {
        VersionRule[] rules =
        [
            Rule(true, osName: "windows", features: new Dictionary<string, bool> { ["has_custom_resolution"] = true })
        ];

        Assert.IsFalse(RuleEvaluator.IsAllowed(rules, Windows));
        Assert.IsFalse(RuleEvaluator.IsAllowed(rules, CreateEnvironment("osx", "arm64", "14.5", "has_custom_resolution")));
        Assert.IsTrue(RuleEvaluator.IsAllowed(rules, CreateEnvironment("windows", "x86_64", "10.0", "has_custom_resolution")));
    }

    private static VersionRule Rule(
        bool allow,
        string? osName = null,
        string? osArch = null,
        string? osVersion = null,
        Dictionary<string, bool>? features = null)
    {
        return new VersionRule(allow, osName, osArch, osVersion, features ?? new Dictionary<string, bool>());
    }

    private static RuleEnvironment CreateEnvironment(string osName, string arch, string osVersion, params string[] features)
    {
        return new RuleEnvironment(osName, arch, osVersion, new HashSet<string>(features));
    }
}
