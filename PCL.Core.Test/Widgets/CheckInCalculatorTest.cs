using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Widgets;

namespace PCL.Core.Test.Widgets;

[TestClass]
public sealed class CheckInCalculatorTest
{
    private static readonly DateOnly Today = new(2026, 3, 15);

    private static IReadOnlySet<DateOnly> Dates(params string[] days)
    {
        return CheckInCalculator.Parse(string.Join(",", days));
    }

    // ----- Parse / Serialize -----

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(",")]
    [DataRow(" , , ")]
    public void ParseShouldBeEmptyWhenNoRecord(string? raw)
    {
        Assert.IsEmpty(CheckInCalculator.Parse(raw));
    }

    [TestMethod]
    public void ParseShouldIgnoreInvalidItems()
    {
        var dates = CheckInCalculator.Parse(
            "2026-03-10,abc,2026-13-01,2026-02-30,2026/03/11,,20260312, 2026-03-13 ,2026-03-14T00:00:00");
        CollectionAssert.AreEqual(
            new[] { new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 13) },
            dates.ToArray());
    }

    [TestMethod]
    public void ParseShouldDeduplicateAndSort()
    {
        var dates = CheckInCalculator.Parse("2026-03-12,2026-03-10,2026-03-12,2026-03-11,2026-03-10");
        CollectionAssert.AreEqual(
            new[] { new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 11), new DateOnly(2026, 3, 12) },
            dates.ToArray());
    }

    [TestMethod]
    public void SerializeShouldDeduplicateSortAndUseIsoFormat()
    {
        var text = CheckInCalculator.Serialize(new[]
        {
            new DateOnly(2026, 3, 12), new DateOnly(2026, 1, 5), new DateOnly(2026, 3, 12), new DateOnly(2025, 12, 31)
        });
        Assert.AreEqual("2025-12-31,2026-01-05,2026-03-12", text);
    }

    [TestMethod]
    public void SerializeShouldReturnEmptyStringForNoDates()
    {
        Assert.AreEqual("", CheckInCalculator.Serialize(Array.Empty<DateOnly>()));
    }

    [TestMethod]
    public void SerializeThenParseShouldRoundTrip()
    {
        var original = new[] { new DateOnly(2024, 2, 29), new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15) };
        var restored = CheckInCalculator.Parse(CheckInCalculator.Serialize(original));
        CollectionAssert.AreEqual(original, restored.ToArray());
    }

    // ----- Compute -----

    [TestMethod]
    public void ComputeShouldBeZeroWhenNoRecord()
    {
        var stats = CheckInCalculator.Compute(Dates(), Today);
        Assert.IsFalse(stats.CheckedToday);
        Assert.AreEqual(0, stats.CurrentStreak);
        Assert.AreEqual(0, stats.LongestStreak);
        Assert.AreEqual(0, stats.TotalDays);
    }

    [TestMethod]
    public void ComputeShouldCountFromTodayWhenCheckedInToday()
    {
        var stats = CheckInCalculator.Compute(Dates("2026-03-13", "2026-03-14", "2026-03-15"), Today);
        Assert.IsTrue(stats.CheckedToday);
        Assert.AreEqual(3, stats.CurrentStreak);
        Assert.AreEqual(3, stats.LongestStreak);
        Assert.AreEqual(3, stats.TotalDays);
    }

    [TestMethod]
    public void ComputeShouldCountOnlyTodayWhenYesterdayMissing()
    {
        var stats = CheckInCalculator.Compute(Dates("2026-03-12", "2026-03-15"), Today);
        Assert.IsTrue(stats.CheckedToday);
        Assert.AreEqual(1, stats.CurrentStreak);
        Assert.AreEqual(1, stats.LongestStreak);
        Assert.AreEqual(2, stats.TotalDays);
    }

    [TestMethod]
    public void ComputeShouldKeepStreakAliveWhenOnlyYesterdayCheckedIn()
    {
        var stats = CheckInCalculator.Compute(Dates("2026-03-12", "2026-03-13", "2026-03-14"), Today);
        Assert.IsFalse(stats.CheckedToday);
        Assert.AreEqual(3, stats.CurrentStreak);
        Assert.AreEqual(3, stats.LongestStreak);
        Assert.AreEqual(3, stats.TotalDays);
    }

    [TestMethod]
    public void ComputeShouldResetCurrentStreakWhenNeitherTodayNorYesterday()
    {
        var stats = CheckInCalculator.Compute(Dates("2026-03-10", "2026-03-11", "2026-03-12"), Today);
        Assert.IsFalse(stats.CheckedToday);
        Assert.AreEqual(0, stats.CurrentStreak);
        Assert.AreEqual(3, stats.LongestStreak);
        Assert.AreEqual(3, stats.TotalDays);
    }

    [TestMethod]
    public void ComputeShouldStopCurrentStreakAtGap()
    {
        var stats = CheckInCalculator.Compute(
            Dates("2026-03-08", "2026-03-09", "2026-03-10", "2026-03-13", "2026-03-14", "2026-03-15"), Today);
        Assert.IsTrue(stats.CheckedToday);
        Assert.AreEqual(3, stats.CurrentStreak);
        Assert.AreEqual(3, stats.LongestStreak);
        Assert.AreEqual(6, stats.TotalDays);
    }

    [TestMethod]
    public void ComputeShouldFindLongestStreakInHistory()
    {
        var stats = CheckInCalculator.Compute(
            Dates("2026-01-01", "2026-01-02", "2026-01-03", "2026-01-04", "2026-01-05",
                "2026-02-10", "2026-02-11",
                "2026-03-14", "2026-03-15"), Today);
        Assert.IsTrue(stats.CheckedToday);
        Assert.AreEqual(2, stats.CurrentStreak);
        Assert.AreEqual(5, stats.LongestStreak);
        Assert.AreEqual(9, stats.TotalDays);
    }

    [TestMethod]
    public void ComputeShouldCountStreakAcrossMonthAndYearBoundary()
    {
        var today = new DateOnly(2026, 1, 2);
        var stats = CheckInCalculator.Compute(Dates("2025-12-30", "2025-12-31", "2026-01-01", "2026-01-02"), today);
        Assert.AreEqual(4, stats.CurrentStreak);
        Assert.AreEqual(4, stats.LongestStreak);
    }

    [TestMethod]
    public void ComputeShouldNotCountFutureDatesInCurrentStreak()
    {
        // 系统时间被调回过的情况：未来的记录不应被计入当前连续天数
        var stats = CheckInCalculator.Compute(Dates("2026-03-14", "2026-03-16", "2026-03-17"), Today);
        Assert.IsFalse(stats.CheckedToday);
        Assert.AreEqual(1, stats.CurrentStreak);
        Assert.AreEqual(2, stats.LongestStreak);
        Assert.AreEqual(3, stats.TotalDays);
    }

    [TestMethod]
    public void ComputeShouldCountDuplicatesOnlyOnce()
    {
        var stats = CheckInCalculator.Compute(Dates("2026-03-15", "2026-03-15", "2026-03-14", "2026-03-14"), Today);
        Assert.AreEqual(2, stats.CurrentStreak);
        Assert.AreEqual(2, stats.LongestStreak);
        Assert.AreEqual(2, stats.TotalDays);
    }

    [TestMethod]
    public void ComputeShouldTolerateInvalidInput()
    {
        var stats = CheckInCalculator.Compute(Dates("garbage", "2026-03-15", "2026-99-99", "2026-03-14"), Today);
        Assert.IsTrue(stats.CheckedToday);
        Assert.AreEqual(2, stats.CurrentStreak);
        Assert.AreEqual(2, stats.TotalDays);
    }

    [TestMethod]
    public void ComputeShouldNotThrowAtMinimumDate()
    {
        var stats = CheckInCalculator.Compute(Dates("0001-01-01"), DateOnly.MinValue);
        Assert.IsTrue(stats.CheckedToday);
        Assert.AreEqual(1, stats.CurrentStreak);
        Assert.AreEqual(1, stats.LongestStreak);
    }
}
