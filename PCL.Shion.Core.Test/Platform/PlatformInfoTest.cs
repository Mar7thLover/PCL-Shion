using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class PlatformInfoTest
{
    [TestMethod]
    public void OsNameFollowsOs()
    {
        var expected = PlatformInfo.Os switch
        {
            OsKind.Windows => "windows",
            OsKind.MacOs => "osx",
            _ => "linux"
        };

        Assert.AreEqual(expected, PlatformInfo.OsName);
    }

    [TestMethod]
    public void ArchUsesRuleNames()
    {
        CollectionAssert.Contains(new[] { "x86", "x86_64", "arm64" }, PlatformInfo.Arch);
    }

    [TestMethod]
    public void ClasspathSeparatorFollowsOs()
    {
        var expected = PlatformInfo.Os == OsKind.Windows ? ";" : ":";

        Assert.AreEqual(expected, PlatformInfo.ClasspathSeparator);
    }
}
