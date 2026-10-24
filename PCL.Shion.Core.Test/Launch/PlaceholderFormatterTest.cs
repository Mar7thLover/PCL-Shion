using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Launch;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class PlaceholderFormatterTest
{
    private static readonly Dictionary<string, string> Values = new()
    {
        ["auth_player_name"] = "Steve",
        ["version_name"] = "1.20.1",
        ["library_directory"] = "/mc/libraries",
        ["classpath_separator"] = ":"
    };

    [TestMethod]
    public void ReplacesWholeValue()
    {
        Assert.AreEqual("Steve", PlaceholderFormatter.Apply("${auth_player_name}", Values));
    }

    [TestMethod]
    public void ReplacesPlaceholdersInsideLargerText()
    {
        var result = PlaceholderFormatter.Apply(
            "${library_directory}/a.jar${classpath_separator}${library_directory}/b.jar",
            Values);

        Assert.AreEqual("/mc/libraries/a.jar:/mc/libraries/b.jar", result);
    }

    [TestMethod]
    public void ReplacesEveryOccurrence()
    {
        Assert.AreEqual("1.20.1/1.20.1.jar", PlaceholderFormatter.Apply("${version_name}/${version_name}.jar", Values));
    }

    [TestMethod]
    public void KeepsUnknownPlaceholders()
    {
        Assert.AreEqual("--quickPlayPath ${quickPlayPath}", PlaceholderFormatter.Apply("--quickPlayPath ${quickPlayPath}", Values));
    }

    [TestMethod]
    public void LeavesTextWithoutPlaceholdersAlone()
    {
        Assert.AreEqual("-XX:+UseG1GC", PlaceholderFormatter.Apply("-XX:+UseG1GC", Values));
        Assert.AreEqual("$auth_player_name", PlaceholderFormatter.Apply("$auth_player_name", Values));
        Assert.AreEqual("${", PlaceholderFormatter.Apply("${", Values));
    }

    [TestMethod]
    public void SubstitutedValuesAreNotExpandedAgain()
    {
        var values = new Dictionary<string, string> { ["a"] = "${b}", ["b"] = "x" };

        Assert.AreEqual("${b}", PlaceholderFormatter.Apply("${a}", values));
    }
}
