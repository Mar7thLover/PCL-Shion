using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Loaders.Forge;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Test.Loaders.Forge;

[TestClass]
public sealed class ProcessorRunnerTest
{
    [TestMethod]
    public void ParseMainClass_ReadsTheMainSection()
    {
        const string manifest = "Manifest-Version: 1.0\r\nMain-Class: net.minecraftforge.binarypatcher.ConsoleTool\r\n\r\n";

        Assert.AreEqual("net.minecraftforge.binarypatcher.ConsoleTool", ProcessorRunner.ParseMainClass(manifest));
    }

    [TestMethod]
    public void ParseMainClass_JoinsContinuationLines()
    {
        const string manifest = "Manifest-Version: 1.0\nMain-Class: net.minecraftforge.installertools.ConsoleTo\n ol\nCreated-By: Gradle\n";

        Assert.AreEqual("net.minecraftforge.installertools.ConsoleTool", ProcessorRunner.ParseMainClass(manifest));
    }

    [TestMethod]
    public void ParseMainClass_ReturnsNullWithoutMainClass()
    {
        Assert.IsNull(ProcessorRunner.ParseMainClass("Manifest-Version: 1.0\n"));
    }

    [TestMethod]
    public void CreateArguments_PutsTheProcessorJarFirstAndTheArgumentsLast()
    {
        var processor = new PreparedProcessor(
            "net.minecraftforge:jarsplitter:1.1.4",
            "jarsplitter.jar",
            ["jopt-simple.jar", "srgutils.jar"],
            ["--input", "client.jar"],
            new Dictionary<string, string>());

        var arguments = ProcessorRunner.CreateArguments(processor, "net.minecraftforge.jarsplitter.ConsoleTool");

        var separator = PlatformInfo.ClasspathSeparator;
        CollectionAssert.AreEqual(
            new[]
            {
                "-Dstdout.encoding=UTF-8",
                "-Dstderr.encoding=UTF-8",
                "-cp",
                $"jarsplitter.jar{separator}jopt-simple.jar{separator}srgutils.jar",
                "net.minecraftforge.jarsplitter.ConsoleTool",
                "--input",
                "client.jar"
            },
            arguments.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_SkipsAProcessorWhoseOutputsAreInPlace()
    {
        using var root = new FakeMinecraftRoot();
        var output = root.WriteFile("libraries/patched.jar", "patched");
        var sha1 = Convert.ToHexStringLower(SHA1.HashData(File.ReadAllBytes(output)));
        var processor = new PreparedProcessor(
            "net.minecraftforge:binarypatcher:1.1.1",
            root.GetPath("libraries/missing.jar"),
            [],
            [],
            new Dictionary<string, string> { [output] = sha1 });

        // 处理器 jar 与 Java 都不存在，只要尝试运行就会失败
        await ProcessorRunner.RunAsync(processor, root.GetPath("missing-java"), root.RootPath, CancellationToken.None);
    }
}
