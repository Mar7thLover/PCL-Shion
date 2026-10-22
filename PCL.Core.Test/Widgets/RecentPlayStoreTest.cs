using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Widgets;

namespace PCL.Core.Test.Widgets;

[TestClass]
public class RecentPlayStoreTest
{
    private const string Path120 = @"C:\Games\.minecraft\versions\1.20.1\";
    private const string Path121 = @"C:\Games\.minecraft\versions\1.21\";

    private static readonly DateTime T0 = new(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    #region RecordStart

    [TestMethod]
    public void RecordStart_CreatesNewEntry()
    {
        var list = new List<RecentPlayEntry>();

        RecentPlayStore.RecordStart(list, Path120, "1.20.1", T0);

        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(Path120, list[0].Path);
        Assert.AreEqual("1.20.1", list[0].Name);
        Assert.AreEqual(T0, list[0].LastPlayedUtc);
        Assert.AreEqual(0L, list[0].TotalSeconds);
    }

    [TestMethod]
    public void RecordStart_UpdatesExistingEntry_KeepsTotalSeconds()
    {
        var list = new List<RecentPlayEntry>();
        RecentPlayStore.RecordStart(list, Path120, "old name", T0);
        RecentPlayStore.RecordSession(list, Path120, TimeSpan.FromMinutes(10));

        var later = T0.AddHours(3);
        RecentPlayStore.RecordStart(list, Path120, "new name", later);

        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("new name", list[0].Name);
        Assert.AreEqual(later, list[0].LastPlayedUtc);
        Assert.AreEqual(600L, list[0].TotalSeconds);
    }

    [TestMethod]
    public void RecordStart_MovesUpdatedEntryToFront()
    {
        var list = new List<RecentPlayEntry>();
        RecentPlayStore.RecordStart(list, Path120, "a", T0);
        RecentPlayStore.RecordStart(list, Path121, "b", T0.AddMinutes(1));
        Assert.AreEqual("b", list[0].Name);

        RecentPlayStore.RecordStart(list, Path120, "a", T0.AddMinutes(2));

        Assert.AreEqual("a", list[0].Name);
        Assert.AreEqual("b", list[1].Name);
    }

    [TestMethod]
    public void RecordStart_EmptyName_FallsBackToFolderName()
    {
        var list = new List<RecentPlayEntry>();

        RecentPlayStore.RecordStart(list, Path120, "  ", T0);

        Assert.AreEqual("1.20.1", list[0].Name);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(@"\")]
    public void RecordStart_EmptyPath_IsIgnored(string path)
    {
        var list = new List<RecentPlayEntry>();

        RecentPlayStore.RecordStart(list, path, "x", T0);

        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void RecordStart_UtcKindIsNormalized()
    {
        var list = new List<RecentPlayEntry>();
        var unspecified = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Unspecified);

        RecentPlayStore.RecordStart(list, Path120, "a", unspecified);

        Assert.AreEqual(DateTimeKind.Utc, list[0].LastPlayedUtc.Kind);
        Assert.AreEqual(unspecified.Ticks, list[0].LastPlayedUtc.Ticks);
    }

    #endregion

    #region RecordSession

    [TestMethod]
    public void RecordSession_AccumulatesDuration()
    {
        var list = new List<RecentPlayEntry>();
        RecentPlayStore.RecordStart(list, Path120, "a", T0);

        RecentPlayStore.RecordSession(list, Path120, TimeSpan.FromMinutes(5));
        RecentPlayStore.RecordSession(list, Path120, TimeSpan.FromSeconds(30.9));

        Assert.AreEqual(5 * 60 + 30L, list[0].TotalSeconds);
    }

    [TestMethod]
    public void RecordSession_NegativeOrZeroDuration_IsIgnored()
    {
        var list = new List<RecentPlayEntry>();
        RecentPlayStore.RecordStart(list, Path120, "a", T0);
        RecentPlayStore.RecordSession(list, Path120, TimeSpan.FromMinutes(1));

        RecentPlayStore.RecordSession(list, Path120, TimeSpan.FromMinutes(-30));
        RecentPlayStore.RecordSession(list, Path120, TimeSpan.Zero);

        Assert.AreEqual(60L, list[0].TotalSeconds);
    }

    [TestMethod]
    public void RecordSession_OverOneDay_IsCappedAt24Hours()
    {
        var list = new List<RecentPlayEntry>();
        RecentPlayStore.RecordStart(list, Path120, "a", T0);

        RecentPlayStore.RecordSession(list, Path120, TimeSpan.FromDays(10));
        Assert.AreEqual(24L * 60 * 60, list[0].TotalSeconds);

        // 正好 24 小时不受影响，之后的会话继续累加
        RecentPlayStore.RecordSession(list, Path120, TimeSpan.FromHours(24));
        Assert.AreEqual(2 * 24L * 60 * 60, list[0].TotalSeconds);
    }

    [TestMethod]
    public void RecordSession_UnknownPath_IsIgnored()
    {
        var list = new List<RecentPlayEntry>();
        RecentPlayStore.RecordStart(list, Path120, "a", T0);

        RecentPlayStore.RecordSession(list, Path121, TimeSpan.FromHours(1));
        RecentPlayStore.RecordSession(list, "", TimeSpan.FromHours(1));

        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(0L, list[0].TotalSeconds);
    }

    #endregion

    #region 路径规范化

    [TestMethod]
    public void Path_TrailingSeparatorAndCaseAreIgnored()
    {
        var list = new List<RecentPlayEntry>();
        RecentPlayStore.RecordStart(list, @"C:\Games\.minecraft\versions\Fabric\", "Fabric", T0);

        RecentPlayStore.RecordStart(list, @"c:\games\.MINECRAFT\versions\fabric", "Fabric 2", T0.AddMinutes(1));
        RecentPlayStore.RecordStart(list, @"C:/Games/.minecraft/versions/Fabric/", "Fabric 3", T0.AddMinutes(2));
        RecentPlayStore.RecordSession(list, @"C:\GAMES\.minecraft\versions\FABRIC\\", TimeSpan.FromMinutes(2));

        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("Fabric 3", list[0].Name);
        Assert.AreEqual(120L, list[0].TotalSeconds);
    }

    [TestMethod]
    public void IsSamePath_HandlesSeparatorsCaseAndEmpty()
    {
        Assert.IsTrue(RecentPlayStore.IsSamePath(Path120, Path120.TrimEnd('\\')));
        Assert.IsTrue(RecentPlayStore.IsSamePath(Path120, Path120.ToUpperInvariant()));
        Assert.IsFalse(RecentPlayStore.IsSamePath(Path120, Path121));
        Assert.IsFalse(RecentPlayStore.IsSamePath("", ""));
        Assert.IsFalse(RecentPlayStore.IsSamePath(null, Path120));
    }

    #endregion

    #region 排序与上限

    [TestMethod]
    public void GetRecent_OrdersByLastPlayedDescendingAndTakesN()
    {
        var list = new List<RecentPlayEntry>
        {
            new() { Path = @"C:\a", Name = "a", LastPlayedUtc = T0.AddHours(1) },
            new() { Path = @"C:\b", Name = "b", LastPlayedUtc = T0.AddHours(5) },
            new() { Path = @"C:\c", Name = "c", LastPlayedUtc = T0.AddHours(3) },
            new() { Path = @"C:\d", Name = "d", LastPlayedUtc = T0 }
        };

        var top = RecentPlayStore.GetRecent(list, 3);

        CollectionAssert.AreEqual(new[] { "b", "c", "a" }, top.Select(e => e.Name).ToArray());
    }

    [TestMethod]
    public void GetRecent_HandlesNonPositiveAndOversizedN()
    {
        var list = new List<RecentPlayEntry>();
        RecentPlayStore.RecordStart(list, Path120, "a", T0);

        Assert.AreEqual(0, RecentPlayStore.GetRecent(list, 0).Count);
        Assert.AreEqual(0, RecentPlayStore.GetRecent(list, -1).Count);
        Assert.AreEqual(1, RecentPlayStore.GetRecent(list, 20).Count);
        Assert.AreEqual(0, RecentPlayStore.GetRecent(new List<RecentPlayEntry>(), 3).Count);
    }

    [TestMethod]
    public void RecordStart_KeepsAtMost50Entries_DroppingOldest()
    {
        var list = new List<RecentPlayEntry>();
        for (var i = 0; i < 55; i++)
            RecentPlayStore.RecordStart(list, $@"C:\mc\versions\v{i}", $"v{i}", T0.AddMinutes(i));

        Assert.AreEqual(RecentPlayStore.MaxEntries, list.Count);
        Assert.AreEqual("v54", list[0].Name);
        Assert.AreEqual("v5", list[^1].Name);
        Assert.IsFalse(list.Any(e => e.Name == "v0"));
        Assert.IsFalse(list.Any(e => e.Name == "v4"));
    }

    [TestMethod]
    public void RecordStart_WhenFullWithEqualTimestamps_KeepsTheNewEntry()
    {
        var list = new List<RecentPlayEntry>();
        for (var i = 0; i < 60; i++)
            RecentPlayStore.RecordStart(list, $@"C:\mc\versions\v{i}", $"v{i}", T0);

        Assert.AreEqual(RecentPlayStore.MaxEntries, list.Count);
        Assert.AreEqual("v59", list[0].Name);
    }

    #endregion

    #region 序列化

    [TestMethod]
    public void SerializeThenParse_RoundTrips()
    {
        var list = new List<RecentPlayEntry>();
        RecentPlayStore.RecordStart(list, Path120, "1.20.1", T0);
        RecentPlayStore.RecordStart(list, Path121, "中文 \"实例\"", T0.AddHours(2));
        RecentPlayStore.RecordSession(list, Path121, TimeSpan.FromMinutes(90));

        var parsed = RecentPlayStore.Parse(RecentPlayStore.Serialize(list));

        Assert.AreEqual(2, parsed.Count);
        Assert.AreEqual(Path121, parsed[0].Path);
        Assert.AreEqual("中文 \"实例\"", parsed[0].Name);
        Assert.AreEqual(T0.AddHours(2), parsed[0].LastPlayedUtc);
        Assert.AreEqual(DateTimeKind.Utc, parsed[0].LastPlayedUtc.Kind);
        Assert.AreEqual(5400L, parsed[0].TotalSeconds);
        Assert.AreEqual(Path120, parsed[1].Path);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("[]")]
    public void Parse_EmptyInput_ReturnsEmptyList(string? json)
    {
        var parsed = RecentPlayStore.Parse(json);

        Assert.AreEqual(0, parsed.Count);
        Assert.IsTrue(RecentPlayStore.TryParse(json, out _));
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("not json")]
    [DataRow("{\"path\":\"C:\\\\a\"}")]
    [DataRow("[1,2,3]")]
    [DataRow("\"text\"")]
    [DataRow("[{\"path\":\"C:\\\\a\",\"lastPlayedUtc\":\"yesterday\"}]")]
    public void Parse_InvalidJson_ReturnsEmptyList(string json)
    {
        var parsed = RecentPlayStore.Parse(json);

        Assert.AreEqual(0, parsed.Count);
        Assert.IsFalse(RecentPlayStore.TryParse(json, out var entries));
        Assert.AreEqual(0, entries.Count);
    }

    [TestMethod]
    public void Parse_SkipsBrokenEntriesAndClampsNegativeSeconds()
    {
        const string json = """
            [
              null,
              { "path": "", "name": "empty path" },
              { "path": null, "name": "null path" },
              { "path": "C:\\mc\\versions\\ok", "name": "ok", "lastPlayedUtc": "2025-03-01T12:00:00Z", "totalSeconds": -50 }
            ]
            """;

        var parsed = RecentPlayStore.Parse(json);

        Assert.AreEqual(1, parsed.Count);
        Assert.AreEqual("ok", parsed[0].Name);
        Assert.AreEqual(0L, parsed[0].TotalSeconds);
    }

    [TestMethod]
    public void Parse_MergesDuplicatePaths()
    {
        const string json = """
            [
              { "path": "C:\\mc\\versions\\ok\\", "name": "old", "lastPlayedUtc": "2025-03-01T12:00:00Z", "totalSeconds": 100 },
              { "path": "c:\\MC\\versions\\ok", "name": "new", "lastPlayedUtc": "2025-03-02T12:00:00Z", "totalSeconds": 50 }
            ]
            """;

        var parsed = RecentPlayStore.Parse(json);

        Assert.AreEqual(1, parsed.Count);
        Assert.AreEqual("new", parsed[0].Name);
        Assert.AreEqual(150L, parsed[0].TotalSeconds);
    }

    [TestMethod]
    public void Parse_SortsByLastPlayedAndLimitsTo50()
    {
        var items = Enumerable.Range(0, 60)
            .OrderBy(i => (i * 7) % 60) // 打乱顺序
            .Select(i => new RecentPlayEntry
            {
                Path = $@"C:\mc\versions\v{i}",
                Name = $"v{i}",
                LastPlayedUtc = T0.AddMinutes(i)
            })
            .ToList();

        var parsed = RecentPlayStore.Parse(RecentPlayStore.Serialize(items));

        Assert.AreEqual(RecentPlayStore.MaxEntries, parsed.Count);
        Assert.AreEqual("v59", parsed[0].Name);
        Assert.AreEqual("v10", parsed[^1].Name);
    }

    #endregion
}

[TestClass]
public class WidgetFormatTest
{
    private static readonly DateTime Now = new(2025, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    [DataRow(0, WidgetFormat.RelativeTimeUnit.JustNow, 0)]
    [DataRow(59, WidgetFormat.RelativeTimeUnit.JustNow, 0)]
    [DataRow(60, WidgetFormat.RelativeTimeUnit.Minutes, 1)]
    [DataRow(119, WidgetFormat.RelativeTimeUnit.Minutes, 1)]
    [DataRow(59 * 60 + 59, WidgetFormat.RelativeTimeUnit.Minutes, 59)]
    [DataRow(60 * 60, WidgetFormat.RelativeTimeUnit.Hours, 1)]
    [DataRow(23 * 3600 + 3599, WidgetFormat.RelativeTimeUnit.Hours, 23)]
    [DataRow(24 * 3600, WidgetFormat.RelativeTimeUnit.Days, 1)]
    [DataRow(3 * 24 * 3600 + 5, WidgetFormat.RelativeTimeUnit.Days, 3)]
    [DataRow(400 * 24 * 3600, WidgetFormat.RelativeTimeUnit.Days, 400)]
    public void GetRelativeTimeParts_PicksBucketAndRoundsDown(int secondsAgo, WidgetFormat.RelativeTimeUnit unit, int value)
    {
        var parts = WidgetFormat.GetRelativeTimeParts(Now.AddSeconds(-secondsAgo), Now);

        Assert.AreEqual(new WidgetFormat.RelativeTimeParts(unit, value), parts);
    }

    [TestMethod]
    public void GetRelativeTimeParts_FutureTime_IsJustNow()
    {
        var parts = WidgetFormat.GetRelativeTimeParts(Now.AddHours(5), Now);

        Assert.AreEqual(new WidgetFormat.RelativeTimeParts(WidgetFormat.RelativeTimeUnit.JustNow, 0), parts);
    }

    [TestMethod]
    public void GetRelativeTimeParts_TreatsLocalAndUtcTheSame()
    {
        var utc = Now.AddMinutes(-30);

        var fromLocal = WidgetFormat.GetRelativeTimeParts(utc.ToLocalTime(), Now);

        Assert.AreEqual(new WidgetFormat.RelativeTimeParts(WidgetFormat.RelativeTimeUnit.Minutes, 30), fromLocal);
    }

    [TestMethod]
    [DataRow(-100, WidgetFormat.DurationUnit.LessThanMinute, 0, 0)]
    [DataRow(0, WidgetFormat.DurationUnit.LessThanMinute, 0, 0)]
    [DataRow(59, WidgetFormat.DurationUnit.LessThanMinute, 0, 0)]
    [DataRow(60, WidgetFormat.DurationUnit.Minutes, 0, 1)]
    [DataRow(35 * 60 + 30, WidgetFormat.DurationUnit.Minutes, 0, 35)]
    [DataRow(59 * 60 + 59, WidgetFormat.DurationUnit.Minutes, 0, 59)]
    [DataRow(3600, WidgetFormat.DurationUnit.HoursMinutes, 1, 0)]
    [DataRow(90 * 60, WidgetFormat.DurationUnit.HoursMinutes, 1, 30)]
    [DataRow(25 * 3600 + 61, WidgetFormat.DurationUnit.HoursMinutes, 25, 1)]
    public void GetDurationParts_PicksBucketAndRoundsDown(int seconds, WidgetFormat.DurationUnit unit, int hours, int minutes)
    {
        var parts = WidgetFormat.GetDurationParts(TimeSpan.FromSeconds(seconds));

        Assert.AreEqual(new WidgetFormat.DurationParts(unit, hours, minutes), parts);
    }
}
