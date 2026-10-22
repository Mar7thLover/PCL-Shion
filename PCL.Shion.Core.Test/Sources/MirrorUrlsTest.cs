using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class MirrorUrlsTest
{
    private const string MirrorRoot = "https://bmclapi2.bangbang93.com";
    private const string Sha1 = "0123456789abcdef0123456789abcdef01234567";

    [TestMethod]
    [DataRow("piston-meta.mojang.com")]
    [DataRow("launchermeta.mojang.com")]
    [DataRow("launcher.mojang.com")]
    [DataRow("piston-data.mojang.com")]
    public void MetaHostsMapToMirrorRootWithOriginalPath(string host)
    {
        AssertBothOrders($"https://{host}/v1/objects/{Sha1}/client.jar", $"{MirrorRoot}/v1/objects/{Sha1}/client.jar");
    }

    [TestMethod]
    [DataRow("0123456789abcdef0123456789abcdef01234567")]
    [DataRow("ffeeddccbbaa99887766554433221100ffeeddcc")]
    [DataRow("00ab00ab00ab00ab00ab00ab00ab00ab00ab00ab")]
    public void ResourcesHostMapsToAssetsPath(string hash)
    {
        var path = $"{hash[..2]}/{hash}";

        AssertBothOrders($"https://resources.download.minecraft.net/{path}", $"{MirrorRoot}/assets/{path}");
    }

    [TestMethod]
    [DataRow("org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar")]
    [DataRow("org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-windows.jar")]
    [DataRow("com/mojang/authlib/6.0.54/authlib-6.0.54.jar")]
    [DataRow("net/sf/jopt-simple/jopt-simple/5.0.4/jopt-simple-5.0.4.jar")]
    [DataRow("ca/weblite/java-objc-bridge/1.1/java-objc-bridge-1.1.jar")]
    public void LibrariesHostMapsToMavenPath(string path)
    {
        AssertBothOrders($"https://libraries.minecraft.net/{path}", $"{MirrorRoot}/maven/{path}");
    }

    [TestMethod]
    [DataRow(
        "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json",
        "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json")]
    [DataRow(
        "https://launchermeta.mojang.com/mc/game/version_manifest.json",
        "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json")]
    [DataRow(
        "https://piston-meta.mojang.com/v1/packages/0123456789abcdef0123456789abcdef01234567/1.21.1.json",
        "https://bmclapi2.bangbang93.com/v1/packages/0123456789abcdef0123456789abcdef01234567/1.21.1.json")]
    [DataRow(
        "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json",
        "https://bmclapi2.bangbang93.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json")]
    [DataRow(
        "https://piston-data.mojang.com/v1/objects/0123456789abcdef0123456789abcdef01234567/client.jar",
        "https://bmclapi2.bangbang93.com/v1/objects/0123456789abcdef0123456789abcdef01234567/client.jar")]
    [DataRow(
        "https://launcher.mojang.com/v1/objects/0123456789abcdef0123456789abcdef01234567/client.jar",
        "https://bmclapi2.bangbang93.com/v1/objects/0123456789abcdef0123456789abcdef01234567/client.jar")]
    [DataRow(
        "https://resources.download.minecraft.net/ab/ab0123456789abcdef0123456789abcdef012345",
        "https://bmclapi2.bangbang93.com/assets/ab/ab0123456789abcdef0123456789abcdef012345")]
    [DataRow(
        "https://libraries.minecraft.net/org/ow2/asm/asm/9.6/asm-9.6.jar",
        "https://bmclapi2.bangbang93.com/maven/org/ow2/asm/asm/9.6/asm-9.6.jar")]
    public void CommonOfficialUrlsMapToExpectedMirrorUrls(string official, string mirror)
    {
        AssertBothOrders(official, mirror);
    }

    [TestMethod]
    [DataRow("https://piston-meta.mojang.com/mc/game/a.json?v=1", "https://bmclapi2.bangbang93.com/mc/game/a.json?v=1")]
    [DataRow("https://launchermeta.mojang.com/mc/game/a.json?v=1&t=2", "https://bmclapi2.bangbang93.com/mc/game/a.json?v=1&t=2")]
    [DataRow("https://launcher.mojang.com/v1/objects/x/client.jar?token=abc", "https://bmclapi2.bangbang93.com/v1/objects/x/client.jar?token=abc")]
    [DataRow("https://piston-data.mojang.com/v1/objects/x/client.jar?a=b&c=d&e=f", "https://bmclapi2.bangbang93.com/v1/objects/x/client.jar?a=b&c=d&e=f")]
    [DataRow("https://resources.download.minecraft.net/ab/abcdef?cache=0", "https://bmclapi2.bangbang93.com/assets/ab/abcdef?cache=0")]
    [DataRow("https://libraries.minecraft.net/org/a/b/1.0/b-1.0.jar?x=y", "https://bmclapi2.bangbang93.com/maven/org/a/b/1.0/b-1.0.jar?x=y")]
    [DataRow("https://piston-meta.mojang.com/mc/game/a.json?name=a%20b", "https://bmclapi2.bangbang93.com/mc/game/a.json?name=a%20b")]
    public void QueryStringIsPreserved(string official, string mirror)
    {
        AssertBothOrders(official, mirror);
    }

    [TestMethod]
    [DataRow("https://libraries.minecraft.net/a/b%20c/1.0/c-1.0.jar", "https://bmclapi2.bangbang93.com/maven/a/b%20c/1.0/c-1.0.jar")]
    [DataRow("https://piston-meta.mojang.com/v1/a%20b/c.json", "https://bmclapi2.bangbang93.com/v1/a%20b/c.json")]
    public void PercentEncodedPathIsNotEncodedAgain(string official, string mirror)
    {
        AssertBothOrders(official, mirror);
    }

    [TestMethod]
    [DataRow("https://piston-meta.mojang.com/", "https://bmclapi2.bangbang93.com/")]
    [DataRow("https://launchermeta.mojang.com/", "https://bmclapi2.bangbang93.com/")]
    [DataRow("https://launcher.mojang.com/", "https://bmclapi2.bangbang93.com/")]
    [DataRow("https://piston-data.mojang.com/", "https://bmclapi2.bangbang93.com/")]
    [DataRow("https://resources.download.minecraft.net/", "https://bmclapi2.bangbang93.com/assets/")]
    [DataRow("https://libraries.minecraft.net/", "https://bmclapi2.bangbang93.com/maven/")]
    public void HostRootMapsToMirrorPrefix(string official, string mirror)
    {
        AssertBothOrders(official, mirror);
    }

    [TestMethod]
    public void HostMatchIgnoresCase()
    {
        AssertBothOrders("https://PISTON-META.Mojang.com/mc/a.json", "https://piston-meta.mojang.com/mc/a.json", $"{MirrorRoot}/mc/a.json");
        AssertBothOrders("https://Resources.Download.Minecraft.NET/ab/abcdef", "https://resources.download.minecraft.net/ab/abcdef", $"{MirrorRoot}/assets/ab/abcdef");
        AssertBothOrders("https://LIBRARIES.minecraft.net/a/b.jar", "https://libraries.minecraft.net/a/b.jar", $"{MirrorRoot}/maven/a/b.jar");
    }

    [TestMethod]
    public void MirrorUsesHttpsEvenWhenOfficialUrlIsHttp()
    {
        AssertBothOrders("http://libraries.minecraft.net/a/b.jar", "http://libraries.minecraft.net/a/b.jar", $"{MirrorRoot}/maven/a/b.jar");
    }

    [TestMethod]
    public void OfficialPreferenceListsOfficialFirstAndMirrorSecond()
    {
        var urls = MirrorUrls.Resolve("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", DownloadSource.Official);

        Assert.HasCount(2, urls);
        Assert.AreEqual("piston-meta.mojang.com", urls[0].Host);
        Assert.AreEqual("bmclapi2.bangbang93.com", urls[1].Host);
    }

    [TestMethod]
    public void BmclapiPreferenceListsMirrorFirstAndOfficialSecond()
    {
        var urls = MirrorUrls.Resolve("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", DownloadSource.Bmclapi);

        Assert.HasCount(2, urls);
        Assert.AreEqual("bmclapi2.bangbang93.com", urls[0].Host);
        Assert.AreEqual("piston-meta.mojang.com", urls[1].Host);
    }

    [TestMethod]
    [DataRow("https://maven.minecraftforge.net/net/minecraftforge/forge/1.21.1-52.1.20/forge-1.21.1-52.1.20-installer.jar")]
    [DataRow("https://maven.fabricmc.net/net/fabricmc/fabric-loader/0.16.5/fabric-loader-0.16.5.jar")]
    [DataRow("https://api.minecraftservices.com/minecraft/profile")]
    [DataRow("https://sessionserver.mojang.com/session/minecraft/join")]
    [DataRow("https://www.minecraft.net/en-us/download")]
    [DataRow("https://github.com/example/launcher/releases/latest")]
    [DataRow("https://example.com/files/client.jar?x=1&y=2")]
    [DataRow("https://bmclapi2.bangbang93.com/maven/org/a/b/1.0/b-1.0.jar")]
    public void UrlsWithoutMirrorPassThroughAsSingleElement(string url)
    {
        AssertUrls(MirrorUrls.Resolve(url, DownloadSource.Official), url);
        AssertUrls(MirrorUrls.Resolve(url, DownloadSource.Bmclapi), url);
    }

    [TestMethod]
    [DataRow("https://piston-meta.mojang.com.example.com/mc/game/a.json")]
    [DataRow("https://evil-piston-meta.mojang.com/mc/game/a.json")]
    [DataRow("https://www.piston-meta.mojang.com/mc/game/a.json")]
    [DataRow("https://cdn.libraries.minecraft.net/a/b.jar")]
    [DataRow("https://resources.download.minecraft.net.example.com/ab/abcdef")]
    [DataRow("https://download.minecraft.net/ab/abcdef")]
    [DataRow("https://minecraft.net/a/b.jar")]
    [DataRow("https://mojang.com/a/b.json")]
    [DataRow("https://example.com/piston-meta.mojang.com/a.json")]
    [DataRow("https://example.com/a?u=https://libraries.minecraft.net/a.jar")]
    public void LookalikeHostsAreNotMapped(string url)
    {
        AssertUrls(MirrorUrls.Resolve(url, DownloadSource.Official), url);
        AssertUrls(MirrorUrls.Resolve(url, DownloadSource.Bmclapi), url);
    }

    private static void AssertBothOrders(string official, string mirror)
    {
        AssertBothOrders(official, official, mirror);
    }

    /// <summary>
    /// <paramref name="canonicalOfficial"/> 是官方地址经 <see cref="Uri"/> 规范化后的形式。
    /// </summary>
    private static void AssertBothOrders(string official, string canonicalOfficial, string mirror)
    {
        AssertUrls(MirrorUrls.Resolve(official, DownloadSource.Official), canonicalOfficial, mirror);
        AssertUrls(MirrorUrls.Resolve(official, DownloadSource.Bmclapi), mirror, canonicalOfficial);
    }

    private static void AssertUrls(IReadOnlyList<Uri> actual, params string[] expected)
    {
        CollectionAssert.AreEqual(expected, actual.Select(uri => uri.AbsoluteUri).ToArray());
    }
}
