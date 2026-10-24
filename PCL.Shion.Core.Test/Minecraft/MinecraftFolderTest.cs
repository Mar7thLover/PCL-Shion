using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class MinecraftFolderTest
{
    [TestMethod]
    public void DefaultFolderMatchesOfficialLauncherLocation()
    {
        var path = MinecraftFolder.GetDefault();

        Assert.IsTrue(Path.IsPathRooted(path));
        switch (PlatformInfo.Os)
        {
            case OsKind.Windows:
                Assert.AreEqual(".minecraft", Path.GetFileName(path));
                Assert.AreEqual(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Path.GetDirectoryName(path));
                break;
            case OsKind.MacOs:
                Assert.IsTrue(path.EndsWith(Path.Combine("Library", "Application Support", "minecraft")));
                break;
            default:
                Assert.AreEqual(".minecraft", Path.GetFileName(path));
                break;
        }
    }
}
