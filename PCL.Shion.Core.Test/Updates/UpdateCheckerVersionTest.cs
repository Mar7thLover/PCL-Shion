using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Updates;

namespace PCL.Shion.Core.Test.Updates;

[TestClass]
public sealed class UpdateCheckerVersionTest
{
    [TestMethod]
    [DataRow("v0.2.0", "0.2.0")]
    [DataRow("V0.2.0", "0.2.0")]
    [DataRow("0.2.0", "0.2.0")]
    [DataRow("0.2.0-beta.1", "0.2.0")]
    [DataRow("v0.2.0-rc.1", "0.2.0")]
    [DataRow("v0.2.0+build.5", "0.2.0")]
    [DataRow("v0.2.0-beta.1+build.5", "0.2.0")]
    [DataRow("v0.2.0-", "0.2.0")]
    [DataRow("v1.2", "1.2")]
    [DataRow("v1.2.3.4", "1.2.3.4")]
    [DataRow("v10.20.30", "10.20.30")]
    [DataRow(" v0.2.0 ", "0.2.0")]
    public void TryParseTag_ParsesVersion(string tag, string expected)
    {
        Assert.IsTrue(UpdateChecker.TryParseTag(tag, out var version));
        Assert.AreEqual(Version.Parse(expected), version);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("v")]
    [DataRow("latest")]
    [DataRow("nightly-2026-10-22")]
    [DataRow("release-1.0")]
    [DataRow("v-1.0")]
    [DataRow("1")]
    [DataRow("v1.x")]
    [DataRow("1.2.3.4.5")]
    public void TryParseTag_RejectsTagsThatAreNotVersions(string tag)
    {
        Assert.IsFalse(UpdateChecker.TryParseTag(tag, out _));
    }

    [TestMethod]
    [DataRow("0.3.0", "0.2.0", true)]
    [DataRow("0.2.1", "0.2.0", true)]
    [DataRow("0.10.0", "0.9.0", true)]
    [DataRow("1.0.0", "0.99.99", true)]
    [DataRow("0.2.1", "0.2.0.5", true)]
    [DataRow("0.2.0", "0.2.0", false)]
    [DataRow("0.1.9", "0.2.0", false)]
    public void IsNewer_ComparesVersions(string release, string current, bool expected)
    {
        Assert.AreEqual(expected, UpdateChecker.IsNewer(CreateRelease(release), Version.Parse(current)));
    }

    [TestMethod]
    [DataRow("0.2.0", "0.2.0.0")]
    [DataRow("0.2.0", "0.2")]
    [DataRow("0.2", "0.2.0")]
    public void IsNewer_TreatsMissingPartsAsZero(string release, string current)
    {
        Assert.IsFalse(UpdateChecker.IsNewer(CreateRelease(release), Version.Parse(current)));
    }

    [TestMethod]
    public void IsNewer_IgnoresPrereleaseFlag()
    {
        Assert.IsTrue(UpdateChecker.IsNewer(CreateRelease("0.3.0", prerelease: true), new Version(0, 2, 0)));
        Assert.IsFalse(UpdateChecker.IsNewer(CreateRelease("0.2.0", prerelease: true), new Version(0, 2, 0)));
    }

    private static ReleaseInfo CreateRelease(string version, bool prerelease = false)
    {
        var tag = $"v{version}";
        return new ReleaseInfo(
            tag,
            Version.Parse(version),
            tag,
            new Uri($"https://github.com/Mar7thLover/PCL-Shion/releases/tag/{tag}"),
            new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
            prerelease);
    }
}
