using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Test;

/// <summary>
/// 通过公开入口验证真实的系统回收站。被移入的是临时目录里的文件，会留在当前用户的回收站中。
/// </summary>
[TestClass]
public sealed class TrashTest
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
    public void MoveToTrash_RemovesFile()
    {
        var path = _root.WriteFile("pcl-shion-trash-test.txt", "content");

        Trash.MoveToTrash(path);

        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void MoveToTrash_RemovesDirectoryWithItsContents()
    {
        var file = _root.WriteFile("pcl-shion-trash-test/mods/a.jar", "a");
        var directory = _root.GetPath("pcl-shion-trash-test");

        Trash.MoveToTrash(directory);

        Assert.IsFalse(Directory.Exists(directory));
        Assert.IsFalse(File.Exists(file));
    }

    [TestMethod]
    public void MoveToTrash_AcceptsDirectoryPathWithTrailingSeparator()
    {
        _root.WriteFile("pcl-shion-trash-test/a.txt", "a");
        var directory = _root.GetPath("pcl-shion-trash-test");

        Trash.MoveToTrash(directory + Path.DirectorySeparatorChar);

        Assert.IsFalse(Directory.Exists(directory));
    }

    [TestMethod]
    public void MoveToTrash_ThrowsWhenPathDoesNotExist()
    {
        var path = _root.GetPath("missing.txt");

        var error = Assert.Throws<FileNotFoundException>(() => Trash.MoveToTrash(path));

        Assert.AreEqual(path, error.FileName);
    }
}
