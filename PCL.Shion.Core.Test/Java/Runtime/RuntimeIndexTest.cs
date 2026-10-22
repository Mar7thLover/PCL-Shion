using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Java.Runtime;

namespace PCL.Shion.Core.Test.Java.Runtime;

[TestClass]
public sealed class RuntimeIndexTest
{
    private const string Index = """
        {
          "linux": {
            "java-runtime-gamma": [
              {
                "availability": { "group": 1, "progress": 100 },
                "manifest": {
                  "sha1": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "size": 1000,
                  "url": "https://piston-meta.mojang.com/v1/packages/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/manifest.json"
                },
                "version": { "name": "17.0.8", "released": "2023-08-01T00:00:00+00:00" }
              }
            ],
            "jre-legacy": []
          },
          "windows-x64": {
            "java-runtime-gamma": [
              {
                "manifest": {
                  "sha1": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                  "size": 2000,
                  "url": "https://piston-meta.mojang.com/v1/packages/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/manifest.json"
                },
                "version": { "name": "17.0.8", "released": "2023-08-01T00:00:00+00:00" }
              }
            ]
          }
        }
        """;

    [TestMethod]
    public void FindManifest_ReturnsTheManifestOfThePlatformAndComponent()
    {
        var linux = RuntimeIndex.FindManifest(Bytes(Index), "linux", "java-runtime-gamma");
        var windows = RuntimeIndex.FindManifest(Bytes(Index), "windows-x64", "java-runtime-gamma");

        Assert.AreEqual(
            new RuntimeManifestRef(
                "https://piston-meta.mojang.com/v1/packages/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/manifest.json",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
            linux);
        Assert.AreEqual("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", windows.Sha1);
    }

    [TestMethod]
    public void FindManifest_RejectsAComponentThePlatformDoesNotHave()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => RuntimeIndex.FindManifest(Bytes(Index), "windows-x64", "jre-legacy"));
    }

    [TestMethod]
    public void FindManifest_RejectsAComponentWithAnEmptyVersionList()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => RuntimeIndex.FindManifest(Bytes(Index), "linux", "jre-legacy"));
    }

    [TestMethod]
    public void FindManifest_RejectsAnUnknownPlatform()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => RuntimeIndex.FindManifest(Bytes(Index), "linux-i386", "java-runtime-gamma"));
    }

    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);
}
