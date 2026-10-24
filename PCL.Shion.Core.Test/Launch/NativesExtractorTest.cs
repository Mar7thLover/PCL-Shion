using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Launch;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class NativesExtractorTest
{
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
    public void ExtractsFilesAndKeepsRelativeLayout()
    {
        var jar = CreateJar(("lwjgl.dll", "dll"), ("windows/x64/openal.dll", "openal"));
        var target = _root.GetPath("natives");

        NativesExtractor.Extract(jar, target, []);

        Assert.AreEqual("dll", File.ReadAllText(Path.Combine(target, "lwjgl.dll")));
        Assert.AreEqual("openal", File.ReadAllText(Path.Combine(target, "windows", "x64", "openal.dll")));
    }

    [TestMethod]
    public void SkipsMetaInfAndExcludedPrefixes()
    {
        var jar = CreateJar(
            ("lwjgl.dll", "dll"),
            ("META-INF/MANIFEST.MF", "manifest"),
            ("META-INF/sub/x.txt", "x"),
            ("docs/readme.txt", "docs"),
            ("docsfoo.txt", "kept"));
        var target = _root.GetPath("natives");

        NativesExtractor.Extract(jar, target, ["docs/"]);

        Assert.IsTrue(File.Exists(Path.Combine(target, "lwjgl.dll")));
        Assert.IsTrue(File.Exists(Path.Combine(target, "docsfoo.txt")));
        Assert.IsFalse(Directory.Exists(Path.Combine(target, "META-INF")));
        Assert.IsFalse(Directory.Exists(Path.Combine(target, "docs")));
    }

    [TestMethod]
    public void IgnoresDirectoryEntries()
    {
        var jar = _root.GetPath("dirs.jar");
        using (var archive = ZipFile.Open(jar, ZipArchiveMode.Create))
        {
            archive.CreateEntry("empty/");
            WriteEntry(archive, "file.dll", "dll");
        }
        var target = _root.GetPath("natives");

        NativesExtractor.Extract(jar, target, []);

        Assert.IsTrue(File.Exists(Path.Combine(target, "file.dll")));
        Assert.IsFalse(Directory.Exists(Path.Combine(target, "empty")));
    }

    [TestMethod]
    public void KeepsExistingFilesOfTheSameSize()
    {
        var jar = CreateJar(("lwjgl.dll", "AAAA"));
        var target = _root.GetPath("natives");
        Directory.CreateDirectory(target);
        var existing = Path.Combine(target, "lwjgl.dll");
        File.WriteAllText(existing, "BBBB");

        NativesExtractor.Extract(jar, target, []);

        Assert.AreEqual("BBBB", File.ReadAllText(existing));
    }

    [TestMethod]
    public void ReplacesExistingFilesOfADifferentSize()
    {
        var jar = CreateJar(("lwjgl.dll", "AAAA"));
        var target = _root.GetPath("natives");
        Directory.CreateDirectory(target);
        var existing = Path.Combine(target, "lwjgl.dll");
        File.WriteAllText(existing, "old and longer");

        NativesExtractor.Extract(jar, target, []);

        Assert.AreEqual("AAAA", File.ReadAllText(existing));
    }

    [TestMethod]
    public void RejectsEntriesThatEscapeTheTargetDirectory()
    {
        var jar = CreateJar(("../evil.dll", "evil"));
        var target = _root.GetPath("natives");
        Directory.CreateDirectory(target);

        Assert.ThrowsExactly<InvalidDataException>(() => NativesExtractor.Extract(jar, target, []));
        Assert.IsFalse(File.Exists(_root.GetPath("evil.dll")));
    }

    private string CreateJar(params (string Name, string Content)[] entries)
    {
        var path = _root.GetPath($"{Guid.NewGuid():N}.jar");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
            WriteEntry(archive, name, content);
        return path;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
