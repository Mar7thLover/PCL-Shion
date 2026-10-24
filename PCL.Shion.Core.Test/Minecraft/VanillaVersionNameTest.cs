using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class VanillaVersionNameTest
{
    [TestMethod]
    [DataRow("1.21.1", "1.21.1")]
    [DataRow("1.21", "1.21")]
    [DataRow("26.3", "26.3")]
    [DataRow("26.3.1", "26.3.1")]
    [DataRow("1.21-pre1", "1.21-pre1")]
    [DataRow("1.21.1-rc2", "1.21.1-rc2")]
    [DataRow("26.4-snapshot-1", "26.4-snapshot-1")]
    [DataRow("24w14a", "24w14a")]
    [DataRow("b1.7.3", "b1.7.3")]
    [DataRow("a1.2.6", "a1.2.6")]
    [DataRow("c0.30_01c", "c0.30_01c")]
    [DataRow("rd-132211", "rd-132211")]
    [DataRow("inf-20100618", "inf-20100618")]
    public void RecognizesVanillaVersions(string id, string expected)
    {
        Assert.AreEqual(expected, VanillaVersionName.TryExtract(id));
    }

    [TestMethod]
    [DataRow("1.21.1-Forge_52.1.20", "1.21.1")]
    [DataRow("1.20.1-Fabric 0.15.7", "1.20.1")]
    [DataRow("1.16.5-OptiFine_HD_U_G8", "1.16.5")]
    [DataRow("1.21-pre1-Fabric", "1.21-pre1")]
    [DataRow("24w14a_test", "24w14a")]
    public void TakesTheVersionAtTheStartOfModdedIds(string id, string expected)
    {
        Assert.AreEqual(expected, VanillaVersionName.TryExtract(id));
    }

    [TestMethod]
    [DataRow("Survival")]
    [DataRow("fabric-loader-0.15.7-1.20.1")]
    [DataRow("my1.21.1")]
    [DataRow("1")]
    [DataRow("1.21.1Forge")]
    [DataRow("")]
    public void RejectsIdsWithoutALeadingVersion(string id)
    {
        Assert.IsNull(VanillaVersionName.TryExtract(id));
    }
}
