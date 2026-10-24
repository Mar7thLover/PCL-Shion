using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class RuleEnvironmentTest
{
    [TestMethod]
    public void CurrentPlatformReportsRuleNamesAndAJavaStyleVersion()
    {
        var environment = RuleEnvironment.ForCurrentPlatform(new HashSet<string> { "has_custom_resolution" });

        Assert.AreEqual(PlatformInfo.OsName, environment.OsName);
        Assert.AreEqual(PlatformInfo.Arch, environment.Arch);
        Assert.IsTrue(Regex.IsMatch(environment.OsVersion, @"^\d+\.\d+$"), environment.OsVersion);
        CollectionAssert.AreEqual(new[] { "has_custom_resolution" }, environment.Features.ToArray());
    }
}
