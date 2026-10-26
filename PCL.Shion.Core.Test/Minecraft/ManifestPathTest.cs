using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class ManifestPathTest
{
    [TestMethod]
    [DataRow("../outside")]
    [DataRow("..\\outside")]
    [DataRow("group/../../outside")]
    [DataRow("/absolute")]
    [DataRow("\\\\server\\share")]
    [DataRow("C:\\outside")]
    [DataRow("C:outside")]
    [DataRow("file.jar:stream")]
    [DataRow("group/.. /outside")]
    [DataRow("")]
    public void RejectsTraversalAndWindowsPathSyntaxOnEveryPlatform(string relativePath)
    {
        Assert.ThrowsExactly<InvalidDataException>(() => ManifestPath.Resolve(Path.GetTempPath(), relativePath));
    }

    [TestMethod]
    public void KeepsLegitimateMavenAndAssetNamesWithinTheRoot()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "shion-manifest-path"));
        var relative = "com/example/mod+extras/1.0-snapshot/mod@client.jar";

        Assert.AreEqual(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)), ManifestPath.Resolve(root, relative));
        Assert.AreEqual(Path.Combine(root, "目录", "资源.png"), ManifestPath.Resolve(root, "目录/资源.png"));
    }

    [TestMethod]
    public void FileNamesCannotSmuggleAParentPathThroughASuffix()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => ManifestPath.ResolveName(Path.GetTempPath(), "../outside", ".json"));
    }
}
