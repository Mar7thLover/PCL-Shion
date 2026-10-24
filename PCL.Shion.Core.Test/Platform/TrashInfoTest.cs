using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class TrashInfoTest
{
    [TestMethod]
    public void TrashInfoFollowsFreedesktopFormat()
    {
        var info = UnixTrash.CreateTrashInfo("/home/user/My Mods/a.jar", new DateTime(2026, 10, 22, 13, 45, 9));

        Assert.AreEqual(
            "[Trash Info]\nPath=/home/user/My%20Mods/a.jar\nDeletionDate=2026-10-22T13:45:09\n",
            info);
    }

    [TestMethod]
    public void DeletionDateUsesTwentyFourHourClock()
    {
        var info = UnixTrash.CreateTrashInfo("/tmp/a", new DateTime(2026, 1, 2, 3, 4, 5));

        StringAssert.EndsWith(info, "DeletionDate=2026-01-02T03:04:05\n");
    }

    [TestMethod]
    [DataRow("/home/user/mods/a-b_c.v1~2.jar", "/home/user/mods/a-b_c.v1~2.jar")]
    [DataRow("/home/user/My Mods/a b.jar", "/home/user/My%20Mods/a%20b.jar")]
    [DataRow("/home/用户/测试.jar", "/home/%E7%94%A8%E6%88%B7/%E6%B5%8B%E8%AF%95.jar")]
    [DataRow("/tmp/100%/a#b.txt", "/tmp/100%25/a%23b.txt")]
    public void EncodePathEscapesEachSegmentAndKeepsSeparators(string path, string expected)
    {
        Assert.AreEqual(expected, UnixTrash.EncodePath(path));
    }
}
