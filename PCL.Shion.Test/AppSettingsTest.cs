using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.IO;
using PCL.Shion.Services;

namespace PCL.Shion.Test;

[TestClass]
public sealed class AppSettingsTest
{
    private string _directory = null!;
    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [TestInitialize]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), "shion-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void NullCollectionsInvalidEnumsAndLimitsCannotBreakStartup()
    {
        File.WriteAllText(SettingsPath, """
            { "MinecraftFolders": null, "JavaManualPaths": null, "JavaDisabledPaths": null,
              "InstanceConfigValues": { "bad": null }, "InstanceStateValues": null,
              "MinecraftFolderNames": null, "MaxDownloadConcurrency": 0,
              "MaxMemoryMb": -1, "SystemRenderBackend": 999, "WindowWidth": -10, "WindowHeight": 0 }
            """);
        var settings = AppSettings.Load(SettingsPath);
        Assert.IsTrue(Path.IsPathFullyQualified(settings.MinecraftRoot));
        Assert.IsNotNull(settings.JavaManualPaths);
        Assert.IsNotNull(settings.InstanceStateValues);
        Assert.IsEmpty(settings.InstanceConfigValues);
        Assert.AreEqual(1, settings.MaxDownloadConcurrency);
        Assert.AreEqual(256, settings.MaxMemoryMb);
        Assert.AreEqual(RenderBackend.Auto, settings.SystemRenderBackend);
        Assert.IsTrue(settings.WindowWidth > 0 && settings.WindowHeight > 0);
    }

    [TestMethod]
    public void BrokenSettingsDoNotGetOverwrittenByDefaults()
    {
        File.WriteAllText(SettingsPath, "{broken");
        var settings = AppSettings.Load(SettingsPath);
        Assert.AreEqual(JsonFileStatus.Unreadable, settings.LoadStatus);
        settings.PlayerName = "temporary";
        settings.Save();
        Assert.AreEqual("{broken", File.ReadAllText(SettingsPath));
    }

    [TestMethod]
    public void LastGoodSettingsAreRecoveredAfterInterruptedWrite()
    {
        var settings = AppSettings.Load(SettingsPath);
        settings.PlayerName = "previous";
        settings.EnableModMetadataParsing = false;
        settings.Save();
        settings.PlayerName = "current";
        settings.Save();
        File.WriteAllText(SettingsPath, "{truncated");
        var recovered = AppSettings.Load(SettingsPath);
        Assert.AreEqual(JsonFileStatus.Recovered, recovered.LoadStatus);
        Assert.AreEqual("previous", recovered.PlayerName);
        Assert.IsFalse(recovered.EnableModMetadataParsing);
        recovered.Save();
        Assert.AreEqual(JsonFileStatus.Loaded, AppSettings.Load(SettingsPath).LoadStatus);
    }

    [TestMethod]
    public void LegacyMinecraftRootStillMigrates()
    {
        var legacy = Path.Combine(_directory, "legacy-minecraft");
        File.WriteAllText(SettingsPath, System.Text.Json.JsonSerializer.Serialize(new { MinecraftRoot = legacy }));
        var settings = AppSettings.Load(SettingsPath);
        Assert.AreEqual(legacy, settings.MinecraftRoot);
        settings.Save();
        Assert.AreEqual(legacy, AppSettings.Load(SettingsPath).MinecraftRoot);
    }
}
