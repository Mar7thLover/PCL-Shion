using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Link;
using PCL.Shion.Core.Link.Lobby;
using PCL.Shion.Core.Link.Scaffolding;

namespace PCL.Shion.Core.Test.Link;

[TestClass]
public sealed class LobbyTest
{
    [TestMethod]
    public void GeneratedCodeCanBeParsed()
    {
        var code = LobbyCodeGenerator.Generate();

        Assert.IsTrue(LobbyCodeGenerator.TryParse(code.FullCode, out _));
    }

    [TestMethod]
    public void ParseCodeRejectsTerracottaCodeWithBadChecksum()
    {
        Assert.IsNull(LobbyInfoProvider.ParseCode("00000-00000-00000-00000-00001"));
    }

    [TestMethod]
    public void ParseCodeRejectsNonAscii()
    {
        Assert.IsNull(LobbyInfoProvider.ParseCode("大厅编号大厅编号大厅编号"));
    }

    [TestMethod]
    public void ParseCodeRejectsShortCode()
    {
        Assert.IsNull(LobbyInfoProvider.ParseCode("ABC"));
    }

    [TestMethod]
    public void ArgumentsBuilderAddsFlagOnlyWhenConditionHolds()
    {
        var builder = new ArgumentsBuilder().AddFlagIf(true, "on").AddFlagIf(false, "off");

        var text = builder.ToString();

        StringAssert.Contains(text, "on");
        Assert.IsFalse(text.Contains("off"));
    }

    [TestMethod]
    public void LinkServersAreSplitBySeparator()
    {
        var old = LinkHost.SecretProvider;
        try
        {
            LinkHost.SecretProvider = key => key == "LINK_SERVER_ROOT" ? "https://a.example| https://b.example |" : null;

            CollectionAssert.AreEqual(new[] { "https://a.example", "https://b.example" }, LinkHost.LinkServers);
        }
        finally
        {
            LinkHost.SecretProvider = old;
        }
    }

    [TestMethod]
    public void LauncherIdIsStableAndSixteenCharacters()
    {
        var first = Identify.LauncherId;
        var second = Identify.LauncherId;

        Assert.AreEqual(first, second);
        Assert.AreEqual(16, first.Length);
    }

    [TestMethod]
    public async Task AnnouncementWithoutServerReportsNoServer()
    {
        var old = LinkHost.SecretProvider;
        try
        {
            LinkHost.SecretProvider = _ => null;

            var result = await LinkAnnouncementService.FetchAsync();

            Assert.AreEqual(LinkAnnouncementFailure.NoServer, result.Failure);
            Assert.IsFalse(LobbyInfoProvider.IsLobbyAvailable);
        }
        finally
        {
            LinkHost.SecretProvider = old;
        }
    }
}
