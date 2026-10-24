using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Instances;

namespace PCL.Shion.Core.Test.Instances;

[TestClass]
public sealed class InstanceStoreTest
{
    private const string Instance = "demo";

    private FakeMinecraftRoot _root = null!;

    private string SettingsPath => _root.GetPath("versions/demo/PCL Shion/instance.json");

    [TestInitialize]
    public void CreateRoot()
    {
        _root = new FakeMinecraftRoot();
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        _root.Dispose();
    }

    [TestMethod]
    public void MissingFileYieldsDefaults()
    {
        var settings = InstanceStore.Load(_root.RootPath, Instance);

        Assert.IsNull(settings.JavaExecutable);
        Assert.IsNull(settings.MaxMemoryMb);
        Assert.IsNull(settings.Isolated);
        Assert.IsNull(settings.ExtraJvmArguments);
        Assert.IsNull(settings.ExtraGameArguments);
        Assert.IsNull(settings.PreLaunchCommand);
        Assert.IsNull(settings.PostExitCommand);
        Assert.IsNull(settings.WrapperCommand);
        Assert.IsEmpty(settings.EnvironmentVariables);
        Assert.IsNull(settings.WindowWidth);
        Assert.IsNull(settings.WindowHeight);
        Assert.IsNull(settings.QuickPlay);
        Assert.AreEqual(0L, settings.TotalPlaySeconds);
        Assert.IsNull(settings.LastPlayed);
    }

    [TestMethod]
    public void SavedSettingsAreLoadedBack()
    {
        var lastPlayed = new DateTimeOffset(2026, 10, 1, 12, 30, 0, TimeSpan.FromHours(8));
        var saved = new InstanceSettings
        {
            JavaExecutable = "/opt/java/bin/java",
            MaxMemoryMb = 6144,
            Isolated = true,
            ExtraJvmArguments = "-XX:+UseZGC -Dfile.encoding=UTF-8",
            ExtraGameArguments = "--demo",
            PreLaunchCommand = "echo start",
            PostExitCommand = "echo stop",
            WrapperCommand = "taskset -c 0-3",
            EnvironmentVariables = { ["__GL_THREADED_OPTIMIZATIONS"] = "0", ["Mixed_Case"] = "value" },
            WindowWidth = 1280,
            WindowHeight = 720,
            QuickPlay = new QuickPlayTarget(QuickPlayKind.Multiplayer, "play.example.com:25566"),
            TotalPlaySeconds = 3600,
            LastPlayed = lastPlayed
        };

        InstanceStore.Save(_root.RootPath, Instance, saved);
        var loaded = InstanceStore.Load(_root.RootPath, Instance);

        Assert.AreEqual("/opt/java/bin/java", loaded.JavaExecutable);
        Assert.AreEqual(6144, loaded.MaxMemoryMb);
        Assert.IsTrue(loaded.Isolated);
        Assert.AreEqual("-XX:+UseZGC -Dfile.encoding=UTF-8", loaded.ExtraJvmArguments);
        Assert.AreEqual("--demo", loaded.ExtraGameArguments);
        Assert.AreEqual("echo start", loaded.PreLaunchCommand);
        Assert.AreEqual("echo stop", loaded.PostExitCommand);
        Assert.AreEqual("taskset -c 0-3", loaded.WrapperCommand);
        Assert.HasCount(2, loaded.EnvironmentVariables);
        Assert.AreEqual("0", loaded.EnvironmentVariables["__GL_THREADED_OPTIMIZATIONS"]);
        Assert.AreEqual("value", loaded.EnvironmentVariables["Mixed_Case"]);
        Assert.AreEqual(1280, loaded.WindowWidth);
        Assert.AreEqual(720, loaded.WindowHeight);
        Assert.AreEqual(new QuickPlayTarget(QuickPlayKind.Multiplayer, "play.example.com:25566"), loaded.QuickPlay);
        Assert.AreEqual(3600L, loaded.TotalPlaySeconds);
        Assert.AreEqual(lastPlayed, loaded.LastPlayed);
    }

    [TestMethod]
    public void WritesIndentedCamelCaseJsonWithoutNullFields()
    {
        InstanceStore.Save(_root.RootPath, Instance, new InstanceSettings
        {
            MaxMemoryMb = 2048,
            PreLaunchCommand = "echo a && echo <b>",
            EnvironmentVariables = { ["JAVA_OPTS"] = "-Dx=1" },
            QuickPlay = new QuickPlayTarget(QuickPlayKind.Singleplayer, "World 1"),
            TotalPlaySeconds = 5
        });

        var text = File.ReadAllText(SettingsPath);

        Assert.Contains("\"maxMemoryMb\": 2048", text);
        Assert.Contains("\"preLaunchCommand\": \"echo a && echo <b>\"", text);
        Assert.Contains("\"JAVA_OPTS\": \"-Dx=1\"", text);
        Assert.Contains("\"kind\": \"singleplayer\"", text);
        Assert.Contains("\"value\": \"World 1\"", text);
        Assert.Contains("\"totalPlaySeconds\": 5", text);
        Assert.DoesNotContain("javaExecutable", text);
        Assert.DoesNotContain("lastPlayed", text);
    }

    [TestMethod]
    public void SavingReplacesThePreviousFileWithoutLeavingTemporaryFiles()
    {
        InstanceStore.Save(_root.RootPath, Instance, new InstanceSettings { MaxMemoryMb = 1024 });
        InstanceStore.Save(_root.RootPath, Instance, new InstanceSettings { MaxMemoryMb = 2048 });

        Assert.AreEqual(2048, InstanceStore.Load(_root.RootPath, Instance).MaxMemoryMb);
        var files = Directory.GetFiles(Path.GetDirectoryName(SettingsPath)!);
        Assert.HasCount(1, files);
        Assert.AreEqual("instance.json", Path.GetFileName(files[0]));
    }

    [TestMethod]
    public void LoadsPartialJsonAndIgnoresUnknownFields()
    {
        _root.WriteFile("versions/demo/PCL Shion/instance.json", """
            { "maxMemoryMb": 3000, "futureOption": [1, 2], "quickPlay": { "kind": "multiplayer", "value": "example.org" } }
            """);

        var settings = InstanceStore.Load(_root.RootPath, Instance);

        Assert.AreEqual(3000, settings.MaxMemoryMb);
        Assert.AreEqual(new QuickPlayTarget(QuickPlayKind.Multiplayer, "example.org"), settings.QuickPlay);
        Assert.IsNull(settings.JavaExecutable);
        Assert.IsEmpty(settings.EnvironmentVariables);
    }

    [TestMethod]
    public void LoadsFileWithByteOrderMark()
    {
        _root.WriteFile(
            "versions/demo/PCL Shion/instance.json",
            """{ "windowWidth": 1920 }""",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Assert.AreEqual(1920, InstanceStore.Load(_root.RootPath, Instance).WindowWidth);
    }

    [TestMethod]
    public void MalformedFileThrows()
    {
        _root.WriteFile("versions/demo/PCL Shion/instance.json", "{ not json");

        Assert.Throws<JsonException>(() => InstanceStore.Load(_root.RootPath, Instance));
    }
}
