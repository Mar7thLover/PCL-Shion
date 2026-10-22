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
        Assert.AreEqual("java-runtime-delta", file.JavaComponent);
        Assert.AreEqual("1.21.1", file.ClientVersion);
        Assert.AreEqual("https://example.invalid/client.jar", file.ClientJarDownload?.Url);
        Assert.AreEqual("https://example.invalid/17.json", file.AssetIndexDownload?.Url);
        Assert.AreEqual("-Dlog4j.configurationFile=${path}", file.LoggingArgument);
        Assert.AreEqual("client-1.12.xml", file.LoggingFileId);
        Assert.IsNull(file.LoggingFileDownload);
    }

    [TestMethod]
    public void AssetIndexFallsBackToAssets()
    {
        var file = VersionFile.Parse("""{ "mainClass": "Main", "assets": "legacy" }""");

        Assert.AreEqual("legacy", file.AssetIndexId);
        Assert.IsNull(file.AssetIndexDownload);
        Assert.IsNull(file.JavaMajorVersion);
        Assert.IsNull(file.JavaComponent);
        Assert.IsNull(file.ClientJarDownload);
        Assert.IsNull(file.LoggingFileId);
    }

    [TestMethod]
    public void ParsesDownloadsOfVersionFiles()
    {
        // 摘自 26.3 的版本 JSON
        var file = VersionFile.Parse("""
            {
              "assetIndex": {
                "id": "34",
                "sha1": "0c8f43b28f837869c1e54a6aeb22c396a1548946",
                "size": 597035,
                "totalSize": 484894038,
                "url": "https://piston-meta.mojang.com/v1/packages/0c8f43b28f837869c1e54a6aeb22c396a1548946/34.json"
              },
              "downloads": {
                "client": {
                  "sha1": "e877b6a07acd633fb3bb475002175cec036e7b87",
                  "size": 41483720,
                  "url": "https://piston-data.mojang.com/v1/objects/e877b6a07acd633fb3bb475002175cec036e7b87/client.jar"
                }
              },
              "logging": {
                "client": {
                  "argument": "-Dlog4j.configurationFile=${path}",
                  "file": {
                    "id": "client-1.21.2.xml",
                    "sha1": "39384bd14c0606d812afec88d8aff595b2587dd9",
                    "size": 1073,
                    "url": "https://piston-data.mojang.com/v1/objects/39384bd14c0606d812afec88d8aff595b2587dd9/client-1.21.2.xml"
                  },
                  "type": "log4j2-xml"
                }
              }
            }
            """);

        Assert.AreEqual(
            new FileDownload(
                "https://piston-meta.mojang.com/v1/packages/0c8f43b28f837869c1e54a6aeb22c396a1548946/34.json",
                "0c8f43b28f837869c1e54a6aeb22c396a1548946",
                597035),
            file.AssetIndexDownload);
        Assert.AreEqual(
            new FileDownload(
                "https://piston-data.mojang.com/v1/objects/e877b6a07acd633fb3bb475002175cec036e7b87/client.jar",
                "e877b6a07acd633fb3bb475002175cec036e7b87",
                41483720),
            file.ClientJarDownload);
        Assert.AreEqual(
            new FileDownload(
                "https://piston-data.mojang.com/v1/objects/39384bd14c0606d812afec88d8aff595b2587dd9/client-1.21.2.xml",
                "39384bd14c0606d812afec88d8aff595b2587dd9",
                1073),
            file.LoggingFileDownload);
    }

    [TestMethod]
    public void DownloadWithoutUrlIsIgnored()
    {
        var file = VersionFile.Parse("""
            {
              "assetIndex": { "id": "17", "sha1": "e61ea955de95de1f99d36616a8b46147e9f6f5d7" },
              "downloads": { "client": { "sha1": "30c73b1c5da787909b2f73340419fdf13b9def88", "size": 26836906 } }
            }
            """);

        Assert.AreEqual("17", file.AssetIndexId);
        Assert.IsNull(file.AssetIndexDownload);
        Assert.IsNull(file.ClientJarDownload);
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
        Assert.AreEqual("net/sf/jopt-simple/jopt-simple/5.0.4/jopt-simple-5.0.4.jar", file.Libraries[0].Artifact?.Path);
        Assert.IsNull(file.Libraries[0].RepositoryUrl);
        Assert.IsNull(file.Libraries[1].Artifact);
        Assert.AreEqual("https://maven.fabricmc.net/", file.Libraries[1].RepositoryUrl);

        var native = file.Libraries[2];
        Assert.IsNull(native.Artifact);
        Assert.AreEqual("natives-windows-${arch}", native.Natives["windows"]);
        Assert.AreEqual("natives-linux", native.Natives["linux"]);
        Assert.AreEqual(
            "org/lwjgl/lwjgl/lwjgl-platform/2.9.4-nightly-20150209/lwjgl-platform-2.9.4-nightly-20150209-natives-windows.jar",
            native.Classifiers["natives-windows"].Path);
        CollectionAssert.AreEqual(new[] { "META-INF/", "docs/" }, native.ExtractExclude.ToArray());
    }

    [TestMethod]
    public void ParsesLibraryDownloadInformation()
    {
        var file = VersionFile.Parse("""
            {
              "libraries": [
                {
                  "downloads": {
                    "artifact": {
                      "path": "at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar",
                      "sha1": "f541d7f910fe3d76f38f799c507c48cc81b12ecb",
                      "size": 910232,
                      "url": "https://libraries.minecraft.net/at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar"
                    }
                  },
                  "name": "at.yawk.lz4:lz4-java:1.10.1"
                },
                {
                  "name": "net.minecraftforge:forge:1.21.1-52.1.20:client",
                  "downloads": {
                    "artifact": {
                      "path": "net/minecraftforge/forge/1.21.1-52.1.20/forge-1.21.1-52.1.20-client.jar",
                      "url": "",
                      "sha1": "28f7acaa0e9b1c2d2a7bf52c07ae16133bd3da4e",
                      "size": 28056385
                    }
                  }
                },
                {
                  "name": "tv.twitch:twitch-platform:5.16",
                  "natives": { "windows": "natives-windows-${arch}" },
                  "downloads": {
                    "classifiers": {
                      "natives-windows-64": {
                        "path": "tv/twitch/twitch-platform/5.16/twitch-platform-5.16-natives-windows-64.jar",
                        "sha1": "b7a1e8ca48a2b9a8f5c4b6e2d0c3a7f9e1d2c3b4",
                        "size": 474225,
                        "url": "https://libraries.minecraft.net/tv/twitch/twitch-platform/5.16/twitch-platform-5.16-natives-windows-64.jar"
                      },
                      "natives-windows-32": { "sha1": "c8b2f9db59b3c0b9a6d5c7f3e1d4b8a0f2e3d4c5" }
                    }
                  }
                }
              ]
            }
            """);

        Assert.AreEqual(
            new LibraryDownload(
                "at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar",
                "https://libraries.minecraft.net/at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar",
                "f541d7f910fe3d76f38f799c507c48cc81b12ecb",
                910232),
            file.Libraries[0].Artifact);

        // Forge 安装器在本地生成的库以空字符串声明地址，与未声明区分开
        Assert.AreEqual(string.Empty, file.Libraries[1].Artifact?.Url);
        Assert.AreEqual(28056385, file.Libraries[1].Artifact?.Size);

        var classifiers = file.Libraries[2].Classifiers;
        Assert.HasCount(2, classifiers);
        Assert.AreEqual(
            new LibraryDownload(
                "tv/twitch/twitch-platform/5.16/twitch-platform-5.16-natives-windows-64.jar",
                "https://libraries.minecraft.net/tv/twitch/twitch-platform/5.16/twitch-platform-5.16-natives-windows-64.jar",
                "b7a1e8ca48a2b9a8f5c4b6e2d0c3a7f9e1d2c3b4",
                474225),
            classifiers["natives-windows-64"]);
        Assert.AreEqual(new LibraryDownload(null, null, "c8b2f9db59b3c0b9a6d5c7f3e1d4b8a0f2e3d4c5", null), classifiers["natives-windows-32"]);
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
