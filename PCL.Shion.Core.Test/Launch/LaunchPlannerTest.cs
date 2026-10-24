using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Launch;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class LaunchPlannerTest
{
    private static readonly string OsName = PlatformInfo.OsName;
    private static readonly string OtherOsName = OsName == "windows" ? "linux" : "windows";
    private static readonly OfflineAccount Steve = OfflineAccount.Create("Steve");

    private FakeMinecraftRoot _root = null!;

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
    public void BuildsArgumentsForModernVersion()
    {
        InstallModern();

        var plan = LaunchPlanner.Build(Request("modern", memoryMb: 2048, width: 1280, height: 720));
        var args = plan.Arguments;

        Assert.AreEqual("java-bin", plan.JavaExecutable);
        Assert.AreEqual(_root.RootPath, plan.WorkingDirectory);
        Assert.AreEqual(_root.GetPath("versions/modern/modern-natives"), plan.NativesDirectory);
        Assert.IsTrue(Directory.Exists(plan.NativesDirectory));
        Assert.HasCount(0, plan.MissingFiles);

        Assert.AreEqual("-Xmx2048m", args[0]);
        CollectionAssert.Contains(args.ToArray(), "-Dfile.encoding=UTF-8");
        Assert.AreEqual("-Dminecraft.launcher.brand=PCL Shion", args.Single(a => a.StartsWith("-Dminecraft.launcher.brand=")));
        Assert.AreEqual("-Dminecraft.launcher.version=0.1.0", args.Single(a => a.StartsWith("-Dminecraft.launcher.version=")));
        Assert.AreEqual($"-Djava.library.path={plan.NativesDirectory}/java", args.Single(a => a.StartsWith("-Djava.library.path=")));
        Assert.AreEqual(ExpectedModernClasspath(), ValueAfter(args, "-cp"));

        Assert.IsLessThan(IndexOf(args, "net.minecraft.client.main.Main"), IndexOf(args, "-cp"));
        Assert.IsLessThan(IndexOf(args, "--username"), IndexOf(args, "net.minecraft.client.main.Main"));

        Assert.AreEqual("Steve", ValueAfter(args, "--username"));
        Assert.AreEqual("modern", ValueAfter(args, "--version"));
        Assert.AreEqual(_root.RootPath, ValueAfter(args, "--gameDir"));
        Assert.AreEqual(_root.GetPath("assets"), ValueAfter(args, "--assetsDir"));
        Assert.AreEqual("17", ValueAfter(args, "--assetIndex"));
        Assert.AreEqual(Steve.Uuid, ValueAfter(args, "--uuid"));
        Assert.AreEqual(Steve.AccessToken, ValueAfter(args, "--accessToken"));
        Assert.AreEqual(string.Empty, ValueAfter(args, "--clientId"));
        Assert.AreEqual(string.Empty, ValueAfter(args, "--xuid"));
        Assert.AreEqual("release", ValueAfter(args, "--versionType"));
        Assert.AreEqual("1280", ValueAfter(args, "--width"));
        Assert.AreEqual("720", ValueAfter(args, "--height"));
    }

    [TestMethod]
    public void AppliesOnlyRulesThatMatchTheCurrentEnvironment()
    {
        InstallModern();

        var args = LaunchPlanner.Build(Request("modern")).Arguments;

        CollectionAssert.DoesNotContain(args.ToArray(), "--demo");
        CollectionAssert.DoesNotContain(args.ToArray(), "--quickPlayPath");
        CollectionAssert.DoesNotContain(args.ToArray(), "--quickPlaySingleplayer");
        CollectionAssert.DoesNotContain(args.ToArray(), "--quickPlayMultiplayer");
        CollectionAssert.DoesNotContain(args.ToArray(), "-Xms2G");
        Assert.AreEqual(OsName == "osx", args.Contains("-XstartOnFirstThread"));
        Assert.AreEqual(PlatformInfo.Arch == "x86", args.Contains("-Xss1M"));
        Assert.AreEqual(OsName == "windows", args.Any(a => a.StartsWith("-XX:HeapDumpPath=")));
        Assert.IsFalse(args.Any(a => a.Contains("${")), "unresolved placeholder left in the arguments");
    }

    [TestMethod]
    public void OmitsResolutionWhenWindowSizeIsNotGiven()
    {
        InstallModern();

        var args = LaunchPlanner.Build(Request("modern", width: 0, height: 0)).Arguments;

        CollectionAssert.DoesNotContain(args.ToArray(), "--width");
        CollectionAssert.DoesNotContain(args.ToArray(), "--height");
    }

    [TestMethod]
    public void UsesInstanceDirectoryAsGameDirectoryWhenIsolated()
    {
        InstallModern();
        _root.WriteFile("versions/modern/PCL/config.v1.yml", "VersionArgumentIndieV2: true\n");

        var plan = LaunchPlanner.Build(Request("modern"));

        Assert.AreEqual(_root.GetPath("versions/modern"), plan.WorkingDirectory);
        Assert.AreEqual(_root.GetPath("versions/modern"), ValueAfter(plan.Arguments, "--gameDir"));
        Assert.AreEqual(_root.GetPath("assets"), ValueAfter(plan.Arguments, "--assetsDir"));
    }

    [TestMethod]
    public void UsesTheGameDirectoryOverride()
    {
        InstallModern();
        var gameDirectory = _root.GetPath("custom/game");

        var plan = LaunchPlanner.Build(Request("modern") with { GameDirectoryOverride = gameDirectory + Path.DirectorySeparatorChar });

        Assert.AreEqual(gameDirectory, plan.WorkingDirectory);
        Assert.AreEqual(gameDirectory, ValueAfter(plan.Arguments, "--gameDir"));
        Assert.IsTrue(Directory.Exists(gameDirectory));
    }

    [TestMethod]
    public void GameDirectoryOverrideTakesPrecedenceOverIsolation()
    {
        InstallModern();
        _root.WriteFile("versions/modern/PCL/config.v1.yml", "VersionArgumentIndieV2: true\n");

        var plan = LaunchPlanner.Build(Request("modern") with { GameDirectoryOverride = _root.RootPath });

        Assert.AreEqual(_root.RootPath, plan.WorkingDirectory);
        Assert.AreEqual(_root.GetPath("versions/modern/modern-natives"), plan.NativesDirectory);
    }

    [TestMethod]
    public void PlacesExtraJvmArgumentsBeforeTheMainClassAndExtraGameArgumentsLast()
    {
        InstallModern();

        var args = LaunchPlanner.Build(Request("modern") with
        {
            ExtraJvmArguments = "-Xmx8G \"-Dshion.note=a b\"",
            ExtraGameArguments = "--fullscreen --note \"x y\""
        }).Arguments;

        // 追加的 JVM 参数排在版本的 JVM 参数之后，能覆盖启动器给出的 -Xmx
        Assert.IsLessThan(IndexOf(args, "-Xmx8G"), IndexOf(args, "-Xmx4096m"));
        Assert.IsLessThan(IndexOf(args, "-Xmx8G"), IndexOf(args, "-cp"));
        Assert.IsLessThan(IndexOf(args, "net.minecraft.client.main.Main"), IndexOf(args, "-Dshion.note=a b"));
        CollectionAssert.AreEqual(new[] { "--fullscreen", "--note", "x y" }, args.TakeLast(3).ToArray());
    }

    [TestMethod]
    public void PassesEnvironmentVariablesAndTheWrapperCommandToThePlan()
    {
        InstallModern();
        var environment = new Dictionary<string, string> { ["__GL_THREADED_OPTIMIZATIONS"] = "1" };

        var plan = LaunchPlanner.Build(Request("modern") with { EnvironmentVariables = environment, WrapperCommand = "prime-run" });
        var plain = LaunchPlanner.Build(Request("modern"));

        Assert.AreEqual("java-bin", plan.JavaExecutable);
        Assert.AreEqual("prime-run", plan.WrapperCommand);
        Assert.AreEqual("1", plan.EnvironmentVariables["__GL_THREADED_OPTIMIZATIONS"]);
        Assert.IsNull(plain.WrapperCommand);
        Assert.HasCount(0, plain.EnvironmentVariables);
    }

    [TestMethod]
    public void EntersAWorldWithQuickPlayWhenTheVersionSupportsIt()
    {
        InstallModern();

        var args = LaunchPlanner.Build(Request("modern") with
        {
            QuickPlay = new QuickPlayTarget(QuickPlayKind.Singleplayer, "My World")
        }).Arguments;

        Assert.AreEqual("My World", ValueAfter(args, "--quickPlaySingleplayer"));
        CollectionAssert.DoesNotContain(args.ToArray(), "--quickPlayMultiplayer");
        CollectionAssert.DoesNotContain(args.ToArray(), "--server");
    }

    [TestMethod]
    public void JoinsAServerWithQuickPlayWhenTheVersionSupportsIt()
    {
        InstallModern();

        var args = LaunchPlanner.Build(Request("modern") with
        {
            QuickPlay = new QuickPlayTarget(QuickPlayKind.Multiplayer, "mc.example.com:25570")
        }).Arguments;

        Assert.AreEqual("mc.example.com:25570", ValueAfter(args, "--quickPlayMultiplayer"));
        CollectionAssert.DoesNotContain(args.ToArray(), "--quickPlaySingleplayer");
        CollectionAssert.DoesNotContain(args.ToArray(), "--server");
        CollectionAssert.DoesNotContain(args.ToArray(), "--port");
    }

    [TestMethod]
    public void JoinsAServerWithLegacyArgumentsOnOlderVersions()
    {
        InstallLegacy();

        var args = LaunchPlanner.Build(Request("legacy") with
        {
            QuickPlay = new QuickPlayTarget(QuickPlayKind.Multiplayer, "mc.example.com:25570"),
            ExtraGameArguments = "--fullscreen"
        }).Arguments;

        CollectionAssert.AreEqual(
            new[] { "--server", "mc.example.com", "--port", "25570", "--fullscreen" },
            args.TakeLast(5).ToArray());
    }

    [TestMethod]
    public void IgnoresWorldTargetsOnOlderVersions()
    {
        InstallLegacy();

        var plain = LaunchPlanner.Build(Request("legacy")).Arguments;
        var args = LaunchPlanner.Build(Request("legacy") with
        {
            QuickPlay = new QuickPlayTarget(QuickPlayKind.Singleplayer, "My World")
        }).Arguments;

        CollectionAssert.AreEqual(plain.ToArray(), args.ToArray());
    }

    [TestMethod]
    public void ReportsMissingFilesWithoutThrowing()
    {
        _root.WriteVersion("modern", ModernJson());

        var plan = LaunchPlanner.Build(Request("modern"));

        CollectionAssert.AreEquivalent(
            new[]
            {
                _root.GetPath("libraries/com/google/code/gson/gson/2.14.0/gson-2.14.0.jar"),
                _root.GetPath("libraries/org/lwjgl/lwjgl/3.4.3/lwjgl-3.4.3.jar"),
                _root.GetPath($"libraries/org/lwjgl/lwjgl/3.4.3/lwjgl-3.4.3-natives-{OsName}.jar"),
                _root.GetPath("versions/modern/modern.jar"),
                _root.GetPath("assets/indexes/17.json")
            },
            plan.MissingFiles.ToArray());
    }

    [TestMethod]
    public void ReportsMissingAssetObjects()
    {
        InstallModern();
        File.Delete(_root.GetPath($"assets/objects/{HashPrefix}/{Hash}"));
        File.Delete(_root.GetPath("libraries/org/lwjgl/lwjgl/3.4.3/lwjgl-3.4.3.jar"));

        var plan = LaunchPlanner.Build(Request("modern"));

        CollectionAssert.AreEquivalent(
            new[]
            {
                _root.GetPath("libraries/org/lwjgl/lwjgl/3.4.3/lwjgl-3.4.3.jar"),
                _root.GetPath($"assets/objects/{HashPrefix}/{Hash}")
            },
            plan.MissingFiles.ToArray());
    }

    [TestMethod]
    public void AddsLogConfigurationOnlyWhenItExists()
    {
        InstallModern();

        var without = LaunchPlanner.Build(Request("modern")).Arguments;
        _root.WriteFile("assets/log_configs/client-1.12.xml", "<Configuration />");
        var with = LaunchPlanner.Build(Request("modern")).Arguments;

        Assert.IsFalse(without.Any(a => a.StartsWith("-Dlog4j.configurationFile=")));
        Assert.AreEqual(
            $"-Dlog4j.configurationFile={_root.GetPath("assets/log_configs/client-1.12.xml")}",
            with.Single(a => a.StartsWith("-Dlog4j.configurationFile=")));
    }

    [TestMethod]
    public void ExpandsModulePathArgumentsOfModdedVersions()
    {
        InstallForge();

        var plan = LaunchPlanner.Build(Request("forge-modern"));
        var args = plan.Arguments;
        var libraries = _root.GetPath("libraries");
        var separator = PlatformInfo.ClasspathSeparator;

        Assert.HasCount(0, plan.MissingFiles);
        Assert.AreEqual(
            "-DignoreList=bootstraplauncher,securejarhandler,forge-,forge-modern.jar",
            args.Single(a => a.StartsWith("-DignoreList=")));
        Assert.AreEqual($"-DlibraryDirectory={libraries}", args.Single(a => a.StartsWith("-DlibraryDirectory=")));
        Assert.AreEqual(
            $"{libraries}/cpw/mods/bootstraplauncher/1.1.2/bootstraplauncher-1.1.2.jar{separator}{libraries}/cpw/mods/securejarhandler/2.1.4/securejarhandler-2.1.4.jar",
            ValueAfter(args, "-p"));
        Assert.AreEqual("ALL-MODULE-PATH", ValueAfter(args, "--add-modules"));

        // 加载器自己的主类、参数排在原版之后；原版的 -cp 仍然保留
        Assert.IsLessThan(IndexOf(args, "-p"), IndexOf(args, "-cp"));
        Assert.IsLessThan(IndexOf(args, "cpw.mods.bootstraplauncher.BootstrapLauncher"), IndexOf(args, "-p"));
        Assert.IsFalse(args.Contains("net.minecraft.client.main.Main"));
        Assert.AreEqual("forgeclient", ValueAfter(args, "--launchTarget"));
        Assert.AreEqual("forge-modern", ValueAfter(args, "--version"));
        Assert.AreEqual("--launchTarget", args[^2]);
    }

    [TestMethod]
    public void ClasspathUsesInheritedClientJarAndLaterLibraryVersions()
    {
        InstallForge();

        var classpath = ValueAfter(LaunchPlanner.Build(Request("forge-modern")).Arguments, "-cp")
            .Split(PlatformInfo.ClasspathSeparator);

        Assert.AreEqual(_root.GetPath("versions/modern/modern.jar"), classpath[^1]);
        Assert.IsTrue(classpath.Contains(_root.GetPath("libraries/com/google/code/gson/gson/2.15.0/gson-2.15.0.jar")));
        Assert.IsFalse(classpath.Contains(_root.GetPath("libraries/com/google/code/gson/gson/2.14.0/gson-2.14.0.jar")));
    }

    [TestMethod]
    public void ExpandsLegacyArgumentsAndExtractsNatives()
    {
        InstallLegacy();

        var plan = LaunchPlanner.Build(Request("legacy"));
        var args = plan.Arguments;

        Assert.HasCount(0, plan.MissingFiles);
        Assert.AreEqual($"-Djava.library.path={plan.NativesDirectory}", args.Single(a => a.StartsWith("-Djava.library.path=")));
        CollectionAssert.AreEqual(
            new[]
            {
                _root.GetPath("libraries/com/mojang/authlib/1.5.25/authlib-1.5.25.jar"),
                _root.GetPath("versions/legacy/legacy.jar")
            },
            ValueAfter(args, "-cp").Split(PlatformInfo.ClasspathSeparator));

        var main = IndexOf(args, "net.minecraft.client.main.Main");
        CollectionAssert.AreEqual(
            new[]
            {
                "--username", "Steve",
                "--version", "legacy",
                "--gameDir", _root.RootPath,
                "--assetsDir", _root.GetPath("assets/virtual/legacy"),
                "--uuid", Steve.Uuid,
                "--accessToken", Steve.AccessToken,
                "--userType", "legacy",
                "--versionType", "release",
                "--userProperties", "{}",
                "--session", $"token:{Steve.AccessToken}:{Steve.Uuid}"
            },
            args.Skip(main + 1).ToArray());

        Assert.AreEqual("native", File.ReadAllText(Path.Combine(plan.NativesDirectory, "native-library")));
        Assert.IsFalse(Directory.Exists(Path.Combine(plan.NativesDirectory, "META-INF")));
        Assert.AreEqual("sound", File.ReadAllText(_root.GetPath("assets/virtual/legacy/sound/ding.ogg")));
    }

    [TestMethod]
    public void LegacyArgumentValuesWithSpacesStayOneArgument()
    {
        WriteLegacy(_root, "legacy", "my games/.minecraft");
        var minecraft = _root.GetPath("my games/.minecraft");

        var args = LaunchPlanner.Build(Request("legacy", root: minecraft)).Arguments;

        Assert.AreEqual(minecraft, ValueAfter(args, "--gameDir"));
    }

    [TestMethod]
    public void LegacyBaseVersionKeepsLoaderJvmArguments()
    {
        InstallLegacy();
        _root.WriteVersion("fabric-legacy", """
            {
              "inheritsFrom": "legacy",
              "mainClass": "net.fabricmc.loader.launch.knot.KnotClient",
              "arguments": { "game": [], "jvm": [ "-DFabricMcEmu= net.minecraft.client.main.Main " ] }
            }
            """);

        var args = LaunchPlanner.Build(Request("fabric-legacy")).Arguments;

        Assert.IsTrue(args.Contains("-DFabricMcEmu= net.minecraft.client.main.Main "));
        Assert.IsTrue(args.Any(a => a.StartsWith("-Djava.library.path=")));
        Assert.IsLessThan(IndexOf(args, "net.fabricmc.loader.launch.knot.KnotClient"), IndexOf(args, "-cp"));
        Assert.AreEqual("fabric-legacy", ValueAfter(args, "--version"));
    }

    [TestMethod]
    public void ExtractingNativesTwiceIsHarmless()
    {
        InstallLegacy();

        LaunchPlanner.Build(Request("legacy"));
        var plan = LaunchPlanner.Build(Request("legacy"));

        Assert.HasCount(0, plan.MissingFiles);
        Assert.AreEqual("native", File.ReadAllText(Path.Combine(plan.NativesDirectory, "native-library")));
    }

    [TestMethod]
    public void ThrowsWhenTheInstanceDoesNotExist()
    {
        Assert.Throws<IOException>(() => LaunchPlanner.Build(Request("absent")));
    }

    private const string Hash = "aa11bb22cc33dd44ee55ff66aa11bb22cc33dd44";
    private const string HashPrefix = "aa";

    private LaunchRequest Request(string instance, string? root = null, int memoryMb = 4096, int width = 854, int height = 480)
    {
        return new LaunchRequest(root ?? _root.RootPath, instance, Steve, "java-bin", memoryMb, width, height);
    }

    private static int IndexOf(IReadOnlyList<string> args, string value)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == value)
                return i;
        }
        Assert.Fail($"'{value}' is not in the arguments: {string.Join(' ', args)}");
        return -1;
    }

    private static string ValueAfter(IReadOnlyList<string> args, string flag)
    {
        return args[IndexOf(args, flag) + 1];
    }

    private string ExpectedModernClasspath()
    {
        return string.Join(
            PlatformInfo.ClasspathSeparator,
            _root.GetPath("libraries/com/google/code/gson/gson/2.14.0/gson-2.14.0.jar"),
            _root.GetPath("libraries/org/lwjgl/lwjgl/3.4.3/lwjgl-3.4.3.jar"),
            _root.GetPath($"libraries/org/lwjgl/lwjgl/3.4.3/lwjgl-3.4.3-natives-{OsName}.jar"),
            _root.GetPath("versions/modern/modern.jar"));
    }

    /// <summary>
    /// 写入 modern 版本及其库、客户端 jar 与资源，使它没有任何缺失文件。
    /// </summary>
    private void InstallModern()
    {
        _root.WriteVersion("modern", ModernJson());
        _root.WriteFile("libraries/com/google/code/gson/gson/2.14.0/gson-2.14.0.jar");
        _root.WriteFile("libraries/org/lwjgl/lwjgl/3.4.3/lwjgl-3.4.3.jar");
        _root.WriteFile($"libraries/org/lwjgl/lwjgl/3.4.3/lwjgl-3.4.3-natives-{OsName}.jar");
        _root.WriteFile("versions/modern/modern.jar");
        _root.WriteFile("assets/indexes/17.json", $$"""{ "objects": { "minecraft/lang/en_us.json": { "hash": "{{Hash}}", "size": 4 } } }""");
        _root.WriteFile($"assets/objects/{HashPrefix}/{Hash}", "data");
    }

    /// <summary>
    /// 在 modern 之上写入 forge-modern 版本及其库。
    /// </summary>
    private void InstallForge()
    {
        InstallModern();
        _root.WriteVersion("forge-modern", ForgeJson());
        _root.WriteFile("libraries/cpw/mods/bootstraplauncher/1.1.2/bootstraplauncher-1.1.2.jar");
        _root.WriteFile("libraries/cpw/mods/securejarhandler/2.1.4/securejarhandler-2.1.4.jar");
        _root.WriteFile("libraries/com/google/code/gson/gson/2.15.0/gson-2.15.0.jar");
    }

    private void InstallLegacy()
    {
        WriteLegacy(_root, "legacy", string.Empty);
    }

    /// <summary>
    /// 在 <paramref name="fake"/> 的 <paramref name="rootRelativePath"/> 下写入旧格式的 legacy 版本，
    /// 含一个需要解压的 natives jar 与旧版虚拟资源。
    /// </summary>
    private static void WriteLegacy(FakeMinecraftRoot fake, string name, string rootRelativePath)
    {
        string Combine(string path) => rootRelativePath.Length == 0 ? path : $"{rootRelativePath}/{path}";

        fake.WriteFile(Combine($"versions/{name}/{name}.json"), LegacyJson(name));
        fake.WriteFile(Combine($"versions/{name}/{name}.jar"));
        fake.WriteFile(Combine("libraries/com/mojang/authlib/1.5.25/authlib-1.5.25.jar"));

        var nativesJar = fake.GetPath(Combine(
            $"libraries/org/lwjgl/lwjgl/lwjgl-platform/2.9.4/lwjgl-platform-2.9.4-natives-{OsName}.jar"));
        Directory.CreateDirectory(Path.GetDirectoryName(nativesJar)!);
        using (var archive = ZipFile.Open(nativesJar, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "native-library", "native");
            WriteEntry(archive, "META-INF/MANIFEST.MF", "manifest");
        }

        fake.WriteFile(Combine("assets/indexes/legacy.json"), $$"""{ "objects": { "sound/ding.ogg": { "hash": "{{Hash}}", "size": 5 } } }""");
        fake.WriteFile(Combine($"assets/objects/{HashPrefix}/{Hash}"), "sound");
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }

    private static string ModernJson()
    {
        return $$"""
            {
              "id": "modern",
              "type": "release",
              "mainClass": "net.minecraft.client.main.Main",
              "assets": "17",
              "assetIndex": { "id": "17" },
              "javaVersion": { "component": "java-runtime-delta", "majorVersion": 21 },
              "downloads": { "client": { "url": "https://example.invalid/client.jar" } },
              "logging": {
                "client": {
                  "argument": "-Dlog4j.configurationFile=${path}",
                  "file": { "id": "client-1.12.xml" }
                }
              },
              "arguments": {
                "default-user-jvm": [ { "value": [ "-Xms2G", "-Xmx4G" ] } ],
                "game": [
                  "--username", "${auth_player_name}",
                  "--version", "${version_name}",
                  "--gameDir", "${game_directory}",
                  "--assetsDir", "${assets_root}",
                  "--assetIndex", "${assets_index_name}",
                  "--uuid", "${auth_uuid}",
                  "--accessToken", "${auth_access_token}",
                  "--clientId", "${clientid}",
                  "--xuid", "${auth_xuid}",
                  "--versionType", "${version_type}",
                  { "rules": [ { "action": "allow", "features": { "is_demo_user": true } } ], "value": "--demo" },
                  {
                    "rules": [ { "action": "allow", "features": { "has_custom_resolution": true } } ],
                    "value": [ "--width", "${resolution_width}", "--height", "${resolution_height}" ]
                  },
                  {
                    "rules": [ { "action": "allow", "features": { "has_quick_plays_support": true } } ],
                    "value": [ "--quickPlayPath", "${quickPlayPath}" ]
                  },
                  {
                    "rules": [ { "action": "allow", "features": { "is_quick_play_singleplayer": true } } ],
                    "value": [ "--quickPlaySingleplayer", "${quickPlaySingleplayer}" ]
                  },
                  {
                    "rules": [ { "action": "allow", "features": { "is_quick_play_multiplayer": true } } ],
                    "value": [ "--quickPlayMultiplayer", "${quickPlayMultiplayer}" ]
                  }
                ],
                "jvm": [
                  { "rules": [ { "action": "allow", "os": { "name": "osx" } } ], "value": [ "-XstartOnFirstThread" ] },
                  {
                    "rules": [ { "action": "allow", "os": { "name": "windows" } } ],
                    "value": "-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump"
                  },
                  { "rules": [ { "action": "allow", "os": { "arch": "x86" } } ], "value": "-Xss1M" },
                  "-Djava.library.path=${natives_directory}/java",
                  "-Dminecraft.launcher.brand=${launcher_name}",
                  "-Dminecraft.launcher.version=${launcher_version}",
                  "-cp", "${classpath}"
                ]
              },
              "libraries": [
                {
                  "name": "com.google.code.gson:gson:2.14.0",
                  "downloads": { "artifact": { "path": "com/google/code/gson/gson/2.14.0/gson-2.14.0.jar" } }
                },
                {
                  "name": "org.lwjgl:lwjgl:3.4.3",
                  "downloads": { "artifact": { "path": "org/lwjgl/lwjgl/3.4.3/lwjgl-3.4.3.jar" } }
                },
                {
                  "name": "org.lwjgl:lwjgl:3.4.3:natives-{{OsName}}",
                  "rules": [ { "action": "allow", "os": { "name": "{{OsName}}" } } ],
                  "downloads": { "artifact": { "path": "org/lwjgl/lwjgl/3.4.3/lwjgl-3.4.3-natives-{{OsName}}.jar" } }
                },
                {
                  "name": "org.lwjgl:lwjgl:3.4.3:natives-{{OtherOsName}}",
                  "rules": [ { "action": "allow", "os": { "name": "{{OtherOsName}}" } } ],
                  "downloads": { "artifact": { "path": "org/lwjgl/lwjgl/3.4.3/lwjgl-3.4.3-natives-{{OtherOsName}}.jar" } }
                }
              ]
            }
            """;
    }

    /// <summary>
    /// 模拟 Forge 安装器生成的版本：继承 modern，用 module path 启动，并换用更新的 gson。
    /// </summary>
    private static string ForgeJson()
    {
        return """
            {
              "id": "forge-modern",
              "inheritsFrom": "modern",
              "type": "release",
              "mainClass": "cpw.mods.bootstraplauncher.BootstrapLauncher",
              "arguments": {
                "game": [ "--launchTarget", "forgeclient" ],
                "jvm": [
                  "-DignoreList=bootstraplauncher,securejarhandler,forge-,${version_name}.jar",
                  "-DlibraryDirectory=${library_directory}",
                  "-p",
                  "${library_directory}/cpw/mods/bootstraplauncher/1.1.2/bootstraplauncher-1.1.2.jar${classpath_separator}${library_directory}/cpw/mods/securejarhandler/2.1.4/securejarhandler-2.1.4.jar",
                  "--add-modules", "ALL-MODULE-PATH",
                  "--add-opens", "java.base/java.util.jar=cpw.mods.securejarhandler"
                ]
              },
              "libraries": [
                {
                  "name": "cpw.mods:bootstraplauncher:1.1.2",
                  "downloads": { "artifact": { "path": "cpw/mods/bootstraplauncher/1.1.2/bootstraplauncher-1.1.2.jar" } }
                },
                {
                  "name": "cpw.mods:securejarhandler:2.1.4",
                  "downloads": { "artifact": { "path": "cpw/mods/securejarhandler/2.1.4/securejarhandler-2.1.4.jar" } }
                },
                { "name": "com.google.code.gson:gson:2.15.0" }
              ]
            }
            """;
    }

    private static string LegacyJson(string name)
    {
        return $$"""
            {
              "id": "{{name}}",
              "type": "release",
              "mainClass": "net.minecraft.client.main.Main",
              "assets": "legacy",
              "minecraftArguments": "--username ${auth_player_name} --version ${version_name} --gameDir ${game_directory} --assetsDir ${game_assets} --uuid ${auth_uuid} --accessToken ${auth_access_token} --userType ${user_type} --versionType ${version_type} --userProperties ${user_properties} --session ${auth_session}",
              "libraries": [
                { "name": "com.mojang:authlib:1.5.25" },
                {
                  "name": "org.lwjgl.lwjgl:lwjgl-platform:2.9.4",
                  "natives": { "windows": "natives-windows", "linux": "natives-linux", "osx": "natives-osx" },
                  "extract": { "exclude": [ "META-INF/" ] },
                  "downloads": {
                    "classifiers": {
                      "natives-windows": { "path": "org/lwjgl/lwjgl/lwjgl-platform/2.9.4/lwjgl-platform-2.9.4-natives-windows.jar" },
                      "natives-linux": { "path": "org/lwjgl/lwjgl/lwjgl-platform/2.9.4/lwjgl-platform-2.9.4-natives-linux.jar" },
                      "natives-osx": { "path": "org/lwjgl/lwjgl/lwjgl-platform/2.9.4/lwjgl-platform-2.9.4-natives-osx.jar" }
                    }
                  }
                }
              ]
            }
            """;
    }
}
