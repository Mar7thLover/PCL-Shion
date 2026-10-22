using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class VersionFileTest
{
    [TestMethod]
    public void ParsesScalarFields()
    {
        var file = VersionFile.Parse("""
            {
              "id": "1.21.1",
              "type": "release",
              "mainClass": "net.minecraft.client.main.Main",
              "assets": "17",
              "assetIndex": { "id": "17", "url": "https://example.invalid/17.json" },
              "javaVersion": { "component": "java-runtime-delta", "majorVersion": 21 },
              "inheritsFrom": "1.21",
              "clientVersion": "1.21.1",
              "downloads": { "client": { "url": "https://example.invalid/client.jar" } },
              "logging": {
                "client": {
                  "argument": "-Dlog4j.configurationFile=${path}",
                  "file": { "id": "client-1.12.xml" }
                }
              }
            }
            """);

        Assert.AreEqual("1.21", file.InheritsFrom);
        Assert.AreEqual("net.minecraft.client.main.Main", file.MainClass);
        Assert.AreEqual("release", file.Type);
        Assert.AreEqual("17", file.AssetIndexId);
        Assert.AreEqual(21, file.JavaMajorVersion);
        Assert.AreEqual("1.21.1", file.ClientVersion);
        Assert.IsTrue(file.DeclaresClientJar);
        Assert.AreEqual("-Dlog4j.configurationFile=${path}", file.LoggingArgument);
        Assert.AreEqual("client-1.12.xml", file.LoggingFileId);
    }

    [TestMethod]
    public void AssetIndexFallsBackToAssets()
    {
        var file = VersionFile.Parse("""{ "mainClass": "Main", "assets": "legacy" }""");

        Assert.AreEqual("legacy", file.AssetIndexId);
        Assert.IsNull(file.JavaMajorVersion);
        Assert.IsFalse(file.DeclaresClientJar);
        Assert.IsNull(file.LoggingFileId);
    }

    [TestMethod]
    public void ParsesStringAndObjectArguments()
    {
        var file = VersionFile.Parse("""
            {
              "arguments": {
                "game": [
                  "--username", "${auth_player_name}",
                  {
                    "rules": [ { "action": "allow", "features": { "has_custom_resolution": true } } ],
                    "value": [ "--width", "${resolution_width}" ]
                  },
                  { "rules": [ { "action": "allow", "features": { "is_demo_user": true } } ], "value": "--demo" }
                ],
                "jvm": [
                  { "rules": [ { "action": "allow", "os": { "name": "osx" } } ], "value": [ "-XstartOnFirstThread" ] },
                  "-cp", "${classpath}"
                ]
              }
            }
            """);

        Assert.HasCount(4, file.GameArguments);
        CollectionAssert.AreEqual(new[] { "--username" }, file.GameArguments[0].Values.ToArray());
        Assert.HasCount(0, file.GameArguments[0].Rules);
        CollectionAssert.AreEqual(new[] { "--width", "${resolution_width}" }, file.GameArguments[2].Values.ToArray());
        Assert.IsTrue(file.GameArguments[2].Rules[0].Features["has_custom_resolution"]);
        CollectionAssert.AreEqual(new[] { "--demo" }, file.GameArguments[3].Values.ToArray());

        Assert.HasCount(3, file.JvmArguments);
        Assert.AreEqual("osx", file.JvmArguments[0].Rules[0].OsName);
        Assert.IsTrue(file.JvmArguments[0].Rules[0].Allow);
    }

    [TestMethod]
    public void ParsesRuleConditions()
    {
        var file = VersionFile.Parse("""
            {
              "libraries": [
                {
                  "name": "a:b:1",
                  "rules": [
                    { "action": "allow" },
                    { "action": "disallow", "os": { "name": "windows", "arch": "x86", "version": "^6\\." } }
                  ]
                }
              ]
            }
            """);

        var rules = file.Libraries[0].Rules;
        Assert.HasCount(2, rules);
        Assert.IsTrue(rules[0].Allow);
        Assert.IsNull(rules[0].OsName);
        Assert.IsFalse(rules[1].Allow);
        Assert.AreEqual("windows", rules[1].OsName);
        Assert.AreEqual("x86", rules[1].OsArch);
        Assert.AreEqual(@"^6\.", rules[1].OsVersion);
    }

    [TestMethod]
    public void ParsesLibraryDownloadsAndNatives()
    {
        var file = VersionFile.Parse("""
            {
              "libraries": [
                { "name": "net.sf.jopt-simple:jopt-simple:5.0.4", "downloads": { "artifact": { "path": "net/sf/jopt-simple/jopt-simple/5.0.4/jopt-simple-5.0.4.jar" } } },
                { "name": "net.fabricmc:fabric-loader:0.16.5", "url": "https://maven.fabricmc.net/" },
                {
                  "name": "org.lwjgl.lwjgl:lwjgl-platform:2.9.4-nightly-20150209",
                  "natives": { "linux": "natives-linux", "windows": "natives-windows-${arch}" },
                  "extract": { "exclude": [ "META-INF/", "docs/" ] },
                  "downloads": {
                    "classifiers": {
                      "natives-windows": { "path": "org/lwjgl/lwjgl/lwjgl-platform/2.9.4-nightly-20150209/lwjgl-platform-2.9.4-nightly-20150209-natives-windows.jar" }
                    }
                  }
                }
              ]
            }
            """);

        Assert.HasCount(3, file.Libraries);
        Assert.AreEqual("net/sf/jopt-simple/jopt-simple/5.0.4/jopt-simple-5.0.4.jar", file.Libraries[0].ArtifactPath);
        Assert.IsNull(file.Libraries[1].ArtifactPath);

        var native = file.Libraries[2];
        Assert.IsNull(native.ArtifactPath);
        Assert.AreEqual("natives-windows-${arch}", native.Natives["windows"]);
        Assert.AreEqual("natives-linux", native.Natives["linux"]);
        Assert.AreEqual(
            "org/lwjgl/lwjgl/lwjgl-platform/2.9.4-nightly-20150209/lwjgl-platform-2.9.4-nightly-20150209-natives-windows.jar",
            native.ClassifierPaths["natives-windows"]);
        CollectionAssert.AreEqual(new[] { "META-INF/", "docs/" }, native.ExtractExclude.ToArray());
    }

    [TestMethod]
    public void ToleratesCommentsAndTrailingCommas()
    {
        var file = VersionFile.Parse("""
            {
              // 手工编辑过的 JSON
              "mainClass": "Main",
              "libraries": [ { "name": "a:b:1", }, ],
            }
            """);

        Assert.AreEqual("Main", file.MainClass);
        Assert.HasCount(1, file.Libraries);
    }

    [TestMethod]
    public void LibraryWithoutNameIsRejected()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => VersionFile.Parse("""{ "libraries": [ { "rules": [] } ] }"""));
    }

    [TestMethod]
    public void NonObjectRootIsRejected()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => VersionFile.Parse("[]"));
    }
}
