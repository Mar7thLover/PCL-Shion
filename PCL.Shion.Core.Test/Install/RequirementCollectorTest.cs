using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Install;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Sources;
using System.Text.Json;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class RequirementCollectorTest
{
    private static readonly RuleEnvironment Windows = CreateEnvironment("windows", "x86_64");
    private static readonly RuleEnvironment Windows32 = CreateEnvironment("windows", "x86");
    private static readonly RuleEnvironment Linux = CreateEnvironment("linux", "x86_64");
    private static readonly RuleEnvironment Mac = CreateEnvironment("osx", "arm64");

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
    public void CollectsClientJarLibrariesAssetIndexAndLogConfiguration()
    {
        _root.WriteVersion("26.3", ModernJson);

        var items = Collect("26.3", Windows);

        CollectionAssert.AreEqual(
            new[]
            {
                _root.GetPath("versions/26.3/26.3.jar"),
                _root.GetPath("libraries/at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar"),
                _root.GetPath("libraries/com/mojang/jtracy/1.14.38/jtracy-1.14.38-natives-windows.jar"),
                _root.GetPath("assets/indexes/34.json"),
                _root.GetPath("assets/log_configs/client-1.21.2.xml")
            },
            items.Select(item => item.TargetPath).ToArray());

        AssertItem(
            items[0],
            "https://piston-data.mojang.com/v1/objects/e877b6a07acd633fb3bb475002175cec036e7b87/client.jar",
            41483720,
            "e877b6a07acd633fb3bb475002175cec036e7b87");
        AssertItem(
            items[1],
            "https://libraries.minecraft.net/at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar",
            910232,
            "f541d7f910fe3d76f38f799c507c48cc81b12ecb");
        AssertItem(
            items[2],
            "https://libraries.minecraft.net/com/mojang/jtracy/1.14.38/jtracy-1.14.38-natives-windows.jar",
            49885,
            "3e26b44141ba853b45f734f643a6a8ac8b8fc259");
        AssertItem(
            items[3],
            "https://piston-meta.mojang.com/v1/packages/0c8f43b28f837869c1e54a6aeb22c396a1548946/34.json",
            597035,
            "0c8f43b28f837869c1e54a6aeb22c396a1548946");
        AssertItem(
            items[4],
            "https://piston-data.mojang.com/v1/objects/39384bd14c0606d812afec88d8aff595b2587dd9/client-1.21.2.xml",
            1073,
            "39384bd14c0606d812afec88d8aff595b2587dd9");
    }

    [TestMethod]
    public void OfficialSourceIsTriedBeforeTheMirror()
    {
        _root.WriteVersion("26.3", ModernJson);

        var items = Collect("26.3", Windows);

        CollectionAssert.AreEqual(
            new[]
            {
                "https://libraries.minecraft.net/at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar",
                "https://bmclapi2.bangbang93.com/maven/at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar"
            },
            items[1].Urls.Select(url => url.AbsoluteUri).ToArray());
    }

    [TestMethod]
    public void PreferredMirrorIsTriedBeforeTheOfficialSource()
    {
        _root.WriteVersion("26.3", ModernJson);

        var items = Collect("26.3", Windows, DownloadSource.Bmclapi);

        CollectionAssert.AreEqual(
            new[]
            {
                "https://bmclapi2.bangbang93.com/v1/objects/e877b6a07acd633fb3bb475002175cec036e7b87/client.jar",
                "https://piston-data.mojang.com/v1/objects/e877b6a07acd633fb3bb475002175cec036e7b87/client.jar"
            },
            items[0].Urls.Select(url => url.AbsoluteUri).ToArray());
        Assert.AreEqual(
            "https://bmclapi2.bangbang93.com/maven/at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar",
            items[1].Urls[0].AbsoluteUri);
    }

    [TestMethod]
    public void SelectsLibrariesByTheRulesOfEachSystem()
    {
        _root.WriteVersion("26.3", ModernJson);

        CollectionAssert.AreEqual(
            new[]
            {
                "at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar",
                "com/mojang/jtracy/1.14.38/jtracy-1.14.38-natives-linux.jar"
            },
            LibraryPaths(Collect("26.3", Linux)));

        // java-objc-bridge 在原 JSON 中出现了两次，只下载一次
        CollectionAssert.AreEqual(
            new[]
            {
                "at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar",
                "ca/weblite/java-objc-bridge/1.1/java-objc-bridge-1.1.jar",
                "com/mojang/jtracy/1.14.38/jtracy-1.14.38-natives-macos.jar"
            },
            LibraryPaths(Collect("26.3", Mac)));
    }

    [TestMethod]
    public void LegacyNativesUseTheClassifierOfTheSystem()
    {
        _root.WriteVersion("legacy", LegacyNativesJson);

        var windows = Collect("legacy", Windows);
        var windows32 = Collect("legacy", Windows32);
        var linux = Collect("legacy", Linux);

        Assert.HasCount(2, windows);
        AssertItem(
            windows[0],
            "https://libraries.minecraft.net/org/lwjgl/lwjgl/lwjgl-platform/2.9.4-nightly-20150209/lwjgl-platform-2.9.4-nightly-20150209-natives-windows.jar",
            613748,
            "b84d5102b9dbfabfeb5e43c7e2828d98a7fc80e0");
        AssertItem(
            windows[1],
            "https://libraries.minecraft.net/tv/twitch/twitch-platform/5.16/twitch-platform-5.16-natives-windows-64.jar",
            474225,
            "b7a1e8ca48a2b9a8f5c4b6e2d0c3a7f9e1d2c3b4");
        Assert.AreEqual(
            _root.GetPath("libraries/tv/twitch/twitch-platform/5.16/twitch-platform-5.16-natives-windows-32.jar"),
            windows32[1].TargetPath);

        // 只有 Windows 的 natives 才声明了下载信息，Linux 的按名称推导路径并从 Mojang 库服务器下载
        Assert.HasCount(1, linux);
        Assert.AreEqual(
            _root.GetPath("libraries/org/lwjgl/lwjgl/lwjgl-platform/2.9.4-nightly-20150209/lwjgl-platform-2.9.4-nightly-20150209-natives-linux.jar"),
            linux[0].TargetPath);
        AssertItem(
            linux[0],
            "https://libraries.minecraft.net/org/lwjgl/lwjgl/lwjgl-platform/2.9.4-nightly-20150209/lwjgl-platform-2.9.4-nightly-20150209-natives-linux.jar",
            null,
            null);
    }

    [TestMethod]
    public void LibraryWithoutUrlFallsBackToMojangLibraries()
    {
        _root.WriteVersion("old", """
            {
              "mainClass": "net.minecraft.client.main.Main",
              "libraries": [
                { "name": "com.mojang:authlib:1.5.25" },
                {
                  "name": "org.ow2.asm:asm:9.7",
                  "downloads": { "artifact": { "path": "org/ow2/asm/asm/9.7/asm-9.7.jar", "sha1": "073d7b3086e14beb604ced229c302feff6449723", "size": 125428 } }
                }
              ]
            }
            """);

        var items = Collect("old", Windows);

        Assert.HasCount(2, items);
        Assert.AreEqual(_root.GetPath("libraries/com/mojang/authlib/1.5.25/authlib-1.5.25.jar"), items[0].TargetPath);
        AssertItem(items[0], "https://libraries.minecraft.net/com/mojang/authlib/1.5.25/authlib-1.5.25.jar", null, null);
        AssertItem(items[1], "https://libraries.minecraft.net/org/ow2/asm/asm/9.7/asm-9.7.jar", 125428, "073d7b3086e14beb604ced229c302feff6449723");
    }

    [TestMethod]
    public void LibraryWithoutDownloadsUsesItsOwnRepository()
    {
        _root.WriteVersion("fabric", """
            {
              "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
              "libraries": [
                { "name": "net.fabricmc:fabric-loader:0.16.5", "url": "https://maven.fabricmc.net/" },
                { "name": "net.fabricmc:intermediary:1.21.1", "url": "https://maven.fabricmc.net" }
              ]
            }
            """);

        var items = Collect("fabric", Windows);

        // 第三方仓库没有镜像，只有一个地址
        CollectionAssert.AreEqual(
            new[] { "https://maven.fabricmc.net/net/fabricmc/fabric-loader/0.16.5/fabric-loader-0.16.5.jar" },
            items[0].Urls.Select(url => url.AbsoluteUri).ToArray());
        CollectionAssert.AreEqual(
            new[] { "https://maven.fabricmc.net/net/fabricmc/intermediary/1.21.1/intermediary-1.21.1.jar" },
            items[1].Urls.Select(url => url.AbsoluteUri).ToArray());
    }

    [TestMethod]
    public void LibraryWithEmptyUrlIsSkipped()
    {
        // 摘自 Forge 1.21.1-52.1.20 实例：client 库由安装器在本地生成，地址为空
        _root.WriteVersion("forge", """
            {
              "mainClass": "net.minecraftforge.bootstrap.ForgeBootstrap",
              "libraries": [
                {
                  "name": "net.minecraftforge:forge:1.21.1-52.1.20:universal",
                  "downloads": {
                    "artifact": {
                      "path": "net/minecraftforge/forge/1.21.1-52.1.20/forge-1.21.1-52.1.20-universal.jar",
                      "url": "https://maven.minecraftforge.net/net/minecraftforge/forge/1.21.1-52.1.20/forge-1.21.1-52.1.20-universal.jar",
                      "sha1": "ab1be5668bbde6216799c1a37ee48803dedbbbc4",
                      "size": 2965568
                    }
                  }
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
                }
              ]
            }
            """);

        var items = Collect("forge", Windows);

        Assert.HasCount(1, items);
        Assert.AreEqual(
            _root.GetPath("libraries/net/minecraftforge/forge/1.21.1-52.1.20/forge-1.21.1-52.1.20-universal.jar"),
            items[0].TargetPath);
        AssertItem(
            items[0],
            "https://maven.minecraftforge.net/net/minecraftforge/forge/1.21.1-52.1.20/forge-1.21.1-52.1.20-universal.jar",
            2965568,
            "ab1be5668bbde6216799c1a37ee48803dedbbbc4");
    }

    [TestMethod]
    public void InheritedVersionUsesTheParentClientJarAndTheNewerLibrary()
    {
        _root.WriteVersion("1.21.1", """
            {
              "mainClass": "net.minecraft.client.main.Main",
              "assetIndex": { "id": "17", "sha1": "e61ea955de95de1f99d36616a8b46147e9f6f5d7", "size": 449557, "url": "https://piston-meta.mojang.com/v1/packages/e61ea955de95de1f99d36616a8b46147e9f6f5d7/17.json" },
              "downloads": { "client": { "sha1": "30c73b1c5da787909b2f73340419fdf13b9def88", "size": 26836906, "url": "https://piston-data.mojang.com/v1/objects/30c73b1c5da787909b2f73340419fdf13b9def88/client.jar" } },
              "libraries": [
                {
                  "name": "org.ow2.asm:asm:9.6",
                  "downloads": { "artifact": { "path": "org/ow2/asm/asm/9.6/asm-9.6.jar", "sha1": "aa205cf0a06dbd8e04ece91c0b37c3f5d567546a", "size": 123598, "url": "https://libraries.minecraft.net/org/ow2/asm/asm/9.6/asm-9.6.jar" } }
                }
              ]
            }
            """);
        _root.WriteVersion("fabric-1.21.1", """
            {
              "inheritsFrom": "1.21.1",
              "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
              "libraries": [
                { "name": "org.ow2.asm:asm:9.7", "url": "https://maven.fabricmc.net/" }
              ]
            }
            """);

        var items = Collect("fabric-1.21.1", Windows);

        CollectionAssert.AreEqual(
            new[]
            {
                _root.GetPath("versions/1.21.1/1.21.1.jar"),
                _root.GetPath("libraries/org/ow2/asm/asm/9.7/asm-9.7.jar"),
                _root.GetPath("assets/indexes/17.json")
            },
            items.Select(item => item.TargetPath).ToArray());
        Assert.AreEqual("30c73b1c5da787909b2f73340419fdf13b9def88", items[0].Sha1);
        AssertItem(items[1], "https://maven.fabricmc.net/org/ow2/asm/asm/9.7/asm-9.7.jar", null, null);
    }

    [TestMethod]
    public void RenamedInstanceDownloadsTheClientJarUnderItsOwnName()
    {
        _root.WriteVersion("My World", ModernJson.Replace("\"id\": \"26.3\"", "\"id\": \"My World\""));

        var items = Collect("My World", Windows);

        Assert.AreEqual(_root.GetPath("versions/My World/My World.jar"), items[0].TargetPath);
        Assert.AreEqual("e877b6a07acd633fb3bb475002175cec036e7b87", items[0].Sha1);
    }

    [TestMethod]
    public void FilesWithoutDownloadInformationAreLeftOut()
    {
        _root.WriteVersion("local", """
            {
              "mainClass": "net.minecraft.client.Minecraft",
              "assets": "legacy",
              "downloads": { "client": { "sha1": "43db9b498cb67058d2e12d394e6507722e71bb45" } },
              "logging": { "client": { "argument": "-Dlog4j.configurationFile=${path}", "file": { "id": "client-1.7.xml" } } }
            }
            """);

        Assert.HasCount(0, Collect("local", Windows));
    }

    [TestMethod]
    public void ListsEachAssetObjectOnce()
    {
        _root.WriteFile("assets/indexes/17.json", """
            {
              "objects": {
                "icons/icon_16x16.png": { "hash": "5ff04807c356f1beed0b86ccf659b44b9983e3fa", "size": 781 },
                "minecraft/sounds/a.ogg": { "hash": "b62ca8ec10d07e6bf5ac8dae0c8c1d2e6a1e3356", "size": 9101 },
                "minecraft/sounds/b.ogg": { "hash": "b62ca8ec10d07e6bf5ac8dae0c8c1d2e6a1e3356", "size": 9101 }
              }
            }
            """);

        var items = RequirementCollector.CollectAssetObjects(_root.RootPath, "17", DownloadSource.Official);

        Assert.HasCount(2, items);
        Assert.AreEqual(_root.GetPath("assets/objects/5f/5ff04807c356f1beed0b86ccf659b44b9983e3fa"), items[0].TargetPath);
        AssertItem(
            items[0],
            "https://resources.download.minecraft.net/5f/5ff04807c356f1beed0b86ccf659b44b9983e3fa",
            781,
            "5ff04807c356f1beed0b86ccf659b44b9983e3fa");
        Assert.AreEqual(_root.GetPath("assets/objects/b6/b62ca8ec10d07e6bf5ac8dae0c8c1d2e6a1e3356"), items[1].TargetPath);
    }

    [TestMethod]
    public void VirtualIndexObjectsAreDownloadedToObjects()
    {
        _root.WriteFile("assets/indexes/legacy.json", """
            {
              "virtual": true,
              "objects": {
                "sound/ding.ogg": { "hash": "8030dd9dc315c0381d52c4782ea36c6baf6e8135", "size": 19642 }
              }
            }
            """);

        var items = RequirementCollector.CollectAssetObjects(_root.RootPath, "legacy", DownloadSource.Bmclapi);

        Assert.HasCount(1, items);
        Assert.AreEqual(_root.GetPath("assets/objects/80/8030dd9dc315c0381d52c4782ea36c6baf6e8135"), items[0].TargetPath);
        CollectionAssert.AreEqual(
            new[]
            {
                "https://bmclapi2.bangbang93.com/assets/80/8030dd9dc315c0381d52c4782ea36c6baf6e8135",
                "https://resources.download.minecraft.net/80/8030dd9dc315c0381d52c4782ea36c6baf6e8135"
            },
            items[0].Urls.Select(url => url.AbsoluteUri).ToArray());
    }

    [TestMethod]
    [DataRow("../outside.jar")]
    [DataRow("..\\outside.jar")]
    [DataRow("C:\\outside.jar")]
    [DataRow("file.jar:payload")]
    public void LibraryDownloadPathsMustStayInsideLibraries(string path)
    {
        _root.WriteVersion("unsafe", $$"""
            { "mainClass": "Main", "libraries": [
              { "name": "example:library:1", "downloads": { "artifact": {
                "path": {{JsonSerializer.Serialize(path)}}, "url": "https://example.invalid/library.jar"
              } } }
            ] }
            """);

        Assert.ThrowsExactly<InvalidDataException>(() => Collect("unsafe", Windows));
    }

    [TestMethod]
    public void IndexAndLogConfigurationNamesCannotEscapeTheirDirectories()
    {
        _root.WriteVersion("index", """
            { "mainClass": "Main", "assetIndex": { "id": "../outside", "url": "https://example.invalid/index.json" } }
            """);
        _root.WriteVersion("logging", """
            { "mainClass": "Main", "logging": { "client": {
                "argument": "-Dlog4j.configurationFile=${path}",
                "file": { "id": "../outside.xml", "url": "https://example.invalid/logging.xml" }
            } } }
            """);

        Assert.ThrowsExactly<InvalidDataException>(() => Collect("index", Windows));
        Assert.ThrowsExactly<InvalidDataException>(() => Collect("logging", Windows));
    }

    [TestMethod]
    [DataRow("a", 1L)]
    [DataRow("../../outside", 1L)]
    [DataRow("5ff04807c356f1beed0b86ccf659b44b9983e3fa", -1L)]
    public void InvalidAssetHashesAndSizesAreRejected(string hash, long size)
    {
        _root.WriteFile("assets/indexes/bad.json", $$"""
            { "objects": { "file": { "hash": {{JsonSerializer.Serialize(hash)}}, "size": {{size}} } } }
            """);

        Assert.ThrowsExactly<InvalidDataException>(() => RequirementCollector.CollectAssetObjects(_root.RootPath, "bad", DownloadSource.Official));
    }

    [TestMethod]
    public void MissingAssetIndexIsReportedAsIoError()
    {
        Assert.Throws<IOException>(() => RequirementCollector.CollectAssetObjects(_root.RootPath, "17", DownloadSource.Official));
    }

    /// <summary>
    /// 摘自 26.3 的版本 JSON：普通库、只在 macOS 上使用且重复列出的库，以及各系统的新式 natives。
    /// </summary>
    private const string ModernJson = """
        {
          "assetIndex": {
            "id": "34",
            "sha1": "0c8f43b28f837869c1e54a6aeb22c396a1548946",
            "size": 597035,
            "totalSize": 484894038,
            "url": "https://piston-meta.mojang.com/v1/packages/0c8f43b28f837869c1e54a6aeb22c396a1548946/34.json"
          },
          "assets": "34",
          "downloads": {
            "client": {
              "sha1": "e877b6a07acd633fb3bb475002175cec036e7b87",
              "size": 41483720,
              "url": "https://piston-data.mojang.com/v1/objects/e877b6a07acd633fb3bb475002175cec036e7b87/client.jar"
            },
            "server": {
              "sha1": "33680f5f2ac32864d6d7cf5e56a705fdb3e05f4c",
              "size": 62294556,
              "url": "https://piston-data.mojang.com/v1/objects/33680f5f2ac32864d6d7cf5e56a705fdb3e05f4c/server.jar"
            }
          },
          "id": "26.3",
          "javaVersion": { "component": "java-runtime-epsilon", "majorVersion": 25 },
          "libraries": [
            {
              "downloads": { "artifact": { "path": "at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar", "sha1": "f541d7f910fe3d76f38f799c507c48cc81b12ecb", "size": 910232, "url": "https://libraries.minecraft.net/at/yawk/lz4/lz4-java/1.10.1/lz4-java-1.10.1.jar" } },
              "name": "at.yawk.lz4:lz4-java:1.10.1"
            },
            {
              "downloads": { "artifact": { "path": "ca/weblite/java-objc-bridge/1.1/java-objc-bridge-1.1.jar", "sha1": "1227f9e0666314f9de41477e3ec277e542ed7f7b", "size": 1330045, "url": "https://libraries.minecraft.net/ca/weblite/java-objc-bridge/1.1/java-objc-bridge-1.1.jar" } },
              "name": "ca.weblite:java-objc-bridge:1.1",
              "rules": [ { "action": "allow", "os": { "name": "osx" } } ]
            },
            {
              "downloads": { "artifact": { "path": "ca/weblite/java-objc-bridge/1.1/java-objc-bridge-1.1.jar", "sha1": "1227f9e0666314f9de41477e3ec277e542ed7f7b", "size": 1330045, "url": "https://libraries.minecraft.net/ca/weblite/java-objc-bridge/1.1/java-objc-bridge-1.1.jar" } },
              "name": "ca.weblite:java-objc-bridge:1.1",
              "rules": [ { "action": "allow", "os": { "name": "osx" } } ]
            },
            {
              "downloads": { "artifact": { "path": "com/mojang/jtracy/1.14.38/jtracy-1.14.38-natives-linux.jar", "sha1": "d2433300f8a4e99595d513546d19ecddc3eaa15e", "size": 256636, "url": "https://libraries.minecraft.net/com/mojang/jtracy/1.14.38/jtracy-1.14.38-natives-linux.jar" } },
              "name": "com.mojang:jtracy:1.14.38:natives-linux",
              "rules": [ { "action": "allow", "os": { "name": "linux" } } ]
            },
            {
              "downloads": { "artifact": { "path": "com/mojang/jtracy/1.14.38/jtracy-1.14.38-natives-macos.jar", "sha1": "5ef8d24352733e915e35e8c39efb099520bcabcf", "size": 228428, "url": "https://libraries.minecraft.net/com/mojang/jtracy/1.14.38/jtracy-1.14.38-natives-macos.jar" } },
              "name": "com.mojang:jtracy:1.14.38:natives-macos",
              "rules": [ { "action": "allow", "os": { "name": "osx" } } ]
            },
            {
              "downloads": { "artifact": { "path": "com/mojang/jtracy/1.14.38/jtracy-1.14.38-natives-windows.jar", "sha1": "3e26b44141ba853b45f734f643a6a8ac8b8fc259", "size": 49885, "url": "https://libraries.minecraft.net/com/mojang/jtracy/1.14.38/jtracy-1.14.38-natives-windows.jar" } },
              "name": "com.mojang:jtracy:1.14.38:natives-windows",
              "rules": [ { "action": "allow", "os": { "name": "windows" } } ]
            }
          ],
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
          },
          "mainClass": "net.minecraft.client.main.Main",
          "type": "release"
        }
        """;

    /// <summary>
    /// 旧式 natives：按系统选 classifier，Windows 的 twitch 库还按位数区分。
    /// </summary>
    private const string LegacyNativesJson = """
        {
          "mainClass": "net.minecraft.client.main.Main",
          "libraries": [
            {
              "name": "org.lwjgl.lwjgl:lwjgl-platform:2.9.4-nightly-20150209",
              "natives": { "linux": "natives-linux", "windows": "natives-windows" },
              "extract": { "exclude": [ "META-INF/" ] },
              "downloads": {
                "classifiers": {
                  "natives-windows": {
                    "path": "org/lwjgl/lwjgl/lwjgl-platform/2.9.4-nightly-20150209/lwjgl-platform-2.9.4-nightly-20150209-natives-windows.jar",
                    "sha1": "b84d5102b9dbfabfeb5e43c7e2828d98a7fc80e0",
                    "size": 613748,
                    "url": "https://libraries.minecraft.net/org/lwjgl/lwjgl/lwjgl-platform/2.9.4-nightly-20150209/lwjgl-platform-2.9.4-nightly-20150209-natives-windows.jar"
                  }
                }
              }
            },
            {
              "name": "tv.twitch:twitch-platform:5.16",
              "natives": { "windows": "natives-windows-${arch}" },
              "extract": { "exclude": [ "META-INF/" ] },
              "downloads": {
                "classifiers": {
                  "natives-windows-32": {
                    "path": "tv/twitch/twitch-platform/5.16/twitch-platform-5.16-natives-windows-32.jar",
                    "sha1": "c8b2f9db59b3c0b9a6d5c7f3e1d4b8a0f2e3d4c5",
                    "size": 386447,
                    "url": "https://libraries.minecraft.net/tv/twitch/twitch-platform/5.16/twitch-platform-5.16-natives-windows-32.jar"
                  },
                  "natives-windows-64": {
                    "path": "tv/twitch/twitch-platform/5.16/twitch-platform-5.16-natives-windows-64.jar",
                    "sha1": "b7a1e8ca48a2b9a8f5c4b6e2d0c3a7f9e1d2c3b4",
                    "size": 474225,
                    "url": "https://libraries.minecraft.net/tv/twitch/twitch-platform/5.16/twitch-platform-5.16-natives-windows-64.jar"
                  }
                }
              }
            }
          ]
        }
        """;

    private IReadOnlyList<DownloadItem> Collect(string instance, RuleEnvironment environment, DownloadSource source = DownloadSource.Official)
    {
        var version = VersionResolver.Resolve(_root.RootPath, instance);
        return RequirementCollector.Collect(_root.RootPath, version, source, environment);
    }

    /// <summary>
    /// 取出库的相对路径（以 / 分隔），便于断言。
    /// </summary>
    private string[] LibraryPaths(IReadOnlyList<DownloadItem> items)
    {
        var librariesDirectory = _root.GetPath("libraries") + Path.DirectorySeparatorChar;
        return items
            .Where(item => item.TargetPath.StartsWith(librariesDirectory, StringComparison.Ordinal))
            .Select(item => item.TargetPath[librariesDirectory.Length..].Replace(Path.DirectorySeparatorChar, '/'))
            .ToArray();
    }

    /// <summary>
    /// 断言首选地址（官方源时即官方地址）以及大小与校验值。
    /// </summary>
    private static void AssertItem(DownloadItem item, string firstUrl, long? size, string? sha1)
    {
        Assert.AreEqual(firstUrl, item.Urls[0].AbsoluteUri);
        Assert.AreEqual(size, item.Size);
        Assert.AreEqual(sha1, item.Sha1);
    }

    private static RuleEnvironment CreateEnvironment(string osName, string arch)
    {
        return new RuleEnvironment(osName, arch, "10.0", new HashSet<string>());
    }
}
