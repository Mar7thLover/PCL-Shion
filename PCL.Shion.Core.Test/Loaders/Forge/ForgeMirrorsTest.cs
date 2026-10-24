using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Loaders.Forge;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Test.Loaders.Forge;

[TestClass]
public sealed class ForgeMirrorsTest
{
    [TestMethod]
    [DataRow(
        "https://maven.minecraftforge.net/net/minecraftforge/forge/1.20.1-47.4.0/forge-1.20.1-47.4.0-installer.jar",
        "https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/1.20.1-47.4.0/forge-1.20.1-47.4.0-installer.jar")]
    [DataRow(
        "http://files.minecraftforge.net/maven/org/scala-lang/scala-library/2.11.1/scala-library-2.11.1.jar",
        "https://bmclapi2.bangbang93.com/maven/org/scala-lang/scala-library/2.11.1/scala-library-2.11.1.jar")]
    [DataRow(
        "https://maven.neoforged.net/releases/net/neoforged/neoforge/21.1.77/neoforge-21.1.77-installer.jar",
        "https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/21.1.77/neoforge-21.1.77-installer.jar")]
    public void Resolve_MapsForgeAndNeoForgeRepositoriesToTheMirror(string official, string mirror)
    {
        CollectionAssert.AreEqual(new[] { new Uri(official), new Uri(mirror) }, ForgeMirrors.Resolve(official, DownloadSource.Official).ToArray());
        CollectionAssert.AreEqual(new[] { new Uri(mirror), new Uri(official) }, ForgeMirrors.Resolve(official, DownloadSource.Bmclapi).ToArray());
    }

    [TestMethod]
    public void Resolve_LeavesMojangAddressesToMirrorUrls()
    {
        const string url = "https://libraries.minecraft.net/net/sf/jopt-simple/jopt-simple/5.0.4/jopt-simple-5.0.4.jar";

        CollectionAssert.AreEqual(
            MirrorUrls.Resolve(url, DownloadSource.Bmclapi).ToArray(),
            ForgeMirrors.Resolve(url, DownloadSource.Bmclapi).ToArray());
    }

    [TestMethod]
    [DataRow("https://files.minecraftforge.net/net/minecraftforge/forge/maven-metadata.json")]
    [DataRow("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge")]
    [DataRow("https://maven.fabricmc.net/net/fabricmc/fabric-loader/0.16.9/fabric-loader-0.16.9.jar")]
    public void Resolve_KeepsAddressesOutsideTheRepositories(string url)
    {
        CollectionAssert.AreEqual(new[] { new Uri(url) }, ForgeMirrors.Resolve(url, DownloadSource.Bmclapi).ToArray());
    }
}
