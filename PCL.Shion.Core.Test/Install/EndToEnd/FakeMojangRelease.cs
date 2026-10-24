using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PCL.Shion.Core.Platform;
using PCL.Shion.Core.Versions;

namespace PCL.Shion.Core.Test.Install.EndToEnd;

/// <summary>
/// 一个精简但结构完整的原版 1.21.1：版本清单、版本 JSON、客户端 jar、各类库、日志配置、资源索引与资源对象。
/// 构造时全部登记到 <see cref="FakeMojangHandler"/>，地址与官方服务一致。
/// </summary>
internal sealed class FakeMojangRelease
{
    public const string VersionId = "1.21.1";
    public const string LanguageAsset = "minecraft/lang/en_us.json";
    public const string GsonJarPath = "libraries/" + GsonJar;
    public const string AssetIndexPath = "assets/indexes/17.json";
    public const string LogConfigPath = "assets/log_configs/client-1.12.xml";

    private const string GsonJar = "com/google/code/gson/gson/2.10.1/gson-2.10.1.jar";
    private const string LwjglJar = "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar";
    private const string LoggingJar = "com/mojang/logging/1.2.7/logging-1.2.7.jar";
    private const string ReleaseTime = "2024-08-08T12:24:45+00:00";

    private const string MetaHost = "https://piston-meta.mojang.com";
    private const string DataHost = "https://piston-data.mojang.com";
    private const string LibrariesHost = "https://libraries.minecraft.net";
    private const string ResourcesHost = "https://resources.download.minecraft.net";

    private static readonly string[] OsNames = ["windows", "linux", "osx"];

    /// <summary>
    /// 资源名与内容。最后两项内容相同，因此共用一个资源对象，和真实的资源索引一样。
    /// </summary>
    private static readonly (string Name, string Content)[] AssetSources =
    [
        ("icons/icon_16x16.png", "icon-16"),
        (LanguageAsset, """{"language.name":"English"}"""),
        ("minecraft/sounds/ding.ogg", "ding"),
        ("minecraft/sounds/ding_copy.ogg", "ding")
    ];

    private static readonly string[] GameArguments =
    [
        "--username", "${auth_player_name}",
        "--version", "${version_name}",
        "--gameDir", "${game_directory}",
        "--assetsDir", "${assets_root}",
        "--assetIndex", "${assets_index_name}",
        "--uuid", "${auth_uuid}",
        "--accessToken", "${auth_access_token}",
        "--versionType", "${version_type}"
    ];

    private static readonly string[] JvmArguments =
    [
        "-Djava.library.path=${natives_directory}",
        "-cp", "${classpath}"
    ];

    private readonly FakeMojangHandler _server;
    private readonly byte[] _clientJar = Text("client jar of 1.21.1");
    private readonly Dictionary<string, byte[]> _files = [];
    private readonly Dictionary<string, string> _assetHashes = [];

    public FakeMojangRelease(FakeMojangHandler server)
    {
        _server = server;
        ClientJarUrl = Serve($"{DataHost}/v1/objects/{Sha1(_clientJar)}/client.jar", _clientJar);

        var versionJson = Text(JsonSerializer.Serialize(new
        {
            id = VersionId,
            type = "release",
            mainClass = "net.minecraft.client.main.Main",
            assets = "17",
            assetIndex = PublishAssets(),
            javaVersion = new { component = "java-runtime-delta", majorVersion = 21 },
            downloads = new { client = new { sha1 = Sha1(_clientJar), size = _clientJar.Length, url = ClientJarUrl } },
            logging = new { client = PublishLogConfig() },
            arguments = new { game = GameArguments, jvm = JvmArguments },
            libraries = PublishLibraries()
        }));

        var versionUrl = Serve($"{MetaHost}/v1/packages/{Sha1(versionJson)}/{VersionId}.json", versionJson);
        _server.Add(VersionManifestClient.ManifestUrl, Text(JsonSerializer.Serialize(new
        {
            latest = new { release = VersionId, snapshot = VersionId },
            versions = new[]
            {
                new
                {
                    id = VersionId,
                    type = "release",
                    url = versionUrl,
                    time = ReleaseTime,
                    releaseTime = ReleaseTime,
                    sha1 = Sha1(versionJson),
                    complianceLevel = 1
                }
            }
        })));
    }

    public string ClientJarUrl { get; }

    public string LwjglJarUrl => $"{LibrariesHost}/{LwjglJar}";

    /// <summary>
    /// 其他系统的名称，它们的 natives 不应该被下载。
    /// </summary>
    public static IEnumerable<string> OtherOsNames => OsNames.Where(os => os != PlatformInfo.OsName);

    public static string VersionJsonPath(string instanceName)
    {
        return $"versions/{instanceName}/{instanceName}.json";
    }

    public static string ClientJarPath(string instanceName)
    {
        return $"versions/{instanceName}/{instanceName}.jar";
    }

    public string AssetObjectUrl(string assetName)
    {
        var hash = _assetHashes[assetName];
        return $"{ResourcesHost}/{hash[..2]}/{hash}";
    }

    public string AssetObjectPath(string assetName)
    {
        var hash = _assetHashes[assetName];
        return $"assets/objects/{hash[..2]}/{hash}";
    }

    /// <summary>
    /// 安装完成后应当存在的全部文件（不含版本 JSON，它的内容随实例名而变）：相对 .minecraft 的路径与内容。
    /// 只包含当前系统用得上的库。
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> GetInstalledFiles(string instanceName)
    {
        return new Dictionary<string, byte[]>(_files) { [ClientJarPath(instanceName)] = _clientJar };
    }

    private object PublishAssets()
    {
        var objects = new Dictionary<string, object>();
        var totalSize = 0;
        foreach (var (name, text) in AssetSources)
        {
            var content = Text(text);
            var hash = Sha1(content);
            _assetHashes[name] = hash;
            objects[name] = new { hash, size = content.Length };
            totalSize += content.Length;

            Serve(AssetObjectUrl(name), content);
            _files[AssetObjectPath(name)] = content;
        }

        var index = Text(JsonSerializer.Serialize(new { objects }));
        var url = Serve($"{MetaHost}/v1/packages/{Sha1(index)}/17.json", index);
        _files[AssetIndexPath] = index;
        return new { id = "17", sha1 = Sha1(index), size = index.Length, totalSize, url };
    }

    private object PublishLogConfig()
    {
        var content = Text("""<Configuration status="WARN" />""");
        var url = Serve($"{DataHost}/v1/objects/{Sha1(content)}/client-1.12.xml", content);
        _files[LogConfigPath] = content;
        return new
        {
            argument = "-Dlog4j.configurationFile=${path}",
            file = new { id = "client-1.12.xml", sha1 = Sha1(content), size = content.Length, url },
            type = "log4j2-xml"
        };
    }

    /// <summary>
    /// 覆盖几种形态的库：带完整下载信息的、按系统筛选的新式 natives、旧式 natives（只含 classifier）、完全没有下载信息的。
    /// </summary>
    private List<object> PublishLibraries()
    {
        var libraries = new List<object>
        {
            new { name = "com.google.code.gson:gson:2.10.1", downloads = new { artifact = PublishLibrary(GsonJar, Text("gson")) } },
            new { name = "org.lwjgl:lwjgl:3.3.3", downloads = new { artifact = PublishLibrary(LwjglJar, Text("lwjgl")) } }
        };

        foreach (var os in OsNames)
        {
            var path = $"org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-{os}.jar";
            libraries.Add(new
            {
                name = $"org.lwjgl:lwjgl:3.3.3:natives-{os}",
                rules = new[] { new { action = "allow", os = new { name = os } } },
                downloads = new { artifact = PublishLibrary(path, Text($"lwjgl natives of {os}"), os == PlatformInfo.OsName) }
            });
        }

        var classifiers = OsNames.ToDictionary(
            os => $"natives-{os}",
            os => PublishLibrary(
                $"net/java/jinput/jinput-platform/2.0.5/jinput-platform-2.0.5-natives-{os}.jar",
                CreateNativesJar(os),
                os == PlatformInfo.OsName));
        libraries.Add(new
        {
            name = "net.java.jinput:jinput-platform:2.0.5",
            natives = OsNames.ToDictionary(os => os, os => $"natives-{os}"),
            extract = new { exclude = new[] { "META-INF/" } },
            downloads = new { classifiers }
        });

        // 没有 downloads：地址与路径都要由库名推导
        var logging = Text("logging");
        Serve($"{LibrariesHost}/{LoggingJar}", logging);
        _files[$"libraries/{LoggingJar}"] = logging;
        libraries.Add(new { name = "com.mojang:logging:1.2.7" });
        return libraries;
    }

    /// <summary>
    /// 登记一个库并返回它在版本 JSON 里的下载信息。<paramref name="installed"/> 为 false 表示安装时不该下载它。
    /// </summary>
    private object PublishLibrary(string path, byte[] content, bool installed = true)
    {
        var url = Serve($"{LibrariesHost}/{path}", content);
        if (installed)
            _files[$"libraries/{path}"] = content;
        return new { path, sha1 = Sha1(content), size = content.Length, url };
    }

    private string Serve(string url, byte[] content)
    {
        _server.Add(url, content);
        return url;
    }

    private static byte[] Text(string text)
    {
        return Encoding.UTF8.GetBytes(text);
    }

    private static string Sha1(byte[] content)
    {
        return Convert.ToHexStringLower(SHA1.HashData(content));
    }

    /// <summary>
    /// 旧式 natives 会在启动时被解压，所以内容必须是合法的 zip。
    /// </summary>
    private static byte[] CreateNativesJar(string os)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry($"native-library-{os}").Open());
            writer.Write(os);
        }
        return stream.ToArray();
    }
}
