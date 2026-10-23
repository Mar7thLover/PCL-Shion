using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Widgets;

namespace PCL.Core.Test.Widgets;

[TestClass]
public class RecentPlayTargetTest
{
    private const string Path120 = @"C:\Games\.minecraft\versions\1.20.1\";

    private static readonly DateTime T0 = new(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void TryParse_ServerConnect_PlainLog()
    {
        Assert.IsTrue(RecentPlayTarget.TryParse("[12:00:00] [Render thread/INFO]: Connecting to mc.example.com, 25565", out var target));
        Assert.AreEqual(new RecentPlayTarget(true, "mc.example.com:25565"), target);
    }

    [TestMethod]
    public void TryParse_ServerConnect_Log4jXml()
    {
        Assert.IsTrue(RecentPlayTarget.TryParse("<log4j:Message><![CDATA[Connecting to 192.168.1.5, 25566]]></log4j:Message>", out var target));
        Assert.AreEqual(new RecentPlayTarget(true, "192.168.1.5:25566"), target);
    }

    [TestMethod]
    public void TryParse_SingleplayerWorld()
    {
        Assert.IsTrue(RecentPlayTarget.TryParse("[12:00:00] [Server thread/INFO]: Preparing level \"New World (2)\"", out var target));
        Assert.AreEqual(new RecentPlayTarget(false, "New World (2)"), target);
    }

    [TestMethod]
    public void TryParse_IgnoresChatAndOtherLines()
    {
        Assert.IsFalse(RecentPlayTarget.TryParse("[Render thread/INFO]: [CHAT] Connecting to evil.example, 25565", out _));
        Assert.IsFalse(RecentPlayTarget.TryParse("[Render thread/INFO]: Setting user: Steve", out _));
        Assert.IsFalse(RecentPlayTarget.TryParse(null, out _));
    }

    [TestMethod]
    public void RecordTarget_KeepsOnlyLatestTarget()
    {
        var list = new List<RecentPlayEntry>();
        RecentPlayStore.RecordStart(list, Path120, "1.20.1", T0);

        RecentPlayStore.RecordTarget(list, Path120, new RecentPlayTarget(false, "Survival"));
        Assert.AreEqual("Survival", list[0].LastWorld);
        Assert.IsNull(list[0].LastServer);

        RecentPlayStore.RecordTarget(list, Path120, new RecentPlayTarget(true, "mc.example.com:25565"));
        Assert.IsNull(list[0].LastWorld);
        Assert.AreEqual("mc.example.com:25565", list[0].LastServer);
    }

    [TestMethod]
    public void RecordTarget_UnknownInstance_Ignored()
    {
        var list = new List<RecentPlayEntry>();

        RecentPlayStore.RecordTarget(list, Path120, new RecentPlayTarget(false, "Survival"));

        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void Serialize_RoundTripsTarget()
    {
        var list = new List<RecentPlayEntry>();
        RecentPlayStore.RecordStart(list, Path120, "1.20.1", T0);
        RecentPlayStore.RecordTarget(list, Path120, new RecentPlayTarget(true, "mc.example.com:25565"));

        var parsed = RecentPlayStore.Parse(RecentPlayStore.Serialize(list));

        Assert.AreEqual("mc.example.com:25565", parsed[0].LastServer);
        Assert.IsNull(parsed[0].LastWorld);
    }
}
