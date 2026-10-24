using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Java.Runtime;
using PCL.Shion.Core.Sources;

namespace PCL.Shion.Core.Test.Java.Runtime;

[TestClass]
public sealed class RuntimeManifestTest
{
    private const string JavaSha1 = "2222222222222222222222222222222222222222";

    private const string JavaUrl = "https://piston-data.mojang.com/v1/objects/2222222222222222222222222222222222222222/java";

    private const string Manifest = """
        {
          "files": {
            "bin": { "type": "directory" },
            "bin/java": {
              "type": "file",
              "executable": true,
              "downloads": {
                "lzma": {
                  "sha1": "1111111111111111111111111111111111111111",
                  "size": 40000,
                  "url": "https://piston-data.mojang.com/v1/objects/1111111111111111111111111111111111111111/java"
                },
                "raw": {
                  "sha1": "2222222222222222222222222222222222222222",
                  "size": 100000,
                  "url": "https://piston-data.mojang.com/v1/objects/2222222222222222222222222222222222222222/java"
                }
              }
            },
            "conf": { "type": "directory" },
            "conf/net.properties": {
              "type": "file",
              "downloads": {
                "raw": {
                  "sha1": "3333333333333333333333333333333333333333",
                  "size": 7000,
                  "url": "https://piston-data.mojang.com/v1/objects/3333333333333333333333333333333333333333/net.properties"
                }
              }
            },
            "legal": { "type": "directory" },
            "legal/java.base": { "type": "directory" },
            "legal/java.base/LICENSE": {
              "type": "file",
              "downloads": {
                "raw": {
                  "sha1": "4444444444444444444444444444444444444444",
                  "size": 0,
                  "url": "https://piston-data.mojang.com/v1/objects/4444444444444444444444444444444444444444/LICENSE"
                }
              }
            },
            "legal/java.compiler": { "type": "directory" },
            "legal/java.compiler/LICENSE": { "type": "link", "target": "../java.base/LICENSE" },
            "release": {
              "type": "file",
              "downloads": {
                "raw": {
                  "sha1": "5555555555555555555555555555555555555555",
                  "size": 90,
                  "url": "https://piston-data.mojang.com/v1/objects/5555555555555555555555555555555555555555/release"
                }
              }
            }
          }
        }
        """;

    [TestMethod]
    public void Parse_ReadsRawDownloadOfEachFile()
    {
        var manifest = Parse(Manifest);

        CollectionAssert.AreEquivalent(
            new[] { "bin/java", "conf/net.properties", "legal/java.base/LICENSE", "release" },
            manifest.Files.Select(file => file.RelativePath).ToArray());
        Assert.AreEqual(
            new RuntimeFile("bin/java", JavaUrl, 100000, JavaSha1, true),
            manifest.Files.Single(file => file.RelativePath == "bin/java"));
    }

    [TestMethod]
    public void Parse_OnlyMarksFilesWithTheExecutableFlag()
    {
        var manifest = Parse(Manifest);

        CollectionAssert.AreEqual(
            new[] { "bin/java" },
            manifest.Files.Where(file => file.Executable).Select(file => file.RelativePath).ToArray());
    }

    [TestMethod]
    public void Parse_ReadsLinkTargets()
    {
        var manifest = Parse(Manifest);

        Assert.AreEqual(
            new RuntimeLink("legal/java.compiler/LICENSE", "../java.base/LICENSE"),
            manifest.Links.Single());
    }

    [TestMethod]
    public void Parse_ListsDeclaredDirectoriesInOrder()
    {
        var manifest = Parse(Manifest);

        CollectionAssert.AreEqual(
            new[] { "bin", "conf", "legal", "legal/java.base", "legal/java.compiler" },
            manifest.Directories.ToArray());
    }

    [TestMethod]
    public void Parse_IncludesDirectoriesOfFilesAndLinksThatWereNotDeclared()
    {
        var manifest = Parse("""
            {
              "files": {
                "lib/server/libjvm.so": {
                  "type": "file",
                  "downloads": { "raw": { "sha1": "aa", "size": 1, "url": "https://piston-data.mojang.com/v1/objects/aa/libjvm.so" } }
                },
                "lib/jli/libjli.so": { "type": "link", "target": "../libjli.so" },
                "release": {
                  "type": "file",
                  "downloads": { "raw": { "sha1": "bb", "size": 1, "url": "https://piston-data.mojang.com/v1/objects/bb/release" } }
                }
              }
            }
            """);

        CollectionAssert.AreEqual(new[] { "lib/jli", "lib/server" }, manifest.Directories.ToArray());
    }

    [TestMethod]
    public void Parse_AllowsMissingSizeAndHash()
    {
        var manifest = Parse("""
            {
              "files": {
                "release": { "type": "file", "downloads": { "raw": { "url": "https://example.com/release" } } }
              }
            }
            """);

        Assert.AreEqual(new RuntimeFile("release", "https://example.com/release", null, null, false), manifest.Files.Single());
    }

    [TestMethod]
    public void Parse_RejectsFileWithoutRawDownload()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => Parse("""
            {
              "files": {
                "release": { "type": "file", "downloads": { "lzma": { "url": "https://example.com/release" } } }
              }
            }
            """));
    }

    [TestMethod]
    public void Parse_RejectsLinkWithoutTarget()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => Parse("""{ "files": { "a/b": { "type": "link" } } }"""));
    }

    [TestMethod]
    public void Parse_RejectsManifestWithoutFiles()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => Parse("{}"));
        Assert.ThrowsExactly<InvalidDataException>(() => Parse("[]"));
    }

    [TestMethod]
    public void ToDownloadItems_MapsFilesOntoTheTargetDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "runtime");

        var items = Parse(Manifest).ToDownloadItems(root, DownloadSource.Official);

        Assert.HasCount(4, items);
        var java = items.Single(item => item.TargetPath == Path.Combine(root, "bin", "java"));
        Assert.AreEqual(100000L, java.Size);
        Assert.AreEqual(JavaSha1, java.Sha1);
        Assert.IsTrue(items.Any(item => item.TargetPath == Path.Combine(root, "legal", "java.base", "LICENSE")));
    }

    [TestMethod]
    public void ToDownloadItems_PutsThePreferredSourceFirst()
    {
        var official = Parse(Manifest).ToDownloadItems("runtime", DownloadSource.Official).First();
        var mirror = Parse(Manifest).ToDownloadItems("runtime", DownloadSource.Bmclapi).First();

        Assert.AreEqual("piston-data.mojang.com", official.Urls[0].Host);
        Assert.AreEqual("bmclapi2.bangbang93.com", official.Urls[1].Host);
        Assert.AreEqual("bmclapi2.bangbang93.com", mirror.Urls[0].Host);
        Assert.AreEqual("piston-data.mojang.com", mirror.Urls[1].Host);
    }

    [TestMethod]
    public void ResolvePath_UsesThePlatformSeparator()
    {
        Assert.AreEqual(
            Path.Combine("root", "legal", "java.base", "LICENSE"),
            RuntimeManifest.ResolvePath("root", "legal/java.base/LICENSE"));
    }

    private static RuntimeManifest Parse(string json) => RuntimeManifest.Parse(Encoding.UTF8.GetBytes(json));
}
