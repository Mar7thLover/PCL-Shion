using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Loaders;
using PCL.Shion.Core.Loaders.Fabric;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Test.Loaders.Fabric;

[TestClass]
public sealed class FabricMetaTest
{
    [TestMethod]
    [DataRow(LoaderKind.Fabric, DownloadSource.Official, FabricTestData.FabricListUrl, FabricTestData.FabricListMirrorUrl)]
    [DataRow(LoaderKind.Fabric, DownloadSource.Bmclapi, FabricTestData.FabricListMirrorUrl, FabricTestData.FabricListUrl)]
    [DataRow(LoaderKind.Quilt, DownloadSource.Official, FabricTestData.QuiltListUrl, FabricTestData.QuiltListMirrorUrl)]
    [DataRow(LoaderKind.Quilt, DownloadSource.Bmclapi, FabricTestData.QuiltListMirrorUrl, FabricTestData.QuiltListUrl)]
    public void LoaderListUrlsPutThePreferredSourceFirst(LoaderKind kind, DownloadSource source, string first, string second)
    {
        var urls = FabricMeta.GetLoaderListUrls(kind, FabricTestData.MinecraftVersion, source);

        AssertUrls(urls, first, second);
    }

    [TestMethod]
    [DataRow(LoaderKind.Fabric, DownloadSource.Official, FabricTestData.FabricProfileUrl, FabricTestData.FabricProfileMirrorUrl)]
    [DataRow(LoaderKind.Fabric, DownloadSource.Bmclapi, FabricTestData.FabricProfileMirrorUrl, FabricTestData.FabricProfileUrl)]
    [DataRow(LoaderKind.Quilt, DownloadSource.Official, FabricTestData.QuiltProfileUrl, FabricTestData.QuiltProfileMirrorUrl)]
    [DataRow(LoaderKind.Quilt, DownloadSource.Bmclapi, FabricTestData.QuiltProfileMirrorUrl, FabricTestData.QuiltProfileUrl)]
    public void ProfileUrlsPutThePreferredSourceFirst(LoaderKind kind, DownloadSource source, string first, string second)
    {
        var loader = new LoaderVersion(kind, kind == LoaderKind.Fabric ? "0.16.9" : "0.26.4", FabricTestData.MinecraftVersion, true);

        AssertUrls(FabricMeta.GetProfileUrls(loader, source), first, second);
    }

    [TestMethod]
    public void VersionsAreEscapedAsPathSegments()
    {
        var loader = new LoaderVersion(LoaderKind.Fabric, "0.4.8+build.155", "1.14 Pre-Release 1", true);

        AssertUrls(
            FabricMeta.GetLoaderListUrls(LoaderKind.Fabric, loader.MinecraftVersion, DownloadSource.Official),
            "https://meta.fabricmc.net/v2/versions/loader/1.14%20Pre-Release%201",
            "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/1.14%20Pre-Release%201");
        AssertUrls(
            FabricMeta.GetProfileUrls(loader, DownloadSource.Official),
            "https://meta.fabricmc.net/v2/versions/loader/1.14%20Pre-Release%201/0.4.8%2Bbuild.155/profile/json",
            "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/1.14%20Pre-Release%201/0.4.8%2Bbuild.155/profile/json");
    }

    private static void AssertUrls(IReadOnlyList<Uri> actual, params string[] expected)
    {
        CollectionAssert.AreEqual(expected, actual.Select(uri => uri.AbsoluteUri).ToArray());
    }
}
