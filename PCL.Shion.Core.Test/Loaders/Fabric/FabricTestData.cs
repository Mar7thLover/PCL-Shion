using System.Security.Cryptography;
using System.Text;
using PCL.Shion.Core.Loaders;

namespace PCL.Shion.Core.Test.Loaders.Fabric;

/// <summary>
/// Fabric 与 Quilt 测试共用的假服务端内容。meta 的响应按真实接口的格式构造，保留了解析时不使用的字段。
/// </summary>
internal static class FabricTestData
{
    public const string MinecraftVersion = "1.21.1";

    public const string FabricHost = "meta.fabricmc.net";
    public const string QuiltHost = "meta.quiltmc.org";
    public const string MirrorHost = "bmclapi2.bangbang93.com";

    public const string FabricListUrl = "https://meta.fabricmc.net/v2/versions/loader/1.21.1";
    public const string FabricListMirrorUrl = "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/1.21.1";
    public const string FabricProfileUrl = "https://meta.fabricmc.net/v2/versions/loader/1.21.1/0.16.9/profile/json";
    public const string FabricProfileMirrorUrl = "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/1.21.1/0.16.9/profile/json";
    public const string QuiltListUrl = "https://meta.quiltmc.org/v3/versions/loader/1.21.1";
    public const string QuiltListMirrorUrl = "https://bmclapi2.bangbang93.com/quilt-meta/v3/versions/loader/1.21.1";
    public const string QuiltProfileUrl = "https://meta.quiltmc.org/v3/versions/loader/1.21.1/0.26.4/profile/json";
    public const string QuiltProfileMirrorUrl = "https://bmclapi2.bangbang93.com/quilt-meta/v3/versions/loader/1.21.1/0.26.4/profile/json";

    public const string IntermediaryJarUrl = "https://maven.fabricmc.net/net/fabricmc/intermediary/1.21.1/intermediary-1.21.1.jar";
    public const string FabricLoaderJarUrl = "https://maven.fabricmc.net/net/fabricmc/fabric-loader/0.16.9/fabric-loader-0.16.9.jar";
    public const string QuiltLoaderJarUrl = "https://maven.quiltmc.org/repository/release/org/quiltmc/quilt-loader/0.26.4/quilt-loader-0.26.4.jar";

    public const string ManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    public const string VanillaJsonUrl = "https://piston-meta.mojang.com/v1/packages/0a0b/1.21.1.json";
    public const string ClientJarUrl = "https://piston-data.mojang.com/v1/objects/0c0d/client.jar";

    public const string IntermediaryJarPath = "libraries/net/fabricmc/intermediary/1.21.1/intermediary-1.21.1.jar";
    public const string FabricLoaderJarPath = "libraries/net/fabricmc/fabric-loader/0.16.9/fabric-loader-0.16.9.jar";
    public const string QuiltLoaderJarPath = "libraries/org/quiltmc/quilt-loader/0.26.4/quilt-loader-0.26.4.jar";

    public const string FabricJvmArgument = "-DFabricMcEmu= net.minecraft.client.main.Main ";

    public static readonly LoaderVersion FabricLoader = new(LoaderKind.Fabric, "0.16.9", MinecraftVersion, true);
    public static readonly LoaderVersion QuiltLoader = new(LoaderKind.Quilt, "0.26.4", MinecraftVersion, true);

    public static readonly byte[] IntermediaryJar = "intermediary"u8.ToArray();
    public static readonly byte[] FabricLoaderJar = "fabric loader"u8.ToArray();
    public static readonly byte[] QuiltLoaderJar = "quilt loader"u8.ToArray();
    public static readonly byte[] ClientJar = "client jar"u8.ToArray();

    /// <summary>
    /// 已安装在本地、不含任何下载信息的原版版本 JSON。
    /// </summary>
    public const string InstalledVanillaJson = """
        {
          "id": "1.21.1",
          "type": "release",
          "mainClass": "net.minecraft.client.main.Main",
          "assets": "17",
          "libraries": []
        }
        """;

    public const string FabricList = """
        [
          {
            "loader": { "separator": ".", "build": 3, "maven": "net.fabricmc:fabric-loader:0.17.0-beta.1", "version": "0.17.0-beta.1", "stable": false },
            "intermediary": { "maven": "net.fabricmc:intermediary:1.21.1", "version": "1.21.1", "stable": true },
            "launcherMeta": { "version": 2, "min_java_version": 17 }
          },
          {
            "loader": { "separator": ".", "build": 2, "maven": "net.fabricmc:fabric-loader:0.16.9", "version": "0.16.9", "stable": true },
            "intermediary": { "maven": "net.fabricmc:intermediary:1.21.1", "version": "1.21.1", "stable": true },
            "launcherMeta": { "version": 2, "min_java_version": 8 }
          },
          {
            "loader": { "separator": ".", "build": 1, "maven": "net.fabricmc:fabric-loader:0.16.8", "version": "0.16.8", "stable": true },
            "intermediary": { "maven": "net.fabricmc:intermediary:1.21.1", "version": "1.21.1", "stable": true },
            "launcherMeta": { "version": 2, "min_java_version": 8 }
          }
        ]
        """;

    /// <summary>
    /// Quilt 的条目没有 stable 字段。
    /// </summary>
    public const string QuiltList = """
        [
          {
            "loader": { "separator": ".", "build": 3, "maven": "org.quiltmc:quilt-loader:0.27.0-beta.2", "version": "0.27.0-beta.2" },
            "hashed": { "maven": "org.quiltmc:hashed:1.21.1", "version": "1.21.1" },
            "intermediary": { "maven": "net.fabricmc:intermediary:1.21.1", "version": "1.21.1" },
            "launcherMeta": { "version": 1 }
          },
          {
            "loader": { "separator": ".", "build": 2, "maven": "org.quiltmc:quilt-loader:0.27.0-rc.1", "version": "0.27.0-rc.1" },
            "hashed": { "maven": "org.quiltmc:hashed:1.21.1", "version": "1.21.1" },
            "intermediary": { "maven": "net.fabricmc:intermediary:1.21.1", "version": "1.21.1" },
            "launcherMeta": { "version": 1 }
          },
          {
            "loader": { "separator": ".", "build": 1, "maven": "org.quiltmc:quilt-loader:0.26.4", "version": "0.26.4" },
            "hashed": { "maven": "org.quiltmc:hashed:1.21.1", "version": "1.21.1" },
            "intermediary": { "maven": "net.fabricmc:intermediary:1.21.1", "version": "1.21.1" },
            "launcherMeta": { "version": 1 }
          }
        ]
        """;

    public const string FabricProfile = """
        {
          "id": "fabric-loader-0.16.9-1.21.1",
          "inheritsFrom": "1.21.1",
          "releaseTime": "2024-10-14T08:12:44+0000",
          "time": "2024-10-14T08:12:44+0000",
          "type": "release",
          "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
          "arguments": {
            "game": [],
            "jvm": [ "-DFabricMcEmu= net.minecraft.client.main.Main " ]
          },
          "libraries": [
            { "name": "net.fabricmc:intermediary:1.21.1", "url": "https://maven.fabricmc.net/" },
            { "name": "net.fabricmc:fabric-loader:0.16.9", "url": "https://maven.fabricmc.net/" }
          ]
        }
        """;

    public const string QuiltProfile = """
        {
          "id": "quilt-loader-0.26.4-1.21.1",
          "inheritsFrom": "1.21.1",
          "releaseTime": "2024-10-14T08:12:44+0000",
          "time": "2024-10-14T08:12:44+0000",
          "type": "release",
          "mainClass": "org.quiltmc.loader.impl.launch.knot.KnotClient",
          "arguments": {
            "game": [],
            "jvm": [ "-DFabricMcEmu= net.minecraft.client.main.Main " ]
          },
          "libraries": [
            { "name": "net.fabricmc:intermediary:1.21.1", "url": "https://maven.fabricmc.net/" },
            { "name": "org.quiltmc:quilt-loader:0.26.4", "url": "https://maven.quiltmc.org/repository/release/" }
          ]
        }
        """;

    /// <summary>
    /// 尚未安装时由版本清单提供的原版版本 JSON，带客户端 jar 的下载信息。
    /// </summary>
    public static readonly string RemoteVanillaJson = $$"""
        {
          "id": "1.21.1",
          "type": "release",
          "mainClass": "net.minecraft.client.main.Main",
          "assets": "17",
          "downloads": { "client": { "sha1": "{{Sha1(ClientJar)}}", "size": {{ClientJar.Length}}, "url": "{{ClientJarUrl}}" } },
          "libraries": []
        }
        """;

    public static readonly string Manifest = $$"""
        {
          "latest": { "release": "1.21.1", "snapshot": "1.21.1" },
          "versions": [
            {
              "id": "1.21.1",
              "type": "release",
              "url": "{{VanillaJsonUrl}}",
              "releaseTime": "2024-08-08T12:24:45+00:00",
              "sha1": "{{Sha1(Encoding.UTF8.GetBytes(RemoteVanillaJson))}}"
            }
          ]
        }
        """;

    /// <summary>
    /// 一个提供完整内容的假服务端：Fabric 与 Quilt 在官方与 BMCLAPI 两处的列表和启动配置、它们的库，以及版本清单、原版版本 JSON 与客户端 jar。
    /// </summary>
    public static FakeMetaHandler CreateServer()
    {
        var server = new FakeMetaHandler();
        server.Add(FabricListUrl, FabricList);
        server.Add(FabricListMirrorUrl, FabricList);
        server.Add(FabricProfileUrl, FabricProfile);
        server.Add(FabricProfileMirrorUrl, FabricProfile);
        server.Add(QuiltListUrl, QuiltList);
        server.Add(QuiltListMirrorUrl, QuiltList);
        server.Add(QuiltProfileUrl, QuiltProfile);
        server.Add(QuiltProfileMirrorUrl, QuiltProfile);
        server.Add(IntermediaryJarUrl, IntermediaryJar);
        server.Add(FabricLoaderJarUrl, FabricLoaderJar);
        server.Add(QuiltLoaderJarUrl, QuiltLoaderJar);
        server.Add(ManifestUrl, Manifest);
        server.Add(VanillaJsonUrl, RemoteVanillaJson);
        server.Add(ClientJarUrl, ClientJar);
        return server;
    }

    private static string Sha1(byte[] data)
    {
        return Convert.ToHexStringLower(SHA1.HashData(data));
    }
}
