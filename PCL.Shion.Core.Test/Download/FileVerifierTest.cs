using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Download;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class FileVerifierTest
{
    private const string Content = "verified content";

    private static readonly string ContentSha1 = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(Content)));

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
    public void MissingFileNeverMatches()
    {
        Assert.IsFalse(FileVerifier.Matches(_root.GetPath("missing.bin"), null, null));
    }

    [TestMethod]
    public void ExistingFileMatchesWhenNothingIsKnownAboutIt()
    {
        var path = _root.WriteFile("data.bin", Content);

        Assert.IsTrue(FileVerifier.Matches(path, null, null));
    }

    [TestMethod]
    public void ChecksTheSize()
    {
        var path = _root.WriteFile("data.bin", Content);

        Assert.IsTrue(FileVerifier.Matches(path, Content.Length, null));
        Assert.IsFalse(FileVerifier.Matches(path, Content.Length + 1, null));
    }

    [TestMethod]
    public void ComparesSha1IgnoringCase()
    {
        var path = _root.WriteFile("data.bin", Content);

        Assert.IsTrue(FileVerifier.Matches(path, Content.Length, ContentSha1));
        Assert.IsTrue(FileVerifier.Matches(path, null, ContentSha1.ToUpperInvariant()));
    }

    [TestMethod]
    public void RejectsADifferentSha1()
    {
        var path = _root.WriteFile("data.bin", Content);

        Assert.IsFalse(FileVerifier.Matches(path, Content.Length, new string('0', 40)));
    }
}
