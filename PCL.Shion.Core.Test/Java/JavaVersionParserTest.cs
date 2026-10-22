using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Java;

namespace PCL.Shion.Core.Test.Java;

[TestClass]
public class JavaVersionParserTest
{
    private const string TemurinRelease = """
        IMPLEMENTOR="Eclipse Adoptium"
        IMPLEMENTOR_VERSION="Temurin-21.0.10+7"
        JAVA_RUNTIME_VERSION="21.0.10+7-LTS"
        JAVA_VERSION="21.0.10"
        JAVA_VERSION_DATE="2026-01-20"
        OS_ARCH="x86_64"
        OS_NAME="Windows"
        """;

    private const string LegacyRelease = """
        JAVA_VERSION="1.8.0_301"
        OS_NAME="Windows"
        OS_VERSION="5.2"
        OS_ARCH="amd64"
        SOURCE=""
        """;

    private const string TemurinOutput = """
        openjdk version "21.0.10" 2026-01-20 LTS
        OpenJDK Runtime Environment Temurin-21.0.10+7 (build 21.0.10+7-LTS)
        OpenJDK 64-Bit Server VM Temurin-21.0.10+7 (build 21.0.10+7-LTS, mixed mode, sharing)
        """;

    private const string LegacyOutput = """
        java version "1.8.0_301"
        Java(TM) SE Runtime Environment (build 1.8.0_301-b09)
        Java HotSpot(TM) 64-Bit Server VM (build 25.301-b09, mixed mode)
        """;

    [TestMethod]
    public void ParseReleaseFile_ReadsModernRelease()
    {
        Assert.AreEqual(new JavaVersionInfo(21, "21.0.10", true), JavaVersionParser.ParseReleaseFile(TemurinRelease));
    }

    [TestMethod]
    public void ParseReleaseFile_ReadsLegacyRelease()
    {
        Assert.AreEqual(new JavaVersionInfo(8, "1.8.0_301", true), JavaVersionParser.ParseReleaseFile(LegacyRelease));
    }

    [TestMethod]
    public void ParseReleaseFile_AcceptsLineFeedOnly()
    {
        var content = TemurinRelease.Replace("\r\n", "\n");
        Assert.AreEqual(new JavaVersionInfo(21, "21.0.10", true), JavaVersionParser.ParseReleaseFile(content));
    }

    [TestMethod]
    public void ParseReleaseFile_AcceptsUnquotedValues()
    {
        var info = JavaVersionParser.ParseReleaseFile("JAVA_VERSION=17.0.9\nOS_ARCH=aarch64\n");
        Assert.AreEqual(new JavaVersionInfo(17, "17.0.9", true), info);
    }

    [TestMethod]
    [DataRow("i586")]
    [DataRow("i386")]
    [DataRow("x86")]
    public void ParseReleaseFile_Detects32BitArchitecture(string arch)
    {
        var info = JavaVersionParser.ParseReleaseFile($"JAVA_VERSION=\"1.8.0_301\"\nOS_ARCH=\"{arch}\"\n");
        Assert.AreEqual(new JavaVersionInfo(8, "1.8.0_301", false), info);
    }

    [TestMethod]
    [DataRow("x86_64")]
    [DataRow("amd64")]
    [DataRow("aarch64")]
    public void ParseReleaseFile_Detects64BitArchitecture(string arch)
    {
        var info = JavaVersionParser.ParseReleaseFile($"JAVA_VERSION=\"17.0.9\"\nOS_ARCH=\"{arch}\"\n");
        Assert.IsTrue(info?.Is64Bit);
    }

    [TestMethod]
    public void ParseReleaseFile_RequiresVersionAndArchitecture()
    {
        Assert.IsNull(JavaVersionParser.ParseReleaseFile("JAVA_VERSION=\"21.0.10\"\n"));
        Assert.IsNull(JavaVersionParser.ParseReleaseFile("OS_ARCH=\"x86_64\"\n"));
        Assert.IsNull(JavaVersionParser.ParseReleaseFile(string.Empty));
        Assert.IsNull(JavaVersionParser.ParseReleaseFile("JAVA_VERSION=\"unknown\"\nOS_ARCH=\"x86_64\"\n"));
    }

    [TestMethod]
    public void ParseVersionOutput_ReadsModernOutput()
    {
        Assert.AreEqual(new JavaVersionInfo(21, "21.0.10", true), JavaVersionParser.ParseVersionOutput(TemurinOutput));
    }

    [TestMethod]
    public void ParseVersionOutput_ReadsLegacyOutput()
    {
        Assert.AreEqual(new JavaVersionInfo(8, "1.8.0_301", true), JavaVersionParser.ParseVersionOutput(LegacyOutput));
    }

    [TestMethod]
    public void ParseVersionOutput_Detects32BitVm()
    {
        const string output = """
            java version "1.8.0_301"
            Java(TM) SE Runtime Environment (build 1.8.0_301-b09)
            Java HotSpot(TM) Client VM (build 25.301-b09, mixed mode, sharing)
            """;
        Assert.AreEqual(new JavaVersionInfo(8, "1.8.0_301", false), JavaVersionParser.ParseVersionOutput(output));
    }

    [TestMethod]
    public void ParseVersionOutput_SkipsToolOptionsNotice()
    {
        var output = "Picked up JAVA_TOOL_OPTIONS: -Dfile.encoding=UTF-8\r\n" + TemurinOutput;
        Assert.AreEqual(new JavaVersionInfo(21, "21.0.10", true), JavaVersionParser.ParseVersionOutput(output));
    }

    [TestMethod]
    public void ParseVersionOutput_ReadsVersionWithoutDots()
    {
        var info = JavaVersionParser.ParseVersionOutput("openjdk version \"17\" 2021-09-14\nOpenJDK 64-Bit Server VM (build 17+35)\n");
        Assert.AreEqual(new JavaVersionInfo(17, "17", true), info);
    }

    [TestMethod]
    public void ParseVersionOutput_ReturnsNullForUnrecognizedOutput()
    {
        Assert.IsNull(JavaVersionParser.ParseVersionOutput(string.Empty));
        Assert.IsNull(JavaVersionParser.ParseVersionOutput("Error: could not find java.dll"));
    }

    [TestMethod]
    [DataRow("1.8.0_301", 8)]
    [DataRow("1.7.0_80", 7)]
    [DataRow("9", 9)]
    [DataRow("10.0.2", 10)]
    [DataRow("11.0.2", 11)]
    [DataRow("17.0.9", 17)]
    [DataRow("21.0.10", 21)]
    [DataRow("22-ea", 22)]
    public void ParseMajorVersion_ReadsMajorVersion(string version, int expected)
    {
        Assert.AreEqual(expected, JavaVersionParser.ParseMajorVersion(version));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("unknown")]
    public void ParseMajorVersion_ReturnsNullForInvalidVersion(string version)
    {
        Assert.IsNull(JavaVersionParser.ParseMajorVersion(version));
    }
}
