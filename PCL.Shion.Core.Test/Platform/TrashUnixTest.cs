using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Test;

/// <summary>
/// macOS 与 Linux 回收站的移动逻辑。回收站目录由测试给出，所以在任何系统上都可以运行。
/// </summary>
[TestClass]
public sealed class TrashUnixTest
{
    private static readonly DateTime DeletedAt = new(2026, 10, 22, 13, 45, 9);

    private FakeMinecraftRoot _root = null!;

    private string TrashDirectory => _root.GetPath("trash");

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
    public void Freedesktop_MovesFileIntoFilesDirectory()
    {
        var path = _root.WriteFile("source/mod.jar", "payload");

        UnixTrash.MoveToFreedesktopTrash(path, TrashDirectory, DeletedAt);

        Assert.IsFalse(File.Exists(path));
        Assert.AreEqual("payload", File.ReadAllText(_root.GetPath("trash/files/mod.jar")));
    }

    [TestMethod]
    public void Freedesktop_WritesTrashInfoWithOriginalPathAndDeletionDate()
    {
        var path = _root.WriteFile("source/mod.jar", "payload");

        UnixTrash.MoveToFreedesktopTrash(path, TrashDirectory, DeletedAt);

        var lines = File.ReadAllText(_root.GetPath("trash/info/mod.jar.trashinfo")).Split('\n');
        Assert.AreEqual("[Trash Info]", lines[0]);
        Assert.StartsWith("Path=", lines[1]);
        Assert.AreEqual(path, Uri.UnescapeDataString(lines[1]["Path=".Length..]));
        Assert.AreEqual("DeletionDate=2026-10-22T13:45:09", lines[2]);
    }

    [TestMethod]
    public void Freedesktop_WritesTrashInfoWithoutByteOrderMark()
    {
        var path = _root.WriteFile("source/mod.jar", "payload");

        UnixTrash.MoveToFreedesktopTrash(path, TrashDirectory, DeletedAt);

        Assert.AreEqual((byte)'[', File.ReadAllBytes(_root.GetPath("trash/info/mod.jar.trashinfo"))[0]);
    }

    [TestMethod]
    public void Freedesktop_MovesDirectoryWithItsContents()
    {
        _root.WriteFile("source/instance/mods/a.jar", "a");
        _root.WriteFile("source/instance/options.txt", "b");

        UnixTrash.MoveToFreedesktopTrash(_root.GetPath("source/instance"), TrashDirectory, DeletedAt);

        Assert.IsFalse(Directory.Exists(_root.GetPath("source/instance")));
        Assert.AreEqual("a", File.ReadAllText(_root.GetPath("trash/files/instance/mods/a.jar")));
        Assert.AreEqual("b", File.ReadAllText(_root.GetPath("trash/files/instance/options.txt")));
        Assert.IsTrue(File.Exists(_root.GetPath("trash/info/instance.trashinfo")));
    }

    [TestMethod]
    public void Freedesktop_NumbersFilesWithSameNameBeforeTheExtension()
    {
        var first = _root.WriteFile("a/mod.jar", "first");
        var second = _root.WriteFile("b/mod.jar", "second");
        var third = _root.WriteFile("c/mod.jar", "third");

        UnixTrash.MoveToFreedesktopTrash(first, TrashDirectory, DeletedAt);
        UnixTrash.MoveToFreedesktopTrash(second, TrashDirectory, DeletedAt);
        UnixTrash.MoveToFreedesktopTrash(third, TrashDirectory, DeletedAt);

        Assert.AreEqual("first", File.ReadAllText(_root.GetPath("trash/files/mod.jar")));
        Assert.AreEqual("second", File.ReadAllText(_root.GetPath("trash/files/mod 2.jar")));
        Assert.AreEqual("third", File.ReadAllText(_root.GetPath("trash/files/mod 3.jar")));
        Assert.IsTrue(File.Exists(_root.GetPath("trash/info/mod 2.jar.trashinfo")));
        Assert.IsTrue(File.Exists(_root.GetPath("trash/info/mod 3.jar.trashinfo")));
    }

    [TestMethod]
    public void Freedesktop_NumbersDirectoriesAfterTheWholeName()
    {
        _root.WriteFile("a/1.20.1/options.txt", "first");
        _root.WriteFile("b/1.20.1/options.txt", "second");

        UnixTrash.MoveToFreedesktopTrash(_root.GetPath("a/1.20.1"), TrashDirectory, DeletedAt);
        UnixTrash.MoveToFreedesktopTrash(_root.GetPath("b/1.20.1"), TrashDirectory, DeletedAt);

        Assert.AreEqual("second", File.ReadAllText(_root.GetPath("trash/files/1.20.1 2/options.txt")));
        Assert.IsTrue(File.Exists(_root.GetPath("trash/info/1.20.1 2.trashinfo")));
    }

    [TestMethod]
    public void Freedesktop_NumbersHiddenFilesAfterTheWholeName()
    {
        var first = _root.WriteFile("a/.config", "first");
        var second = _root.WriteFile("b/.config", "second");

        UnixTrash.MoveToFreedesktopTrash(first, TrashDirectory, DeletedAt);
        UnixTrash.MoveToFreedesktopTrash(second, TrashDirectory, DeletedAt);

        Assert.AreEqual("second", File.ReadAllText(_root.GetPath("trash/files/.config 2")));
    }

    [TestMethod]
    public void Freedesktop_DoesNotOverwriteFileThatHasNoTrashInfo()
    {
        _root.WriteFile("trash/files/mod.jar", "leftover");
        var path = _root.WriteFile("source/mod.jar", "payload");

        UnixTrash.MoveToFreedesktopTrash(path, TrashDirectory, DeletedAt);

        Assert.AreEqual("leftover", File.ReadAllText(_root.GetPath("trash/files/mod.jar")));
        Assert.AreEqual("payload", File.ReadAllText(_root.GetPath("trash/files/mod 2.jar")));
    }

    [TestMethod]
    public void Freedesktop_RemovesTrashInfoWhenTheMoveFails()
    {
        var missing = _root.GetPath("source/missing.jar");
        Directory.CreateDirectory(_root.GetPath("source"));

        Assert.Throws<FileNotFoundException>(() => UnixTrash.MoveToFreedesktopTrash(missing, TrashDirectory, DeletedAt));

        Assert.IsEmpty(Directory.GetFileSystemEntries(_root.GetPath("trash/info")));
        Assert.IsEmpty(Directory.GetFileSystemEntries(_root.GetPath("trash/files")));
    }

    [TestMethod]
    public void Mac_MovesFileIntoTrashDirectory()
    {
        var path = _root.WriteFile("source/mod.jar", "payload");

        UnixTrash.MoveToMacTrash(path, TrashDirectory);

        Assert.IsFalse(File.Exists(path));
        Assert.AreEqual("payload", File.ReadAllText(_root.GetPath("trash/mod.jar")));
    }

    [TestMethod]
    public void Mac_MovesDirectoryWithItsContents()
    {
        _root.WriteFile("source/instance/mods/a.jar", "a");

        UnixTrash.MoveToMacTrash(_root.GetPath("source/instance"), TrashDirectory);

        Assert.IsFalse(Directory.Exists(_root.GetPath("source/instance")));
        Assert.AreEqual("a", File.ReadAllText(_root.GetPath("trash/instance/mods/a.jar")));
    }

    [TestMethod]
    public void Mac_NumbersFilesWithSameNameBeforeTheExtension()
    {
        UnixTrash.MoveToMacTrash(_root.WriteFile("a/mod.jar", "first"), TrashDirectory);
        UnixTrash.MoveToMacTrash(_root.WriteFile("b/mod.jar", "second"), TrashDirectory);

        Assert.AreEqual("first", File.ReadAllText(_root.GetPath("trash/mod.jar")));
        Assert.AreEqual("second", File.ReadAllText(_root.GetPath("trash/mod 2.jar")));
    }

    [TestMethod]
    public void Mac_NumbersDirectoriesWithSameNameAfterTheWholeName()
    {
        _root.WriteFile("a/1.20.1/options.txt", "first");
        _root.WriteFile("b/1.20.1/options.txt", "second");

        UnixTrash.MoveToMacTrash(_root.GetPath("a/1.20.1"), TrashDirectory);
        UnixTrash.MoveToMacTrash(_root.GetPath("b/1.20.1"), TrashDirectory);

        Assert.AreEqual("first", File.ReadAllText(_root.GetPath("trash/1.20.1/options.txt")));
        Assert.AreEqual("second", File.ReadAllText(_root.GetPath("trash/1.20.1 2/options.txt")));
    }
}
