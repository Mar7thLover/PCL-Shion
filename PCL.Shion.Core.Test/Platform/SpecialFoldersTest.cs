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
    public void MissingConfigDirectoryStillResolvesUnderHome()
    {
        if (OperatingSystem.IsWindows()) Assert.Inconclusive("HOME 只在类 Unix 系统上决定数据目录。");

        var home = Path.Combine(Path.GetTempPath(), "PCL.Shion.Test." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        var originalConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var originalData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", home);
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", null);

            var expectedAppData = OperatingSystem.IsMacOS()
                ? Path.Combine(home, "Library", "Application Support")
                : Path.Combine(home, ".config");
            Assert.AreEqual(expectedAppData, SpecialFolders.Get(Environment.SpecialFolder.ApplicationData));
            Assert.IsFalse(Directory.Exists(expectedAppData));
            Assert.StartsWith(home, SpecialFolders.Get(Environment.SpecialFolder.LocalApplicationData));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalConfig);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", originalData);
            Directory.Delete(home, true);
        }
    }

    [TestMethod]
    public void RelativeHomeFallsBackToBaseDirectory()
    {
        if (OperatingSystem.IsWindows()) Assert.Inconclusive("HOME 只在类 Unix 系统上决定数据目录。");

        var originalHome = Environment.GetEnvironmentVariable("HOME");
        var originalConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", "relative-home");
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);

            Assert.AreEqual(AppContext.BaseDirectory, SpecialFolders.Get(Environment.SpecialFolder.ApplicationData));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalConfig);
        }
    }
}
