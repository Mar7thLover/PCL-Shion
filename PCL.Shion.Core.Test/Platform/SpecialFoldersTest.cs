using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class SpecialFoldersTest
{
    [TestMethod]
    [DataRow(Environment.SpecialFolder.ApplicationData)]
    [DataRow(Environment.SpecialFolder.LocalApplicationData)]
    [DataRow(Environment.SpecialFolder.UserProfile)]
    public void ReturnsFullyQualifiedPath(Environment.SpecialFolder folder)
    {
        Assert.IsTrue(Path.IsPathFullyQualified(SpecialFolders.Get(folder)));
    }

    [TestMethod]
    [DataRow(Environment.SpecialFolder.ApplicationData)]
    [DataRow(Environment.SpecialFolder.LocalApplicationData)]
    [DataRow(Environment.SpecialFolder.UserProfile)]
    public void MissingDirectoryStillResolvesWithoutCreatingIt(Environment.SpecialFolder folder)
    {
        var home = Path.Combine(Path.GetTempPath(), "PCL.Shion.Test." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        try
        {
            var expected = Path.Combine(home, "not-created", folder.ToString());
            var actual = SpecialFolders.Get(folder, (requested, option) =>
            {
                Assert.AreEqual(folder, requested);
                Assert.AreEqual(Environment.SpecialFolderOption.DoNotVerify, option,
                    "Missing directories must be resolved without verifying or creating them.");
                return expected;
            });

            Assert.AreEqual(expected, actual);
            Assert.IsTrue(Path.IsPathFullyQualified(actual));
            Assert.IsFalse(Directory.Exists(Path.Combine(home, "not-created")));
        }
        finally
        {
            Directory.Delete(home, true);
        }
    }

    [TestMethod]
    [DataRow("relative-home")]
    [DataRow("relative-home/Library/Application Support")]
    [DataRow(".")]
    [DataRow("")]
    public void RelativeOrMissingSystemPathFallsBackToBaseDirectory(string path)
    {
        var actual = SpecialFolders.Get(Environment.SpecialFolder.ApplicationData, (_, _) => path);

        Assert.AreEqual(AppContext.BaseDirectory, actual);
        Assert.IsTrue(Path.IsPathFullyQualified(actual));
    }
}
